using System;
using Witch.Application.Ports;

namespace Witch.Application.Tests
{
    internal sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } =
            new DateTimeOffset(2026, 10, 23, 8, 0, 0, TimeSpan.Zero);
    }
}
