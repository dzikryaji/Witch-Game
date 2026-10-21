using System;

namespace Witch.Domain.Potions
{
    [Serializable]
    public sealed class PotIngredient
    {
        public PotIngredient(string herbId)
        {
            if (string.IsNullOrWhiteSpace(herbId))
            {
                throw new ArgumentException("Herb ID must not be empty.", nameof(herbId));
            }

            HerbId = herbId;
        }

        public string HerbId { get; }
    }
}
