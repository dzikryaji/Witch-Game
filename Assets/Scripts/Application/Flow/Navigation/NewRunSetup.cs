using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Witch.Application.Flow
{
    public sealed class NewRunSetup
    {
        private readonly ReadOnlyCollection<string> unlockedCategoryIds;
        private readonly ReadOnlyCollection<string> unlockedHerbIds;
        private readonly ReadOnlyCollection<string> unlockedRecipeIds;
        private readonly ReadOnlyCollection<string> unlockedGuidebookEntryIds;

        public NewRunSetup(
            int startingMoney,
            IReadOnlyList<string> unlockedCategoryIds,
            IReadOnlyList<string> unlockedHerbIds,
            IReadOnlyList<string> unlockedRecipeIds,
            IReadOnlyList<string> unlockedGuidebookEntryIds,
            TimeSpan firstDayTimerDuration,
            string firstDayExpenseId,
            int firstDayExpenseValue,
            int saveVersion)
        {
            if (startingMoney < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startingMoney));
            }

            if (unlockedCategoryIds == null)
            {
                throw new ArgumentNullException(nameof(unlockedCategoryIds));
            }

            if (unlockedHerbIds == null)
            {
                throw new ArgumentNullException(nameof(unlockedHerbIds));
            }

            if (unlockedRecipeIds == null)
            {
                throw new ArgumentNullException(nameof(unlockedRecipeIds));
            }

            if (unlockedGuidebookEntryIds == null)
            {
                throw new ArgumentNullException(nameof(unlockedGuidebookEntryIds));
            }

            if (firstDayTimerDuration <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(firstDayTimerDuration));
            }

            if (string.IsNullOrWhiteSpace(firstDayExpenseId))
            {
                throw new ArgumentException("Expense ID must not be empty.", nameof(firstDayExpenseId));
            }

            if (firstDayExpenseValue < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(firstDayExpenseValue));
            }

            if (saveVersion < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(saveVersion));
            }

            StartingMoney = startingMoney;
            this.unlockedCategoryIds = new List<string>(unlockedCategoryIds).AsReadOnly();
            this.unlockedHerbIds = new List<string>(unlockedHerbIds).AsReadOnly();
            this.unlockedRecipeIds = new List<string>(unlockedRecipeIds).AsReadOnly();
            this.unlockedGuidebookEntryIds = new List<string>(unlockedGuidebookEntryIds).AsReadOnly();
            FirstDayTimerDuration = firstDayTimerDuration;
            FirstDayExpenseId = firstDayExpenseId;
            FirstDayExpenseValue = firstDayExpenseValue;
            SaveVersion = saveVersion;
        }

        public int StartingMoney { get; }

        public IReadOnlyList<string> UnlockedCategoryIds => unlockedCategoryIds;

        public IReadOnlyList<string> UnlockedHerbIds => unlockedHerbIds;

        public IReadOnlyList<string> UnlockedRecipeIds => unlockedRecipeIds;

        public IReadOnlyList<string> UnlockedGuidebookEntryIds => unlockedGuidebookEntryIds;

        public TimeSpan FirstDayTimerDuration { get; }

        public string FirstDayExpenseId { get; }

        public int FirstDayExpenseValue { get; }

        public int SaveVersion { get; }

        internal StartNewRunCommand CreateCommand(
            string runId,
            int firstDayOrderSeed,
            DateTimeOffset capturedAtUtc)
        {
            return new StartNewRunCommand(
                runId,
                StartingMoney,
                UnlockedCategoryIds,
                UnlockedHerbIds,
                UnlockedRecipeIds,
                UnlockedGuidebookEntryIds,
                firstDayOrderSeed,
                FirstDayTimerDuration,
                FirstDayExpenseId,
                FirstDayExpenseValue,
                SaveVersion,
                capturedAtUtc);
        }
    }
}
