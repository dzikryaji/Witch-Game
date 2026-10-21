using System;

namespace Witch.Application.Flow
{
    public sealed class StartNewGameResult
    {
        private StartNewGameResult(GameSession session, StartNewGameError error, string errorMessage)
        {
            Session = session;
            Error = error;
            ErrorMessage = errorMessage;
        }

        public bool Succeeded => Error == StartNewGameError.None;

        public GameSession Session { get; }

        public StartNewGameError Error { get; }

        public string ErrorMessage { get; }

        public static StartNewGameResult Success(GameSession session)
        {
            return new StartNewGameResult(
                session ?? throw new ArgumentNullException(nameof(session)),
                StartNewGameError.None,
                null);
        }

        public static StartNewGameResult Failure(StartNewGameError error, string errorMessage)
        {
            if (error == StartNewGameError.None)
            {
                throw new ArgumentException("A failed new-game result requires an error.", nameof(error));
            }

            return new StartNewGameResult(null, error, errorMessage);
        }
    }
}
