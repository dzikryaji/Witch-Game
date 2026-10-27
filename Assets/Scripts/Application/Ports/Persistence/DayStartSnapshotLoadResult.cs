using System;
using Witch.Domain.Progression;

namespace Witch.Application.Ports
{
    public sealed class DayStartSnapshotLoadResult
    {
        private DayStartSnapshotLoadResult(
            DayStartSnapshot snapshot,
            LoadStateError error,
            string errorMessage)
        {
            Snapshot = snapshot;
            Error = error;
            ErrorMessage = errorMessage;
        }

        public bool Succeeded => Error == LoadStateError.None;
        public DayStartSnapshot Snapshot { get; }
        public LoadStateError Error { get; }
        public string ErrorMessage { get; }

        public static DayStartSnapshotLoadResult Success(DayStartSnapshot snapshot)
        {
            return new DayStartSnapshotLoadResult(
                snapshot ?? throw new ArgumentNullException(nameof(snapshot)),
                LoadStateError.None,
                null);
        }

        public static DayStartSnapshotLoadResult Failure(LoadStateError error, string message)
        {
            if (error == LoadStateError.None)
            {
                throw new ArgumentException("A failed load result requires an error.", nameof(error));
            }

            return new DayStartSnapshotLoadResult(null, error, message);
        }
    }
}
