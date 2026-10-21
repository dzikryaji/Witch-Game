using System;

namespace Witch.Application.Flow
{
    public sealed class StartNewRunResult
    {
        private StartNewRunResult(GameSession session, StartNewRunError error, string errorMessage)
        {
            Session = session;
            Error = error;
            ErrorMessage = errorMessage;
        }

        public bool Succeeded => Session != null;

        public GameSession Session { get; }

        public StartNewRunError Error { get; }

        public string ErrorMessage { get; }

        public static StartNewRunResult Success(GameSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            return new StartNewRunResult(session, StartNewRunError.None, null);
        }

        public static StartNewRunResult Failure(StartNewRunError error, string errorMessage)
        {
            if (error == StartNewRunError.None)
            {
                throw new ArgumentException("A failed start-run result requires an error.", nameof(error));
            }

            return new StartNewRunResult(null, error, errorMessage);
        }
    }
}
