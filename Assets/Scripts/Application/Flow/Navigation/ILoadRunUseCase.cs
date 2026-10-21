using System.Threading;
using System.Threading.Tasks;

namespace Witch.Application.Flow
{
    public interface ILoadRunUseCase
    {
        Task<LoadRunResult> ExecuteAsync(
            LoadRunCommand command,
            CancellationToken cancellationToken = default);
    }
}
