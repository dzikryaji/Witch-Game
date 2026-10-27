using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Witch.Domain.Progression;

namespace Witch.Application.Flow
{
    public sealed class DayUnlockSet
    {
        public DayUnlockSet(int firstDay, IReadOnlyList<string> categories, IReadOnlyList<string> herbs, IReadOnlyList<string> recipes, IReadOnlyList<string> guidebookEntries)
        {
            if (firstDay < 1) throw new ArgumentOutOfRangeException(nameof(firstDay));
            FirstDay = firstDay;
            CategoryIds = Copy(categories);
            HerbIds = Copy(herbs);
            RecipeIds = Copy(recipes);
            GuidebookEntryIds = Copy(guidebookEntries);
        }

        public int FirstDay { get; }
        public IReadOnlyList<string> CategoryIds { get; }
        public IReadOnlyList<string> HerbIds { get; }
        public IReadOnlyList<string> RecipeIds { get; }
        public IReadOnlyList<string> GuidebookEntryIds { get; }

        private static IReadOnlyList<string> Copy(IReadOnlyList<string> values) =>
            new List<string>(values ?? Array.Empty<string>()).AsReadOnly();
    }

    public sealed class DayProgressionSetup
    {
        public DayProgressionSetup(
            DayTimingRules timingRules,
            int dailyExpenseValue,
            string expenseIdPrefix,
            int saveVersion,
            IReadOnlyList<DayUnlockSet> unlockSets = null)
        {
            TimingRules = timingRules ?? throw new ArgumentNullException(nameof(timingRules));
            if (dailyExpenseValue < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(dailyExpenseValue));
            }

            if (string.IsNullOrWhiteSpace(expenseIdPrefix))
            {
                throw new ArgumentException("Expense ID prefix is required.", nameof(expenseIdPrefix));
            }

            if (saveVersion < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(saveVersion));
            }

            DailyExpenseValue = dailyExpenseValue;
            ExpenseIdPrefix = expenseIdPrefix;
            SaveVersion = saveVersion;
            UnlockSets = new List<DayUnlockSet>(unlockSets ?? Array.Empty<DayUnlockSet>()).AsReadOnly();
        }

        public DayTimingRules TimingRules { get; }
        public int DailyExpenseValue { get; }
        public string ExpenseIdPrefix { get; }
        public int SaveVersion { get; }
        public IReadOnlyList<DayUnlockSet> UnlockSets { get; }

        public string GetExpenseId(int dayNumber)
        {
            return ExpenseIdPrefix + dayNumber;
        }

        public RunState ApplyUnlocks(RunState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            IEnumerable<DayUnlockSet> active = UnlockSets.Where(value => value.FirstDay <= state.CurrentDayNumber);
            return new RunState(
                state.RunId,
                state.CurrentDayNumber,
                state.Money,
                state.Suspicion,
                state.RewindCount,
                state.UnlockedCategoryIds.Concat(active.SelectMany(value => value.CategoryIds)).Distinct(StringComparer.Ordinal).ToArray(),
                state.UnlockedHerbIds.Concat(active.SelectMany(value => value.HerbIds)).Distinct(StringComparer.Ordinal).ToArray(),
                state.UnlockedRecipeIds.Concat(active.SelectMany(value => value.RecipeIds)).Distinct(StringComparer.Ordinal).ToArray(),
                state.UnlockedGuidebookEntryIds.Concat(active.SelectMany(value => value.GuidebookEntryIds)).Distinct(StringComparer.Ordinal).ToArray(),
                state.TutorialFlags,
                state.NarrativeFlags);
        }
    }
}
