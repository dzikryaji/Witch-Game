using System;

namespace Witch.Domain.Progression
{
    public sealed class DayTimingRules
    {
        public DayTimingRules(TimeSpan firstDayDuration, TimeSpan dailyIncrease)
        {
            if (firstDayDuration <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(firstDayDuration));
            }

            if (dailyIncrease < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(dailyIncrease));
            }

            FirstDayDuration = firstDayDuration;
            DailyIncrease = dailyIncrease;
        }

        public TimeSpan FirstDayDuration { get; }

        public TimeSpan DailyIncrease { get; }

        public TimeSpan GetDuration(int dayNumber)
        {
            if (dayNumber < RunState.FirstDay)
            {
                throw new ArgumentOutOfRangeException(nameof(dayNumber));
            }

            return FirstDayDuration + TimeSpan.FromTicks(
                checked(DailyIncrease.Ticks * (dayNumber - RunState.FirstDay)));
        }
    }
}
