using System;
using System.Threading;
using System.Threading.Tasks;
using Witch.Application.Ports;
using Witch.Domain;
using Witch.Domain.Progression;

namespace Witch.Application.Flow
{
    public sealed class CompleteMinigameUseCase
    {
        private readonly IRunStateTransactionRepository saveRepository;
        private readonly ISceneLoader sceneLoader;
        private readonly IRandomSource randomSource;
        private readonly IGameCenterService gameCenter;
        private bool memoryMenderReported;

        public CompleteMinigameUseCase(
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

        public async Task<MinigameTransactionResult> ExecuteAsync(
            GameSession session,
            CancellationToken cancellationToken = default)
        {
            if (session == null)
            {
                return MinigameTransactionResult.Failure(MinigameTransactionError.MissingSession, "Session is required.");
            }

            if (session.MinigameCompletionApplied
                && !session.AttemptMetadata.PendingMinigame
                && (session.CurrentPhase == GamePhase.ShopPreparation
                    || session.CurrentPhase == GamePhase.CustomerEncounter
                    || session.CurrentPhase == GamePhase.CustomerArrival
                    || session.CurrentPhase == GamePhase.ClosingEncounter))
            {
                SceneLoadResult retryScene = await sceneLoader.LoadAsync(SceneId.Shop, cancellationToken);
                if (retryScene.Succeeded)
                {
                    return MinigameTransactionResult.Success(null, alreadyApplied: true);
                }

                await sceneLoader.LoadAsync(SceneId.MainMenu, cancellationToken);
                return MinigameTransactionResult.Failure(MinigameTransactionError.SceneLoadFailed, retryScene.ErrorMessage);
            }

            if (session.CurrentPhase != GamePhase.MinigameActive || !session.AttemptMetadata.PendingMinigame)
            {
                return MinigameTransactionResult.Failure(MinigameTransactionError.InvalidPhase, "No active minigame can complete.");
            }

            int nextSeed = randomSource.NextInt(1, int.MaxValue);
            if (nextSeed == session.DayStartSnapshot.OrderSeed)
            {
                nextSeed = nextSeed == int.MaxValue - 1 ? 1 : nextSeed + 1;
            }

            int completedMinigameWinsBeforeThisOne = session.RunState.MinigameWinCount;
            MatchCardOutcome outcome = MatchCardRules.Complete(
                session.DayStartSnapshot,
                session.RunState,
                nextSeed);
            DayStartSnapshot persistedSnapshot = new DayStartSnapshot(
                outcome.RestoredRunState,
                outcome.NextAttemptSeed,
                session.DayStartSnapshot.TimerDuration,
                session.DayStartSnapshot.ExpenseId,
                session.DayStartSnapshot.ExpenseValue,
                session.DayStartSnapshot.SaveVersion,
                session.DayStartSnapshot.CapturedAtUtc);

            // Persisted metadata: session metadata with only suspicion reset, win count incremented,
            // pending/board seed cleared, and MinigamePlayedToday kept true. Day counters unchanged.
            // Active order seed handling kept as today (next attempt seed from completion).
            ActiveAttemptMetadata metadata = new ActiveAttemptMetadata(
                session.AttemptMetadata.RunId,
                session.AttemptMetadata.DayNumber,
                0,
                completedMinigameWinsBeforeThisOne + 1,
                false,
                0,
                outcome.NextAttemptSeed,
                session.AttemptMetadata.WrongServiceCountToday,
                session.AttemptMetadata.ServiceCountToday,
                true,
                session.AttemptMetadata.CorrectServiceCountToday,
                session.AttemptMetadata.CoinsEarnedToday,
                session.AttemptMetadata.WrongOrdersForSummary);

            SaveStateResult save = await saveRepository.SaveRunStateAsync(
                persistedSnapshot,
                metadata,
                cancellationToken);
            if (!save.Succeeded)
            {
                return MinigameTransactionResult.Failure(MinigameTransactionError.SaveFailed, save.ErrorMessage);
            }

            session.CompleteMinigameAfterWin(persistedSnapshot, metadata);
            session.MarkHunterFarewell(completedMinigameWinsBeforeThisOne);

            if (gameCenter != null && !memoryMenderReported)
            {
                memoryMenderReported = true;
                _ = GameCenterReporting.ReportQuietlyAsync(gameCenter, GameCenterAchievementIds.MemoryMender, 100d);
            }

            SceneLoadResult scene = await sceneLoader.LoadAsync(SceneId.Shop, cancellationToken);
            if (scene.Succeeded)
            {
                return MinigameTransactionResult.Success(null);
            }

            await sceneLoader.LoadAsync(SceneId.MainMenu, cancellationToken);
            return MinigameTransactionResult.Failure(MinigameTransactionError.SceneLoadFailed, scene.ErrorMessage);
        }
    }
}
