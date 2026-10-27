using Witch.Domain.Orders;

namespace Witch.Application.Flow
{
    public sealed class AdmitCustomerCommand
    {
        public AdmitCustomerCommand(string customerId, OrderRequest order)
        {
            CustomerId = customerId;
            Order = order;
        }

        public string CustomerId { get; }

        public OrderRequest Order { get; }
    }
}
