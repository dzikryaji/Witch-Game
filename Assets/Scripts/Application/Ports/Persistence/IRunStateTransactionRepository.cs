using System.Threading;
using System.Threading.Tasks;
using Witch.Domain.Progression;

namespace Witch.Application.Ports
{
    public interface IRunStateTransactionRepository
    {
        Task<SaveStateResult> SaveRunStateAsync(
            DayStartSnapshot snapshot,
            ActiveAttemptMetadata metadata,
            CancellationToken cancellationToken = default);
    }
}
