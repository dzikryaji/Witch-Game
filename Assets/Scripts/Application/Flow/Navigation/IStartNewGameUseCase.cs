using System.Threading;
using System.Threading.Tasks;

namespace Witch.Application.Flow
{
    public interface IStartNewGameUseCase
    {
        Task<StartNewGameResult> ExecuteAsync(
            StartNewGameCommand command,
            CancellationToken cancellationToken = default);
    }
}
