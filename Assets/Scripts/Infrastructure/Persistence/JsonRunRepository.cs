using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Witch.Application.Ports;
using Witch.Domain.Progression;

namespace Witch.Infrastructure.Persistence
{
    public sealed class JsonRunRepository : ISaveRepository
    {
        public const string CurrentRunFileName = "current-run.json";

        private readonly string primaryPath;
        private readonly string previousPath;
        private readonly int supportedVersion;
        private readonly IAtomicFileStore fileStore;

        public JsonRunRepository(int supportedVersion)
            : this(Path.Combine(UnityEngine.Application.persistentDataPath, "Saves"), supportedVersion)
        {
        }

        public JsonRunRepository(
            string saveDirectory,
            int supportedVersion,
            IAtomicFileStore fileStore = null)
        {
            if (string.IsNullOrWhiteSpace(saveDirectory))
            {
                throw new ArgumentException("Save directory is required.", nameof(saveDirectory));
            }

            if (supportedVersion < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(supportedVersion));
            }

            primaryPath = Path.Combine(saveDirectory, CurrentRunFileName);
            previousPath = primaryPath + ".previous";
            this.supportedVersion = supportedVersion;
            this.fileStore = fileStore ?? new SystemAtomicFileStore();
        }

        public async Task<SaveStateResult> SaveRunStateAsync(
            DayStartSnapshot snapshot,
            ActiveAttemptMetadata metadata,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await Task.Run(() => Write(snapshot, metadata), cancellationToken);
        }

        public async Task<SaveStateResult> SaveDayStartSnapshotAsync(
            DayStartSnapshot snapshot,
            CancellationToken cancellationToken = default)
        {
            LoadStateResult current = await LoadLatestAsync(cancellationToken);
            if (!current.Succeeded)
            {
                return SaveStateResult.Failure(SaveStateError.InvalidState, "Existing attempt metadata is required.");
            }

            return await SaveRunStateAsync(snapshot, current.SavedGame.AttemptMetadata, cancellationToken);
        }

        public async Task<SaveStateResult> SaveActiveAttemptMetadataAsync(
            ActiveAttemptMetadata metadata,
            CancellationToken cancellationToken = default)
        {
            LoadStateResult current = await LoadLatestAsync(cancellationToken);
            if (!current.Succeeded)
            {
                return SaveStateResult.Failure(SaveStateError.InvalidState, "Existing day-start snapshot is required.");
            }

            return await SaveRunStateAsync(current.SavedGame.DayStartSnapshot, metadata, cancellationToken);
        }

        public async Task<DayStartSnapshotLoadResult> LoadLatestDayStartSnapshotAsync(
            CancellationToken cancellationToken = default)
        {
            LoadStateResult result = await LoadLatestAsync(cancellationToken);
            return result.Succeeded
                ? DayStartSnapshotLoadResult.Success(result.SavedGame.DayStartSnapshot)
                : DayStartSnapshotLoadResult.Failure(result.Error, result.ErrorMessage);
        }

        public async Task<DayStartSnapshotLoadResult> LoadDayStartSnapshotAsync(
            string runId,
            CancellationToken cancellationToken = default)
        {
            LoadStateResult result = await LoadAsync(runId, cancellationToken);
            return result.Succeeded
                ? DayStartSnapshotLoadResult.Success(result.SavedGame.DayStartSnapshot)
                : DayStartSnapshotLoadResult.Failure(result.Error, result.ErrorMessage);
        }

        public async Task<ActiveAttemptMetadataLoadResult> LoadLatestActiveAttemptMetadataAsync(
            CancellationToken cancellationToken = default)
        {
            LoadStateResult result = await LoadLatestAsync(cancellationToken);
            return result.Succeeded
                ? ActiveAttemptMetadataLoadResult.Success(result.SavedGame.AttemptMetadata)
                : ActiveAttemptMetadataLoadResult.Failure(result.Error, result.ErrorMessage);
        }

        public async Task<ActiveAttemptMetadataLoadResult> LoadActiveAttemptMetadataAsync(
            string runId,
            CancellationToken cancellationToken = default)
        {
            LoadStateResult result = await LoadAsync(runId, cancellationToken);
            return result.Succeeded
                ? ActiveAttemptMetadataLoadResult.Success(result.SavedGame.AttemptMetadata)
                : ActiveAttemptMetadataLoadResult.Failure(result.Error, result.ErrorMessage);
        }

        public Task<SaveStateResult> SaveAsync(
            GameSaveState savedGame,
            CancellationToken cancellationToken = default)
        {
            if (savedGame == null)
            {
                return Task.FromResult(SaveStateResult.Failure(SaveStateError.InvalidState, "Save state is required."));
            }

            return SaveRunStateAsync(savedGame.DayStartSnapshot, savedGame.AttemptMetadata, cancellationToken);
        }

        public async Task<LoadStateResult> LoadLatestAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await Task.Run(ReadWithRecovery, cancellationToken);
        }

        public async Task<LoadStateResult> LoadAsync(
            string runId,
            CancellationToken cancellationToken = default)
        {
            LoadStateResult result = await LoadLatestAsync(cancellationToken);
            if (!result.Succeeded)
            {
                return result;
            }

            return string.Equals(result.SavedGame.DayStartSnapshot.RunState.RunId, runId, StringComparison.Ordinal)
                ? result
                : LoadStateResult.Failure(LoadStateError.NotFound, "Run was not found.");
        }

        private SaveStateResult Write(DayStartSnapshot snapshot, ActiveAttemptMetadata metadata)
        {
            try
            {
                _ = new GameSaveState(snapshot, metadata);
                RunEnvelopeDto envelope = SaveDtoMapper.ToDto(snapshot, metadata);
                string json = JsonUtility.ToJson(envelope, true);
                RunEnvelopeDto validationCopy = JsonUtility.FromJson<RunEnvelopeDto>(json);
                _ = SaveDtoMapper.FromDto(validationCopy, supportedVersion);
                fileStore.WriteAtomically(primaryPath, previousPath, json);
                return SaveStateResult.Success();
            }
            catch (ArgumentException exception)
            {
                return SaveStateResult.Failure(SaveStateError.InvalidState, exception.Message);
            }
            catch (InvalidDataException exception)
            {
                return SaveStateResult.Failure(SaveStateError.InvalidState, exception.Message);
            }
            catch (Exception exception)
            {
                return SaveStateResult.Failure(SaveStateError.WriteFailed, exception.Message);
            }
        }

        private LoadStateResult ReadWithRecovery()
        {
            LoadStateResult primary = Read(primaryPath);
            if (primary.Succeeded || primary.Error == LoadStateError.UnsupportedVersion)
            {
                return primary;
            }

            LoadStateResult previous = Read(previousPath);
            return previous.Succeeded ? previous : primary;
        }

        private LoadStateResult Read(string path)
        {
            if (!fileStore.Exists(path))
            {
                return LoadStateResult.Failure(LoadStateError.NotFound, "No current run exists.");
            }

            try
            {
                string json = fileStore.ReadAllText(path);
                RunEnvelopeDto envelope = JsonUtility.FromJson<RunEnvelopeDto>(json);
                return LoadStateResult.Success(SaveDtoMapper.FromDto(envelope, supportedVersion));
            }
            catch (UnsupportedSaveVersionException exception)
            {
                return LoadStateResult.Failure(LoadStateError.UnsupportedVersion, exception.Message);
            }
            catch (InvalidDataException exception)
            {
                return LoadStateResult.Failure(LoadStateError.CorruptData, exception.Message);
            }
            catch (Exception exception)
            {
                return LoadStateResult.Failure(LoadStateError.ReadFailed, exception.Message);
            }
        }
    }

    internal sealed class UnsupportedSaveVersionException : Exception
    {
        public UnsupportedSaveVersionException(string message) : base(message) { }
    }

    [Serializable]
    internal sealed class RunEnvelopeDto
    {
        public int saveVersion;
        public DayStartSnapshotDto snapshot;
        public ActiveAttemptMetadataDto attempt;
    }

    [Serializable]
    internal sealed class DayStartSnapshotDto
    {
        public RunStateDto runState;
        public int orderSeed;
        public long timerTicks;
        public string expenseId;
        public int expenseValue;
        public int saveVersion;
        public string capturedAtUtc;
    }

    [Serializable]
    internal sealed class RunStateDto
    {
        public string runId;
        public int day;
        public int money;
        public int suspicion;
        public int rewindCount;
        public string[] categoryIds;
        public string[] herbIds;
        public string[] recipeIds;
        public string[] guidebookIds;
        public FlagDto[] tutorialFlags;
        public FlagDto[] narrativeFlags;
    }

    [Serializable]
    internal sealed class ActiveAttemptMetadataDto
    {
        public string runId;
        public int day;
        public int suspicion;
        public int rewindCount;
        public bool pendingRewind;
        public int minigameBoardSeed;
        public int activeOrderSeed;
    }

    [Serializable]
    internal sealed class FlagDto
    {
        public string id;
        public bool value;
    }

    internal static class SaveDtoMapper
    {
        public static RunEnvelopeDto ToDto(DayStartSnapshot snapshot, ActiveAttemptMetadata metadata)
        {
            return new RunEnvelopeDto
            {
                saveVersion = snapshot.SaveVersion,
                snapshot = new DayStartSnapshotDto
                {
                    runState = ToDto(snapshot.RunState),
                    orderSeed = snapshot.OrderSeed,
                    timerTicks = snapshot.TimerDuration.Ticks,
                    expenseId = snapshot.ExpenseId,
                    expenseValue = snapshot.ExpenseValue,
                    saveVersion = snapshot.SaveVersion,
                    capturedAtUtc = snapshot.CapturedAtUtc.ToString("O", CultureInfo.InvariantCulture)
                },
                attempt = new ActiveAttemptMetadataDto
                {
                    runId = metadata.RunId,
                    day = metadata.DayNumber,
                    suspicion = metadata.Suspicion,
                    rewindCount = metadata.RewindCount,
                    pendingRewind = metadata.PendingRewind,
                    minigameBoardSeed = metadata.MinigameBoardSeed,
                    activeOrderSeed = metadata.ActiveOrderSeed
                }
            };
        }

        public static GameSaveState FromDto(RunEnvelopeDto envelope, int supportedVersion)
        {
            if (envelope == null || envelope.snapshot == null || envelope.snapshot.runState == null || envelope.attempt == null)
            {
                throw new InvalidDataException("Save file is missing required data.");
            }

            if (envelope.saveVersion != supportedVersion || envelope.snapshot.saveVersion != supportedVersion)
            {
                throw new UnsupportedSaveVersionException(
                    "Save version '" + envelope.saveVersion + "' is unsupported.");
            }

            RunStateDto state = envelope.snapshot.runState;
            var runState = new RunState(
                state.runId,
                state.day,
                state.money,
                state.suspicion,
                state.rewindCount,
                state.categoryIds ?? new string[0],
                MigrateHerbIds(state.herbIds),
                state.recipeIds ?? new string[0],
                state.guidebookIds ?? new string[0],
                FromFlags(state.tutorialFlags),
                FromFlags(state.narrativeFlags));
            var snapshot = new DayStartSnapshot(
                runState,
                envelope.snapshot.orderSeed,
                TimeSpan.FromTicks(envelope.snapshot.timerTicks),
                envelope.snapshot.expenseId,
                envelope.snapshot.expenseValue,
                envelope.snapshot.saveVersion,
                DateTimeOffset.Parse(
                    envelope.snapshot.capturedAtUtc,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind));
            var metadata = new ActiveAttemptMetadata(
                envelope.attempt.runId,
                envelope.attempt.day,
                envelope.attempt.suspicion,
                envelope.attempt.rewindCount,
                envelope.attempt.pendingRewind,
                envelope.attempt.minigameBoardSeed,
                envelope.attempt.activeOrderSeed);
            return new GameSaveState(snapshot, metadata);
        }

        private static RunStateDto ToDto(RunState state)
        {
            return new RunStateDto
            {
                runId = state.RunId,
                day = state.CurrentDayNumber,
                money = state.Money,
                suspicion = state.Suspicion,
                rewindCount = state.RewindCount,
                categoryIds = Copy(state.UnlockedCategoryIds),
                herbIds = Copy(state.UnlockedHerbIds),
                recipeIds = Copy(state.UnlockedRecipeIds),
                guidebookIds = Copy(state.UnlockedGuidebookEntryIds),
                tutorialFlags = ToFlags(state.TutorialFlags),
                narrativeFlags = ToFlags(state.NarrativeFlags)
            };
        }

        private static string[] Copy(IReadOnlyList<string> values)
        {
            var copy = new string[values.Count];
            for (int index = 0; index < values.Count; index++)
            {
                copy[index] = values[index];
            }

            return copy;
        }

        private static string[] MigrateHerbIds(string[] values)
        {
            string[] copy = values == null ? new string[0] : (string[])values.Clone();
            for (int index = 0; index < copy.Length; index++)
            {
                if (string.Equals(copy[index], "herb.aloe", StringComparison.Ordinal))
                {
                    copy[index] = "herb.borage";
                }
            }

            return copy;
        }

        private static FlagDto[] ToFlags(IReadOnlyDictionary<string, bool> flags)
        {
            var result = new FlagDto[flags.Count];
            int index = 0;
            foreach (KeyValuePair<string, bool> flag in flags)
            {
                result[index++] = new FlagDto { id = flag.Key, value = flag.Value };
            }

            return result;
        }

        private static IReadOnlyDictionary<string, bool> FromFlags(FlagDto[] flags)
        {
            var result = new Dictionary<string, bool>(StringComparer.Ordinal);
            if (flags == null)
            {
                return result;
            }

            foreach (FlagDto flag in flags)
            {
                if (flag == null || string.IsNullOrWhiteSpace(flag.id))
                {
                    throw new InvalidDataException("Save file contains an invalid flag.");
                }

                result.Add(flag.id, flag.value);
            }

            return result;
        }
    }
}
