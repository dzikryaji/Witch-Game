using System;
using System.Collections.Generic;
using System.Linq;

namespace Witch.Infrastructure.Content
{
    public sealed class GameContentValidator
    {
        public ContentValidationReport Validate(GameContentCatalogDefinition catalog)
        {
            var errors = new List<string>();
            if (catalog == null)
            {
                errors.Add("Game content catalog is required.");
                return new ContentValidationReport(errors);
            }

            ValidateStableIds(catalog.Herbs, "Herb", errors);
            ValidateStableIds(catalog.Recipes, "Recipe", errors);
            ValidateStableIds(catalog.Orders, "Order", errors);
            ValidateStableIds(catalog.Customers, "Customer", errors);
            ValidateStableIds(catalog.Days, "Day", errors);
            ValidateStableIds(catalog.GuidebookEntries, "Guidebook entry", errors);
            ValidateStableIds(catalog.CategoryUnlocks, "Category unlock", errors);
            ValidateStableIds(catalog.RewindCards, "Rewind card", errors);
            ValidateRequiredDefinition(catalog.Economy, "Economy", errors);
            ValidateRequiredDefinition(catalog.RewindDifficulty, "Rewind difficulty", errors);
            ValidateRequiredDefinition(catalog.RunConfiguration, "run configuration", errors);

            foreach (HerbDefinition herb in catalog.Herbs.Where(value => value != null))
            {
                ValidateHerbGuidebookContent(herb, errors);
            }

            var herbIds = IdSet(catalog.Herbs);
            var recipeIds = IdSet(catalog.Recipes);
            var orderIds = IdSet(catalog.Orders);
            var customerIds = IdSet(catalog.Customers);
            var clueIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (HerbDefinition herb in catalog.Herbs.Where(value => value != null))
            {
                AddNonEmpty(clueIds, herb.ClueIds);
            }

            foreach (GuidebookEntryDefinition entry in catalog.GuidebookEntries.Where(value => value != null))
            {
                AddNonEmpty(clueIds, entry.ClueIds);
            }

            foreach (RecipeDefinition recipe in catalog.Recipes.Where(value => value != null))
            {
                if (string.Equals(recipe.FirstHerbId, recipe.SecondHerbId, StringComparison.Ordinal))
                {
                    errors.Add("Recipe '" + recipe.StableId + "' must use two different herbs.");
                }

                ValidateReference(recipe.FirstHerbId, herbIds, "Recipe '" + recipe.StableId + "' first herb", errors);
                ValidateReference(recipe.SecondHerbId, herbIds, "Recipe '" + recipe.StableId + "' second herb", errors);
            }

            foreach (OrderDefinition order in catalog.Orders.Where(value => value != null))
            {
                int matchingRecipes = catalog.Recipes.Count(recipe =>
                    recipe != null && SamePair(
                        recipe.FirstHerbId,
                        recipe.SecondHerbId,
                        order.FirstHerbId,
                        order.SecondHerbId));
                if (matchingRecipes != 1)
                {
                    errors.Add(
                        "Order '" + order.StableId + "' must resolve to exactly one recipe; found "
                        + matchingRecipes + ".");
                }

                foreach (string clueId in order.ClueIds)
                {
                    if (string.IsNullOrWhiteSpace(clueId) || !clueIds.Contains(clueId))
                    {
                        errors.Add("Order '" + order.StableId + "' uses unknown guidebook clue '" + clueId + "'.");
                    }
                }
            }

            foreach (CategoryUnlockDefinition unlock in catalog.CategoryUnlocks.Where(value => value != null))
            {
                if (unlock.FirstDay < 1)
                {
                    errors.Add("Category unlock '" + unlock.StableId + "' must start on Day 1 or later.");
                }

                ValidateReferences(unlock.HerbIds, herbIds, "Category unlock '" + unlock.StableId + "' herb", errors);
                ValidateReferences(unlock.RecipeIds, recipeIds, "Category unlock '" + unlock.StableId + "' recipe", errors);
            }

            var daysSeen = new HashSet<int>();
            var ordersById = catalog.Orders
                .Where(value => value != null && !string.IsNullOrWhiteSpace(value.StableId))
                .GroupBy(value => value.StableId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var herbsById = catalog.Herbs
                .Where(value => value != null && !string.IsNullOrWhiteSpace(value.StableId))
                .GroupBy(value => value.StableId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            foreach (DayDefinition day in catalog.Days.Where(value => value != null))
            {
                if (day.DayNumber < 1 || !daysSeen.Add(day.DayNumber))
                {
                    errors.Add("Day number '" + day.DayNumber + "' must be unique and positive.");
                }

                if (day.TimerSeconds < 1)
                {
                    errors.Add("Day '" + day.StableId + "' timer must be at least 1 second.");
                }

                ValidateDayOrders(
                    day,
                    orderIds,
                    ordersById,
                    herbsById,
                    catalog.GuidebookEntries,
                    catalog.CategoryUnlocks,
                    errors);
                ValidateReferences(day.CustomerIds, customerIds, "Day '" + day.StableId + "' customer", errors);
            }

            ValidateConfiguration(catalog, daysSeen, errors);
            return new ContentValidationReport(errors);
        }

        private static void ValidateHerbGuidebookContent(
            HerbDefinition herb,
            ICollection<string> errors)
        {
            string prefix = "Herb '" + herb.StableId + "'";
            if (string.IsNullOrWhiteSpace(herb.DisplayName))
            {
                errors.Add(prefix + " needs a display name.");
            }

            if (string.IsNullOrWhiteSpace(herb.CategoryId))
            {
                errors.Add(prefix + " needs a category ID.");
            }

            if (string.IsNullOrWhiteSpace(herb.ScientificName))
            {
                errors.Add(prefix + " needs a scientific name.");
            }

            if (string.IsNullOrWhiteSpace(herb.TreatmentText))
            {
                errors.Add(prefix + " needs treatment text.");
            }

            if (string.IsNullOrWhiteSpace(herb.WarningText))
            {
                errors.Add(prefix + " needs warning text.");
            }

            if (string.IsNullOrWhiteSpace(herb.AccessibleLabel))
            {
                errors.Add(prefix + " needs an accessible label.");
            }

            if (herb.GuidebookPage < 1)
            {
                errors.Add(prefix + " needs a positive guidebook page.");
            }
        }

        private static void ValidateDayOrders(
            DayDefinition day,
            ISet<string> knownOrderIds,
            IReadOnlyDictionary<string, OrderDefinition> ordersById,
            IReadOnlyDictionary<string, HerbDefinition> herbsById,
            IReadOnlyList<GuidebookEntryDefinition> guidebookEntries,
            IReadOnlyList<CategoryUnlockDefinition> unlocks,
            ICollection<string> errors)
        {
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (string orderId in day.OrderIds)
            {
                if (!unique.Add(orderId))
                {
                    errors.Add("Day '" + day.StableId + "' repeats order '" + orderId + "'.");
                }

                if (string.IsNullOrWhiteSpace(orderId) || !knownOrderIds.Contains(orderId))
                {
                    errors.Add("Day '" + day.StableId + "' order references unknown ID '" + orderId + "'.");
                }
            }

            if (unique.Count < 2)
            {
                errors.Add("Day '" + day.StableId + "' needs at least two distinct orders for an alternate order seed.");
            }

            var unlockedHerbs = new HashSet<string>(StringComparer.Ordinal);
            var unlockedCategories = new HashSet<string>(StringComparer.Ordinal);
            foreach (CategoryUnlockDefinition unlock in unlocks.Where(value =>
                         value != null && value.FirstDay <= day.DayNumber))
            {
                AddNonEmpty(unlockedHerbs, unlock.HerbIds);
                if (!string.IsNullOrWhiteSpace(unlock.CategoryId))
                {
                    unlockedCategories.Add(unlock.CategoryId);
                }
            }

            var unlockedClues = new HashSet<string>(StringComparer.Ordinal);
            foreach (string herbId in unlockedHerbs)
            {
                if (herbsById.TryGetValue(herbId, out HerbDefinition herb))
                {
                    AddNonEmpty(unlockedClues, herb.ClueIds);
                }
            }

            foreach (GuidebookEntryDefinition entry in guidebookEntries.Where(value =>
                         value != null && unlockedCategories.Contains(value.CategoryId)))
            {
                AddNonEmpty(unlockedClues, entry.ClueIds);
            }

            foreach (string orderId in unique)
            {
                if (!ordersById.TryGetValue(orderId, out OrderDefinition order))
                {
                    continue;
                }

                ValidateUnlockedHerb(day, order, order.FirstHerbId, unlockedHerbs, errors);
                ValidateUnlockedHerb(day, order, order.SecondHerbId, unlockedHerbs, errors);
                foreach (string clueId in order.ClueIds)
                {
                    if (!unlockedClues.Contains(clueId))
                    {
                        errors.Add(
                            "Day '" + day.StableId + "' order '" + order.StableId
                            + "' uses guidebook clue '" + clueId + "' before it is unlocked.");
                    }
                }
            }
        }

        private static void ValidateUnlockedHerb(
            DayDefinition day,
            OrderDefinition order,
            string herbId,
            ISet<string> unlockedHerbs,
            ICollection<string> errors)
        {
            if (string.IsNullOrWhiteSpace(herbId) || !unlockedHerbs.Contains(herbId))
            {
                errors.Add(
                    "Day '" + day.StableId + "' order '" + order.StableId
                    + "' uses locked or unknown herb '" + herbId + "'.");
            }
        }

        private static void ValidateConfiguration(
            GameContentCatalogDefinition catalog,
            ISet<int> dayNumbers,
            ICollection<string> errors)
        {
            if (catalog.RunConfiguration != null)
            {
                if (!dayNumbers.Contains(catalog.RunConfiguration.FirstDay))
                {
                    errors.Add("Run configuration first day has no Day definition.");
                }

                if (catalog.RunConfiguration.StartingMoney < 0
                    || catalog.RunConfiguration.SaveVersion < 1
                    || catalog.RunConfiguration.DailyTimerIncreaseSeconds < 0)
                {
                    errors.Add("Run configuration contains invalid numeric values.");
                }
            }

            if (catalog.Economy != null
                && (catalog.Economy.CorrectOrderPayment < 0
                    || catalog.Economy.DailyExpense < 0
                    || catalog.Economy.CompletionDay < 1
                    || string.IsNullOrWhiteSpace(catalog.Economy.ExpenseIdPrefix)))
            {
                errors.Add("Economy definition contains invalid values.");
            }

            if (catalog.RewindDifficulty != null)
            {
                int start = catalog.RewindDifficulty.StartingPairCount;
                int maximum = catalog.RewindDifficulty.MaximumPairCount;
                if (start < 1 || maximum < start)
                {
                    errors.Add("Rewind difficulty requires a positive pair range.");
                }
                else
                {
                    int uniqueCards = catalog.RewindCards
                        .Where(value => value != null && !string.IsNullOrWhiteSpace(value.StableId))
                        .Select(value => value.StableId)
                        .Distinct(StringComparer.Ordinal)
                        .Count();
                    if (uniqueCards < maximum)
                    {
                        errors.Add("Rewind pool requires at least " + maximum + " unique rewind cards.");
                    }
                }
            }
        }

        private static void ValidateRequiredDefinition(
            StableContentDefinition definition,
            string label,
            ICollection<string> errors)
        {
            if (definition == null)
            {
                errors.Add(label + " definition is required.");
            }
            else if (string.IsNullOrWhiteSpace(definition.StableId))
            {
                errors.Add(label + " stable ID is required.");
            }
        }

        private static void ValidateStableIds<T>(
            IReadOnlyList<T> definitions,
            string label,
            ICollection<string> errors)
            where T : StableContentDefinition
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (T definition in definitions)
            {
                if (definition == null)
                {
                    errors.Add(label + " definition must not be null.");
                    continue;
                }

                string id = definition.StableId ?? string.Empty;
                if (string.IsNullOrWhiteSpace(id))
                {
                    errors.Add(label + " stable ID is required.");
                }

                if (!seen.Add(id))
                {
                    errors.Add("Duplicate " + label.ToLowerInvariant() + " stable ID '" + id + "'.");
                }
            }
        }

        private static HashSet<string> IdSet<T>(IReadOnlyList<T> definitions)
            where T : StableContentDefinition
        {
            return new HashSet<string>(
                definitions
                    .Where(value => value != null && !string.IsNullOrWhiteSpace(value.StableId))
                    .Select(value => value.StableId),
                StringComparer.Ordinal);
        }

        private static void ValidateReferences(
            IReadOnlyList<string> references,
            ISet<string> knownIds,
            string label,
            ICollection<string> errors)
        {
            foreach (string id in references)
            {
                ValidateReference(id, knownIds, label, errors);
            }
        }

        private static void ValidateReference(
            string id,
            ISet<string> knownIds,
            string label,
            ICollection<string> errors)
        {
            if (string.IsNullOrWhiteSpace(id) || !knownIds.Contains(id))
            {
                errors.Add(label + " references unknown ID '" + id + "'.");
            }
        }

        private static void AddNonEmpty(ISet<string> target, IReadOnlyList<string> values)
        {
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    target.Add(value);
                }
            }
        }

        private static bool SamePair(string firstA, string secondA, string firstB, string secondB)
        {
            return string.Equals(firstA, firstB, StringComparison.Ordinal)
                       && string.Equals(secondA, secondB, StringComparison.Ordinal)
                   || string.Equals(firstA, secondB, StringComparison.Ordinal)
                       && string.Equals(secondA, firstB, StringComparison.Ordinal);
        }
    }
}
