using System;

namespace Witch.Application.Ports
{
    public sealed class ServiceResult
    {
        private ServiceResult(ServiceError error, string errorMessage)
        {
            Error = error;
            ErrorMessage = errorMessage;
        }

        public bool Succeeded => Error == ServiceError.None;

        public ServiceError Error { get; }

        public string ErrorMessage { get; }

        public static ServiceResult Success()
        {
            return new ServiceResult(ServiceError.None, null);
        }

        public static ServiceResult Failure(ServiceError error, string errorMessage)
        {
            if (error == ServiceError.None)
            {
                throw new ArgumentException("A failed service result requires an error.", nameof(error));
            }

            return new ServiceResult(error, errorMessage);
        }
    }
}
