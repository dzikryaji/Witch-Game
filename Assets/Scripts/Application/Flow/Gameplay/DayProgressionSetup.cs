using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Witch.Domain.Progression;

namespace Witch.Application.Flow
{
    public sealed class DayUnlockSet
    {
        public DayUnlockSet(int firstDay, IReadOnlyList<string> categories, IReadOnlyList<string> herbs, IReadOnlyList<string> guidebookEntries)
        {
            if (firstDay < 1) throw new ArgumentOutOfRangeException(nameof(firstDay));
            FirstDay = firstDay;
            CategoryIds = Copy(categories);
            HerbIds = Copy(herbs);
            GuidebookEntryIds = Copy(guidebookEntries);
        }

        public int FirstDay { get; }
        public IReadOnlyList<string> CategoryIds { get; }
        public IReadOnlyList<string> HerbIds { get; }
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
            : this(
                timingRules,
                new Dictionary<int, int>(),
                dailyExpenseValue,
                expenseIdPrefix,
                saveVersion,
                unlockSets)
        {
        }

        public DayProgressionSetup(
            DayTimingRules timingRules,
            IReadOnlyDictionary<int, int> dailyExpenseValues,
            string expenseIdPrefix,
            int saveVersion,
            IReadOnlyList<DayUnlockSet> unlockSets = null)
            : this(timingRules, dailyExpenseValues, 0, expenseIdPrefix, saveVersion, unlockSets)
        {
        }

        private DayProgressionSetup(
            DayTimingRules timingRules,
            IReadOnlyDictionary<int, int> dailyExpenseValues,
            int fallbackDailyExpenseValue,
            string expenseIdPrefix,
            int saveVersion,
            IReadOnlyList<DayUnlockSet> unlockSets)
        {
            TimingRules = timingRules ?? throw new ArgumentNullException(nameof(timingRules));
            if (fallbackDailyExpenseValue < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fallbackDailyExpenseValue));
            }

            if (string.IsNullOrWhiteSpace(expenseIdPrefix))
            {
                throw new ArgumentException("Expense ID prefix is required.", nameof(expenseIdPrefix));
            }

            if (saveVersion < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(saveVersion));
            }

            DailyExpenseValue = fallbackDailyExpenseValue;
            DailyExpenseValues = new ReadOnlyDictionary<int, int>(
                new Dictionary<int, int>(dailyExpenseValues ?? throw new ArgumentNullException(nameof(dailyExpenseValues))));
            ExpenseIdPrefix = expenseIdPrefix;
            SaveVersion = saveVersion;
            UnlockSets = new List<DayUnlockSet>(unlockSets ?? Array.Empty<DayUnlockSet>()).AsReadOnly();
        }

        public DayTimingRules TimingRules { get; }
        public int DailyExpenseValue { get; }
        public IReadOnlyDictionary<int, int> DailyExpenseValues { get; }
        public string ExpenseIdPrefix { get; }
        public int SaveVersion { get; }
        public IReadOnlyList<DayUnlockSet> UnlockSets { get; }

        public string GetExpenseId(int dayNumber)
        {
            return ExpenseIdPrefix + dayNumber;
        }

        public int GetExpenseValue(int dayNumber)
        {
            return DailyExpenseValues.TryGetValue(dayNumber, out int value) ? value : DailyExpenseValue;
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
                state.UnlockedGuidebookEntryIds.Concat(active.SelectMany(value => value.GuidebookEntryIds)).Distinct(StringComparer.Ordinal).ToArray(),
                state.TutorialFlags,
                state.NarrativeFlags,
                state.SeenOrderIds);
        }
    }
}
