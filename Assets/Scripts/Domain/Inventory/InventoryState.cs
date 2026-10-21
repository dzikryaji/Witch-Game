using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Witch.Domain.Inventory
{
    [Serializable]
    public sealed class InventoryState
    {
        private readonly Dictionary<string, int> herbQuantities;
        private readonly ReadOnlyDictionary<string, int> readOnlyHerbQuantities;

        public InventoryState(IReadOnlyDictionary<string, int> startingQuantities = null)
        {
            herbQuantities = new Dictionary<string, int>(StringComparer.Ordinal);

            if (startingQuantities != null)
            {
                foreach (KeyValuePair<string, int> item in startingQuantities)
                {
                    ValidateHerbId(item.Key);
                    ValidateQuantity(item.Value, nameof(startingQuantities));
                    herbQuantities.Add(item.Key, item.Value);
                }
            }

            readOnlyHerbQuantities = new ReadOnlyDictionary<string, int>(herbQuantities);
        }

        public IReadOnlyDictionary<string, int> HerbQuantities => readOnlyHerbQuantities;

        public int GetHerbQuantity(string herbId)
        {
            ValidateHerbId(herbId);
            return herbQuantities.TryGetValue(herbId, out int quantity) ? quantity : 0;
        }

        private static void ValidateHerbId(string herbId)
        {
            if (string.IsNullOrWhiteSpace(herbId))
            {
                throw new ArgumentException("Herb ID must not be empty.", nameof(herbId));
            }
        }

        private static void ValidateQuantity(int quantity, string parameterName)
        {
            if (quantity < 0)
            {
                throw new ArgumentOutOfRangeException(parameterName, "Inventory quantity must not be negative.");
            }
        }
    }
}
