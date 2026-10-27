using System;
using Witch.Application.Ports;
using Witch.Domain;

namespace Witch.Application.Flow
{
    public sealed class AdmitCustomerUseCase
    {
        private readonly IClock clock;

        public AdmitCustomerUseCase(IClock clock)
        {
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public AdmitCustomerResult Execute(GameSession session, AdmitCustomerCommand command)
        {
            if (session == null)
            {
                return AdmitCustomerResult.Failure(AdmitCustomerError.MissingSession, "Session is required.");
            }

            if (command == null)
            {
                return AdmitCustomerResult.Failure(AdmitCustomerError.MissingCommand, "Arrival command is required.");
            }

            if (string.IsNullOrWhiteSpace(command.CustomerId))
            {
                return AdmitCustomerResult.Failure(AdmitCustomerError.InvalidCustomer, "Customer ID is required.");
            }

            if (command.Order == null)
            {
                return AdmitCustomerResult.Failure(AdmitCustomerError.InvalidOrder, "Order is required.");
            }

            if (session.CurrentPhase == GamePhase.CustomerEncounter)
            {
                session.RefreshTimer(clock.UtcNow);
                bool sameArrival = string.Equals(
                        session.AttemptState.ActiveCustomerId,
                        command.CustomerId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        session.AttemptState.ActiveOrder?.OrderId,
                        command.Order.OrderId,
                        StringComparison.Ordinal);
                return sameArrival
                    ? AdmitCustomerResult.Success(alreadyAdmitted: true)
                    : AdmitCustomerResult.Failure(
                        AdmitCustomerError.ConflictingArrival,
                        "Another customer encounter is already active.");
            }

            if (session.CurrentPhase != GamePhase.CustomerArrival)
            {
                return AdmitCustomerResult.Failure(
                    AdmitCustomerError.InvalidPhase,
                    "Customers may enter only during the arrival phase.");
            }

            session.BeginCustomerEncounter(command.CustomerId, command.Order, clock.UtcNow);
            return AdmitCustomerResult.Success(alreadyAdmitted: false);
        }
    }
}
