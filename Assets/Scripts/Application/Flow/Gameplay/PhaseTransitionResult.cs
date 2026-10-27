using System;
using Witch.Domain;

namespace Witch.Application.Flow
{
    public sealed class PhaseTransitionResult
    {
        private PhaseTransitionResult(bool succeeded, PhaseTransitionError error, GamePhase? phase)
        {
            Succeeded = succeeded;
            Error = error;
            Phase = phase;
        }

        public bool Succeeded { get; }

        public PhaseTransitionError Error { get; }

        public GamePhase? Phase { get; }

        public static PhaseTransitionResult Success(GamePhase phase)
        {
            return new PhaseTransitionResult(true, PhaseTransitionError.None, phase);
        }

        public static PhaseTransitionResult Failure(
            PhaseTransitionError error,
            GamePhase? unchangedPhase = null)
        {
            if (error == PhaseTransitionError.None)
            {
                throw new ArgumentException("A failed game-flow result requires an error.", nameof(error));
            }

            return new PhaseTransitionResult(false, error, unchangedPhase);
        }
    }
}
