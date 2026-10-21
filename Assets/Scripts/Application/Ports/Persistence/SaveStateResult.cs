using System;

namespace Witch.Application.Ports
{
    public sealed class SaveStateResult
    {
        private SaveStateResult(SaveStateError error, string errorMessage)
        {
            Error = error;
            ErrorMessage = errorMessage;
        }

        public bool Succeeded => Error == SaveStateError.None;

        public SaveStateError Error { get; }

        public string ErrorMessage { get; }

        public static SaveStateResult Success()
        {
            return new SaveStateResult(SaveStateError.None, null);
        }

        public static SaveStateResult Failure(SaveStateError error, string errorMessage)
        {
            if (error == SaveStateError.None)
            {
                throw new ArgumentException("A failed save result requires an error.", nameof(error));
            }

            return new SaveStateResult(error, errorMessage);
        }
    }
}
