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
                        GamePhase.MinigamePending
                    }
                },
                {
                    GamePhase.ClosingEncounter,
                    new[] { GamePhase.ExpenseCheck, GamePhase.MinigamePending }
                },
                {
                    GamePhase.ExpenseCheck,
                    new[] { GamePhase.DaySummary, GamePhase.Victory, GamePhase.GameOver }
                },
                { GamePhase.MinigamePending, new[] { GamePhase.MinigameActive } },
                // { GamePhase.MinigameActive, new[] { GamePhase.ShopPreparation } },
                { GamePhase.MinigameActive, new[] { GamePhase.ShopPreparation, GamePhase.CustomerEncounter } },
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
