using System;
using Witch.Domain.Progression;

namespace Witch.Application.Ports
{
    public sealed class ActiveAttemptMetadataLoadResult
    {
        private ActiveAttemptMetadataLoadResult(
            ActiveAttemptMetadata metadata,
            LoadStateError error,
            string errorMessage)
        {
            Metadata = metadata;
            Error = error;
            ErrorMessage = errorMessage;
        }

        public bool Succeeded => Error == LoadStateError.None;
        public ActiveAttemptMetadata Metadata { get; }
        public LoadStateError Error { get; }
        public string ErrorMessage { get; }

        public static ActiveAttemptMetadataLoadResult Success(ActiveAttemptMetadata metadata)
        {
            return new ActiveAttemptMetadataLoadResult(
                metadata ?? throw new ArgumentNullException(nameof(metadata)),
                LoadStateError.None,
                null);
        }

        public static ActiveAttemptMetadataLoadResult Failure(LoadStateError error, string message)
        {
            if (error == LoadStateError.None)
            {
                throw new ArgumentException("A failed load result requires an error.", nameof(error));
            }

            return new ActiveAttemptMetadataLoadResult(null, error, message);
        }
    }
}
