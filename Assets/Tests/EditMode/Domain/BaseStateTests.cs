using System;
using System.Collections.Generic;
using NUnit.Framework;
using Witch.Domain.Inventory;
using Witch.Domain.Potions;
using Witch.Domain.Progression;

namespace Witch.Domain.Tests
{
    public sealed class BaseStateTests
    {
        [Test]
        public void NewRun_HasExpectedInitialState()
        {
            var inventory = new InventoryState(new Dictionary<string, int>
            {
                { "herb.test", 3 }
            });
            var day = new DayState(1, new[] { "customer.villager.test" });
            var run = new RunState("run.test", 10, inventory, 1, day);
            var draft = new PotionDraft();

            Assert.That(run.RunId, Is.EqualTo("run.test"));
            Assert.That(run.Money, Is.EqualTo(10));
            Assert.That(run.Inventory.GetHerbQuantity("herb.test"), Is.EqualTo(3));
            Assert.That(run.HexCount, Is.EqualTo(1));
            Assert.That(run.CurrentDay.DayNumber, Is.EqualTo(1));
            Assert.That(run.CurrentDay.QualifyingIncome, Is.Zero);
            Assert.That(run.CurrentDay.CustomerSequence, Is.EqualTo(new[] { "customer.villager.test" }));
            Assert.That(draft.Ingredients, Is.Empty);
            Assert.That(draft.IsReadyToBrew, Is.False);
        }

        [Test]
        public void Inventory_RejectsNegativeQuantity()
        {
            Assert.That(
                () => new InventoryState(new Dictionary<string, int> { { "herb.test", -1 } }),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void PotionDraft_WithTwoDifferentGroundHerbs_IsReadyToBrew()
        {
            var draft = new PotionDraft(new[]
            {
                new PotIngredient("herb.ginger"),
                new PotIngredient("herb.mint")
            });

            Assert.That(draft.IsReadyToBrew, Is.True);
        }

        [Test]
        public void PotionDraft_RejectsDuplicateHerbTypes()
        {
            Assert.That(
                () => new PotionDraft(new[]
                {
                    new PotIngredient("herb.ginger"),
                    new PotIngredient("herb.ginger")
                }),
                Throws.TypeOf<ArgumentException>());
        }
    }
}
