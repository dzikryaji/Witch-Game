using System;
using UnityEngine;
using Witch.Application.Ports;

namespace Witch.Infrastructure.Platform
{
    public sealed class UnityMonotonicClock : IClock
    {
        private readonly DateTimeOffset anchorUtc;
        private readonly double anchorRealtime;

        public UnityMonotonicClock()
            : this(DateTimeOffset.UtcNow, Time.realtimeSinceStartupAsDouble)
        {
        }

        internal UnityMonotonicClock(DateTimeOffset anchorUtc, double anchorRealtime)
        {
            this.anchorUtc = anchorUtc.ToUniversalTime();
            this.anchorRealtime = anchorRealtime;
        }

        public DateTimeOffset UtcNow => anchorUtc.AddSeconds(
            Math.Max(0d, Time.realtimeSinceStartupAsDouble - anchorRealtime));
    }
}
