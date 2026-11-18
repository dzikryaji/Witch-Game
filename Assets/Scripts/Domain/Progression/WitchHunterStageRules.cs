using System;

namespace Witch.Domain.Progression
{
    public static class WitchHunterStageRules
    {
        public static int ResolveStageIndex(int completedMinigameWins, int stageCount)
        {
            if (stageCount < 1) throw new ArgumentOutOfRangeException(nameof(stageCount));
            if (completedMinigameWins < 0) throw new ArgumentOutOfRangeException(nameof(completedMinigameWins));

            return Math.Min(completedMinigameWins, stageCount - 1);
        }
    }
}
