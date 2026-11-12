using System.Threading.Tasks;
using Witch.Application.Ports;

namespace Witch.Infrastructure.GameCenter
{
    /// <summary>
    /// Failure-tolerant Game Center stand-in for the Unity Editor, unsupported
    /// platforms, local unsigned builds, and deliberate feature-disable builds.
    /// Every operation reports the service as unavailable and never touches
    /// gameplay, saves, or startup.
    /// </summary>
    public sealed class NoOpGameCenterService : IGameCenterService
    {
        public const string UnavailableMessage = "Game Center is unavailable in this build.";

        public bool IsAuthenticated => false;

        public Task<ServiceResult> InitializeAsync()
        {
            return Task.FromResult(Unavailable());
        }

        public Task<ServiceResult> ReportAchievementAsync(string achievementId, double percentComplete)
        {
            if (!IsValidAchievementRequest(achievementId, percentComplete, out ServiceResult invalid))
            {
                return Task.FromResult(invalid);
            }

            return Task.FromResult(Unavailable());
        }

        public Task<ServiceResult> ShowDashboardAsync()
        {
            return Task.FromResult(Unavailable());
        }

        private static ServiceResult Unavailable()
        {
            return ServiceResult.Failure(ServiceError.Unsupported, UnavailableMessage);
        }

        internal static bool IsValidAchievementRequest(
            string achievementId,
            double percentComplete,
            out ServiceResult failure)
        {
            if (string.IsNullOrWhiteSpace(achievementId))
            {
                failure = ServiceResult.Failure(ServiceError.InvalidRequest, "An achievement ID is required.");
                return false;
            }

            if (double.IsNaN(percentComplete) || double.IsInfinity(percentComplete)
                || percentComplete < 0d || percentComplete > 100d)
            {
                failure = ServiceResult.Failure(
                    ServiceError.InvalidRequest,
                    "Achievement completion must be between 0 and 100 percent.");
                return false;
            }

            failure = null;
            return true;
        }
    }
}
