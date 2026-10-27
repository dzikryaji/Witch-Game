using System;
using System.Threading;
using System.Threading.Tasks;
using Witch.Application.Ports;

namespace Witch.Application.Flow
{
    public sealed class GameSettingsService : IGameSettingsService
    {
        private readonly ISettingsRepository repository;

        public GameSettingsService(ISettingsRepository repository)
        {
            this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public GameSettingsState Current { get; private set; } = GameSettingsState.Default;

        public bool IsLoaded { get; private set; }

        public async Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default)
        {
            SettingsLoadResult result = await repository.LoadSettingsAsync(cancellationToken);
            Current = result.Succeeded ? result.Settings : GameSettingsState.Default;
            IsLoaded = true;
            return result;
        }

        public async Task<SaveStateResult> SaveAsync(
            GameSettingsState settings,
            CancellationToken cancellationToken = default)
        {
            if (settings == null)
            {
                return SaveStateResult.Failure(SaveStateError.InvalidState, "Settings are required.");
            }

            SaveStateResult result = await repository.SaveSettingsAsync(settings, cancellationToken);
            if (result.Succeeded)
            {
                Current = settings;
                IsLoaded = true;
            }

            return result;
        }
    }
}
