using System;

namespace Witch.Domain.Potions
{
    [Serializable]
    public sealed class HerbPair : IEquatable<HerbPair>
    {
        public HerbPair(string firstHerbId, string secondHerbId)
        {
            ValidateHerbId(firstHerbId, nameof(firstHerbId));
            ValidateHerbId(secondHerbId, nameof(secondHerbId));

            if (string.Equals(firstHerbId, secondHerbId, StringComparison.Ordinal))
            {
                throw new ArgumentException("A herb pair must contain two different herbs.", nameof(secondHerbId));
            }

            if (string.CompareOrdinal(firstHerbId, secondHerbId) < 0)
            {
                FirstHerbId = firstHerbId;
                SecondHerbId = secondHerbId;
            }
            else
            {
                FirstHerbId = secondHerbId;
                SecondHerbId = firstHerbId;
            }
        }

        public string FirstHerbId { get; }

        public string SecondHerbId { get; }

        public bool Equals(HerbPair other)
        {
            return other != null
                && string.Equals(FirstHerbId, other.FirstHerbId, StringComparison.Ordinal)
                && string.Equals(SecondHerbId, other.SecondHerbId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as HerbPair);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (StringComparer.Ordinal.GetHashCode(FirstHerbId) * 397)
                    ^ StringComparer.Ordinal.GetHashCode(SecondHerbId);
            }
        }

        private static void ValidateHerbId(string herbId, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(herbId))
            {
                throw new ArgumentException("Herb ID must not be empty.", parameterName);
            }
        }
    }
}
