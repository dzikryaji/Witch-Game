using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Witch.Domain.Potions
{
    [Serializable]
    public sealed class PotionDraft
    {
        public const int RequiredIngredientCount = 2;

        private readonly List<PotIngredient> ingredients;
        private readonly ReadOnlyCollection<PotIngredient> readOnlyIngredients;

        public PotionDraft(IEnumerable<PotIngredient> ingredients = null)
        {
            this.ingredients = ingredients == null
                ? new List<PotIngredient>()
                : new List<PotIngredient>(ingredients);

            if (this.ingredients.Count > RequiredIngredientCount)
            {
                throw new ArgumentException(
                    $"A potion draft supports at most {RequiredIngredientCount} ingredients.",
                    nameof(ingredients));
            }

            if (this.ingredients.Exists(ingredient => ingredient == null))
            {
                throw new ArgumentException("Potion ingredients must not contain null values.", nameof(ingredients));
            }

            var herbIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (PotIngredient ingredient in this.ingredients)
            {
                if (!herbIds.Add(ingredient.HerbId))
                {
                    throw new ArgumentException(
                        "A potion must use two different herb types.",
                        nameof(ingredients));
                }
            }

            readOnlyIngredients = this.ingredients.AsReadOnly();
        }

        public IReadOnlyList<PotIngredient> Ingredients => readOnlyIngredients;

        public bool IsReadyToBrew => ingredients.Count == RequiredIngredientCount;
    }
}
