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
        private const string ExpenseIdPrefix = "expense.day.";

        public DayProgressionSetup(
            IReadOnlyDictionary<int, TimeSpan> dayTimerDurations,
            IReadOnlyDictionary<int, int> dailyExpenseValues,
            int saveVersion,
            IReadOnlyList<DayUnlockSet> unlockSets = null)
        {
            if (dayTimerDurations == null)
            {
                throw new ArgumentNullException(nameof(dayTimerDurations));
            }

            if (dailyExpenseValues == null)
            {
                throw new ArgumentNullException(nameof(dailyExpenseValues));
            }

            if (saveVersion < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(saveVersion));
            }

            DayTimerDurations = new ReadOnlyDictionary<int, TimeSpan>(
                new Dictionary<int, TimeSpan>(dayTimerDurations));
            DailyExpenseValues = new ReadOnlyDictionary<int, int>(
                new Dictionary<int, int>(dailyExpenseValues));
            SaveVersion = saveVersion;
            UnlockSets = new List<DayUnlockSet>(unlockSets ?? Array.Empty<DayUnlockSet>()).AsReadOnly();
        }

        public IReadOnlyDictionary<int, TimeSpan> DayTimerDurations { get; }
        public IReadOnlyDictionary<int, int> DailyExpenseValues { get; }
        public int SaveVersion { get; }
        public IReadOnlyList<DayUnlockSet> UnlockSets { get; }

        public string GetExpenseId(int dayNumber)
        {
            return ExpenseIdPrefix + dayNumber;
        }

        public TimeSpan GetTimerDuration(int dayNumber)
        {
            if (!DayTimerDurations.TryGetValue(dayNumber, out TimeSpan value))
            {
                throw new KeyNotFoundException("No timer configured for day " + dayNumber + ".");
            }

            return value;
        }

        public int GetExpenseValue(int dayNumber)
        {
            if (!DailyExpenseValues.TryGetValue(dayNumber, out int value))
            {
                throw new KeyNotFoundException("No expense configured for day " + dayNumber + ".");
            }

            return value;
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
                state.SeenOrderIds,
                state.ServedCategoryIds);
        }
    }
}
