using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Witch.Application.Ports;

namespace Witch.Infrastructure.Persistence
{
    public sealed class JsonSettingsRepository : ISettingsRepository
    {
        public const string SettingsFileName = "settings.json";

        private readonly string primaryPath;
        private readonly string previousPath;
        private readonly IAtomicFileStore fileStore;

        public JsonSettingsRepository()
            : this(Path.Combine(UnityEngine.Application.persistentDataPath, "Settings"))
        {
        }

        public JsonSettingsRepository(string directory, IAtomicFileStore fileStore = null)
        {
            primaryPath = Path.Combine(directory, SettingsFileName);
            previousPath = primaryPath + ".previous";
            this.fileStore = fileStore ?? new SystemAtomicFileStore();
        }

        public async Task<SaveStateResult> SaveSettingsAsync(
            GameSettingsState settings,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (settings == null)
            {
                return SaveStateResult.Failure(SaveStateError.InvalidState, "Settings are required.");
            }

            return await Task.Run(() =>
            {
                try
                {
                    string json = JsonUtility.ToJson(new SettingsDto
                    {
                        schemaVersion = 1,
                        masterVolume = settings.MasterVolume,
                        musicVolume = settings.MusicVolume,
                        soundVolume = settings.SoundVolume,
                        reducedMismatchDelay = settings.ReducedMismatchDelay
                    }, true);
                    fileStore.WriteAtomically(primaryPath, previousPath, json);
                    return SaveStateResult.Success();
                }
                catch (Exception exception)
                {
                    return SaveStateResult.Failure(SaveStateError.WriteFailed, exception.Message);
                }
            }, cancellationToken);
        }

        public async Task<SettingsLoadResult> LoadSettingsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await Task.Run(ReadWithRecovery, cancellationToken);
        }

        private SettingsLoadResult ReadWithRecovery()
        {
            SettingsLoadResult primary = Read(primaryPath);
            if (primary.Succeeded)
            {
                return primary;
            }

            SettingsLoadResult previous = Read(previousPath);
            return previous.Succeeded ? previous : primary;
        }

        private SettingsLoadResult Read(string path)
        {
            if (!fileStore.Exists(path))
            {
                return SettingsLoadResult.Failure(LoadStateError.NotFound, "Settings do not exist.");
            }

            try
            {
                SettingsDto dto = JsonUtility.FromJson<SettingsDto>(fileStore.ReadAllText(path));
                float masterVolume = dto.schemaVersion > 0 ? dto.masterVolume : 1f;
                bool reducedDelay = dto.schemaVersion > 0 ? dto.reducedMismatchDelay : dto.reducedMotion;
                return SettingsLoadResult.Success(
                    new GameSettingsState(masterVolume, dto.musicVolume, dto.soundVolume, reducedDelay));
            }
            catch (Exception exception)
            {
                return SettingsLoadResult.Failure(LoadStateError.CorruptData, exception.Message);
            }
        }

        [Serializable]
        private sealed class SettingsDto
        {
            public int schemaVersion;
            public float masterVolume;
            public float musicVolume;
            public float soundVolume;
            public bool reducedMismatchDelay;
            public bool reducedMotion;
        }
    }
}
