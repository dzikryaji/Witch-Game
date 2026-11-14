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
            IReadOnlyList<string> orderBubbles,
            CustomerContent customer)
        {
            Request = request;
            Difficulty = difficulty;
            OrderBubbles = new List<string>(orderBubbles).AsReadOnly();
            Customer = customer ?? throw new ArgumentNullException(nameof(customer));
        }

        public OrderRequest Request { get; }
        public OrderDifficulty Difficulty { get; }
        public IReadOnlyList<string> OrderBubbles { get; }
        public CustomerContent Customer { get; }

        public bool IsAvailable(ISet<string> unlockedHerbIds)
        {
            if (unlockedHerbIds == null) throw new ArgumentNullException(nameof(unlockedHerbIds));
            return unlockedHerbIds.Contains(Request.RequestedPair.FirstHerbId)
                   && unlockedHerbIds.Contains(Request.RequestedPair.SecondHerbId);
        }
    }

    public sealed class CustomerContent
    {
        public CustomerContent(string stableId, string displayName, Sprite illustration)
        {
            StableId = stableId;
            DisplayName = displayName;
            Illustration = illustration;
        }

        public string StableId { get; }
        public string DisplayName { get; }
        public Sprite Illustration { get; }
    }

    public sealed class DayContent
    {
        public DayContent(
            string stableId,
            int dayNumber,
            TimeSpan timerDuration,
            int dailyExpense,
            IReadOnlyList<string> unlockedCategoryIds,
            IReadOnlyList<OrderDifficulty> orderDifficulties)
        {
            StableId = stableId;
            DayNumber = dayNumber;
            TimerDuration = timerDuration;
            DailyExpense = dailyExpense;
            UnlockedCategoryIds = new List<string>(unlockedCategoryIds).AsReadOnly();
            OrderDifficulties = new List<OrderDifficulty>(orderDifficulties).AsReadOnly();
        }

        public string StableId { get; }
        public int DayNumber { get; }
        public TimeSpan TimerDuration { get; }
        public int DailyExpense { get; }
        public IReadOnlyList<string> UnlockedCategoryIds { get; }
        public IReadOnlyList<OrderDifficulty> OrderDifficulties { get; }
    }

    public sealed class HerbCategoryContent
    {
        public HerbCategoryContent(
            string stableId,
            string displayName,
            string description)
        {
            StableId = stableId;
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
        }

        public string StableId { get; }
        public string DisplayName { get; }
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

    public sealed class WitchHunterStageContent
    {
        public WitchHunterStageContent(IReadOnlyList<string> accusations, string successLine)
        {
            Accusations = new List<string>(accusations ?? Array.Empty<string>()).AsReadOnly();
            SuccessLine = successLine ?? string.Empty;
        }

        public IReadOnlyList<string> Accusations { get; }
        public string SuccessLine { get; }
    }

    public sealed class WitchHunterDialogueContent
    {
        private readonly IReadOnlyList<WitchHunterStageContent> stages;

        public WitchHunterDialogueContent(IReadOnlyList<WitchHunterStageContent> stageValues)
        {
            stages = new List<WitchHunterStageContent>(stageValues ?? Array.Empty<WitchHunterStageContent>())
                .AsReadOnly();
        }

        public int StageCount => stages.Count;

        public IReadOnlyList<string> GetAccusations(int stageIndex)
        {
            return GetStage(stageIndex).Accusations;
        }

        public string GetSuccessLine(int stageIndex)
        {
            return GetStage(stageIndex).SuccessLine;
        }

        private WitchHunterStageContent GetStage(int stageIndex)
        {
            if (stageIndex < 0 || stageIndex >= stages.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(stageIndex));
            }

            return stages[stageIndex];
        }
    }
}
