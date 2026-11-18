using Witch.Domain.Progression;

namespace Witch.Application.Flow
{
    public enum MinigameTransactionError
    {
        None,
        MissingSession,
        InvalidPhase,
        SaveFailed,
        SceneLoadFailed
    }

    public sealed class MinigameTransactionResult
    {
        private MinigameTransactionResult(
            bool succeeded,
            bool alreadyApplied,
            MinigameTransactionError error,
            string errorMessage,
            MatchCardBoard board)
        {
            Succeeded = succeeded;
            AlreadyApplied = alreadyApplied;
            Error = error;
            ErrorMessage = errorMessage;
            Board = board;
        }

        public bool Succeeded { get; }
        public bool AlreadyApplied { get; }
        public MinigameTransactionError Error { get; }
        public string ErrorMessage { get; }
        public MatchCardBoard Board { get; }

        public static MinigameTransactionResult Success(MatchCardBoard board, bool alreadyApplied = false)
        {
            return new MinigameTransactionResult(true, alreadyApplied, MinigameTransactionError.None, null, board);
        }

        public static MinigameTransactionResult Failure(MinigameTransactionError error, string message)
        {
            return new MinigameTransactionResult(false, false, error, message, null);
        }
    }
}
