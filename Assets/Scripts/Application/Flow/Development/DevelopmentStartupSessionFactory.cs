#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Witch.Domain;
using Witch.Domain.Progression;

namespace Witch.Application.Flow
{
    public static class DevelopmentStartupSessionFactory
    {
        public static GameSession CreatePendingMinigame(
            NewRunSetup setup,
            int startingMoney,
            int minigameWinCount,
            int orderSeed,
            int boardSeed,
            DateTimeOffset capturedAtUtc)
        {
            if (setup == null)
            {
                throw new ArgumentNullException(nameof(setup));
            }

            var runState = new RunState(
                "development-minigame",
                RunState.FirstDay,
                startingMoney,
                RunState.MinigameSuspicionThreshold,
                minigameWinCount,
                setup.UnlockedCategoryIds,
                setup.UnlockedHerbIds,
                setup.UnlockedGuidebookEntryIds,
                null,
                null,
                new string[0],
                new string[0]);
            var snapshot = new DayStartSnapshot(
                runState,
                orderSeed,
                setup.FirstDayTimerDuration,
                setup.FirstDayExpenseId,
                setup.FirstDayExpenseValue,
                setup.SaveVersion,
                capturedAtUtc);
            var metadata = new ActiveAttemptMetadata(
                runState.RunId,
                runState.CurrentDayNumber,
                RunState.MinigameSuspicionThreshold,
                minigameWinCount,
                true,
                boardSeed,
                orderSeed,
                0,
                0,
                true);

            return new GameSession(snapshot, metadata, GamePhase.MinigameActive);
        }

        /// <summary>
        /// Editor-only: builds a ShopPreparation session for an arbitrary day as if naturally reached.
        /// Uses in-memory/debug callers only; does not touch real save data or achievements.
        /// </summary>
        public static GameSession CreateShopPreparationForDay(
            NewRunSetup setup,
            DayProgressionSetup progression,
            int dayNumber,
            int startingMoney,
            int orderSeed,
            DateTimeOffset capturedAtUtc)
        {
            if (setup == null) throw new ArgumentNullException(nameof(setup));
            if (progression == null) throw new ArgumentNullException(nameof(progression));

            int day = Math.Max(RunState.FirstDay, dayNumber);
            var baseState = new RunState(
                "debug-day-" + day + "-" + Guid.NewGuid().ToString("N"),
                day,
                startingMoney,
                0,
                0,
                setup.UnlockedCategoryIds,
                setup.UnlockedHerbIds,
                setup.UnlockedGuidebookEntryIds,
                day >= 2
                    ? new Dictionary<string, bool> { { "shop.tutorial.day1.guided", true } }
                    : null,
                null,
                new string[0],
                new string[0]);

            RunState dayState = progression.ApplyUnlocks(baseState);
            var snapshot = new DayStartSnapshot(
                dayState,
                orderSeed,
                progression.GetTimerDuration(day),
                progression.GetExpenseId(day),
                progression.GetExpenseValue(day),
                setup.SaveVersion,
                capturedAtUtc);
            var metadata = new ActiveAttemptMetadata(
                dayState.RunId,
                day,
                0,
                0,
                false,
                0,
                orderSeed);

            return new GameSession(snapshot, metadata, GamePhase.ShopPreparation);
        }
    }
}
#endif
