using System;
using Witch.Domain;
using Witch.Domain.Progression;

namespace Witch.Application.Flow
{
    public sealed class StartNewRunUseCase : IStartNewRunUseCase
    {
        public StartNewRunResult Execute(StartNewRunCommand command)
        {
            if (command == null)
            {
                return StartNewRunResult.Failure(
                    StartNewRunError.MissingCommand,
                    "Start command is required.");
            }

            try
            {
                var runState = new RunState(
                    command.RunId,
                    RunState.FirstDay,
                    command.StartingMoney,
                    0,
                    0,
                    command.UnlockedCategoryIds,
                    command.UnlockedHerbIds,
                    command.UnlockedRecipeIds,
                    command.UnlockedGuidebookEntryIds,
                    command.TutorialFlags,
                    command.NarrativeFlags);
                var snapshot = new DayStartSnapshot(
                    runState,
                    command.FirstDayOrderSeed,
                    command.FirstDayTimerDuration,
                    command.FirstDayExpenseId,
                    command.FirstDayExpenseValue,
                    command.SaveVersion,
                    command.CapturedAtUtc);
                var metadata = new ActiveAttemptMetadata(
                    runState.RunId,
                    runState.CurrentDayNumber,
                    0,
                    0,
                    false,
                    0,
                    command.FirstDayOrderSeed);

                return StartNewRunResult.Success(
                    new GameSession(snapshot, metadata, GamePhase.ShopPreparation));
            }
            catch (ArgumentException exception)
            {
                return StartNewRunResult.Failure(
                    StartNewRunError.InvalidInitialState,
                    exception.Message);
            }
        }
    }
}
