using System;

namespace Witch.Domain.Potions
{
    [Serializable]
    public sealed class Recipe
    {
        public Recipe(string recipeId, HerbPair herbPair)
        {
            if (string.IsNullOrWhiteSpace(recipeId))
            {
                throw new ArgumentException("Recipe ID must not be empty.", nameof(recipeId));
            }

            RecipeId = recipeId;
            HerbPair = herbPair ?? throw new ArgumentNullException(nameof(herbPair));
        }

        public string RecipeId { get; }

        public HerbPair HerbPair { get; }
    }
}
