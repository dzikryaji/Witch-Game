using System;
using System.Collections.Generic;
using System.Linq;
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
                herbs.Add(new ShopHerbViewData(herb.StableId, herb.DisplayName, herb.HerbIllustration));
            }

            List<GuidebookPageViewData> guidebook = CreateGuidebookPages(session);

            bool showTutorial = session.RunState.CurrentDayNumber == 1
                && tutorialShownRunIds.Add(session.RunState.RunId);
            return new ShopPresentationSetup(
                encounters,
                herbs,
                guidebook,
                session.RunState.CurrentDayNumber,
                showTutorial,
                session.AttemptMetadata.ActiveOrderSeed,
                randomizeEncounters: true);
        }

        /// <summary>Contents page first (unlocked categories, in unlock order), then one page per unlocked herb.</summary>
        private List<GuidebookPageViewData> CreateGuidebookPages(GameSession session)
        {
            var categoryLabels = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (GuidebookContentsItemContent item in content.GuidebookEntriesById.Values
                         .SelectMany(entry => entry.ContentsItems))
            {
                if (!string.IsNullOrEmpty(item.CategoryId) && !categoryLabels.ContainsKey(item.CategoryId))
                {
                    categoryLabels.Add(item.CategoryId, item.Name);
                }
            }

            // Herb pages come right after the Contents page (index 0), in guidebook order.
            List<HerbContent> herbPages = session.RunState.UnlockedHerbIds
                .Select(herbId => content.HerbsById[herbId])
                .OrderBy(value => value.GuidebookPage)
                .ToList();
            var firstPageOfCategory = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = 0; index < herbPages.Count; index++)
            {
                string categoryId = herbPages[index].CategoryId;
                if (!string.IsNullOrEmpty(categoryId) && !firstPageOfCategory.ContainsKey(categoryId))
                {
                    firstPageOfCategory.Add(categoryId, index + 1);
                }
            }

            var rows = new List<GuidebookContentsRow>();
            foreach (string categoryId in session.RunState.UnlockedCategoryIds)
            {
                foreach (GuidebookEntryContent entry in content.GuidebookEntriesById.Values
                             .Where(value => value.CategoryId == categoryId)
                             .OrderBy(value => value.Title, StringComparer.Ordinal))
                {
                    foreach (GuidebookContentsItemContent item in entry.ContentsItems)
                    {
                        int target = firstPageOfCategory.TryGetValue(item.CategoryId ?? string.Empty, out int page) ? page : -1;
                        rows.Add(new GuidebookContentsRow(item.Name, item.Description, target));
                    }
                }
            }

            var pages = new List<GuidebookPageViewData>
            {
                GuidebookPageViewData.Contents(1, "Content", rows)
            };

            foreach (HerbContent herb in herbPages)
            {
                string categoryLabel = null;
                if (!string.IsNullOrEmpty(herb.CategoryId)) categoryLabels.TryGetValue(herb.CategoryId, out categoryLabel);
                pages.Add(GuidebookPageViewData.Herb(
                    pages.Count + 1,
                    herb.DisplayName,
                    herb.ScientificName,
                    herb.TreatmentText,
                    herb.WarningText,
                    categoryLabel,
                    herb.HerbIllustration));
            }

            return pages;
        }

        private static int PositiveModulo(int value, int count)
        {
            int result = value % count;
            return result < 0 ? result + count : result;
        }
    }
}
