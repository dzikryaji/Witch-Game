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
        private const string WitchHunterCustomerId = "customer.witchHunter";
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
            if (!content.CustomersById.TryGetValue(WitchHunterCustomerId, out CustomerContent witchHunter))
            {
                throw new InvalidOperationException(
                    "Game content must include the Witch Hunter customer definition.");
            }

            var unlockedHerbs = new HashSet<string>(session.RunState.UnlockedHerbIds, StringComparer.Ordinal);
            List<OrderContent> availableOrders = content.OrdersById.Values
                .Where(order => order.IsAvailable(unlockedHerbs)
                    && day.OrderDifficulties.Contains(order.Difficulty))
                .OrderBy(order => order.Request.OrderId, StringComparer.Ordinal)
                .ToList();
            string introductionOrderId = null;
            string introductionCategoryId = null;
            if (day.DayNumber > 1 && day.UnlockedCategoryIds.Count > 0)
            {
                introductionCategoryId = day.UnlockedCategoryIds[0];
                List<OrderContent> candidates = content.OrdersById.Values
                    .Where(order => order.Difficulty == OrderDifficulty.Simple
                        && OrderUsesCategory(order, introductionCategoryId))
                    .OrderBy(order => order.Request.OrderId, StringComparer.Ordinal)
                    .ToList();
                if (candidates.Count > 0)
                {
                    OrderContent introduction = candidates[PositiveModulo(session.DayStartSnapshot.OrderSeed, candidates.Count)];
                    introductionOrderId = introduction.Request.OrderId;
                    if (availableOrders.All(value => value.Request.OrderId != introductionOrderId))
                    {
                        availableOrders.Add(introduction);
                    }
                }
            }
            var encounters = new List<ShopEncounterViewData>();
            for (int offset = 0; offset < availableOrders.Count; offset++)
            {
                OrderContent order = availableOrders[offset];
                CustomerContent customer = order.Customer;
                encounters.Add(new ShopEncounterViewData(
                    customer.StableId,
                    customer.DisplayName,
                    customer.Illustration,
                    order.Request,
                    order.OrderBubbles));
            }

            var herbs = new List<ShopHerbViewData>();
            foreach (string herbId in session.RunState.UnlockedHerbIds)
            {
                HerbContent herb = content.HerbsById[herbId];
                herbs.Add(new ShopHerbViewData(herb.StableId, herb.DisplayName, herb.HerbIllustration));
            }

            List<GuidebookPageViewData> guidebook = CreateGuidebookPages(session);

            const string guidedTutorialOrderId = "order.simple.w01";
            bool guidedTutorialEligible = session.RunState.CurrentDayNumber == 1
                && !(session.RunState.TutorialFlags.TryGetValue(ShopTutorialFlags.Day1GuidedCompleted, out bool completed) && completed);
            // In-memory guard to avoid re-triggering within same session lifetime edge cases.
            if (guidedTutorialEligible)
            {
                tutorialShownRunIds.Add(session.RunState.RunId);
            }

            if (guidedTutorialEligible && encounters.All(e => e.Order.OrderId != guidedTutorialOrderId))
            {
                // Ensure the fixed tutorial order is available when eligible.
                if (content.OrdersById.TryGetValue(guidedTutorialOrderId, out OrderContent tutorialOrder))
                {
                    CustomerContent tutorialCustomer = tutorialOrder.Customer;
                    encounters.Insert(0, new ShopEncounterViewData(
                        tutorialCustomer.StableId,
                        tutorialCustomer.DisplayName,
                        tutorialCustomer.Illustration,
                        tutorialOrder.Request,
                        tutorialOrder.OrderBubbles));
                }
                else
                {
                    guidedTutorialEligible = false;
                }
            }

            return new ShopPresentationSetup(
                encounters,
                herbs,
                guidebook,
                session.RunState.CurrentDayNumber,
                guidedTutorialEligible,
                session.AttemptMetadata.ActiveOrderSeed,
                randomizeEncounters: true,
                seenOrderIds: session.RunState.SeenOrderIds,
                introductionOrderId: introductionOrderId,
                oneTimeIntroduction: introductionOrderId != null
                    && !day.OrderDifficulties.Contains(OrderDifficulty.Simple),
                introductionCategoryOrderIds: introductionCategoryId == null
                    ? Array.Empty<string>()
                    : content.OrdersById.Values
                        .Where(order => OrderUsesCategory(order, introductionCategoryId))
                        .Select(order => order.Request.OrderId)
                        .ToArray(),
                witchHunter: new ShopCustomerViewData(
                    witchHunter.StableId,
                    witchHunter.DisplayName,
                    witchHunter.Illustration),
                guidedTutorialOrderId: guidedTutorialEligible ? guidedTutorialOrderId : null);
        }

        private bool OrderUsesCategory(OrderContent order, string categoryId)
        {
            HerbContent herb = content.HerbsById[order.Request.RequestedPair.FirstHerbId];
            return string.Equals(herb.CategoryId, categoryId, StringComparison.Ordinal);
        }

        /// <summary>Contents page first (unlocked categories, in unlock order), then one page per unlocked herb.</summary>
        private List<GuidebookPageViewData> CreateGuidebookPages(GameSession session)
        {
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
                if (content.HerbCategoriesById.TryGetValue(categoryId, out HerbCategoryContent category))
                {
                    int target = firstPageOfCategory.TryGetValue(categoryId, out int page) ? page : -1;
                    rows.Add(new GuidebookContentsRow(category.DisplayName, category.Description, target));
                }
            }

            var pages = new List<GuidebookPageViewData>
            {
                GuidebookPageViewData.Contents(1, "Content", rows)
            };

            foreach (HerbContent herb in herbPages)
            {
                string categoryLabel = content.HerbCategoriesById.TryGetValue(
                    herb.CategoryId,
                    out HerbCategoryContent category)
                    ? category.DisplayName
                    : null;
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
