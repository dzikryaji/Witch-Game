using NUnit.Framework;
using Witch.Domain.Orders;
using Witch.Domain.Potions;

namespace Witch.Domain.Tests
{
    public sealed class OrderRequestTests
    {
        [Test]
        public void ExactRequestedPair_IsCorrectRegardlessOfSelectionOrder()
        {
            var order = new OrderRequest(
                "order.fever.direct",
                new HerbPair("herb.ginger", "herb.peppermint"));
            var servedPotion = new PreparedPotion(
                new HerbPair("herb.peppermint", "herb.ginger"));

            Assert.That(order.IsCorrect(servedPotion), Is.True);
        }

        [Test]
        public void DifferentPair_IsWrongForCurrentOrder()
        {
            var order = new OrderRequest(
                "order.fever.direct",
                new HerbPair("herb.ginger", "herb.peppermint"));
            var otherPotion = new PreparedPotion(
                new HerbPair("herb.peppermint", "herb.sage"));

            Assert.That(order.IsCorrect(otherPotion), Is.False);
        }
    }
}
