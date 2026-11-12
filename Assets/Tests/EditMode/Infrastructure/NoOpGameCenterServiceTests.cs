using System.Threading.Tasks;
using NUnit.Framework;
using Witch.Application.Ports;
using Witch.Infrastructure.GameCenter;

namespace Witch.Infrastructure.Tests
{
    /// <summary>
    /// No-op Game Center semantics from the Game Center Implementation Plan:
    /// Editor, unsupported platforms, and feature-disabled builds get a
    /// service that is simply unavailable, never authenticated, and never
    /// throws into gameplay.
    /// </summary>
    public sealed class NoOpGameCenterServiceTests
    {
        [Test]
        public void IsAuthenticated_IsAlwaysFalse()
        {
            Assert.That(new NoOpGameCenterService().IsAuthenticated, Is.False);
        }

        [Test]
        public async Task InitializeAsync_ReturnsUnavailableFailure()
        {
            ServiceResult result = await new NoOpGameCenterService().InitializeAsync();

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(ServiceError.Unsupported));
            Assert.That(result.ErrorMessage, Is.EqualTo(NoOpGameCenterService.UnavailableMessage));
        }

        [Test]
        public async Task ReportAchievementAsync_ReturnsUnavailableFailure()
        {
            ServiceResult result = await new NoOpGameCenterService()
                .ReportAchievementAsync(GameCenterAchievementIds.FirstShift, 100d);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(ServiceError.Unsupported));
            Assert.That(result.ErrorMessage, Is.EqualTo(NoOpGameCenterService.UnavailableMessage));
        }

        [Test]
        public async Task ShowDashboardAsync_ReturnsUnavailableFailure()
        {
            ServiceResult result = await new NoOpGameCenterService().ShowDashboardAsync();

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(ServiceError.Unsupported));
            Assert.That(result.ErrorMessage, Is.EqualTo(NoOpGameCenterService.UnavailableMessage));
        }

        [Test]
        public async Task ReportAchievementAsync_RejectsInvalidRequests()
        {
            var service = new NoOpGameCenterService();

            ServiceResult missingId = await service.ReportAchievementAsync(null, 100d);
            ServiceResult emptyId = await service.ReportAchievementAsync("  ", 100d);
            ServiceResult tooLow = await service.ReportAchievementAsync(GameCenterAchievementIds.FirstShift, -1d);
            ServiceResult tooHigh = await service.ReportAchievementAsync(GameCenterAchievementIds.FirstShift, 100.1d);
            ServiceResult notANumber = await service.ReportAchievementAsync(GameCenterAchievementIds.FirstShift, double.NaN);
            ServiceResult infinite = await service.ReportAchievementAsync(GameCenterAchievementIds.FirstShift, double.PositiveInfinity);

            Assert.That(missingId.Error, Is.EqualTo(ServiceError.InvalidRequest));
            Assert.That(emptyId.Error, Is.EqualTo(ServiceError.InvalidRequest));
            Assert.That(tooLow.Error, Is.EqualTo(ServiceError.InvalidRequest));
            Assert.That(tooHigh.Error, Is.EqualTo(ServiceError.InvalidRequest));
            Assert.That(notANumber.Error, Is.EqualTo(ServiceError.InvalidRequest));
            Assert.That(infinite.Error, Is.EqualTo(ServiceError.InvalidRequest));
        }
    }
}
