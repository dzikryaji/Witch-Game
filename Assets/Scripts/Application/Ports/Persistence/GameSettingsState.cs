using System;

namespace Witch.Application.Ports
{
    [Serializable]
    public sealed class GameSettingsState
    {
        public GameSettingsState(float musicVolume, float soundVolume, bool reducedMotion)
            : this(1f, musicVolume, soundVolume, reducedMotion)
        {
        }

        public GameSettingsState(float masterVolume, float musicVolume, float soundVolume, bool reducedMismatchDelay)
        {
            ValidateVolume(masterVolume, nameof(masterVolume));
            ValidateVolume(musicVolume, nameof(musicVolume));
            ValidateVolume(soundVolume, nameof(soundVolume));

            MasterVolume = masterVolume;
            MusicVolume = musicVolume;
            SoundVolume = soundVolume;
            ReducedMismatchDelay = reducedMismatchDelay;
        }

        public static GameSettingsState Default => new GameSettingsState(1f, 0.45f, 0.8f, false);

        public float MasterVolume { get; }
        public float MusicVolume { get; }
        public float SoundVolume { get; }
        public bool ReducedMismatchDelay { get; }

        // Kept as a compatibility alias for the initial prototype settings schema.
        public bool ReducedMotion => ReducedMismatchDelay;

        private static void ValidateVolume(float value, string parameterName)
        {
            if (value < 0f || value > 1f)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }
}
