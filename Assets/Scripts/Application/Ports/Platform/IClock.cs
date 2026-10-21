using System;

namespace Witch.Application.Ports
{
    public interface IClock
    {
        DateTimeOffset UtcNow { get; }
    }
}
