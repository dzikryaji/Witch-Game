using System;

namespace Witch.Domain.Progression
{
    public enum ExpenseOutcomeKind
    {
        NextDay,
        Victory,
        GameOver
    }

    public sealed class ExpenseOutcome
    {
        public ExpenseOutcome(ExpenseOutcomeKind kind, RunState updatedRunState)
        {
            Kind = kind;
            UpdatedRunState = updatedRunState ?? throw new ArgumentNullException(nameof(updatedRunState));
        }

        public ExpenseOutcomeKind Kind { get; }

        public RunState UpdatedRunState { get; }
    }
}
