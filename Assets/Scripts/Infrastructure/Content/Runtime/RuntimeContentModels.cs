using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Witch.Domain.Orders;

namespace Witch.Infrastructure.Content
{
    public sealed class HerbContent
    {
        public HerbContent(string stableId, string displayName, string categoryId)
        {
            StableId = stableId;
            DisplayName = displayName;
            CategoryId = categoryId;
        }

        public string StableId { get; }
        public string DisplayName { get; }
        public string CategoryId { get; }
    }

    public sealed class OrderContent
    {
        public OrderContent(OrderRequest request, string clueText, IReadOnlyList<string> clueIds)
        {
            Request = request;
            ClueText = clueText;
            ClueIds = new List<string>(clueIds).AsReadOnly();
        }

        public OrderRequest Request { get; }
        public string ClueText { get; }
        public IReadOnlyList<string> ClueIds { get; }
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
            IReadOnlyList<string> orderIds,
            IReadOnlyList<string> customerIds)
        {
            StableId = stableId;
            DayNumber = dayNumber;
            TimerDuration = timerDuration;
            OrderIds = new List<string>(orderIds).AsReadOnly();
            CustomerIds = new List<string>(customerIds).AsReadOnly();
        }

        public string StableId { get; }
        public int DayNumber { get; }
        public TimeSpan TimerDuration { get; }
        public IReadOnlyList<string> OrderIds { get; }
        public IReadOnlyList<string> CustomerIds { get; }
    }

    public sealed class GuidebookEntryContent
    {
        public GuidebookEntryContent(
            string stableId,
            string title,
            string categoryId,
            IReadOnlyList<string> herbIds,
            IReadOnlyList<string> clueIds,
            string bodyText = "")
        {
            StableId = stableId;
            Title = title;
            CategoryId = categoryId;
            HerbIds = new List<string>(herbIds).AsReadOnly();
            ClueIds = new List<string>(clueIds).AsReadOnly();
            BodyText = bodyText ?? string.Empty;
        }

        public string StableId { get; }
        public string Title { get; }
        public string CategoryId { get; }
        public IReadOnlyList<string> HerbIds { get; }
        public IReadOnlyList<string> ClueIds { get; }
        public string BodyText { get; }
    }

    public sealed class CategoryUnlockContent
    {
        public CategoryUnlockContent(
            string stableId,
            string categoryId,
            int firstDay,
            IReadOnlyList<string> herbIds,
            IReadOnlyList<string> recipeIds,
            IReadOnlyList<string> guidebookEntryIds)
        {
            StableId = stableId;
            CategoryId = categoryId;
            FirstDay = firstDay;
            HerbIds = new List<string>(herbIds).AsReadOnly();
            RecipeIds = new List<string>(recipeIds).AsReadOnly();
            GuidebookEntryIds = new List<string>(guidebookEntryIds).AsReadOnly();
        }

        public string StableId { get; }
        public string CategoryId { get; }
        public int FirstDay { get; }
        public IReadOnlyList<string> HerbIds { get; }
        public IReadOnlyList<string> RecipeIds { get; }
        public IReadOnlyList<string> GuidebookEntryIds { get; }
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
