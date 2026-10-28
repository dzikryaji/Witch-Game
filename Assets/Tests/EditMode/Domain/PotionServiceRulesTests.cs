using NUnit.Framework;
using Witch.Domain.Orders;
using Witch.Domain.Potions;
using Witch.Domain.Progression;

namespace Witch.Domain.Tests
{
    public sealed class PotionServiceRulesTests
    {
        private static readonly Recipe CorrectRecipe = new Recipe(
            "recipe.warming-tonic",
            new HerbPair("herb.ginger", "herb.mint"));

        private static readonly Recipe OtherRecipe = new Recipe(
            "recipe.cooling-tonic",
            new HerbPair("herb.mint", "herb.sage"));

        [Test]
        public void CorrectRecipe_PaysFixedAmountWithoutSuspicion()
        {
            PotionServiceRules rules = CreateRules();
            RunState run = CreateRunState();
            var order = new OrderRequest("order.fever", CorrectRecipe.HerbPair);

            ServiceOutcome outcome = rules.Serve(run, order, CorrectRecipe);

            Assert.That(outcome.Kind, Is.EqualTo(ServiceOutcomeKind.Correct));
            Assert.That(outcome.Payment, Is.EqualTo(10));
            Assert.That(outcome.SuspicionAdded, Is.Zero);
            Assert.That(outcome.UpdatedRunState.Money, Is.EqualTo(10));
            Assert.That(outcome.UpdatedRunState.Suspicion, Is.Zero);
        }

        [Test]
        public void OtherValidRecipe_PaysNothingAndAddsOneSuspicion()
        {
            PotionServiceRules rules = CreateRules();
            RunState run = CreateRunState();
            var order = new OrderRequest("order.fever", CorrectRecipe.HerbPair);

            ServiceOutcome outcome = rules.Serve(run, order, OtherRecipe);

            Assert.That(outcome.Kind, Is.EqualTo(ServiceOutcomeKind.WrongValid));
            Assert.That(outcome.Payment, Is.Zero);
            Assert.That(outcome.SuspicionAdded, Is.EqualTo(1));
            Assert.That(outcome.UpdatedRunState.Money, Is.Zero);
            Assert.That(outcome.UpdatedRunState.Suspicion, Is.EqualTo(1));
        }

        [Test]
        public void UnknownPair_BrewsFreeformPotionAndDoesNotChangeRun()
        {
            PotionServiceRules rules = CreateRules();
            RunState run = CreateRunState();

            BrewOutcome brew = rules.Brew(new HerbPair("herb.ginger", "herb.sage"));

            Assert.That(brew.Kind, Is.EqualTo(BrewOutcomeKind.ValidRecipe));
            Assert.That(brew.Recipe.HerbPair, Is.EqualTo(new HerbPair("herb.ginger", "herb.sage")));
            Assert.That(run.Money, Is.Zero);
            Assert.That(run.Suspicion, Is.Zero);
        }

        [Test]
        public void Discard_DoesNotChangeMoneyOrSuspicion()
        {
            PotionServiceRules rules = CreateRules();
            RunState run = CreateRunState();

            ServiceOutcome outcome = rules.Discard(run);

            Assert.That(outcome.Kind, Is.EqualTo(ServiceOutcomeKind.Discarded));
            Assert.That(outcome.Payment, Is.Zero);
            Assert.That(outcome.SuspicionAdded, Is.Zero);
            Assert.That(outcome.UpdatedRunState, Is.SameAs(run));
        }

        private static PotionServiceRules CreateRules()
        {
            return new PotionServiceRules(
                10,
                new RecipeCatalog(new[] { CorrectRecipe, OtherRecipe }));
        }

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
                new[] { CorrectRecipe.RecipeId, OtherRecipe.RecipeId },
                new[] { "guidebook.warm" });
        }
    }
}
