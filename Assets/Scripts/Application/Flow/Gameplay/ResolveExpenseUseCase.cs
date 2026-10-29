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

        public ResolveExpenseUseCase(
            ExpenseRules expenseRules,
            DayProgressionSetup setup,
            IRunStateTransactionRepository saveRepository,
            IRandomSource randomSource,
            IClock clock)
        {
            this.expenseRules = expenseRules ?? throw new ArgumentNullException(nameof(expenseRules));
            this.setup = setup ?? throw new ArgumentNullException(nameof(setup));
            this.saveRepository = saveRepository ?? throw new ArgumentNullException(nameof(saveRepository));
            this.randomSource = randomSource ?? throw new ArgumentNullException(nameof(randomSource));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
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

                session.BeginExpenseCheck();
                session.ApplyVictory(completedSnapshot, completedMetadata);
                return ExpenseResolutionResult.Success(
                    new ExpenseOutcome(ExpenseOutcomeKind.Victory, completedRun));
            }

            RunState nextRunState = setup.ApplyUnlocks(outcome.UpdatedRunState);
            int nextDay = nextRunState.CurrentDayNumber;
            int nextSeed = randomSource.NextInt(1, int.MaxValue);
            var snapshot = new DayStartSnapshot(
                nextRunState,
                nextSeed,
                setup.TimingRules.GetDuration(nextDay),
                setup.GetExpenseId(nextDay),
                setup.DailyExpenseValue,
                setup.SaveVersion,
                clock.UtcNow);
            var metadata = new ActiveAttemptMetadata(
                nextRunState.RunId,
                nextDay,
                nextRunState.Suspicion,
                nextRunState.RewindCount,
                false,
                0,
                nextSeed);
            SaveStateResult save = await saveRepository.SaveRunStateAsync(
                snapshot,
                metadata,
                cancellationToken);
            if (!save.Succeeded)
            {
                return ExpenseResolutionResult.Failure(ExpenseResolutionError.SaveFailed, save.ErrorMessage);
            }

            session.BeginExpenseCheck();
            session.AdvanceDay(snapshot, metadata);
            return ExpenseResolutionResult.Success(outcome);
        }
    }
}
