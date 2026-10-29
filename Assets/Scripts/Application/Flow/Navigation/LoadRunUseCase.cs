using System;
using System.Threading;
using System.Threading.Tasks;
using Witch.Application.Ports;
using Witch.Domain;

namespace Witch.Application.Flow
{
    public sealed class LoadRunUseCase : ILoadRunUseCase
    {
        private readonly IDayStartSnapshotRepository snapshotRepository;
        private readonly IActiveAttemptMetadataRepository attemptRepository;
        private readonly IGameSessionStore sessionStore;
        private readonly ISceneLoader sceneLoader;

        public LoadRunUseCase(
            IDayStartSnapshotRepository snapshotRepository,
            IActiveAttemptMetadataRepository attemptRepository,
            IGameSessionStore sessionStore,
            ISceneLoader sceneLoader)
        {
            this.snapshotRepository = snapshotRepository ?? throw new ArgumentNullException(nameof(snapshotRepository));
            this.attemptRepository = attemptRepository ?? throw new ArgumentNullException(nameof(attemptRepository));
            this.sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
            this.sceneLoader = sceneLoader ?? throw new ArgumentNullException(nameof(sceneLoader));
        }

        public async Task<LoadRunResult> ExecuteAsync(
            LoadRunCommand command,
            CancellationToken cancellationToken = default)
        {
            if (command == null)
            {
                return LoadRunResult.Failure(LoadRunError.MissingCommand, "Load command is required.");
            }

            if (!command.LoadLatest && string.IsNullOrWhiteSpace(command.RunId))
            {
                return LoadRunResult.Failure(LoadRunError.InvalidRunId, "Run ID is required.");
            }

            DayStartSnapshotLoadResult snapshotResult = command.LoadLatest
                ? await snapshotRepository.LoadLatestDayStartSnapshotAsync(cancellationToken)
                : await snapshotRepository.LoadDayStartSnapshotAsync(command.RunId, cancellationToken);
            if (!snapshotResult.Succeeded)
            {
                return LoadRunResult.Failure(LoadRunError.LoadFailed, snapshotResult.ErrorMessage);
            }

            ActiveAttemptMetadataLoadResult attemptResult = command.LoadLatest
                ? await attemptRepository.LoadLatestActiveAttemptMetadataAsync(cancellationToken)
                : await attemptRepository.LoadActiveAttemptMetadataAsync(command.RunId, cancellationToken);
            if (!attemptResult.Succeeded)
            {
                return LoadRunResult.Failure(LoadRunError.LoadFailed, attemptResult.ErrorMessage);
            }

            bool pendingRewind = attemptResult.Metadata.PendingRewind;
            bool thresholdReached = attemptResult.Metadata.Suspicion >= Witch.Domain.Progression.RunState.RewindSuspicionThreshold;
            bool runCompleted = snapshotResult.Snapshot.RunState.RunCompleted;
            var session = new GameSession(
                snapshotResult.Snapshot,
                attemptResult.Metadata,
                runCompleted
                    ? GamePhase.Victory
                    : pendingRewind || thresholdReached
                        ? GamePhase.RewindPending
                        : GamePhase.ShopPreparation);
            sessionStore.SetActive(session);

            SceneId targetScene = pendingRewind && !runCompleted ? SceneId.RewindMinigame : SceneId.Shop;
            SceneLoadResult sceneResult = await sceneLoader.LoadAsync(targetScene, cancellationToken);
            if (!sceneResult.Succeeded)
            {
                sessionStore.Clear();
                return LoadRunResult.Failure(LoadRunError.SceneLoadFailed, sceneResult.ErrorMessage);
            }

            if (pendingRewind)
            {
                session.TryTransition(GamePhase.RewindMinigame);
            }

            return LoadRunResult.Success(session);
        }
    }
}
