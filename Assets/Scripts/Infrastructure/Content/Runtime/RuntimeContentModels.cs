using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using Witch.Domain.Orders;

namespace Witch.Infrastructure.Content
{
    public sealed class HerbContent
    {
        public HerbContent(
            string stableId,
            string displayName,
            string categoryId,
            string scientificName,
            string treatmentText,
            string warningText,
            Sprite herbIllustration,
            string accessibleLabel,
            int guidebookPage)
        {
            StableId = stableId;
            DisplayName = displayName;
            CategoryId = categoryId;
            ScientificName = scientificName;
            TreatmentText = treatmentText;
            WarningText = warningText;
            HerbIllustration = herbIllustration;
            AccessibleLabel = accessibleLabel;
            GuidebookPage = guidebookPage;
        }

        public string StableId { get; }
        public string DisplayName { get; }
        public string CategoryId { get; }
        public string ScientificName { get; }
        public string TreatmentText { get; }
        public string WarningText { get; }
        public Sprite HerbIllustration { get; }
        public string AccessibleLabel { get; }
        public int GuidebookPage { get; }
    }

    public sealed class OrderContent
    {
        public OrderContent(
            OrderRequest request,
            OrderDifficulty difficulty,
            IReadOnlyList<string> orderBubbles)
        {
            Request = request;
            Difficulty = difficulty;
            OrderBubbles = new List<string>(orderBubbles).AsReadOnly();
        }

        public OrderRequest Request { get; }
        public OrderDifficulty Difficulty { get; }
        public IReadOnlyList<string> OrderBubbles { get; }

        public bool IsAvailable(ISet<string> unlockedHerbIds)
        {
            if (unlockedHerbIds == null) throw new ArgumentNullException(nameof(unlockedHerbIds));
            return unlockedHerbIds.Contains(Request.RequestedPair.FirstHerbId)
                   && unlockedHerbIds.Contains(Request.RequestedPair.SecondHerbId);
        }
    }

    public sealed class CustomerContent
    {
        public CustomerContent(string stableId, string displayName)
        {
            StableId = stableId;
            DisplayName = displayName;
        }

        public string StableId { get; }
        public string DisplayName { get; }
    }

    public sealed class DayContent
    {
        public DayContent(
            string stableId,
            int dayNumber,
            TimeSpan timerDuration,
            int dailyExpense,
            IReadOnlyList<string> unlockedCategoryIds,
            IReadOnlyList<OrderDifficulty> orderDifficulties,
            IReadOnlyList<string> customerIds)
        {
            StableId = stableId;
            DayNumber = dayNumber;
            TimerDuration = timerDuration;
            DailyExpense = dailyExpense;
            UnlockedCategoryIds = new List<string>(unlockedCategoryIds).AsReadOnly();
            OrderDifficulties = new List<OrderDifficulty>(orderDifficulties).AsReadOnly();
            CustomerIds = new List<string>(customerIds).AsReadOnly();
        }

        public string StableId { get; }
        public int DayNumber { get; }
        public TimeSpan TimerDuration { get; }
        public int DailyExpense { get; }
        public IReadOnlyList<string> UnlockedCategoryIds { get; }
        public IReadOnlyList<OrderDifficulty> OrderDifficulties { get; }
        public IReadOnlyList<string> CustomerIds { get; }
    }

    public sealed class GuidebookEntryContent
    {
        public GuidebookEntryContent(
            string stableId,
            string title,
            string categoryId,
            string bodyText = "",
            IReadOnlyList<GuidebookContentsItemContent> contentsItems = null)
        {
            StableId = stableId;
            Title = title;
            CategoryId = categoryId;
            BodyText = bodyText ?? string.Empty;
            ContentsItems = new List<GuidebookContentsItemContent>(
                contentsItems ?? Array.Empty<GuidebookContentsItemContent>()).AsReadOnly();
        }

        public string StableId { get; }
        public string Title { get; }
        public string CategoryId { get; }
        public string BodyText { get; }
        public IReadOnlyList<GuidebookContentsItemContent> ContentsItems { get; }
    }

    public sealed class GuidebookContentsItemContent
    {
        public GuidebookContentsItemContent(string categoryId, string name, string description)
        {
            CategoryId = categoryId ?? string.Empty;
            Name = name ?? string.Empty;
            Description = description ?? string.Empty;
        }

        public string CategoryId { get; }
        public string Name { get; }
        public string Description { get; }
    }

    public sealed class RewindCardContent
    {
        public RewindCardContent(string stableId, string symbolKey)
        {
            StableId = stableId;
            SymbolKey = symbolKey;
        }

        public string StableId { get; }
        public string SymbolKey { get; }
    }

    public sealed class RewindDifficultyContent
    {
        public RewindDifficultyContent(string stableId, int startingPairCount, int maximumPairCount)
        {
            StableId = stableId;
            StartingPairCount = startingPairCount;
            MaximumPairCount = maximumPairCount;
        }

        public string StableId { get; }
        public int StartingPairCount { get; }
        public int MaximumPairCount { get; }
    }
}
