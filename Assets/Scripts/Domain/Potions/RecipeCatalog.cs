using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Witch.Domain.Potions
{
    public sealed class RecipeCatalog
    {
        private readonly Dictionary<HerbPair, Recipe> recipesByPair;
        private readonly ReadOnlyCollection<Recipe> recipes;

        public RecipeCatalog(IEnumerable<Recipe> recipes)
        {
            if (recipes == null)
            {
                throw new ArgumentNullException(nameof(recipes));
            }

            var recipeList = new List<Recipe>();
            var recipeIds = new HashSet<string>(StringComparer.Ordinal);
            recipesByPair = new Dictionary<HerbPair, Recipe>();

            foreach (Recipe recipe in recipes)
            {
                if (recipe == null)
                {
                    throw new ArgumentException("Recipes must not contain null values.", nameof(recipes));
                }

                if (!recipeIds.Add(recipe.RecipeId))
                {
                    throw new ArgumentException("Recipe IDs must be unique.", nameof(recipes));
                }

                if (recipesByPair.ContainsKey(recipe.HerbPair))
                {
                    throw new ArgumentException("A herb pair may define only one recipe.", nameof(recipes));
                }

                recipeList.Add(recipe);
                recipesByPair.Add(recipe.HerbPair, recipe);
            }

            this.recipes = recipeList.AsReadOnly();
        }

        public IReadOnlyList<Recipe> Recipes => recipes;

        public bool TryFind(HerbPair herbPair, out Recipe recipe)
        {
            if (herbPair == null)
            {
                recipe = null;
                return false;
            }

            return recipesByPair.TryGetValue(herbPair, out recipe);
        }
    }
}
