using System.Threading;
using System.Threading.Tasks;

namespace Witch.Application.Ports
{
    public interface ISettingsRepository
    {
        Task<SaveStateResult> SaveSettingsAsync(
            GameSettingsState settings,
            CancellationToken cancellationToken = default);

        Task<SettingsLoadResult> LoadSettingsAsync(CancellationToken cancellationToken = default);
    }
}
