using System.Threading.Tasks;

namespace Witch.Application.Ports
{
    /// <summary>
    /// Optional Apple Game Center platform service. Game Center is never
    /// required: local gameplay, local saves, and offline play must keep
    /// working when every operation of this service fails.
    /// </summary>
    public interface IGameCenterService
    {
        /// <summary>
        /// Initializes the local Game Center player. May present Apple-owned
        /// sign-in UI. Must never block or fail startup; a failure result
        /// only means Game Center features are unavailable.
        /// </summary>
        Task<ServiceResult> InitializeAsync();

        /// <summary>
        /// Reports an achievement percentage. Reporting an already earned
        /// achievement at 100 percent again must be harmless.
        /// </summary>
        Task<ServiceResult> ReportAchievementAsync(string achievementId, double percentComplete);

        /// <summary>
        /// Presents the native Game Center dashboard. Only valid while
        /// <see cref="IsAuthenticated"/> is true.
        /// </summary>
        Task<ServiceResult> ShowDashboardAsync();

        /// <summary>
        /// Whether the local player is currently authenticated to Game Center.
        /// </summary>
        bool IsAuthenticated { get; }
    }
}
