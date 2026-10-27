using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Witch.Application.Flow;
using Witch.Infrastructure.Content;
using Witch.Presentation.Shop;

namespace Witch.Bootstrap
{
    /// <summary>Maps runtime content and session state into data consumed by the authored Shop view.</summary>
    internal sealed class ShopPresentationSetupFactory
    {
        private readonly GameRuntimeContent content;
        private readonly HashSet<string> tutorialShownRunIds = new HashSet<string>(StringComparer.Ordinal);

        public ShopPresentationSetupFactory(GameRuntimeContent content)
        {
            this.content = content ?? throw new ArgumentNullException(nameof(content));
        }

        public ShopPresentationSetup Create(GameSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));

            DayContent day = content.DaysByNumber[session.RunState.CurrentDayNumber];
            int customerStart = PositiveModulo(session.AttemptMetadata.ActiveOrderSeed, day.CustomerIds.Count);
            var encounters = new List<ShopEncounterViewData>();
            for (int offset = 0; offset < day.OrderIds.Count; offset++)
            {
                OrderContent order = content.OrdersById[day.OrderIds[offset]];
                CustomerContent customer = content.CustomersById[day.CustomerIds[(customerStart + offset) % day.CustomerIds.Count]];
                encounters.Add(new ShopEncounterViewData(
                    customer.StableId,
                    customer.DisplayName,
                    order.Request,
                    order.ClueText));
            }

            var herbs = new List<ShopHerbViewData>();
            foreach (string herbId in session.RunState.UnlockedHerbIds)
            {
                HerbContent herb = content.HerbsById[herbId];
                herbs.Add(new ShopHerbViewData(herb.StableId, herb.DisplayName));
            }

            var guidebook = new StringBuilder();
            var unlockedCategories = new HashSet<string>(
                session.RunState.UnlockedCategoryIds,
                StringComparer.Ordinal);
            foreach (GuidebookEntryContent entry in content.GuidebookEntriesById.Values
                         .Where(value => unlockedCategories.Contains(value.CategoryId))
                         .OrderBy(value => value.Title, StringComparer.Ordinal))
            {
                if (guidebook.Length > 0) guidebook.Append("\n\n");
                guidebook.Append(entry.Title).Append("\n").Append(entry.BodyText);
            }

            foreach (HerbContent herb in session.RunState.UnlockedHerbIds
                         .Select(herbId => content.HerbsById[herbId])
                         .OrderBy(value => value.GuidebookPage))
            {
                if (guidebook.Length > 0) guidebook.Append("\n\n");
                guidebook
                    .Append(herb.DisplayName)
                    .Append("\n")
                    .Append(herb.ScientificName)
                    .Append("\n")
                    .Append(herb.TreatmentText)
                    .Append("\nWarning: ")
                    .Append(herb.WarningText);
            }

            bool showTutorial = session.RunState.CurrentDayNumber == 1
                && tutorialShownRunIds.Add(session.RunState.RunId);
            return new ShopPresentationSetup(
                encounters,
                herbs,
                guidebook.ToString(),
                session.RunState.CurrentDayNumber,
                showTutorial,
                session.AttemptMetadata.ActiveOrderSeed,
                randomizeEncounters: true);
        }

        private static int PositiveModulo(int value, int count)
        {
            int result = value % count;
            return result < 0 ? result + count : result;
        }
    }
}
