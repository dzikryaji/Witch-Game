namespace Witch.Application.Flow
{
    public sealed class ServePotionResult
    {
        private ServePotionResult(Witch.Domain.Potions.ServiceOutcome outcome, string errorMessage)
        {
            Outcome = outcome;
            ErrorMessage = errorMessage;
        }

        public bool Succeeded => Outcome != null;

        public Witch.Domain.Potions.ServiceOutcome Outcome { get; }

        public string ErrorMessage { get; }

        public static ServePotionResult Success(Witch.Domain.Potions.ServiceOutcome outcome)
        {
            return new ServePotionResult(outcome, null);
        }

        public static ServePotionResult Failure(string errorMessage)
        {
            return new ServePotionResult(null, errorMessage);
        }
    }
}
