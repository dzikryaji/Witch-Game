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
                    definition.GuidebookPage),
                StringComparer.Ordinal);
            var orders = catalog.Orders.ToDictionary(
                definition => definition.StableId,
                definition => new OrderContent(
                    new OrderRequest(
                        definition.StableId,
                        new HerbPair(definition.FirstHerbId, definition.SecondHerbId)),
                    definition.Difficulty,
                    definition.OrderBubbles),
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
                    definition.DailyExpense,
                    definition.UnlockedCategoryIds,
                    definition.OrderDifficulties,
                    definition.CustomerIds));
            var guidebook = catalog.GuidebookEntries.ToDictionary(
                definition => definition.StableId,
                definition => new GuidebookEntryContent(
                    definition.StableId,
                    definition.Title,
                    definition.CategoryId,
                    definition.BodyText,
                    definition.ContentsItems
                        .Where(item => item != null)
                        .Select(item => new GuidebookContentsItemContent(item.CategoryId, item.Name, item.Description))
                        .ToList()),
                StringComparer.Ordinal);
            var cards = catalog.RewindCards.Select(definition =>
                new RewindCardContent(definition.StableId, definition.SymbolKey)).ToList();
            var difficulty = new RewindDifficultyContent(
                catalog.RewindDifficulty.StableId,
                catalog.RewindDifficulty.StartingPairCount,
                catalog.RewindDifficulty.MaximumPairCount);

            int firstDayNumber = catalog.RunConfiguration.FirstDay;
            DayContent firstDay = days[firstDayNumber];
            string[] categoryIds = days.Values
                .Where(value => value.DayNumber <= firstDayNumber)
                .SelectMany(value => value.UnlockedCategoryIds)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            string[] herbIds = herbs.Values
                .Where(value => categoryIds.Contains(value.CategoryId, StringComparer.Ordinal))
                .Select(value => value.StableId)
                .ToArray();
            var newRun = new NewRunSetup(
                catalog.RunConfiguration.StartingMoney,
                categoryIds,
                herbIds,
                Array.Empty<string>(),
                firstDay.TimerDuration,
                catalog.RunConfiguration.ExpenseIdPrefix + firstDayNumber,
                firstDay.DailyExpense,
                catalog.RunConfiguration.SaveVersion);
            var dayProgression = new DayProgressionSetup(
                new DayTimingRules(
                    firstDay.TimerDuration,
                    TimeSpan.FromSeconds(catalog.RunConfiguration.DailyTimerIncreaseSeconds)),
                days.ToDictionary(value => value.Key, value => value.Value.DailyExpense),
                catalog.RunConfiguration.ExpenseIdPrefix,
                catalog.RunConfiguration.SaveVersion,
                days.Values
                    .Where(value => value.UnlockedCategoryIds.Count > 0)
                    .Select(value => new DayUnlockSet(
                    value.DayNumber,
                    value.UnlockedCategoryIds,
                    herbs.Values
                        .Where(herb => value.UnlockedCategoryIds.Contains(herb.CategoryId, StringComparer.Ordinal))
                        .Select(herb => herb.StableId)
                        .ToArray(),
                    Array.Empty<string>())).ToArray());

            return GameContentLoadResult.Success(new GameRuntimeContent(
                herbs,
                orders,
                customers,
                days,
                guidebook,
                cards,
                difficulty,
                newRun,
                dayProgression,
                new ExpenseRules(catalog.RunConfiguration.CompletionDay),
                catalog.RunConfiguration.CorrectOrderPayment));
        }
    }
}
