using System;
using Witch.Application.Ports;

namespace Witch.Infrastructure.Platform
{
    public sealed class DeterministicRandomSource : IRandomSource
    {
        private readonly Random random;

        public DeterministicRandomSource(int seed)
        {
            random = new Random(seed);
        }

        public int NextInt(int minimumInclusive, int maximumExclusive)
        {
            if (minimumInclusive >= maximumExclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumExclusive));
            }

            return random.Next(minimumInclusive, maximumExclusive);
        }
    }
}
