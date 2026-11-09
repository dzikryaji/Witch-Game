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
            ValidateStableIds(catalog.Orders, "Order", errors);
            ValidateStableIds(catalog.Customers.Where(value => value != null).ToArray(), "Customer", errors);
            ValidateStableIds(catalog.Days, "Day", errors);
            ValidateStableIds(catalog.HerbCategories, "Herb category", errors);
            ValidateStableIds(catalog.RewindCards, "Rewind card", errors);
            ValidateRequiredDefinition(catalog.RewindDifficulty, "Rewind difficulty", errors);

            if (catalog.StartingMoney < 0 || catalog.CorrectOrderPayment < 0)
            {
                errors.Add("Game catalog contains invalid economy values.");
            }

            foreach (HerbCategoryDefinition category in catalog.HerbCategories.Where(value => value != null))
            {
                if (string.IsNullOrWhiteSpace(category.DisplayName))
                {
                    errors.Add("Herb category '" + category.StableId + "' needs a display name.");
                }

                if (string.IsNullOrWhiteSpace(category.Description))
                {
                    errors.Add("Herb category '" + category.StableId + "' needs a description.");
                }
            }

            foreach (HerbDefinition herb in catalog.Herbs.Where(value => value != null))
            {
                ValidateHerbGuidebookContent(herb, errors);
            }

            var herbIds = IdSet(catalog.Herbs);
            var customerIds = IdSet(catalog.Customers);
            var categoryIds = IdSet(catalog.HerbCategories);
            var herbsById = catalog.Herbs
                .Where(value => value != null && !string.IsNullOrWhiteSpace(value.StableId))
                .GroupBy(value => value.StableId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

            foreach (HerbDefinition herb in herbsById.Values)
            {
                ValidateReference(herb.CategoryId, categoryIds, "Herb '" + herb.StableId + "' category", errors);
            }

            foreach (HerbCategoryDefinition category in catalog.HerbCategories.Where(value => value != null))
            {
                if (!herbsById.Values.Any(herb => string.Equals(herb.CategoryId, category.StableId, StringComparison.Ordinal)))
                {
                    errors.Add("Herb category '" + category.StableId + "' needs at least one herb.");
                }
            }

            foreach (OrderDefinition order in catalog.Orders.Where(value => value != null))
            {
                if (order.OrderBubbles.Count == 0 || order.OrderBubbles.Any(string.IsNullOrWhiteSpace))
                {
                    errors.Add("Order '" + order.StableId + "' needs at least one non-empty bubble.");
                }
                ValidateReference(order.FirstHerbId, herbIds, "Order '" + order.StableId + "' first herb", errors);
                ValidateReference(order.SecondHerbId, herbIds, "Order '" + order.StableId + "' second herb", errors);
                if (order.Customer != null)
                {
                    ValidateReference(
                        order.Customer.StableId,
                        customerIds,
                        "Order '" + order.StableId + "' customer",
                        errors);
                }
                if (string.Equals(order.FirstHerbId, order.SecondHerbId, StringComparison.Ordinal))
                {
                    errors.Add("Order '" + order.StableId + "' must use two different herbs.");
                }
                else if (herbsById.TryGetValue(order.FirstHerbId, out HerbDefinition firstHerb)
                         && herbsById.TryGetValue(order.SecondHerbId, out HerbDefinition secondHerb)
                         && !string.Equals(firstHerb.CategoryId, secondHerb.CategoryId, StringComparison.Ordinal))
                {
                    errors.Add("Order '" + order.StableId + "' herbs must share one category.");
                }
            }

            var daysSeen = new HashSet<int>();
            var unlockedCategories = new HashSet<string>(StringComparer.Ordinal);
            var ordersById = catalog.Orders
                .Where(value => value != null && !string.IsNullOrWhiteSpace(value.StableId))
                .GroupBy(value => value.StableId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            foreach (DayDefinition day in catalog.Days.Where(value => value != null).OrderBy(value => value.DayNumber))
            {
                if (day.DayNumber < 1 || !daysSeen.Add(day.DayNumber))
                {
                    errors.Add("Day number '" + day.DayNumber + "' must be unique and positive.");
                }

                if (day.TimerSeconds < 1)
                {
                    errors.Add("Day '" + day.StableId + "' timer must be at least 1 second.");
                }

                if (day.DailyExpense < 0)
                {
                    errors.Add("Day '" + day.StableId + "' expense must not be negative.");
                }

                if (day.OrderDifficulties.Count < 1
                    || day.OrderDifficulties.Count > 2
                    || day.OrderDifficulties.Distinct().Count() != day.OrderDifficulties.Count)
                {
                    errors.Add("Day '" + day.StableId + "' needs one or two unique order difficulties.");
                }

                foreach (string categoryId in day.UnlockedCategoryIds)
                {
                    if (string.IsNullOrWhiteSpace(categoryId) || !categoryIds.Contains(categoryId))
                    {
                        errors.Add("Day '" + day.StableId + "' unlocks unknown category '" + categoryId + "'.");
                        continue;
                    }

                    if (!ordersById.Values.Any(order =>
                            order.Difficulty == OrderDifficulty.Simple
                            && OrderUsesCategory(order, categoryId, herbsById)))
                    {
                        errors.Add("Day '" + day.StableId + "' category '" + categoryId + "' needs a Simple introduction order.");
                    }

                    unlockedCategories.Add(categoryId);
                }

                ValidateAvailableOrders(day, ordersById.Values, herbsById, unlockedCategories, errors);
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

        private static void ValidateAvailableOrders(
            DayDefinition day,
            IEnumerable<OrderDefinition> orders,
            IReadOnlyDictionary<string, HerbDefinition> herbsById,
            ISet<string> unlockedCategories,
            ICollection<string> errors)
        {
            int availableOrderCount = orders.Count(order =>
                day.OrderDifficulties.Contains(order.Difficulty)
                && herbsById.TryGetValue(order.FirstHerbId, out HerbDefinition firstHerb)
                && herbsById.TryGetValue(order.SecondHerbId, out HerbDefinition secondHerb)
                && unlockedCategories.Contains(firstHerb.CategoryId)
                && unlockedCategories.Contains(secondHerb.CategoryId));
            if (availableOrderCount < 2)
            {
                errors.Add("Day '" + day.StableId + "' needs at least two available orders for an alternate order seed.");
            }
        }

        private static bool OrderUsesCategory(
            OrderDefinition order,
            string categoryId,
            IReadOnlyDictionary<string, HerbDefinition> herbsById)
        {
            return herbsById.TryGetValue(order.FirstHerbId, out HerbDefinition firstHerb)
                   && herbsById.TryGetValue(order.SecondHerbId, out HerbDefinition secondHerb)
                   && string.Equals(firstHerb.CategoryId, categoryId, StringComparison.Ordinal)
                   && string.Equals(secondHerb.CategoryId, categoryId, StringComparison.Ordinal);
        }

        private static void ValidateConfiguration(
            GameContentCatalogDefinition catalog,
            ISet<int> dayNumbers,
            ICollection<string> errors)
        {
            if (dayNumbers.Count == 0)
            {
                errors.Add("Game catalog needs at least one day.");
            }
            else if (!dayNumbers.SetEquals(Enumerable.Range(1, dayNumbers.Count)))
            {
                errors.Add("Day definitions must be contiguous starting at day 1.");
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

    }
}
