using UnityEngine;

namespace Witch.Infrastructure.Content
{
    [CreateAssetMenu(menuName = "Witch/Content/Rewind Difficulty", fileName = "RewindDifficulty_")]
    public sealed class RewindDifficultyDefinition : StableContentDefinition
    {
        [SerializeField, Min(1)] private int startingPairCount = 3;
        [SerializeField, Min(1)] private int maximumPairCount = 8;

        public int StartingPairCount => startingPairCount;
        public int MaximumPairCount => maximumPairCount;

        public void Configure(string id, int startingPairs, int maximumPairs)
        {
            SetStableId(id);
            startingPairCount = startingPairs;
            maximumPairCount = maximumPairs;
        }
    }
}
