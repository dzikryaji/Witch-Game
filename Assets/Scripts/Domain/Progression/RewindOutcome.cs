using System;

namespace Witch.Domain.Progression
{
    public sealed class RewindOutcome
    {
        public RewindOutcome(RunState restoredRunState, int nextAttemptSeed)
        {
            RestoredRunState = restoredRunState ?? throw new ArgumentNullException(nameof(restoredRunState));
            NextAttemptSeed = nextAttemptSeed;
        }

        public RunState RestoredRunState { get; }

        public int NextAttemptSeed { get; }
    }
}
