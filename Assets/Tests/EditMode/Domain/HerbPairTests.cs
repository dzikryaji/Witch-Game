using System;
using NUnit.Framework;
using Witch.Domain.Potions;

namespace Witch.Domain.Tests
{
    public sealed class HerbPairTests
    {
        [Test]
        public void SameHerbsInDifferentOrder_AreEqual()
        {
            var first = new HerbPair("herb.ginger", "herb.mint");
            var reversed = new HerbPair("herb.mint", "herb.ginger");

            Assert.That(reversed, Is.EqualTo(first));
            Assert.That(reversed.GetHashCode(), Is.EqualTo(first.GetHashCode()));
        }

        [Test]
        public void DifferentHerbs_AreNotEqual()
        {
            var first = new HerbPair("herb.ginger", "herb.mint");
            var other = new HerbPair("herb.ginger", "herb.sage");

            Assert.That(other, Is.Not.EqualTo(first));
        }

        [Test]
        public void DuplicateHerbs_AreRejected()
        {
            Assert.That(
                () => new HerbPair("herb.ginger", "herb.ginger"),
                Throws.TypeOf<ArgumentException>());
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void EmptyStableHerbId_IsRejected(string herbId)
        {
            Assert.That(
                () => new HerbPair(herbId, "herb.mint"),
                Throws.TypeOf<ArgumentException>());
        }
    }
}
