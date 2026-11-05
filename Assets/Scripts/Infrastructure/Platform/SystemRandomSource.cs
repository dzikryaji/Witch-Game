using System;
using Witch.Application.Ports;

namespace Witch.Infrastructure.Platform
{
    public sealed class SystemRandomSource : IRandomSource
    {
        private readonly Random random = new Random(Guid.NewGuid().GetHashCode());

        public int NextInt(int minimumInclusive, int maximumExclusive)
        {
            if (minimumInclusive >= maximumExclusive) throw new ArgumentOutOfRangeException(nameof(maximumExclusive));
            return random.Next(minimumInclusive, maximumExclusive);
        }
    }
}
