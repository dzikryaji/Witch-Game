using System;

namespace Witch.Application.Flow
{
    public sealed class LoadRunResult
    {
        private LoadRunResult(GameSession session, LoadRunError error, string errorMessage)
        {
            Session = session;
            Error = error;
            ErrorMessage = errorMessage;
        }

        public bool Succeeded => Error == LoadRunError.None;

        public GameSession Session { get; }

        public LoadRunError Error { get; }

        public string ErrorMessage { get; }

        public static LoadRunResult Success(GameSession session)
        {
            return new LoadRunResult(
                session ?? throw new ArgumentNullException(nameof(session)),
                LoadRunError.None,
                null);
        }

        public static LoadRunResult Failure(LoadRunError error, string errorMessage)
        {
            if (error == LoadRunError.None)
            {
                throw new ArgumentException("A failed load-run result requires an error.", nameof(error));
            }

            return new LoadRunResult(null, error, errorMessage);
        }
    }
}
