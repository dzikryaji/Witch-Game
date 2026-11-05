using System;
using Witch.Domain.Potions;

namespace Witch.Domain.Orders
{
    [Serializable]
    public sealed class OrderRequest
    {
        public OrderRequest(string orderId, HerbPair requestedPair)
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                throw new ArgumentException("Order ID must not be empty.", nameof(orderId));
            }

            OrderId = orderId;
            RequestedPair = requestedPair ?? throw new ArgumentNullException(nameof(requestedPair));
        }

        public string OrderId { get; }

        public HerbPair RequestedPair { get; }

        public bool IsCorrect(PreparedPotion servedPotion)
        {
            return servedPotion != null && RequestedPair.Equals(servedPotion.HerbPair);
        }
    }
}
