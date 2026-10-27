using NUnit.Framework;
using Witch.Domain.Potions;

namespace Witch.Domain.Tests
{
    public sealed class RecipeCatalogTests
    {
        [Test]
        public void KnownPair_ReturnsItsRecipe()
        {
            var expected = new Recipe(
                "recipe.warming-tonic",
                new HerbPair("herb.ginger", "herb.mint"));
            var catalog = new RecipeCatalog(new[] { expected });

            bool found = catalog.TryFind(
                new HerbPair("herb.mint", "herb.ginger"),
                out Recipe recipe);

            Assert.That(found, Is.True);
            Assert.That(recipe, Is.SameAs(expected));
        }

        [Test]
        public void UnknownPair_IsNotAValidRecipe()
        {
            var catalog = new RecipeCatalog(new[]
            {
                new Recipe(
                    "recipe.warming-tonic",
                    new HerbPair("herb.ginger", "herb.mint"))
            });

            bool found = catalog.TryFind(
                new HerbPair("herb.ginger", "herb.sage"),
                out Recipe recipe);

            Assert.That(found, Is.False);
            Assert.That(recipe, Is.Null);
        }
    }
}
