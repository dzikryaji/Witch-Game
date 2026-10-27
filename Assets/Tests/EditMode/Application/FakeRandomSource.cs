using Witch.Application.Ports;

namespace Witch.Application.Tests
{
    internal sealed class FakeRandomSource : IRandomSource
    {
        public int NextValue { get; set; } = 1234;

        public int CallCount { get; private set; }

        public int NextInt(int minimumInclusive, int maximumExclusive)
        {
            CallCount++;
            return NextValue;
        }
    }
}
