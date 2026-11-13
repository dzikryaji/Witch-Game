using System;

namespace Witch.Domain.Progression
{
    [Serializable]
    public sealed class ActiveAttemptMetadata
    {
        public ActiveAttemptMetadata(
            string runId,
            int dayNumber,
            int suspicion,
            int rewindCount,
            bool pendingRewind,
            int minigameBoardSeed,
            int activeOrderSeed,
            int wrongServiceCountToday = 0,
            int serviceCountToday = 0,
            bool minigamePlayedToday = false,
            int correctServiceCountToday = 0,
            int coinsEarnedToday = 0,
            int wrongOrdersForSummary = 0)
        {
            if (string.IsNullOrWhiteSpace(runId))
            {
                throw new ArgumentException("Run ID must not be empty.", nameof(runId));
            }

            if (dayNumber < RunState.FirstDay)
            {
                throw new ArgumentOutOfRangeException(nameof(dayNumber));
            }

            if (suspicion < 0 || suspicion > RunState.RewindSuspicionThreshold)
            {
                throw new ArgumentOutOfRangeException(nameof(suspicion));
            }

            if (rewindCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rewindCount));
            }

            if (wrongServiceCountToday < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(wrongServiceCountToday));
            }

            if (serviceCountToday < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(serviceCountToday));
            }

            if (correctServiceCountToday < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(correctServiceCountToday));
            }

            if (coinsEarnedToday < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(coinsEarnedToday));
            }

            if (wrongOrdersForSummary < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(wrongOrdersForSummary));
            }

            if (pendingRewind && suspicion != RunState.RewindSuspicionThreshold)
            {
                throw new ArgumentException("Pending rewind requires suspicion at the threshold.", nameof(suspicion));
            }

            RunId = runId;
            DayNumber = dayNumber;
            Suspicion = suspicion;
            RewindCount = rewindCount;
            PendingRewind = pendingRewind;
            MinigameBoardSeed = minigameBoardSeed;
            ActiveOrderSeed = activeOrderSeed;
            WrongServiceCountToday = wrongServiceCountToday;
            MinigamePlayedToday = minigamePlayedToday;
            ServiceCountToday = serviceCountToday;
            CorrectServiceCountToday = correctServiceCountToday;
            CoinsEarnedToday = coinsEarnedToday;
            WrongOrdersForSummary = wrongOrdersForSummary;
        }

        public string RunId { get; }

        public int DayNumber { get; }

        public int Suspicion { get; }

        public int RewindCount { get; }

        public bool PendingRewind { get; }

        public int MinigameBoardSeed { get; }

        public int ActiveOrderSeed { get; }

        public int WrongServiceCountToday { get; }

        public bool MinigamePlayedToday { get; }

        public int ServiceCountToday { get; }

        public int CorrectServiceCountToday { get; }

        public int CoinsEarnedToday { get; }

        public int WrongOrdersForSummary { get; }
    }
}
