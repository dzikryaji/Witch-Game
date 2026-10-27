using System.Threading;
using System.Threading.Tasks;
using Witch.Domain.Progression;

namespace Witch.Application.Ports
{
    public interface IDayStartSnapshotRepository
    {
        Task<SaveStateResult> SaveDayStartSnapshotAsync(
            DayStartSnapshot snapshot,
            CancellationToken cancellationToken = default);

        Task<DayStartSnapshotLoadResult> LoadLatestDayStartSnapshotAsync(
            CancellationToken cancellationToken = default);

        Task<DayStartSnapshotLoadResult> LoadDayStartSnapshotAsync(
            string runId,
            CancellationToken cancellationToken = default);
    }
}
