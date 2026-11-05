using System;

namespace Witch.Domain.Potions
{
    [Serializable]
    public sealed class PreparedPotion
    {
        public PreparedPotion(HerbPair herbPair)
        {
            HerbPair = herbPair ?? throw new ArgumentNullException(nameof(herbPair));
        }

        public HerbPair HerbPair { get; }
    }
}
