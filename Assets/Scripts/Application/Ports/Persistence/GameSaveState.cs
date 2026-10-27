using System;
using Witch.Domain.Progression;

namespace Witch.Application.Ports
{
    public sealed class GameSaveState
    {
        public GameSaveState(
            DayStartSnapshot dayStartSnapshot,
            ActiveAttemptMetadata attemptMetadata)
        {
            DayStartSnapshot = dayStartSnapshot ?? throw new ArgumentNullException(nameof(dayStartSnapshot));
            AttemptMetadata = attemptMetadata ?? throw new ArgumentNullException(nameof(attemptMetadata));

            if (!string.Equals(
                    dayStartSnapshot.RunState.RunId,
                    attemptMetadata.RunId,
                    StringComparison.Ordinal)
                || dayStartSnapshot.RunState.CurrentDayNumber != attemptMetadata.DayNumber)
            {
                throw new ArgumentException("Save snapshot and attempt metadata must identify the same run and day.");
            }
        }

        public DayStartSnapshot DayStartSnapshot { get; }

        public ActiveAttemptMetadata AttemptMetadata { get; }
    }
}
