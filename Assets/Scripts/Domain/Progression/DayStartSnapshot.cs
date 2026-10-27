using System;

namespace Witch.Domain.Progression
{
    [Serializable]
    public sealed class DayStartSnapshot
    {
        public DayStartSnapshot(
            RunState runState,
            int orderSeed,
            TimeSpan timerDuration,
            string expenseId,
            int expenseValue,
            int saveVersion,
            DateTimeOffset capturedAtUtc)
        {
            if (timerDuration <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timerDuration));
            }

            if (string.IsNullOrWhiteSpace(expenseId))
            {
                throw new ArgumentException("Expense ID must not be empty.", nameof(expenseId));
            }

            if (expenseValue < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(expenseValue));
            }

            if (saveVersion < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(saveVersion));
            }

            RunState = runState ?? throw new ArgumentNullException(nameof(runState));
            OrderSeed = orderSeed;
            TimerDuration = timerDuration;
            ExpenseId = expenseId;
            ExpenseValue = expenseValue;
            SaveVersion = saveVersion;
            CapturedAtUtc = capturedAtUtc.ToUniversalTime();
        }

        public RunState RunState { get; }

        public int OrderSeed { get; }

        public TimeSpan TimerDuration { get; }

        public string ExpenseId { get; }

        public int ExpenseValue { get; }

        public int SaveVersion { get; }

        public DateTimeOffset CapturedAtUtc { get; }
    }
}
