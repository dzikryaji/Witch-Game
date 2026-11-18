using System;

namespace Witch.Domain.Progression
{
    public static class MatchCardRules
    {
        public const int StartingPairCount = 8;
        public const int MaximumPairCount = 12;

        private static readonly int[] PairCountSteps = { 8, 9, 10, 11, 12 };

        public static int GetPairCount(int minigameWinCount)
        {
            return GetPairCount(minigameWinCount, StartingPairCount, MaximumPairCount);
        }

        public static int GetPairCount(int minigameWinCount, int startingPairCount, int maximumPairCount)
        {
            if (minigameWinCount < 0) throw new ArgumentOutOfRangeException(nameof(minigameWinCount));
            if (startingPairCount < 1) throw new ArgumentOutOfRangeException(nameof(startingPairCount));
            if (maximumPairCount < startingPairCount) throw new ArgumentOutOfRangeException(nameof(maximumPairCount));

            int step = Math.Min(minigameWinCount, PairCountSteps.Length - 1);
            return Math.Max(startingPairCount, Math.Min(PairCountSteps[step], maximumPairCount));
        }

        public static MatchCardOutcome Complete(
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
                throw new ArgumentException("Minigame state must belong to the active run and day.");
            }

            if (failedAttemptRunState.Suspicion != RunState.MinigameSuspicionThreshold)
            {
                throw new InvalidOperationException("Minigame completes only at the suspicion threshold.");
            }

            if (nextAttemptSeed == dayStartSnapshot.OrderSeed)
            {
                throw new ArgumentException("A completed minigame requires a new attempt seed.", nameof(nextAttemptSeed));
            }

            RunState restored = dayStartSnapshot.RunState.WithProgress(
                dayStartSnapshot.RunState.CurrentDayNumber,
                dayStartSnapshot.RunState.Money,
                0,
                failedAttemptRunState.MinigameWinCount + 1)
                .WithSeenOrders(failedAttemptRunState.SeenOrderIds)
                .WithTutorialFlagsFrom(failedAttemptRunState);
            return new MatchCardOutcome(restored, nextAttemptSeed);
        }
    }
}
