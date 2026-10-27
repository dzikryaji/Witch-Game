using System;
using System.Threading;
using System.Threading.Tasks;
using Witch.Application.Ports;
using Witch.Domain.Progression;

namespace Witch.Infrastructure.Persistence
{
    public sealed class InMemoryRunRepository : ISaveRepository
    {
        private readonly object gate = new object();
        private DayStartSnapshot snapshot;
        private ActiveAttemptMetadata metadata;

        public Task<SaveStateResult> SaveRunStateAsync(
            DayStartSnapshot value,
            ActiveAttemptMetadata attempt,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                _ = new GameSaveState(value, attempt);
                lock (gate)
                {
                    snapshot = value;
                    metadata = attempt;
                }

                return Task.FromResult(SaveStateResult.Success());
            }
            catch (ArgumentException exception)
            {
                return Task.FromResult(SaveStateResult.Failure(SaveStateError.InvalidState, exception.Message));
            }
        }

        public Task<SaveStateResult> SaveDayStartSnapshotAsync(
            DayStartSnapshot value,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (value == null)
            {
                return Task.FromResult(SaveStateResult.Failure(SaveStateError.InvalidState, "Snapshot is required."));
            }

            lock (gate)
            {
                snapshot = value;
            }

            return Task.FromResult(SaveStateResult.Success());
        }

        public Task<SaveStateResult> SaveActiveAttemptMetadataAsync(
            ActiveAttemptMetadata value,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (gate)
            {
                if (snapshot == null || value == null || !Matches(snapshot, value))
                {
                    return Task.FromResult(SaveStateResult.Failure(
                        SaveStateError.InvalidState,
                        "Attempt metadata must match an existing day-start snapshot."));
                }

                metadata = value;
            }

            return Task.FromResult(SaveStateResult.Success());
        }

        public Task<DayStartSnapshotLoadResult> LoadLatestDayStartSnapshotAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (gate)
            {
                return Task.FromResult(snapshot == null
                    ? DayStartSnapshotLoadResult.Failure(LoadStateError.NotFound, "No current run exists.")
                    : DayStartSnapshotLoadResult.Success(snapshot));
            }
        }

        public async Task<DayStartSnapshotLoadResult> LoadDayStartSnapshotAsync(
            string runId,
            CancellationToken cancellationToken = default)
        {
            DayStartSnapshotLoadResult result = await LoadLatestDayStartSnapshotAsync(cancellationToken);
            return result.Succeeded && string.Equals(result.Snapshot.RunState.RunId, runId, StringComparison.Ordinal)
                ? result
                : DayStartSnapshotLoadResult.Failure(LoadStateError.NotFound, "Run was not found.");
        }

        public Task<ActiveAttemptMetadataLoadResult> LoadLatestActiveAttemptMetadataAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (gate)
            {
                return Task.FromResult(metadata == null
                    ? ActiveAttemptMetadataLoadResult.Failure(LoadStateError.NotFound, "No active attempt exists.")
                    : ActiveAttemptMetadataLoadResult.Success(metadata));
            }
        }

        public async Task<ActiveAttemptMetadataLoadResult> LoadActiveAttemptMetadataAsync(
            string runId,
            CancellationToken cancellationToken = default)
        {
            ActiveAttemptMetadataLoadResult result = await LoadLatestActiveAttemptMetadataAsync(cancellationToken);
            return result.Succeeded && string.Equals(result.Metadata.RunId, runId, StringComparison.Ordinal)
                ? result
                : ActiveAttemptMetadataLoadResult.Failure(LoadStateError.NotFound, "Run was not found.");
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
            DayStartSnapshotLoadResult snapshotResult = await LoadLatestDayStartSnapshotAsync(cancellationToken);
            ActiveAttemptMetadataLoadResult metadataResult = await LoadLatestActiveAttemptMetadataAsync(cancellationToken);
            if (!snapshotResult.Succeeded || !metadataResult.Succeeded)
            {
                return LoadStateResult.Failure(
                    !snapshotResult.Succeeded ? snapshotResult.Error : metadataResult.Error,
                    !snapshotResult.Succeeded ? snapshotResult.ErrorMessage : metadataResult.ErrorMessage);
            }

            return LoadStateResult.Success(new GameSaveState(snapshotResult.Snapshot, metadataResult.Metadata));
        }

        public async Task<LoadStateResult> LoadAsync(
            string runId,
            CancellationToken cancellationToken = default)
        {
            LoadStateResult result = await LoadLatestAsync(cancellationToken);
            return result.Succeeded
                && string.Equals(result.SavedGame.DayStartSnapshot.RunState.RunId, runId, StringComparison.Ordinal)
                ? result
                : LoadStateResult.Failure(LoadStateError.NotFound, "Run was not found.");
        }

        private static bool Matches(DayStartSnapshot value, ActiveAttemptMetadata attempt)
        {
            return string.Equals(value.RunState.RunId, attempt.RunId, StringComparison.Ordinal)
                   && value.RunState.CurrentDayNumber == attempt.DayNumber;
        }
    }
}
