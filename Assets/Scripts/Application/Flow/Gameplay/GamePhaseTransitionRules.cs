using System.Collections.Generic;
using Witch.Domain;

namespace Witch.Application.Flow
{
    public static class GamePhaseTransitionRules
    {
        private static readonly IReadOnlyDictionary<GamePhase, IReadOnlyCollection<GamePhase>> AllowedTransitions =
            new Dictionary<GamePhase, IReadOnlyCollection<GamePhase>>
            {
                { GamePhase.ShopPreparation, new[] { GamePhase.CustomerArrival } },
                { GamePhase.CustomerArrival, new[] { GamePhase.CustomerEncounter, GamePhase.ClosingEncounter } },
                {
                    GamePhase.CustomerEncounter,
                    new[]
                    {
                        GamePhase.CustomerArrival,
                        GamePhase.ClosingEncounter,
                        GamePhase.RewindPending
                    }
                },
                {
                    GamePhase.ClosingEncounter,
                    new[] { GamePhase.ExpenseCheck, GamePhase.RewindPending }
                },
                {
                    GamePhase.ExpenseCheck,
                    new[] { GamePhase.DaySummary, GamePhase.Victory, GamePhase.GameOver }
                },
                { GamePhase.RewindPending, new[] { GamePhase.RewindMinigame } },
                { GamePhase.RewindMinigame, new[] { GamePhase.ShopPreparation } },
                { GamePhase.DaySummary, new[] { GamePhase.ShopPreparation } },
                { GamePhase.Victory, new GamePhase[0] },
                { GamePhase.GameOver, new[] { GamePhase.ShopPreparation } }
            };

        public static bool CanTransition(GamePhase from, GamePhase to)
        {
            if (!AllowedTransitions.TryGetValue(from, out IReadOnlyCollection<GamePhase> allowed))
            {
                return false;
            }

            foreach (GamePhase candidate in allowed)
            {
                if (candidate == to)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
