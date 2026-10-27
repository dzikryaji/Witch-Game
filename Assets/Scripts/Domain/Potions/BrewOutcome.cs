using System;

namespace Witch.Domain.Potions
{
    public enum BrewOutcomeKind
    {
        ValidRecipe,
        InvalidRecipe
    }

    public sealed class BrewOutcome
    {
        private BrewOutcome(BrewOutcomeKind kind, Recipe recipe)
        {
            Kind = kind;
            Recipe = recipe;
        }

        public BrewOutcomeKind Kind { get; }

        public Recipe Recipe { get; }

        public static BrewOutcome Valid(Recipe recipe)
        {
            return new BrewOutcome(
                BrewOutcomeKind.ValidRecipe,
                recipe ?? throw new ArgumentNullException(nameof(recipe)));
        }

        public static BrewOutcome Invalid()
        {
            return new BrewOutcome(BrewOutcomeKind.InvalidRecipe, null);
        }
    }
}
