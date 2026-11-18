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
                    || session.CurrentPhase == GamePhase.CustomerEncounter))
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
            var snapshot = new DayStartSnapshot(
                outcome.RestoredRunState,
                outcome.NextAttemptSeed,
                session.DayStartSnapshot.TimerDuration,
                session.DayStartSnapshot.ExpenseId,
                session.DayStartSnapshot.ExpenseValue,
                session.DayStartSnapshot.SaveVersion,
                session.DayStartSnapshot.CapturedAtUtc);
            // Full minigame reset restores money to day start: new day counters reset to 0.
            // In-place minigame resume restores money and per-day summary counters to pre-service snapshot.
            // WrongServiceCountToday/ServiceCountToday carry forward unchanged (Clean Ledger depends on them).
            bool inPlace = session.CanResumeInPlace;
            int savedCorrectServiceCountToday = 0;
            int savedCoinsEarnedToday = 0;
            int savedWrongOrdersForSummary = 0;
            if (inPlace)
            {
                var resume = session.GetResumeSummaryCounters();
                savedCorrectServiceCountToday = resume.CorrectServiceCountToday;
                savedCoinsEarnedToday = resume.CoinsEarnedToday;
                savedWrongOrdersForSummary = resume.WrongOrdersForSummary;
            }

            var metadata = new ActiveAttemptMetadata(
                outcome.RestoredRunState.RunId,
                outcome.RestoredRunState.CurrentDayNumber,
                0,
                outcome.RestoredRunState.MinigameWinCount,
                false,
                0,
                outcome.NextAttemptSeed,
                session.AttemptMetadata.WrongServiceCountToday,
                session.AttemptMetadata.ServiceCountToday,
                true,
                savedCorrectServiceCountToday,
                savedCoinsEarnedToday,
                savedWrongOrdersForSummary);
            SaveStateResult save = await saveRepository.SaveRunStateAsync(
                snapshot,
                metadata,
                cancellationToken);
            if (!save.Succeeded)
            {
                return MinigameTransactionResult.Failure(MinigameTransactionError.SaveFailed, save.ErrorMessage);
            }

            if (gameCenter != null && !memoryMenderReported)
            {
                memoryMenderReported = true;
                _ = GameCenterReporting.ReportQuietlyAsync(gameCenter, GameCenterAchievementIds.MemoryMender, 100d);
            }

            if (inPlace) session.CompleteMinigameInPlace(snapshot, metadata);
            else session.CompleteMinigame(snapshot, metadata);
            session.MarkHunterFarewell(completedMinigameWinsBeforeThisOne);
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
