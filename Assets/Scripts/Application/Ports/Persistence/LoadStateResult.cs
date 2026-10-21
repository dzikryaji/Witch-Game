using System;
using Witch.Domain.Progression;

namespace Witch.Application.Ports
{
    public sealed class LoadStateResult
    {
        private LoadStateResult(RunState runState, LoadStateError error, string errorMessage)
        {
            RunState = runState;
            Error = error;
            ErrorMessage = errorMessage;
        }

        public bool Succeeded => Error == LoadStateError.None;

        public RunState RunState { get; }

        public LoadStateError Error { get; }

        public string ErrorMessage { get; }

        public static LoadStateResult Success(RunState runState)
        {
            return new LoadStateResult(
                runState ?? throw new ArgumentNullException(nameof(runState)),
                LoadStateError.None,
                null);
        }

        public static LoadStateResult Failure(LoadStateError error, string errorMessage)
        {
            if (error == LoadStateError.None)
            {
                throw new ArgumentException("A failed load result requires an error.", nameof(error));
            }

            return new LoadStateResult(null, error, errorMessage);
        }
    }
}
