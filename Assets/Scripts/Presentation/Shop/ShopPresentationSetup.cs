using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Witch.Domain.Orders;

namespace Witch.Presentation.Shop
{
    public enum GuidebookPageKind { Text, Herb, Contents }

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
        /// <summary>Plain text page (fallback / legacy pages).</summary>
        public GuidebookPageViewData(
            int pageNumber,
            string text,
            Sprite illustration = null,
            string accessibleLabel = "")
            : this(
                GuidebookPageKind.Text,
                pageNumber,
                description: text,
                illustration: illustration,
                accessibleLabel: accessibleLabel)
        {
        }

        private GuidebookPageViewData(
            GuidebookPageKind kind,
            int pageNumber,
            string title = "",
            string scientificName = "",
            string description = "",
            string warning = "",
            string categoryLabel = "",
            Sprite illustration = null,
            IReadOnlyList<GuidebookContentsRow> rows = null,
            string accessibleLabel = "")
        {
            Kind = kind;
            PageNumber = pageNumber;
            Title = title ?? string.Empty;
            ScientificName = scientificName ?? string.Empty;
            Description = description ?? string.Empty;
            Warning = warning ?? string.Empty;
            CategoryLabel = categoryLabel ?? string.Empty;
            Illustration = illustration;
            Rows = rows ?? Array.Empty<GuidebookContentsRow>();
            AccessibleLabel = string.IsNullOrEmpty(accessibleLabel) ? Title : accessibleLabel;
        }

        public static GuidebookPageViewData Herb(
            int pageNumber,
            string title,
            string scientificName,
            string description,
            string warning,
            string categoryLabel,
            Sprite illustration)
        {
            return new GuidebookPageViewData(
                GuidebookPageKind.Herb,
                pageNumber,
                title,
                scientificName,
                description,
                warning,
                categoryLabel,
                illustration);
        }

        public static GuidebookPageViewData Contents(
            int pageNumber,
            string title,
            IReadOnlyList<GuidebookContentsRow> rows)
        {
            return new GuidebookPageViewData(GuidebookPageKind.Contents, pageNumber, title, rows: rows);
        }

        public GuidebookPageKind Kind { get; }
        public int PageNumber { get; }
        public string Title { get; }
        public string ScientificName { get; }
        public string Description { get; }
        public string Warning { get; }
        public string CategoryLabel { get; }
        public Sprite Illustration { get; }
        public IReadOnlyList<GuidebookContentsRow> Rows { get; }
        public string AccessibleLabel { get; }

        /// <summary>The whole page as one block of text, used when a page view has no separate text slots.</summary>
        public string Text
        {
            get
            {
                switch (Kind)
                {
                    case GuidebookPageKind.Herb:
                        return Join("\n", Title, ScientificName, Description, Warning);
                    case GuidebookPageKind.Contents:
                        return Join(
                            "\n",
                            Title,
                            string.Join("\n", Rows.Select(row => row.Name + " \u2014 " + row.Description)));
                    default:
                        return Description;
                }
            }
        }

        private static string Join(string separator, params string[] parts)
        {
            return string.Join(separator, parts.Where(part => !string.IsNullOrEmpty(part)));
        }
    }

    public sealed class GuidebookContentsRow
    {
        public GuidebookContentsRow(string name, string description, int targetPageIndex = -1)
        {
            Name = name ?? string.Empty;
            Description = description ?? string.Empty;
            TargetPageIndex = targetPageIndex;
        }

        public string Name { get; }
        public string Description { get; }

        /// <summary>Zero-based guidebook page this row links to, or -1 when it has no link.</summary>
        public int TargetPageIndex { get; }
        public bool HasLink => TargetPageIndex >= 0;
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
