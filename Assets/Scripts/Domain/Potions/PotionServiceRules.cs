using System;
using Witch.Domain.Orders;
using Witch.Domain.Progression;

namespace Witch.Domain.Potions
{
    public sealed class PotionServiceRules
    {
        public PotionServiceRules(int correctPayment)
        {
            if (correctPayment <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(correctPayment));
            }

            CorrectPayment = correctPayment;
        }

        public int CorrectPayment { get; }

        public PreparedPotion Brew(HerbPair herbPair)
        {
            if (herbPair == null)
            {
                throw new ArgumentNullException(nameof(herbPair));
            }

            return new PreparedPotion(herbPair);
        }

        public ServiceOutcome Serve(RunState runState, OrderRequest order, PreparedPotion servedPotion)
        {
            if (runState == null)
            {
                throw new ArgumentNullException(nameof(runState));
            }

            if (order == null)
            {
                throw new ArgumentNullException(nameof(order));
            }

            if (servedPotion == null)
            {
                throw new ArgumentNullException(nameof(servedPotion));
            }

            if (runState.Suspicion >= RunState.RewindSuspicionThreshold)
            {
                throw new InvalidOperationException("Service cannot continue while rewind is pending.");
            }

            if (order.IsCorrect(servedPotion))
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
