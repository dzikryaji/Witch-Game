using Witch.Domain.Progression;

namespace Witch.Application.Flow
{
    public enum ExpenseResolutionError
    {
        None,
        MissingSession,
        InvalidPhase,
        SaveFailed,
        SceneLoadFailed
    }

    public sealed class ExpenseResolutionResult
    {
        private ExpenseResolutionResult(
            bool succeeded,
            ExpenseResolutionError error,
            string errorMessage,
            ExpenseOutcome outcome)
        {
            Succeeded = succeeded;
            Error = error;
            ErrorMessage = errorMessage;
            Outcome = outcome;
        }

        public bool Succeeded { get; }
        public ExpenseResolutionError Error { get; }
        public string ErrorMessage { get; }
        public ExpenseOutcome Outcome { get; }

        public static ExpenseResolutionResult Success(ExpenseOutcome outcome = null)
        {
            return new ExpenseResolutionResult(true, ExpenseResolutionError.None, null, outcome);
        }

        public static ExpenseResolutionResult Failure(ExpenseResolutionError error, string message)
        {
            return new ExpenseResolutionResult(false, error, message, null);
        }
    }
}
