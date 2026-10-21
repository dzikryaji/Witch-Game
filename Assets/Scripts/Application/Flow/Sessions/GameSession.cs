using System;
using System.Collections.Generic;
using Witch.Domain;
using Witch.Domain.Progression;

namespace Witch.Application.Flow
{
    public sealed class GameSession
    {
        private static readonly IReadOnlyDictionary<GamePhase, IReadOnlyCollection<GamePhase>> AllowedTransitions =
            new Dictionary<GamePhase, IReadOnlyCollection<GamePhase>>
            {
                { GamePhase.ShopPreparation, new[] { GamePhase.CustomerEncounter } },
                { GamePhase.CustomerEncounter, new[] { GamePhase.IncomeCheck, GamePhase.GameOver } },
                { GamePhase.IncomeCheck, new[] { GamePhase.EndOfDayShopping, GamePhase.Victory, GamePhase.GameOver } },
                { GamePhase.EndOfDayShopping, new[] { GamePhase.ShopPreparation } },
                { GamePhase.Victory, Array.Empty<GamePhase>() },
                { GamePhase.GameOver, Array.Empty<GamePhase>() }
            };

        internal GameSession(RunState runState, GamePhase initialPhase)
        {
            RunState = runState ?? throw new ArgumentNullException(nameof(runState));
            CurrentPhase = initialPhase;
        }

        public RunState RunState { get; }

        public GamePhase CurrentPhase { get; private set; }

        internal GameFlowResult TryTransition(GamePhase nextPhase)
        {
            if (!AllowedTransitions.TryGetValue(CurrentPhase, out IReadOnlyCollection<GamePhase> allowed)
                || !Contains(allowed, nextPhase))
            {
                return GameFlowResult.Failure(GameFlowError.InvalidTransition, CurrentPhase);
            }

            CurrentPhase = nextPhase;
            return GameFlowResult.Success(CurrentPhase);
        }

        private static bool Contains(IReadOnlyCollection<GamePhase> phases, GamePhase phase)
        {
            foreach (GamePhase candidate in phases)
            {
                if (candidate == phase)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
