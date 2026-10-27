using System;

namespace Witch.Application.Flow
{
    public enum AdmitCustomerError
    {
        None,
        MissingSession,
        MissingCommand,
        InvalidCustomer,
        InvalidOrder,
        InvalidPhase,
        ConflictingArrival
    }

    public sealed class AdmitCustomerResult
    {
        private AdmitCustomerResult(
            AdmitCustomerError error,
            string errorMessage,
            bool alreadyAdmitted)
        {
            Error = error;
            ErrorMessage = errorMessage;
            AlreadyAdmitted = alreadyAdmitted;
        }

        public bool Succeeded => Error == AdmitCustomerError.None;

        public AdmitCustomerError Error { get; }

        public string ErrorMessage { get; }

        public bool AlreadyAdmitted { get; }

        public static AdmitCustomerResult Success(bool alreadyAdmitted)
        {
            return new AdmitCustomerResult(AdmitCustomerError.None, null, alreadyAdmitted);
        }

        public static AdmitCustomerResult Failure(AdmitCustomerError error, string errorMessage)
        {
            if (error == AdmitCustomerError.None)
            {
                throw new ArgumentException("A failed arrival requires an error.", nameof(error));
            }

            return new AdmitCustomerResult(error, errorMessage, false);
        }
    }
}
