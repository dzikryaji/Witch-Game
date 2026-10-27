using System;

namespace Witch.Domain.Progression
{
    public static class RewindRules
    {
        public const int StartingPairCount = 3;
        public const int MaximumPairCount = 8;

        public static int GetPairCount(int rewindCount)
        {
            if (rewindCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rewindCount));
            }

            return Math.Min(StartingPairCount + rewindCount, MaximumPairCount);
        }

        public static RewindOutcome Complete(
            DayStartSnapshot dayStartSnapshot,
            RunState failedAttemptRunState,
            int nextAttemptSeed)
        {
            if (dayStartSnapshot == null)
            {
                throw new ArgumentNullException(nameof(dayStartSnapshot));
            }

            if (failedAttemptRunState == null)
            {
                throw new ArgumentNullException(nameof(failedAttemptRunState));
            }

            if (!string.Equals(
                    dayStartSnapshot.RunState.RunId,
                    failedAttemptRunState.RunId,
                    StringComparison.Ordinal)
                || dayStartSnapshot.RunState.CurrentDayNumber != failedAttemptRunState.CurrentDayNumber)
            {
                throw new ArgumentException("Rewind state must belong to the active run and day.");
            }

            if (failedAttemptRunState.Suspicion != RunState.RewindSuspicionThreshold)
            {
                throw new InvalidOperationException("Rewind completes only at the suspicion threshold.");
            }

            if (nextAttemptSeed == dayStartSnapshot.OrderSeed)
            {
                throw new ArgumentException("A completed rewind requires a new attempt seed.", nameof(nextAttemptSeed));
            }

            RunState restored = dayStartSnapshot.RunState.WithProgress(
                dayStartSnapshot.RunState.CurrentDayNumber,
                dayStartSnapshot.RunState.Money,
                0,
                failedAttemptRunState.RewindCount + 1);
            return new RewindOutcome(restored, nextAttemptSeed);
        }
    }
}
