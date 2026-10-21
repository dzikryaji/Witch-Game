using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Witch.Application.Flow
{
    public sealed class StartNewRunCommand
    {
        public StartNewRunCommand(
            string runId,
            int startingMoney,
            IReadOnlyDictionary<string, int> startingInventory,
            int startingHexCount,
            IReadOnlyList<string> firstDayCustomerSequence)
        {
            RunId = runId;
            StartingMoney = startingMoney;
            StartingInventory = startingInventory == null
                ? null
                : new ReadOnlyDictionary<string, int>(
                    new Dictionary<string, int>(startingInventory, System.StringComparer.Ordinal));
            StartingHexCount = startingHexCount;
            FirstDayCustomerSequence = firstDayCustomerSequence == null
                ? null
                : new List<string>(firstDayCustomerSequence).AsReadOnly();
        }

        public string RunId { get; }

        public int StartingMoney { get; }

        public IReadOnlyDictionary<string, int> StartingInventory { get; }

        public int StartingHexCount { get; }

        public IReadOnlyList<string> FirstDayCustomerSequence { get; }
    }
}
