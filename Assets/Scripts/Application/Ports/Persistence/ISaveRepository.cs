using System.Threading;
using System.Threading.Tasks;

namespace Witch.Application.Ports
{
    public interface ISaveRepository :
        IDayStartSnapshotRepository,
        IActiveAttemptMetadataRepository,
        IRunStateTransactionRepository
    {
        Task<SaveStateResult> SaveAsync(
            GameSaveState savedGame,
            CancellationToken cancellationToken = default);

        Task<LoadStateResult> LoadLatestAsync(CancellationToken cancellationToken = default);

        Task<LoadStateResult> LoadAsync(
            string runId,
            CancellationToken cancellationToken = default);
    }
}
