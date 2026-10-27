using Witch.Domain.Progression;

namespace Witch.Application.Flow
{
    public enum RewindTransactionError
    {
        None,
        MissingSession,
        InvalidPhase,
        SaveFailed,
        SceneLoadFailed
    }

    public sealed class RewindTransactionResult
    {
        private RewindTransactionResult(
            bool succeeded,
            bool alreadyApplied,
            RewindTransactionError error,
            string errorMessage,
            RewindBoard board)
        {
            Succeeded = succeeded;
            AlreadyApplied = alreadyApplied;
            Error = error;
            ErrorMessage = errorMessage;
            Board = board;
        }

        public bool Succeeded { get; }
        public bool AlreadyApplied { get; }
        public RewindTransactionError Error { get; }
        public string ErrorMessage { get; }
        public RewindBoard Board { get; }

        public static RewindTransactionResult Success(RewindBoard board, bool alreadyApplied = false)
        {
            return new RewindTransactionResult(true, alreadyApplied, RewindTransactionError.None, null, board);
        }

        public static RewindTransactionResult Failure(RewindTransactionError error, string message)
        {
            return new RewindTransactionResult(false, false, error, message, null);
        }
    }
}
