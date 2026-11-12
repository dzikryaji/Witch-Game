#if UNITY_EDITOR || UNITY_STANDALONE_OSX
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Apple.Core.Runtime;
using Apple.GameKit;
using Witch.Application.Ports;

namespace Witch.Infrastructure.GameCenter
{
    /// <summary>
    /// Apple.GameKit adapter for signed macOS builds. Bounded and
    /// failure-tolerant: no operation throws into gameplay, startup, or
    /// saves, and no player identifier or secret is ever logged.
    ///
    /// Reports requested while the player is not yet authenticated are
    /// queued in memory and retried after a later successful authentication.
    /// </summary>
    public sealed class AppleGameCenterService : IGameCenterService, IDisposable
    {
        private const string NotAuthenticatedMessage = "Game Center player is not authenticated.";
        private readonly object gate = new object();
        private readonly Dictionary<string, double> pendingReports = new Dictionary<string, double>();
        private bool eventsHooked;

        public bool IsAuthenticated
        {
            get
            {
                try
                {
                    return GKLocalPlayer.Local.IsAuthenticated;
                }
                catch (Exception)
                {
                    // GameKit is unavailable in this environment.
                    return false;
                }
            }
        }

        public async Task<ServiceResult> InitializeAsync()
        {
            try
            {
                HookAuthenticationEvents();

                try
                {
                    // Presents Apple-owned sign-in UI when account action is
                    // required. The task throws when the player declines,
                    // is restricted, or is offline; the authenticated state is
                    // re-checked below either way.
                    await GKLocalPlayer.Authenticate();
                }
                catch (GameKitException)
                {
                    // Bounded: keep Game Center optional and silent.
                }

                if (GKLocalPlayer.Local.IsAuthenticated)
                {
                    await FlushPendingReportsAsync();
                    return ServiceResult.Success();
                }

                return ServiceResult.Failure(ServiceError.OperationFailed, NotAuthenticatedMessage);
            }
            catch (Exception exception)
            {
                return ServiceResult.Failure(
                    ServiceError.OperationFailed,
                    "Game Center initialization failed: " + exception.Message);
            }
        }

        public async Task<ServiceResult> ReportAchievementAsync(string achievementId, double percentComplete)
        {
            if (!NoOpGameCenterService.IsValidAchievementRequest(achievementId, percentComplete, out ServiceResult invalid))
            {
                return invalid;
            }

            try
            {
                if (!IsAuthenticated)
                {
                    QueuePendingReport(achievementId, percentComplete);
                    return ServiceResult.Failure(ServiceError.OperationFailed, NotAuthenticatedMessage);
                }

                GKAchievement achievement = GKAchievement.Init(achievementId);
                achievement.PercentComplete = percentComplete;
                achievement.ShowCompletionBanner = true;
                await GKAchievement.Report(achievement);
                return ServiceResult.Success();
            }
            catch (Exception exception)
            {
                // Reporting failures never affect gameplay results; the report
                // is queued for a later authentication retry.
                QueuePendingReport(achievementId, percentComplete);
                return ServiceResult.Failure(
                    ServiceError.OperationFailed,
                    "Game Center achievement report failed: " + exception.Message);
            }
        }

        public async Task<ServiceResult> ShowDashboardAsync()
        {
            try
            {
                if (!IsAuthenticated)
                {
                    return ServiceResult.Failure(ServiceError.OperationFailed, NotAuthenticatedMessage);
                }

                GKGameCenterViewController dashboard =
                    GKGameCenterViewController.Init(GKGameCenterViewControllerState.Dashboard);
                await dashboard.Present();
                return ServiceResult.Success();
            }
            catch (Exception exception)
            {
                return ServiceResult.Failure(
                    ServiceError.OperationFailed,
                    "Game Center dashboard failed: " + exception.Message);
            }
        }

        private void HookAuthenticationEvents()
        {
            if (eventsHooked)
            {
                return;
            }

            eventsHooked = true;
            GKLocalPlayer.AuthenticateUpdate += OnAuthenticateUpdate;
            GKLocalPlayer.AuthenticateError += OnAuthenticateError;
        }

        public void Dispose()
        {
            if (!eventsHooked)
            {
                return;
            }

            eventsHooked = false;
            GKLocalPlayer.AuthenticateUpdate -= OnAuthenticateUpdate;
            GKLocalPlayer.AuthenticateError -= OnAuthenticateError;
        }

        private async void OnAuthenticateUpdate(GKLocalPlayer localPlayer)
        {
            // The authentication handler can run more than once; always
            // re-check the authenticated state instead of trusting the event.
            if (localPlayer == null || !localPlayer.IsAuthenticated)
            {
                return;
            }

            await FlushPendingReportsAsync();
        }

        private void OnAuthenticateError(NSError error)
        {
            // Bounded logging without player identifiers or secrets.
            UnityEngine.Debug.LogWarning("[GameCenter] Authentication error: " + (error?.LocalizedDescription ?? "unknown"));
        }

        private void QueuePendingReport(string achievementId, double percentComplete)
        {
            lock (gate)
            {
                // Two production achievements exist, so at most one queued
                // percentage per achievement ID is retained.
                pendingReports[achievementId] = percentComplete;
            }
        }

        private async Task FlushPendingReportsAsync()
        {
            List<KeyValuePair<string, double>> queued;
            lock (gate)
            {
                if (pendingReports.Count == 0)
                {
                    return;
                }

                queued = new List<KeyValuePair<string, double>>(pendingReports);
                pendingReports.Clear();
            }

            foreach (KeyValuePair<string, double> report in queued)
            {
                await ReportAchievementAsync(report.Key, report.Value);
            }
        }
    }
}
#endif
