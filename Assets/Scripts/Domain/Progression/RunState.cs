using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Witch.Domain.Inventory;

namespace Witch.Domain.Progression
{
    [Serializable]
    public sealed class RunState
    {
        private readonly ReadOnlyCollection<string> unlockIds;
        private readonly ReadOnlyCollection<string> upgradeIds;
        private readonly ReadOnlyDictionary<string, bool> readOnlyNarrativeFlags;

        public RunState(
            string runId,
            int money,
            InventoryState inventory,
            int hexCount,
            DayState currentDay,
            IReadOnlyList<string> unlockIds = null,
            IReadOnlyList<string> upgradeIds = null,
            IReadOnlyDictionary<string, bool> narrativeFlags = null)
        {
            if (string.IsNullOrWhiteSpace(runId))
            {
                throw new ArgumentException("Run ID must not be empty.", nameof(runId));
            }

            if (money < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(money));
            }

            if (hexCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(hexCount));
            }

            RunId = runId;
            Money = money;
            Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            HexCount = hexCount;
            CurrentDay = currentDay ?? throw new ArgumentNullException(nameof(currentDay));
            this.unlockIds = CopyIds(unlockIds, nameof(unlockIds));
            this.upgradeIds = CopyIds(upgradeIds, nameof(upgradeIds));
            readOnlyNarrativeFlags = new ReadOnlyDictionary<string, bool>(
                CopyFlags(narrativeFlags, nameof(narrativeFlags)));
        }

        public string RunId { get; }

        public int Money { get; }

        public InventoryState Inventory { get; }

        public int HexCount { get; }

        public DayState CurrentDay { get; }

        public IReadOnlyList<string> UnlockIds => unlockIds;

        public IReadOnlyList<string> UpgradeIds => upgradeIds;

        public IReadOnlyDictionary<string, bool> NarrativeFlags => readOnlyNarrativeFlags;

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
                    throw new ArgumentException("Narrative flag IDs must not be empty.", parameterName);
                }

                copy.Add(flag.Key, flag.Value);
            }

            return copy;
        }
    }
}
