using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Witch.Domain.Potions;

namespace Witch.Domain.Orders
{
    [Serializable]
    public sealed class OrderSelectionState
    {
        private readonly ReadOnlyCollection<string> usedOrderIds;
        private readonly ReadOnlyCollection<string> givenPairKeys;
        private readonly ReadOnlyCollection<string> scheduledIntroOrderIds;

        public OrderSelectionState(
            IReadOnlyList<string> usedOrderIds = null,
            IReadOnlyList<string> givenPairKeys = null,
            IReadOnlyList<string> scheduledIntroOrderIds = null,
            string pendingIntroOrderId = null,
            int introSlot = 0,
            string lastPairKey = null)
        {
            this.usedOrderIds = CopyIds(usedOrderIds);
            this.givenPairKeys = CopyIds(givenPairKeys);
            this.scheduledIntroOrderIds = CopyIds(scheduledIntroOrderIds);
            PendingIntroOrderId = string.IsNullOrWhiteSpace(pendingIntroOrderId) ? null : pendingIntroOrderId;
            IntroSlot = introSlot;
            LastPairKey = string.IsNullOrWhiteSpace(lastPairKey) ? null : lastPairKey;
        }

        public IReadOnlyList<string> UsedOrderIds => usedOrderIds;
        public IReadOnlyList<string> GivenPairKeys => givenPairKeys;
        public IReadOnlyList<string> ScheduledIntroOrderIds => scheduledIntroOrderIds;
        public string PendingIntroOrderId { get; }
        public int IntroSlot { get; }
        public string LastPairKey { get; }

        public OrderSelectionState WithAdmittedOrder(string orderId, HerbPair pair)
        {
            string pairKey = OrderPairKey.From(pair);
            bool alreadyUsed = usedOrderIds.Any(id => string.Equals(id, orderId, StringComparison.Ordinal));
            var used = alreadyUsed ? usedOrderIds.ToArray() : usedOrderIds.Concat(new[] { orderId }).ToArray();
            var pairs = givenPairKeys.Contains(pairKey, StringComparer.Ordinal)
                ? givenPairKeys.ToArray()
                : givenPairKeys.Concat(new[] { pairKey }).ToArray();
            var scheduled = scheduledIntroOrderIds.Where(id => !string.Equals(id, orderId, StringComparison.Ordinal)).ToArray();
            bool pendingDelivered = string.Equals(PendingIntroOrderId, orderId, StringComparison.Ordinal);
            return new OrderSelectionState(
                used,
                pairs,
                scheduled,
                pendingDelivered ? null : PendingIntroOrderId,
                IntroSlot,
                pairKey);
        }

        public OrderSelectionState WithPairCycleCleared()
        {
            return new OrderSelectionState(
                usedOrderIds,
                new string[0],
                scheduledIntroOrderIds,
                PendingIntroOrderId,
                IntroSlot,
                LastPairKey);
        }

        public OrderSelectionState WithSchedule(
            IReadOnlyList<string> scheduledIntroOrderIds,
            string pendingIntroOrderId,
            int introSlot)
        {
            return new OrderSelectionState(
                usedOrderIds,
                givenPairKeys,
                scheduledIntroOrderIds,
                pendingIntroOrderId,
                introSlot,
                LastPairKey);
        }

        private static ReadOnlyCollection<string> CopyIds(IReadOnlyList<string> source)
        {
            if (source == null) return new List<string>().AsReadOnly();
            var copy = new List<string>(source.Count);
            foreach (string id in source)
            {
                if (!string.IsNullOrWhiteSpace(id)) copy.Add(id);
            }
            return copy.AsReadOnly();
        }
    }

    public static class OrderPairKey
    {
        public static string From(HerbPair pair)
        {
            if (pair == null) throw new ArgumentNullException(nameof(pair));
            return string.Join("|", new[] { pair.FirstHerbId, pair.SecondHerbId }
                .OrderBy(id => id, StringComparer.Ordinal));
        }
    }
}
