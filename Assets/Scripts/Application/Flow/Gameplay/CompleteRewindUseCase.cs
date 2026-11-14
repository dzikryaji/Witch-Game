using System;
using System.Threading;
using System.Threading.Tasks;
using Witch.Application.Ports;
using Witch.Domain;
using Witch.Domain.Progression;

namespace Witch.Application.Flow
{
    public sealed class CompleteRewindUseCase
    {
        private readonly IRunStateTransactionRepository saveRepository;
        private readonly ISceneLoader sceneLoader;
        private readonly IRandomSource randomSource;
        private readonly IGameCenterService gameCenter;
        private bool memoryMenderReported;

        public CompleteRewindUseCase(
            IRunStateTransactionRepository saveRepository,
            ISceneLoader sceneLoader,
            IRandomSource randomSource,
            IGameCenterService gameCenter = null)
        {
            this.saveRepository = saveRepository ?? throw new ArgumentNullException(nameof(saveRepository));
            this.sceneLoader = sceneLoader ?? throw new ArgumentNullException(nameof(sceneLoader));
            this.randomSource = randomSource ?? throw new ArgumentNullException(nameof(randomSource));
            this.gameCenter = gameCenter;
            this.memoryMenderReported = false;
        }

        public async Task<RewindTransactionResult> ExecuteAsync(
            GameSession session,
            CancellationToken cancellationToken = default)
        {
            if (session == null)
            {
                return RewindTransactionResult.Failure(RewindTransactionError.MissingSession, "Session is required.");
            }

            if (session.RewindCompletionApplied
                && !session.AttemptMetadata.PendingRewind
                && (session.CurrentPhase == GamePhase.ShopPreparation
                    || session.CurrentPhase == GamePhase.CustomerEncounter))
            {
                SceneLoadResult retryScene = await sceneLoader.LoadAsync(SceneId.Shop, cancellationToken);
                if (retryScene.Succeeded)
                {
                    return RewindTransactionResult.Success(null, alreadyApplied: true);
                }

                await sceneLoader.LoadAsync(SceneId.MainMenu, cancellationToken);
                return RewindTransactionResult.Failure(RewindTransactionError.SceneLoadFailed, retryScene.ErrorMessage);
            }

            if (session.CurrentPhase != GamePhase.RewindMinigame || !session.AttemptMetadata.PendingRewind)
            {
                return RewindTransactionResult.Failure(RewindTransactionError.InvalidPhase, "No active rewind can complete.");
            }

            int nextSeed = randomSource.NextInt(1, int.MaxValue);
            if (nextSeed == session.DayStartSnapshot.OrderSeed)
            {
                nextSeed = nextSeed == int.MaxValue - 1 ? 1 : nextSeed + 1;
            }

            int completedRewindsBeforeThisOne = session.RunState.RewindCount;
            RewindOutcome outcome = RewindRules.Complete(
                session.DayStartSnapshot,
                session.RunState,
                nextSeed);
            var snapshot = new DayStartSnapshot(
                outcome.RestoredRunState,
                outcome.NextAttemptSeed,
                session.DayStartSnapshot.TimerDuration,
                session.DayStartSnapshot.ExpenseId,
                session.DayStartSnapshot.ExpenseValue,
                session.DayStartSnapshot.SaveVersion,
                session.DayStartSnapshot.CapturedAtUtc);
            // Full rewind restores money to day start: new day counters reset to 0.
            // In-place rewind restores money to pre-service snapshot: session restores resume counters.
            // WrongServiceCountToday/ServiceCountToday carry forward unchanged (Clean Ledger depends on them).
            var metadata = new ActiveAttemptMetadata(
                outcome.RestoredRunState.RunId,
                outcome.RestoredRunState.CurrentDayNumber,
                0,
                outcome.RestoredRunState.RewindCount,
                false,
                0,
                outcome.NextAttemptSeed,
                session.AttemptMetadata.WrongServiceCountToday,
                session.AttemptMetadata.ServiceCountToday,
                true,
                0,
                0,
                0);
            SaveStateResult save = await saveRepository.SaveRunStateAsync(
                snapshot,
                metadata,
                cancellationToken);
            if (!save.Succeeded)
            {
                return RewindTransactionResult.Failure(RewindTransactionError.SaveFailed, save.ErrorMessage);
            }

            if (gameCenter != null && !memoryMenderReported)
            {
                memoryMenderReported = true;
                _ = GameCenterReporting.ReportQuietlyAsync(gameCenter, GameCenterAchievementIds.MemoryMender, 100d);
            }

            if (session.CanResumeInPlace) session.CompleteRewindInPlace(snapshot, metadata);
            else session.CompleteRewind(snapshot, metadata);
            session.MarkHunterFarewell(completedRewindsBeforeThisOne);
            SceneLoadResult scene = await sceneLoader.LoadAsync(SceneId.Shop, cancellationToken);
            if (scene.Succeeded)
            {
                return RewindTransactionResult.Success(null);
            }

            await sceneLoader.LoadAsync(SceneId.MainMenu, cancellationToken);
            return RewindTransactionResult.Failure(RewindTransactionError.SceneLoadFailed, scene.ErrorMessage);
        }
    }
}
