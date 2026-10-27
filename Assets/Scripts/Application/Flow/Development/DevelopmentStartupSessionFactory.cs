#if UNITY_EDITOR
using System;
using Witch.Domain;
using Witch.Domain.Progression;

namespace Witch.Application.Flow
{
    public static class DevelopmentStartupSessionFactory
    {
        public static GameSession CreatePendingRewind(
            NewRunSetup setup,
            int startingMoney,
            int rewindCount,
            int orderSeed,
            int boardSeed,
            DateTimeOffset capturedAtUtc)
        {
            if (setup == null)
            {
                throw new ArgumentNullException(nameof(setup));
            }

            var runState = new RunState(
                "development-rewind",
                RunState.FirstDay,
                startingMoney,
                RunState.RewindSuspicionThreshold,
                rewindCount,
                setup.UnlockedCategoryIds,
                setup.UnlockedHerbIds,
                setup.UnlockedRecipeIds,
                setup.UnlockedGuidebookEntryIds);
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
                RunState.RewindSuspicionThreshold,
                rewindCount,
                true,
                boardSeed,
                orderSeed);

            return new GameSession(snapshot, metadata, GamePhase.RewindMinigame);
        }
    }
}
#endif
