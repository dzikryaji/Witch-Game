using System;

namespace Witch.Application.Flow
{
    public enum PreparationResultKind
    {
        Ground,
        AddedToPot,
        GroundDiscarded,
        RemovedFromPot,
        Prepared,
        InvalidRecipeDiscarded,
        Discarded
    }

    public sealed class PreparationResult
    {
        private PreparationResult(bool succeeded, PreparationResultKind kind, string errorMessage)
        {
            Succeeded = succeeded;
            Kind = kind;
            ErrorMessage = errorMessage;
        }

        public bool Succeeded { get; }

        public PreparationResultKind Kind { get; }

        public string ErrorMessage { get; }

        public static PreparationResult Success(PreparationResultKind kind)
        {
            return new PreparationResult(true, kind, null);
        }

        public static PreparationResult Failure(string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                throw new ArgumentException("Preparation failure requires a message.", nameof(errorMessage));
            }

            return new PreparationResult(false, default, errorMessage);
        }
    }
}
