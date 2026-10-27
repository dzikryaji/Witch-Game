using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Witch.Domain.Progression
{
    [Serializable]
    public sealed class RunState
    {
        public const int FirstDay = 1;
        public const int RewindSuspicionThreshold = 3;

        private readonly ReadOnlyCollection<string> unlockedCategoryIds;
        private readonly ReadOnlyCollection<string> unlockedHerbIds;
        private readonly ReadOnlyCollection<string> unlockedRecipeIds;
        private readonly ReadOnlyCollection<string> unlockedGuidebookEntryIds;
        private readonly ReadOnlyDictionary<string, bool> readOnlyTutorialFlags;
        private readonly ReadOnlyDictionary<string, bool> readOnlyNarrativeFlags;

        public RunState(
            string runId,
            int currentDayNumber,
            int money,
            int suspicion,
            int rewindCount,
            IReadOnlyList<string> unlockedCategoryIds = null,
            IReadOnlyList<string> unlockedHerbIds = null,
            IReadOnlyList<string> unlockedRecipeIds = null,
            IReadOnlyList<string> unlockedGuidebookEntryIds = null,
            IReadOnlyDictionary<string, bool> tutorialFlags = null,
            IReadOnlyDictionary<string, bool> narrativeFlags = null)
        {
            if (string.IsNullOrWhiteSpace(runId))
            {
                throw new ArgumentException("Run ID must not be empty.", nameof(runId));
            }

            if (currentDayNumber < FirstDay)
            {
                throw new ArgumentOutOfRangeException(nameof(currentDayNumber));
            }

            if (money < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(money));
            }

            if (suspicion < 0 || suspicion > RewindSuspicionThreshold)
            {
                throw new ArgumentOutOfRangeException(nameof(suspicion));
            }

            if (rewindCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rewindCount));
            }

            RunId = runId;
            CurrentDayNumber = currentDayNumber;
            Money = money;
            Suspicion = suspicion;
            RewindCount = rewindCount;
            this.unlockedCategoryIds = CopyIds(unlockedCategoryIds, nameof(unlockedCategoryIds));
            this.unlockedHerbIds = CopyIds(unlockedHerbIds, nameof(unlockedHerbIds));
            this.unlockedRecipeIds = CopyIds(unlockedRecipeIds, nameof(unlockedRecipeIds));
            this.unlockedGuidebookEntryIds = CopyIds(
                unlockedGuidebookEntryIds,
                nameof(unlockedGuidebookEntryIds));
            readOnlyTutorialFlags = new ReadOnlyDictionary<string, bool>(
                CopyFlags(tutorialFlags, nameof(tutorialFlags)));
            readOnlyNarrativeFlags = new ReadOnlyDictionary<string, bool>(
                CopyFlags(narrativeFlags, nameof(narrativeFlags)));
        }

        public string RunId { get; }

        public int CurrentDayNumber { get; }

        public int Money { get; }

        public int Suspicion { get; }

        public int RewindCount { get; }

        public IReadOnlyList<string> UnlockedCategoryIds => unlockedCategoryIds;

        public IReadOnlyList<string> UnlockedHerbIds => unlockedHerbIds;

        public IReadOnlyList<string> UnlockedRecipeIds => unlockedRecipeIds;

        public IReadOnlyList<string> UnlockedGuidebookEntryIds => unlockedGuidebookEntryIds;

        public IReadOnlyDictionary<string, bool> TutorialFlags => readOnlyTutorialFlags;

        public IReadOnlyDictionary<string, bool> NarrativeFlags => readOnlyNarrativeFlags;

        public RunState WithEconomy(int money, int suspicion)
        {
            return WithProgress(CurrentDayNumber, money, suspicion, RewindCount);
        }

        public RunState WithProgress(
            int currentDayNumber,
            int money,
            int suspicion,
            int rewindCount)
        {
            return new RunState(
                RunId,
                currentDayNumber,
                money,
                suspicion,
                rewindCount,
                UnlockedCategoryIds,
                UnlockedHerbIds,
                UnlockedRecipeIds,
                UnlockedGuidebookEntryIds,
                TutorialFlags,
                NarrativeFlags);
        }

        private static ReadOnlyCollection<string> CopyIds(IReadOnlyList<string> source, string parameterName)
        {
            if (source == null)
            {
                return new List<string>().AsReadOnly();
            }

            var copy = new List<string>(source.Count);
            var uniqueIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in source)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    throw new ArgumentException("Stable IDs must not be empty.", parameterName);
                }

                if (!uniqueIds.Add(id))
                {
                    throw new ArgumentException("Stable IDs must not contain duplicates.", parameterName);
                }

                copy.Add(id);
            }

            return copy.AsReadOnly();
        }

        private static Dictionary<string, bool> CopyFlags(
            IReadOnlyDictionary<string, bool> source,
            string parameterName)
        {
            var copy = new Dictionary<string, bool>(StringComparer.Ordinal);
            if (source == null)
            {
                return copy;
            }

            foreach (KeyValuePair<string, bool> flag in source)
            {
                if (string.IsNullOrWhiteSpace(flag.Key))
                {
                    throw new ArgumentException("Flag IDs must not be empty.", parameterName);
                }

                copy.Add(flag.Key, flag.Value);
            }

            return copy;
        }
    }
}
