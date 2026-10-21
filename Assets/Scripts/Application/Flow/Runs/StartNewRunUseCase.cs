using System;
using Witch.Domain;
using Witch.Domain.Inventory;
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
                var inventory = new InventoryState(command.StartingInventory);
                var firstDay = new DayState(DayState.FirstDay, command.FirstDayCustomerSequence);
                var runState = new RunState(
                    command.RunId,
                    command.StartingMoney,
                    inventory,
                    command.StartingHexCount,
                    firstDay);

                return StartNewRunResult.Success(new GameSession(runState, GamePhase.ShopPreparation));
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
