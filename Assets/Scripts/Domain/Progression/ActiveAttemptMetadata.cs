using System;
using Witch.Domain.Orders;

namespace Witch.Domain.Progression
{
    [Serializable]
    public sealed class ActiveAttemptMetadata
    {
        public ActiveAttemptMetadata(
            string runId,
            int dayNumber,
            int suspicion,
            int minigameWinCount,
            bool pendingMinigame,
            int minigameBoardSeed,
            int activeOrderSeed,
            int wrongServiceCountToday = 0,
            int serviceCountToday = 0,
            bool minigamePlayedToday = false,
            int correctServiceCountToday = 0,
            int coinsEarnedToday = 0,
            int wrongOrdersForSummary = 0,
            OrderSelectionState selection = null)
        {
            if (string.IsNullOrWhiteSpace(runId))
            {
                throw new ArgumentException("Run ID must not be empty.", nameof(runId));
            }

            if (dayNumber < RunState.FirstDay)
            {
                throw new ArgumentOutOfRangeException(nameof(dayNumber));
            }

            if (suspicion < 0 || suspicion > RunState.MinigameSuspicionThreshold)
            {
                throw new ArgumentOutOfRangeException(nameof(suspicion));
            }

            if (minigameWinCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minigameWinCount));
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

            if (pendingMinigame && suspicion != RunState.MinigameSuspicionThreshold)
            {
                throw new ArgumentException("Pending minigame requires suspicion at the threshold.", nameof(suspicion));
            }

            RunId = runId;
            DayNumber = dayNumber;
            Suspicion = suspicion;
            MinigameWinCount = minigameWinCount;
            PendingMinigame = pendingMinigame;
            MinigameBoardSeed = minigameBoardSeed;
            ActiveOrderSeed = activeOrderSeed;
            WrongServiceCountToday = wrongServiceCountToday;
            MinigamePlayedToday = minigamePlayedToday;
            ServiceCountToday = serviceCountToday;
            CorrectServiceCountToday = correctServiceCountToday;
            CoinsEarnedToday = coinsEarnedToday;
            WrongOrdersForSummary = wrongOrdersForSummary;
            Selection = selection ?? new OrderSelectionState();
        }

        public string RunId { get; }

        public int DayNumber { get; }

        public int Suspicion { get; }

        public int MinigameWinCount { get; }

        public bool PendingMinigame { get; }

        public int MinigameBoardSeed { get; }

        public int ActiveOrderSeed { get; }

        public int WrongServiceCountToday { get; }

        public bool MinigamePlayedToday { get; }

        public int ServiceCountToday { get; }

        public int CorrectServiceCountToday { get; }

        public int CoinsEarnedToday { get; }

        public int WrongOrdersForSummary { get; }

        public OrderSelectionState Selection { get; }

        public ActiveAttemptMetadata WithSelection(OrderSelectionState selection)
        {
            return new ActiveAttemptMetadata(
                RunId,
                DayNumber,
                Suspicion,
                MinigameWinCount,
                PendingMinigame,
                MinigameBoardSeed,
                ActiveOrderSeed,
                WrongServiceCountToday,
                ServiceCountToday,
                MinigamePlayedToday,
                CorrectServiceCountToday,
                CoinsEarnedToday,
                WrongOrdersForSummary,
                selection);
        }
    }
}
