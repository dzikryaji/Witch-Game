using System.Collections.Generic;
using System.Collections.ObjectModel;
using System;

namespace Witch.Application.Flow
{
    public sealed class StartNewRunCommand
    {
        private readonly ReadOnlyDictionary<string, bool> tutorialFlags;
        private readonly ReadOnlyDictionary<string, bool> narrativeFlags;

        public StartNewRunCommand(
            string runId,
            int startingMoney,
            IReadOnlyList<string> unlockedCategoryIds,
            IReadOnlyList<string> unlockedHerbIds,
            IReadOnlyList<string> unlockedRecipeIds,
            IReadOnlyList<string> unlockedGuidebookEntryIds,
            int firstDayOrderSeed,
            TimeSpan firstDayTimerDuration,
            string firstDayExpenseId,
            int firstDayExpenseValue,
            int saveVersion,
            DateTimeOffset capturedAtUtc,
            IReadOnlyDictionary<string, bool> tutorialFlags = null,
            IReadOnlyDictionary<string, bool> narrativeFlags = null)
        {
            RunId = runId;
            StartingMoney = startingMoney;
            UnlockedCategoryIds = CopyIds(unlockedCategoryIds);
            UnlockedHerbIds = CopyIds(unlockedHerbIds);
            UnlockedRecipeIds = CopyIds(unlockedRecipeIds);
            UnlockedGuidebookEntryIds = CopyIds(unlockedGuidebookEntryIds);
            FirstDayOrderSeed = firstDayOrderSeed;
            FirstDayTimerDuration = firstDayTimerDuration;
            FirstDayExpenseId = firstDayExpenseId;
            FirstDayExpenseValue = firstDayExpenseValue;
            SaveVersion = saveVersion;
            CapturedAtUtc = capturedAtUtc;
            this.tutorialFlags = CopyFlags(tutorialFlags);
            this.narrativeFlags = CopyFlags(narrativeFlags);
        }

        public string RunId { get; }

        public int StartingMoney { get; }

        public IReadOnlyList<string> UnlockedCategoryIds { get; }

        public IReadOnlyList<string> UnlockedHerbIds { get; }

        public IReadOnlyList<string> UnlockedRecipeIds { get; }

        public IReadOnlyList<string> UnlockedGuidebookEntryIds { get; }

        public int FirstDayOrderSeed { get; }

        public TimeSpan FirstDayTimerDuration { get; }

        public string FirstDayExpenseId { get; }

        public int FirstDayExpenseValue { get; }

        public int SaveVersion { get; }

        public DateTimeOffset CapturedAtUtc { get; }

        public IReadOnlyDictionary<string, bool> TutorialFlags => tutorialFlags;

        public IReadOnlyDictionary<string, bool> NarrativeFlags => narrativeFlags;

        private static IReadOnlyList<string> CopyIds(IReadOnlyList<string> source)
        {
            return source == null ? null : new List<string>(source).AsReadOnly();
        }

        private static ReadOnlyDictionary<string, bool> CopyFlags(IReadOnlyDictionary<string, bool> source)
        {
            return new ReadOnlyDictionary<string, bool>(
                source == null
                    ? new Dictionary<string, bool>(System.StringComparer.Ordinal)
                    : new Dictionary<string, bool>(source, System.StringComparer.Ordinal));
        }
    }
}
