using System.Threading;
using System.Threading.Tasks;
using Witch.Domain.Progression;

namespace Witch.Application.Ports
{
    public interface ISaveRepository
    {
        Task<SaveStateResult> SaveAsync(
            RunState snapshot,
            CancellationToken cancellationToken = default);

        Task<LoadStateResult> LoadLatestAsync(CancellationToken cancellationToken = default);

        Task<LoadStateResult> LoadAsync(
            string runId,
            CancellationToken cancellationToken = default);
    }
}
