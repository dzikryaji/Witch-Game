using System;
using System.Threading;
using System.Threading.Tasks;
using Witch.Application.Ports;
using Witch.Domain;
using Witch.Domain.Progression;

namespace Witch.Application.Flow
{
    public sealed class ResolveExpenseUseCase
    {
        private readonly ExpenseRules expenseRules;
        private readonly DayProgressionSetup setup;
        private readonly IRunStateTransactionRepository saveRepository;
        private readonly IRandomSource randomSource;
        private readonly IClock clock;
        private readonly IGameCenterService gameCenter;
        private bool cleanLedgerReported;

        public ResolveExpenseUseCase(
            ExpenseRules expenseRules,
            DayProgressionSetup setup,
            IRunStateTransactionRepository saveRepository,
            IRandomSource randomSource,
            IClock clock,
            IGameCenterService gameCenter = null)
        {
            this.expenseRules = expenseRules ?? throw new ArgumentNullException(nameof(expenseRules));
            this.setup = setup ?? throw new ArgumentNullException(nameof(setup));
            this.saveRepository = saveRepository ?? throw new ArgumentNullException(nameof(saveRepository));
            this.randomSource = randomSource ?? throw new ArgumentNullException(nameof(randomSource));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.gameCenter = gameCenter;
        }

        public async Task<ExpenseResolutionResult> ExecuteAsync(
            GameSession session,
            CancellationToken cancellationToken = default)
        {
            if (session == null)
            {
                return ExpenseResolutionResult.Failure(ExpenseResolutionError.MissingSession, "Session is required.");
            }

            if (session.CurrentPhase != GamePhase.ClosingEncounter)
            {
                return ExpenseResolutionResult.Failure(ExpenseResolutionError.InvalidPhase, "Expense check is not active.");
            }

            int paidDay = session.RunState.CurrentDayNumber;

            ExpenseOutcome outcome = expenseRules.Resolve(
                session.RunState,
                session.DayStartSnapshot.ExpenseValue);
            if (outcome.Kind == ExpenseOutcomeKind.GameOver)
            {
                session.BeginExpenseCheck();
                session.ApplyExpenseFailure(outcome.UpdatedRunState);
                return ExpenseResolutionResult.Success(outcome);
            }

            if (outcome.Kind == ExpenseOutcomeKind.Victory)
            {
                RunState completedRun = outcome.UpdatedRunState.MarkCompleted();
                var completedSnapshot = new DayStartSnapshot(
                    completedRun,
                    session.DayStartSnapshot.OrderSeed,
                    session.DayStartSnapshot.TimerDuration,
                    session.DayStartSnapshot.ExpenseId,
                    session.DayStartSnapshot.ExpenseValue,
                    session.DayStartSnapshot.SaveVersion,
                    clock.UtcNow);
                var completedMetadata = new ActiveAttemptMetadata(
                    completedRun.RunId,
                    completedRun.CurrentDayNumber,
                    completedRun.Suspicion,
                    completedRun.RewindCount,
                    false,
                    0,
                    session.AttemptMetadata.ActiveOrderSeed);
                SaveStateResult completedSave = await saveRepository.SaveRunStateAsync(
                    completedSnapshot,
                    completedMetadata,
                    cancellationToken);
                if (!completedSave.Succeeded)
                {
                    return ExpenseResolutionResult.Failure(
                        ExpenseResolutionError.SaveFailed,
                        completedSave.ErrorMessage);
                }

                bool servedWrongOnVictoryDay = session.AttemptMetadata.WrongServiceCountToday > 0;
                int victoryServiceCountToday = session.AttemptMetadata.ServiceCountToday;
                bool victoryMinigamePlayedToday = session.AttemptMetadata.MinigamePlayedToday;
                session.BeginExpenseCheck();
                session.ApplyVictory(completedSnapshot, completedMetadata);
                _ = GameCenterReporting.ReportQuietlyAsync(gameCenter, GameCenterAchievementIds.SecretKept, 100d);
                if (paidDay > RunState.FirstDay && !servedWrongOnVictoryDay && victoryServiceCountToday > 0 && !victoryMinigamePlayedToday && !cleanLedgerReported)
                {
                    cleanLedgerReported = true;
                    _ = GameCenterReporting.ReportQuietlyAsync(gameCenter, GameCenterAchievementIds.CleanLedger, 100d);
                }

                return ExpenseResolutionResult.Success(
                    new ExpenseOutcome(ExpenseOutcomeKind.Victory, completedRun));
            }

            RunState nextRunState = setup.ApplyUnlocks(outcome.UpdatedRunState);
            int nextDay = nextRunState.CurrentDayNumber;
            int nextSeed = randomSource.NextInt(1, int.MaxValue);
            var snapshot = new DayStartSnapshot(
                nextRunState,
                nextSeed,
                setup.GetTimerDuration(nextDay),
                setup.GetExpenseId(nextDay),
                setup.GetExpenseValue(nextDay),
                setup.SaveVersion,
                clock.UtcNow);
            var metadata = new ActiveAttemptMetadata(
                nextRunState.RunId,
                nextDay,
                nextRunState.Suspicion,
                nextRunState.RewindCount,
                false,
                0,
                nextSeed,
                0,
                0,
                false);
            SaveStateResult save = await saveRepository.SaveRunStateAsync(
                snapshot,
                metadata,
                cancellationToken);
            if (!save.Succeeded)
            {
                return ExpenseResolutionResult.Failure(ExpenseResolutionError.SaveFailed, save.ErrorMessage);
            }

            bool servedWrongToday = session.AttemptMetadata.WrongServiceCountToday > 0;
            int serviceCountToday = session.AttemptMetadata.ServiceCountToday;
            bool minigamePlayedToday = session.AttemptMetadata.MinigamePlayedToday;
            session.BeginExpenseCheck();
            session.AdvanceDay(snapshot, metadata);
            if (paidDay == RunState.FirstDay)
            {
                _ = GameCenterReporting.ReportQuietlyAsync(gameCenter, GameCenterAchievementIds.FirstShift, 100d);
            }

            // D1: Clean Ledger can only unlock on Day 2 or later, never together with First Shift.
            // D2: Clean Ledger needs at least one customer served that day.
            // D3: Clean Ledger fails if the rewind minigame was played that day.
            if (paidDay > RunState.FirstDay && !servedWrongToday && serviceCountToday > 0 && !minigamePlayedToday && !cleanLedgerReported)
            {
                cleanLedgerReported = true;
                _ = GameCenterReporting.ReportQuietlyAsync(gameCenter, GameCenterAchievementIds.CleanLedger, 100d);
            }

            return ExpenseResolutionResult.Success(outcome);
        }
    }
}
