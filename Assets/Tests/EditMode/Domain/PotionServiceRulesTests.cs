using NUnit.Framework;
using Witch.Domain.Orders;
using Witch.Domain.Potions;
using Witch.Domain.Progression;

namespace Witch.Domain.Tests
{
    public sealed class PotionServiceRulesTests
    {
        private static readonly PreparedPotion CorrectPotion = new PreparedPotion(
            new HerbPair("herb.ginger", "herb.mint"));

        private static readonly PreparedPotion OtherPotion = new PreparedPotion(
            new HerbPair("herb.mint", "herb.sage"));

        [Test]
        public void CorrectPotion_PaysFixedAmountWithoutSuspicion()
        {
            PotionServiceRules rules = CreateRules();
            RunState run = CreateRunState();
            var order = new OrderRequest("order.fever", CorrectPotion.HerbPair);

            ServiceOutcome outcome = rules.Serve(run, order, CorrectPotion);

            Assert.That(outcome.Kind, Is.EqualTo(ServiceOutcomeKind.Correct));
            Assert.That(outcome.Payment, Is.EqualTo(10));
            Assert.That(outcome.SuspicionAdded, Is.Zero);
            Assert.That(outcome.UpdatedRunState.Money, Is.EqualTo(10));
            Assert.That(outcome.UpdatedRunState.Suspicion, Is.Zero);
        }

        [Test]
        public void OtherPotion_PaysNothingAndAddsOneSuspicion()
        {
            PotionServiceRules rules = CreateRules();
            RunState run = CreateRunState();
            var order = new OrderRequest("order.fever", CorrectPotion.HerbPair);

            ServiceOutcome outcome = rules.Serve(run, order, OtherPotion);

            Assert.That(outcome.Kind, Is.EqualTo(ServiceOutcomeKind.WrongValid));
            Assert.That(outcome.Payment, Is.Zero);
            Assert.That(outcome.SuspicionAdded, Is.EqualTo(1));
            Assert.That(outcome.UpdatedRunState.Money, Is.Zero);
            Assert.That(outcome.UpdatedRunState.Suspicion, Is.EqualTo(1));
        }

        [Test]
        public void AnyTwoDifferentHerbs_BrewPotionWithoutChangingRunState()
        {
            PotionServiceRules rules = CreateRules();
            RunState run = CreateRunState();

            PreparedPotion potion = rules.Brew(new HerbPair("herb.ginger", "herb.sage"));

            Assert.That(potion.HerbPair, Is.EqualTo(new HerbPair("herb.ginger", "herb.sage")));
            Assert.That(run.Money, Is.Zero);
            Assert.That(run.Suspicion, Is.Zero);
        }

        private static PotionServiceRules CreateRules() => new PotionServiceRules(10);

        private static RunState CreateRunState()
        {
            return new RunState(
                "run.test",
                1,
                0,
                0,
                0,
                new[] { "category.warm" },
                new[] { "herb.ginger", "herb.mint", "herb.sage" },
                new[] { "guidebook.warm" });
        }
    }
}
