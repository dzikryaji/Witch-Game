using System.Threading;
using System.Threading.Tasks;
using Witch.Domain.Progression;

namespace Witch.Application.Ports
{
    public interface IActiveAttemptMetadataRepository
    {
        Task<SaveStateResult> SaveActiveAttemptMetadataAsync(
            ActiveAttemptMetadata metadata,
            CancellationToken cancellationToken = default);

        Task<ActiveAttemptMetadataLoadResult> LoadLatestActiveAttemptMetadataAsync(
            CancellationToken cancellationToken = default);

        Task<ActiveAttemptMetadataLoadResult> LoadActiveAttemptMetadataAsync(
            string runId,
            CancellationToken cancellationToken = default);
    }
}
