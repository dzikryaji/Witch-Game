using System;
using Witch.Domain.Orders;
using Witch.Domain.Progression;

namespace Witch.Domain.Potions
{
    public sealed class PotionServiceRules
    {
        private readonly RecipeCatalog recipeCatalog;

        public PotionServiceRules(int correctPayment, RecipeCatalog recipeCatalog)
        {
            if (correctPayment <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(correctPayment));
            }

            CorrectPayment = correctPayment;
            this.recipeCatalog = recipeCatalog ?? throw new ArgumentNullException(nameof(recipeCatalog));
        }

        public int CorrectPayment { get; }

        public BrewOutcome Brew(HerbPair herbPair)
        {
            if (herbPair == null)
            {
                throw new ArgumentNullException(nameof(herbPair));
            }

            return BrewOutcome.Valid(
                recipeCatalog.TryFind(herbPair, out Recipe recipe)
                    ? recipe
                    : new Recipe("potion.freeform", herbPair));
        }

        public ServiceOutcome Serve(RunState runState, OrderRequest order, Recipe servedRecipe)
        {
            if (runState == null)
            {
                throw new ArgumentNullException(nameof(runState));
            }

            if (order == null)
            {
                throw new ArgumentNullException(nameof(order));
            }

            if (servedRecipe == null)
            {
                throw new ArgumentNullException(nameof(servedRecipe));
            }

            if (runState.Suspicion >= RunState.RewindSuspicionThreshold)
            {
                throw new InvalidOperationException("Service cannot continue while rewind is pending.");
            }

            if (order.IsCorrect(servedRecipe))
            {
                int updatedMoney = checked(runState.Money + CorrectPayment);
                return new ServiceOutcome(
                    ServiceOutcomeKind.Correct,
                    CorrectPayment,
                    0,
                    runState.WithEconomy(updatedMoney, runState.Suspicion));
            }

            return new ServiceOutcome(
                ServiceOutcomeKind.WrongValid,
                0,
                1,
                runState.WithEconomy(runState.Money, runState.Suspicion + 1));
        }

        public ServiceOutcome Discard(RunState runState)
        {
            return new ServiceOutcome(
                ServiceOutcomeKind.Discarded,
                0,
                0,
                runState ?? throw new ArgumentNullException(nameof(runState)));
        }
    }
}
