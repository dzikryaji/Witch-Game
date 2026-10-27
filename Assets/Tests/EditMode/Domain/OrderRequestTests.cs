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
                new HerbPair("herb.ginger", "herb.mint"));
            var servedRecipe = new Recipe(
                "recipe.warming-tonic",
                new HerbPair("herb.mint", "herb.ginger"));

            Assert.That(order.IsCorrect(servedRecipe), Is.True);
        }

        [Test]
        public void DifferentValidRecipe_IsWrongForCurrentOrder()
        {
            var order = new OrderRequest(
                "order.fever.direct",
                new HerbPair("herb.ginger", "herb.mint"));
            var otherValidRecipe = new Recipe(
                "recipe.cooling-tonic",
                new HerbPair("herb.mint", "herb.sage"));

            Assert.That(order.IsCorrect(otherValidRecipe), Is.False);
        }
    }
}
