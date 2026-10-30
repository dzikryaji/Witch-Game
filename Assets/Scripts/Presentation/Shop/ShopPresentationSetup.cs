using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Witch.Domain.Orders;

namespace Witch.Presentation.Shop
{
    public sealed class ShopEncounterViewData
    {
        public ShopEncounterViewData(string customerId, string customerName, OrderRequest order, string clueText)
        {
            CustomerId = customerId;
            CustomerName = customerName;
            Order = order ?? throw new ArgumentNullException(nameof(order));
            ClueText = clueText ?? string.Empty;
        }
        public string CustomerId { get; }
        public string CustomerName { get; }
        public OrderRequest Order { get; }
        public string ClueText { get; }
    }

    public sealed class ShopHerbViewData
    {
        public ShopHerbViewData(string stableId, string displayName, Sprite herbIllustration = null)
        {
            StableId = string.IsNullOrWhiteSpace(stableId)
                ? throw new ArgumentException("Herb ID is required.", nameof(stableId))
                : stableId;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? stableId : displayName;
            HerbIllustration = herbIllustration;
        }

        public string StableId { get; }
        public string DisplayName { get; }
        public Sprite HerbIllustration { get; }
    }

    public sealed class GuidebookPageViewData
    {
        public GuidebookPageViewData(
            int pageNumber,
            string text,
            Sprite illustration = null,
            string accessibleLabel = "")
        {
            PageNumber = pageNumber;
            Text = text ?? string.Empty;
            Illustration = illustration;
            AccessibleLabel = accessibleLabel ?? string.Empty;
        }

        public int PageNumber { get; }
        public string Text { get; }
        public Sprite Illustration { get; }
        public string AccessibleLabel { get; }
    }

    public sealed class ShopPresentationSetup
    {
        public ShopPresentationSetup(
            string customerId,
            string customerName,
            OrderRequest order,
            string clueText,
            IReadOnlyList<ShopHerbViewData> herbs)
            : this(
                new[] { new ShopEncounterViewData(customerId, customerName, order, clueText) },
                herbs,
                "GUIDEBOOK\n\nGinger: knotted warming root\nMint: cool green leaves\n\nThe shop clock keeps moving while you read.")
        {
        }

        public ShopPresentationSetup(
            IReadOnlyList<ShopEncounterViewData> encounters,
            IReadOnlyList<ShopHerbViewData> herbs,
            string guidebookText = "",
            int dayNumber = 1,
            bool showOpeningTutorial = false,
            int encounterSeed = 0,
            bool randomizeEncounters = false)
            : this(
                encounters,
                herbs,
                (guidebookText ?? string.Empty)
                    .Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select((text, index) => new GuidebookPageViewData(index + 1, text))
                    .ToArray(),
                dayNumber,
                showOpeningTutorial,
                encounterSeed,
                randomizeEncounters)
        {
        }

        public ShopPresentationSetup(
            IReadOnlyList<ShopEncounterViewData> encounters,
            IReadOnlyList<ShopHerbViewData> herbs,
            IReadOnlyList<GuidebookPageViewData> guidebookPages,
            int dayNumber = 1,
            bool showOpeningTutorial = false,
            int encounterSeed = 0,
            bool randomizeEncounters = false)
        {
            Encounters = encounters ?? throw new ArgumentNullException(nameof(encounters));
            if (Encounters.Count == 0) throw new ArgumentException("At least one encounter is required.", nameof(encounters));
            Herbs = herbs ?? throw new ArgumentNullException(nameof(herbs));
            GuidebookPages = guidebookPages ?? throw new ArgumentNullException(nameof(guidebookPages));
            DayNumber = dayNumber;
            ShowOpeningTutorial = showOpeningTutorial;
            EncounterSeed = encounterSeed;
            RandomizeEncounters = randomizeEncounters;
        }

        public IReadOnlyList<ShopEncounterViewData> Encounters { get; }
        public string CustomerId => Encounters[0].CustomerId;
        public string CustomerName => Encounters[0].CustomerName;
        public OrderRequest Order => Encounters[0].Order;
        public string ClueText => Encounters[0].ClueText;
        public IReadOnlyList<ShopHerbViewData> Herbs { get; }
        public IReadOnlyList<GuidebookPageViewData> GuidebookPages { get; }
        public int DayNumber { get; }
        public bool ShowOpeningTutorial { get; }
        public int EncounterSeed { get; }
        public bool RandomizeEncounters { get; }
    }
}
