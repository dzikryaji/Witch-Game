using System;
using NUnit.Framework;
using Witch.Domain.Potions;

namespace Witch.Domain.Tests
{
    public sealed class PotionDraftTests
    {
        [Test]
        public void EmptyDraft_IsNotReadyToBrew()
        {
            var draft = new PotionDraft();

            Assert.That(draft.Ingredients, Is.Empty);
            Assert.That(draft.IsReadyToBrew, Is.False);
        }

        [Test]
        public void TwoDifferentGroundHerbs_AreReadyToBrew()
        {
            var draft = new PotionDraft(new[]
            {
                new PotIngredient("herb.ginger"),
                new PotIngredient("herb.mint")
            });

            Assert.That(draft.IsReadyToBrew, Is.True);
        }

        [Test]
        public void DuplicateHerbTypes_AreRejected()
        {
            Assert.That(
                () => new PotionDraft(new[]
                {
                    new PotIngredient("herb.ginger"),
                    new PotIngredient("herb.ginger")
                }),
                Throws.TypeOf<ArgumentException>());
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Ingredient_WithEmptyStableId_IsRejected(string herbId)
        {
            Assert.That(
                () => new PotIngredient(herbId),
                Throws.TypeOf<ArgumentException>());
        }
    }
}
