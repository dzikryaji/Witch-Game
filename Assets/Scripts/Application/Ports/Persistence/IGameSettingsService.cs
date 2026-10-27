using System.Threading;
using System.Threading.Tasks;

namespace Witch.Application.Ports
{
    public interface IGameSettingsService
    {
        GameSettingsState Current { get; }

        bool IsLoaded { get; }

        Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default);

        Task<SaveStateResult> SaveAsync(
            GameSettingsState settings,
            CancellationToken cancellationToken = default);
    }
}
