using System;

namespace Witch.Domain.Progression
{
    public static class WitchHunterStageRules
    {
        public static int ResolveStageIndex(int completedRewinds, int stageCount)
        {
            if (stageCount < 1) throw new ArgumentOutOfRangeException(nameof(stageCount));
            if (completedRewinds < 0) throw new ArgumentOutOfRangeException(nameof(completedRewinds));

            return Math.Min(completedRewinds, stageCount - 1);
        }
    }
}
