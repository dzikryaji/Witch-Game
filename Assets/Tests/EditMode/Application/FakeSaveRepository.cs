using System.Threading;
using System.Threading.Tasks;
using Witch.Application.Ports;
using Witch.Domain.Progression;

namespace Witch.Application.Tests
{
    internal sealed class FakeSaveRepository : ISaveRepository
    {
        public System.Collections.Generic.IList<string> Operations { get; set; }

        public int SaveCallCount { get; private set; }

        public SaveStateResult SaveResult { get; set; } = SaveStateResult.Success();

        public LoadStateResult LatestLoadResult { get; set; } =
            LoadStateResult.Failure(LoadStateError.NotFound, "no save");

        public LoadStateResult SpecificLoadResult { get; set; } =
            LoadStateResult.Failure(LoadStateError.NotFound, "no save");

        public GameSaveState SavedGame { get; private set; }

        public DayStartSnapshot SavedSnapshot { get; private set; }

        public ActiveAttemptMetadata SavedAttemptMetadata { get; private set; }

        public string LastRequestedRunId { get; private set; }

        public Task<SaveStateResult> SaveAsync(
            GameSaveState savedGame,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SaveCallCount++;
            Operations?.Add("save");
            SavedGame = savedGame;
            SavedSnapshot = savedGame.DayStartSnapshot;
            SavedAttemptMetadata = savedGame.AttemptMetadata;
            return Task.FromResult(SaveResult);
        }

        public Task<SaveStateResult> SaveRunStateAsync(
            DayStartSnapshot snapshot,
            ActiveAttemptMetadata metadata,
            CancellationToken cancellationToken = default)
        {
            return SaveAsync(new GameSaveState(snapshot, metadata), cancellationToken);
        }

        public Task<SaveStateResult> SaveDayStartSnapshotAsync(
            DayStartSnapshot snapshot,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SaveCallCount++;
            Operations?.Add("save");
            SavedSnapshot = snapshot;
            if (SavedAttemptMetadata != null)
            {
                SavedGame = new GameSaveState(snapshot, SavedAttemptMetadata);
            }

            return Task.FromResult(SaveResult);
        }

        public Task<SaveStateResult> SaveActiveAttemptMetadataAsync(
            ActiveAttemptMetadata metadata,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SaveCallCount++;
            Operations?.Add("save");
            SavedAttemptMetadata = metadata;
            if (SavedSnapshot != null)
            {
                SavedGame = new GameSaveState(SavedSnapshot, metadata);
            }

            return Task.FromResult(SaveResult);
        }

        public Task<LoadStateResult> LoadLatestAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(LatestLoadResult);
        }

        public Task<LoadStateResult> LoadAsync(
            string runId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequestedRunId = runId;
            return Task.FromResult(SpecificLoadResult);
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
    }
}
