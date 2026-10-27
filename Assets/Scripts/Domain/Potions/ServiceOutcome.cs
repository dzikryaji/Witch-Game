using System;
using Witch.Domain.Progression;

namespace Witch.Domain.Potions
{
    public enum ServiceOutcomeKind
    {
        Correct,
        WrongValid,
        Discarded
    }

    public sealed class ServiceOutcome
    {
        public ServiceOutcome(
            ServiceOutcomeKind kind,
            int payment,
            int suspicionAdded,
            RunState updatedRunState)
        {
            if (payment < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(payment));
            }

            if (suspicionAdded < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(suspicionAdded));
            }

            Kind = kind;
            Payment = payment;
            SuspicionAdded = suspicionAdded;
            UpdatedRunState = updatedRunState ?? throw new ArgumentNullException(nameof(updatedRunState));
        }

        public ServiceOutcomeKind Kind { get; }

        public int Payment { get; }

        public int SuspicionAdded { get; }

        public RunState UpdatedRunState { get; }
    }
}
