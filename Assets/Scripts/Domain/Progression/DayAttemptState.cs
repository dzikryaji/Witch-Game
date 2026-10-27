using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Witch.Domain.Orders;
using Witch.Domain.Potions;

namespace Witch.Domain.Progression
{
    [Serializable]
    public sealed class DayAttemptState
    {
        private readonly ReadOnlyCollection<string> usedOrderIds;

        public DayAttemptState(
            int attemptSeed,
            TimeSpan remainingTime,
            IReadOnlyList<string> usedOrderIds,
            string activeCustomerId,
            OrderRequest activeOrder,
            int earnedMoney,
            PotionDraft potionDraft,
            Recipe preparedRecipe)
        {
            if (remainingTime < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(remainingTime));
            }

            if (earnedMoney < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(earnedMoney));
            }

            bool hasCustomer = !string.IsNullOrWhiteSpace(activeCustomerId);
            bool hasOrder = activeOrder != null;
            if (hasCustomer != hasOrder)
            {
                throw new ArgumentException("Active customer and order must either both be set or both be empty.");
            }

            AttemptSeed = attemptSeed;
            RemainingTime = remainingTime;
            this.usedOrderIds = CopyIds(usedOrderIds);
            ActiveCustomerId = activeCustomerId;
            ActiveOrder = activeOrder;
            EarnedMoney = earnedMoney;
            PotionDraft = potionDraft ?? throw new ArgumentNullException(nameof(potionDraft));
            PreparedRecipe = preparedRecipe;
        }

        public int AttemptSeed { get; }

        public TimeSpan RemainingTime { get; }

        public IReadOnlyList<string> UsedOrderIds => usedOrderIds;

        public string ActiveCustomerId { get; }

        public OrderRequest ActiveOrder { get; }

        public int EarnedMoney { get; }

        public PotionDraft PotionDraft { get; }

        public Recipe PreparedRecipe { get; }

        private static ReadOnlyCollection<string> CopyIds(IReadOnlyList<string> source)
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
                    throw new ArgumentException("Used order IDs must not be empty.", nameof(source));
                }

                if (!uniqueIds.Add(id))
                {
                    throw new ArgumentException("Used order IDs must not contain duplicates.", nameof(source));
                }

                copy.Add(id);
            }

            return copy.AsReadOnly();
        }
    }
}
