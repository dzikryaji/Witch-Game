using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Witch.Domain.Progression
{
    public sealed class RewindBoard
    {
        private readonly ReadOnlyCollection<int> cards;

        private RewindBoard(int seed, int pairCount, IList<int> cards)
        {
            Seed = seed;
            PairCount = pairCount;
            this.cards = new List<int>(cards).AsReadOnly();
        }

        public int Seed { get; }

        public int PairCount { get; }

        public IReadOnlyList<int> Cards => cards;

        public static RewindBoard Create(int seed, int rewindCount)
        {
            int pairCount = RewindRules.GetPairCount(rewindCount);
            var shuffled = new List<int>(pairCount * 2);
            for (int pair = 0; pair < pairCount; pair++)
            {
                shuffled.Add(pair);
                shuffled.Add(pair);
            }

            uint state = unchecked((uint)seed);
            if (state == 0)
            {
                state = 0x9E3779B9u;
            }

            for (int index = shuffled.Count - 1; index > 0; index--)
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                int swapIndex = (int)(state % (uint)(index + 1));
                int value = shuffled[index];
                shuffled[index] = shuffled[swapIndex];
                shuffled[swapIndex] = value;
            }

            return new RewindBoard(seed, pairCount, shuffled);
        }
    }
}
