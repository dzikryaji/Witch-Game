using System;
using System.Threading.Tasks;
using Witch.Application.Ports;

namespace Witch.Application.Flow
{
    /// <summary>
    /// Game Center reporting never changes a gameplay result: a failed or
    /// throwing service is swallowed here.
    /// </summary>
    internal static class GameCenterReporting
    {
        public static async Task ReportQuietlyAsync(
            IGameCenterService gameCenter,
            string achievementId,
            double percentComplete)
        {
            if (gameCenter == null)
            {
                return;
            }

            try
            {
                await gameCenter.ReportAchievementAsync(achievementId, percentComplete);
            }
            catch (Exception)
            {
                // Game Center failure never fails gameplay or saves.
            }
        }
    }
}
