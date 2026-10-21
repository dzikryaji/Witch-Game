using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Witch.Domain.Progression
{
    [Serializable]
    public sealed class DayState
    {
        public const int FirstDay = 1;
        public const int FinalDay = 14;

        private readonly ReadOnlyCollection<string> customerSequence;
        private readonly ReadOnlyDictionary<string, bool> readOnlyDayFlags;

        public DayState(
            int dayNumber,
            IReadOnlyList<string> customerSequence,
            int currentEncounterIndex = 0,
            int qualifyingIncome = 0,
            IReadOnlyDictionary<string, bool> dayFlags = null)
        {
            if (dayNumber < FirstDay || dayNumber > FinalDay)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(dayNumber),
                    $"Day number must be between {FirstDay} and {FinalDay}.");
            }

            if (customerSequence == null)
            {
                throw new ArgumentNullException(nameof(customerSequence));
            }

            if (currentEncounterIndex < 0 || currentEncounterIndex > customerSequence.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(currentEncounterIndex));
            }

            if (qualifyingIncome < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(qualifyingIncome));
            }

            var customerIds = new List<string>(customerSequence.Count);
            foreach (string customerId in customerSequence)
            {
                if (string.IsNullOrWhiteSpace(customerId))
                {
                    throw new ArgumentException("Customer IDs must not be empty.", nameof(customerSequence));
                }

                customerIds.Add(customerId);
            }

            var copiedDayFlags = CopyFlags(dayFlags, nameof(dayFlags));

            DayNumber = dayNumber;
            CurrentEncounterIndex = currentEncounterIndex;
            QualifyingIncome = qualifyingIncome;
            this.customerSequence = customerIds.AsReadOnly();
            readOnlyDayFlags = new ReadOnlyDictionary<string, bool>(copiedDayFlags);
        }

        public int DayNumber { get; }

        public IReadOnlyList<string> CustomerSequence => customerSequence;

        public int CurrentEncounterIndex { get; }

        public int QualifyingIncome { get; }

        public IReadOnlyDictionary<string, bool> DayFlags => readOnlyDayFlags;

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
                    throw new ArgumentException("Day flag IDs must not be empty.", parameterName);
                }

                copy.Add(flag.Key, flag.Value);
            }

            return copy;
        }
    }
}
