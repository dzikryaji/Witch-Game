using System;
using Witch.Domain;

namespace Witch.Application.Flow
{
    public sealed class GameFlowResult
    {
        private GameFlowResult(bool succeeded, GameFlowError error, GamePhase? phase)
        {
            Succeeded = succeeded;
            Error = error;
            Phase = phase;
        }

        public bool Succeeded { get; }

        public GameFlowError Error { get; }

        public GamePhase? Phase { get; }

        public static GameFlowResult Success(GamePhase phase)
        {
            return new GameFlowResult(true, GameFlowError.None, phase);
        }

        public static GameFlowResult Failure(GameFlowError error, GamePhase? unchangedPhase = null)
        {
            if (error == GameFlowError.None)
            {
                throw new ArgumentException("A failed game-flow result requires an error.", nameof(error));
            }

            return new GameFlowResult(false, error, unchangedPhase);
        }
    }
}
