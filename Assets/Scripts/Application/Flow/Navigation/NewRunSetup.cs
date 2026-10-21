using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Witch.Application.Flow
{
    public sealed class NewRunSetup
    {
        private readonly ReadOnlyDictionary<string, int> startingInventory;
        private readonly ReadOnlyCollection<string> firstDayCustomerSequence;

        public NewRunSetup(
            int startingMoney,
            IReadOnlyDictionary<string, int> startingInventory,
            int startingHexCount,
            IReadOnlyList<string> firstDayCustomerSequence)
        {
            if (startingMoney < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startingMoney));
            }

            if (startingInventory == null)
            {
                throw new ArgumentNullException(nameof(startingInventory));
            }

            if (startingHexCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startingHexCount));
            }

            if (firstDayCustomerSequence == null)
            {
                throw new ArgumentNullException(nameof(firstDayCustomerSequence));
            }

            StartingMoney = startingMoney;
            this.startingInventory = new ReadOnlyDictionary<string, int>(
                new Dictionary<string, int>(startingInventory, StringComparer.Ordinal));
            StartingHexCount = startingHexCount;
            this.firstDayCustomerSequence = new List<string>(firstDayCustomerSequence).AsReadOnly();
        }

        public int StartingMoney { get; }

        public IReadOnlyDictionary<string, int> StartingInventory => startingInventory;

        public int StartingHexCount { get; }

        public IReadOnlyList<string> FirstDayCustomerSequence => firstDayCustomerSequence;

        internal StartNewRunCommand CreateCommand(string runId)
        {
            return new StartNewRunCommand(
                runId,
                StartingMoney,
                StartingInventory,
                StartingHexCount,
                FirstDayCustomerSequence);
        }
    }
}
