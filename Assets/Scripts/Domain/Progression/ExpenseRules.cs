using System;

namespace Witch.Domain.Progression
{
    public sealed class ExpenseRules
    {
        public ExpenseRules(int completionDay)
        {
            if (completionDay < RunState.FirstDay)
            {
                throw new ArgumentOutOfRangeException(nameof(completionDay));
            }

            CompletionDay = completionDay;
        }

        public int CompletionDay { get; }

        public ExpenseOutcome Resolve(RunState runState, int expenseValue)
        {
            if (runState == null)
            {
                throw new ArgumentNullException(nameof(runState));
            }

            if (expenseValue < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(expenseValue));
            }

            if (runState.Money < expenseValue)
            {
                return new ExpenseOutcome(ExpenseOutcomeKind.GameOver, runState);
            }

            int remainingMoney = runState.Money - expenseValue;
            if (runState.CurrentDayNumber >= CompletionDay)
            {
                return new ExpenseOutcome(
                    ExpenseOutcomeKind.Victory,
                    runState.WithProgress(
                        runState.CurrentDayNumber,
                        remainingMoney,
                        runState.Suspicion,
                        runState.RewindCount));
            }

            return new ExpenseOutcome(
                ExpenseOutcomeKind.NextDay,
                runState.WithProgress(
                    runState.CurrentDayNumber + 1,
                    remainingMoney,
                    runState.Suspicion,
                    runState.RewindCount));
        }
    }
}
