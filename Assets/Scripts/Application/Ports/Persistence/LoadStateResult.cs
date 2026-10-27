using System;

namespace Witch.Application.Ports
{
    public sealed class LoadStateResult
    {
        private LoadStateResult(GameSaveState savedGame, LoadStateError error, string errorMessage)
        {
            SavedGame = savedGame;
            Error = error;
            ErrorMessage = errorMessage;
        }

        public bool Succeeded => Error == LoadStateError.None;

        public GameSaveState SavedGame { get; }

        public LoadStateError Error { get; }

        public string ErrorMessage { get; }

        public static LoadStateResult Success(GameSaveState savedGame)
        {
            return new LoadStateResult(
                savedGame ?? throw new ArgumentNullException(nameof(savedGame)),
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
