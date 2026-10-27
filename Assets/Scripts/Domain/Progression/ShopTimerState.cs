using System;

namespace Witch.Domain.Progression
{
    [Serializable]
    public sealed class ShopTimerState
    {
        public ShopTimerState(TimeSpan remainingTime, bool hasActiveEncounter)
        {
            if (remainingTime < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(remainingTime));
            }

            RemainingTime = remainingTime;
            HasActiveEncounter = hasActiveEncounter;
        }

        public TimeSpan RemainingTime { get; }

        public bool HasActiveEncounter { get; }

        public bool QueueOpen => RemainingTime > TimeSpan.Zero;

        public bool CanAdmitCustomer => QueueOpen && !HasActiveEncounter;

        public bool CanCompleteActiveEncounter => HasActiveEncounter;

        public ShopTimerState Elapse(TimeSpan elapsed)
        {
            if (elapsed < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsed));
            }

            TimeSpan remaining = elapsed >= RemainingTime
                ? TimeSpan.Zero
                : RemainingTime - elapsed;
            return new ShopTimerState(remaining, HasActiveEncounter);
        }
    }
}
