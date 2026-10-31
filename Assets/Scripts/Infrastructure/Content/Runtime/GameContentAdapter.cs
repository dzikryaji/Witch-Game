using System;
using System.Collections.Generic;
using System.Linq;
using Witch.Application.Flow;
using Witch.Domain.Orders;
using Witch.Domain.Potions;
using Witch.Domain.Progression;

namespace Witch.Infrastructure.Content
{
    public sealed class GameContentAdapter
    {
        private readonly GameContentValidator validator;

        public GameContentAdapter()
            : this(new GameContentValidator())
        {
        }

        public GameContentAdapter(GameContentValidator validator)
        {
            this.validator = validator ?? throw new ArgumentNullException(nameof(validator));
        }

        public GameContentLoadResult Load(GameContentCatalogDefinition catalog)
        {
            ContentValidationReport report = validator.Validate(catalog);
            if (!report.IsValid)
            {
                return GameContentLoadResult.Failure(report.Errors);
            }

            var recipes = new RecipeCatalog(catalog.Recipes.Select(definition =>
                new Recipe(
                    definition.StableId,
                    new HerbPair(definition.FirstHerbId, definition.SecondHerbId))));
            var herbs = catalog.Herbs.ToDictionary(
                definition => definition.StableId,
                definition => new HerbContent(
                    definition.StableId,
                    definition.DisplayName,
                    definition.CategoryId,
                    definition.ScientificName,
                    definition.TreatmentText,
                    definition.WarningText,
                    definition.HerbIllustration,
                    definition.AccessibleLabel,
                    definition.GuidebookPage,
                    definition.ClueIds),
                StringComparer.Ordinal);
            var orders = catalog.Orders.ToDictionary(
                definition => definition.StableId,
                definition => new OrderContent(
                    new OrderRequest(
                        definition.StableId,
                        new HerbPair(definition.FirstHerbId, definition.SecondHerbId)),
                    definition.ClueText,
                    definition.ClueIds),
                StringComparer.Ordinal);
            var customers = catalog.Customers.ToDictionary(
                definition => definition.StableId,
                definition => new CustomerContent(definition.StableId, definition.DisplayName),
                StringComparer.Ordinal);
            var days = catalog.Days.ToDictionary(
                definition => definition.DayNumber,
                definition => new DayContent(
                    definition.StableId,
                    definition.DayNumber,
                    TimeSpan.FromSeconds(definition.TimerSeconds),
                    definition.OrderIds,
                    definition.CustomerIds));
            var guidebook = catalog.GuidebookEntries.ToDictionary(
                definition => definition.StableId,
                definition => new GuidebookEntryContent(
                    definition.StableId,
                    definition.Title,
                    definition.CategoryId,
                    definition.ClueIds,
                    definition.BodyText,
                    definition.ContentsItems
                        .Where(item => item != null)
                        .Select(item => new GuidebookContentsItemContent(item.CategoryId, item.Name, item.Description))
                        .ToList()),
                StringComparer.Ordinal);
            var unlocks = catalog.CategoryUnlocks.Select(definition =>
                new CategoryUnlockContent(
                    definition.StableId,
                    definition.CategoryId,
                    definition.FirstDay,
                    definition.HerbIds,
                    definition.RecipeIds)).ToList();
            var cards = catalog.RewindCards.Select(definition =>
                new RewindCardContent(definition.StableId, definition.SymbolKey)).ToList();
            var difficulty = new RewindDifficultyContent(
                catalog.RewindDifficulty.StableId,
                catalog.RewindDifficulty.StartingPairCount,
                catalog.RewindDifficulty.MaximumPairCount);

            int firstDayNumber = catalog.RunConfiguration.FirstDay;
            DayContent firstDay = days[firstDayNumber];
            List<CategoryUnlockContent> firstDayUnlocks = unlocks
                .Where(value => value.FirstDay <= firstDayNumber)
                .ToList();
            string[] categoryIds = firstDayUnlocks
                .Select(value => value.CategoryId)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            string[] herbIds = firstDayUnlocks.SelectMany(value => value.HerbIds)
                .Distinct(StringComparer.Ordinal).ToArray();
            string[] recipeIds = firstDayUnlocks.SelectMany(value => value.RecipeIds)
                .Distinct(StringComparer.Ordinal).ToArray();
            var newRun = new NewRunSetup(
                catalog.RunConfiguration.StartingMoney,
                categoryIds,
                herbIds,
                recipeIds,
                Array.Empty<string>(),
                firstDay.TimerDuration,
                catalog.Economy.ExpenseIdPrefix + firstDayNumber,
                catalog.Economy.DailyExpense,
                catalog.RunConfiguration.SaveVersion);
            var dayProgression = new DayProgressionSetup(
                new DayTimingRules(
                    firstDay.TimerDuration,
                    TimeSpan.FromSeconds(catalog.RunConfiguration.DailyTimerIncreaseSeconds)),
                catalog.Economy.DailyExpense,
                catalog.Economy.ExpenseIdPrefix,
                catalog.RunConfiguration.SaveVersion,
                unlocks.Select(value => new DayUnlockSet(
                    value.FirstDay,
                    new[] { value.CategoryId },
                    value.HerbIds,
                    value.RecipeIds,
                    Array.Empty<string>())).ToArray());

            return GameContentLoadResult.Success(new GameRuntimeContent(
                recipes,
                herbs,
                orders,
                customers,
                days,
                guidebook,
                unlocks,
                cards,
                difficulty,
                newRun,
                dayProgression,
                new ExpenseRules(catalog.Economy.CompletionDay),
                catalog.Economy.CorrectOrderPayment));
        }
    }
}
