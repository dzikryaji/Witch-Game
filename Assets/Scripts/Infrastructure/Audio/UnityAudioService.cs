using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Witch.Application.Ports;

namespace Witch.Infrastructure.Audio
{
    /// <summary>Four-channel audio bus backed by authored project clips.</summary>
    public sealed class UnityAudioService : MonoBehaviour, IGameAudioService
    {
        [Serializable]
        private sealed class Cue
        {
            public string cueId;
            public AudioClip clip;
        }

        [SerializeField] private Cue[] cues = Array.Empty<Cue>();
        [Header("Authored Channels")]
        [SerializeField] private AudioSource music;
        [SerializeField] private AudioSource sfx;
        [SerializeField] private AudioSource ambience;
        [SerializeField] private AudioSource ui;
        private readonly Dictionary<string, AudioClip> clipsById = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> lastPlayedAt = new Dictionary<string, float>(StringComparer.Ordinal);
        private string currentMusic;
        private GameSettingsState settings = GameSettingsState.Default;

        public string CurrentMusicCue => currentMusic;
        public bool HasAuthoredChannels => music != null && sfx != null && ambience != null && ui != null;
        public int AuthoredCueCount => clipsById.Count;

        public bool TryValidateRequiredCues(out string error)
        {
            string[] missing = GameAudioCueIds.Required.Where(id => !TryGetClip(id, out _)).ToArray();
            error = missing.Length == 0
                ? string.Empty
                : "Bootstrap audio is missing authored cues: " + string.Join(", ", missing);
            return missing.Length == 0;
        }

        private void Awake()
        {
            if (!HasAuthoredChannels)
            {
                Debug.LogError("UnityAudioService requires four authored AudioSource channels.", this);
                enabled = false;
                return;
            }

            foreach (Cue cue in cues)
            {
                if (cue != null && !string.IsNullOrWhiteSpace(cue.cueId) && cue.clip != null)
                {
                    clipsById[cue.cueId] = cue.clip;
                }
            }
            ApplySettings(settings);
        }

        public ServiceResult Play(string cueId)
        {
            if (string.IsNullOrWhiteSpace(cueId))
            {
                return ServiceResult.Failure(ServiceError.InvalidRequest, "Audio cue ID is required.");
            }

            if (lastPlayedAt.TryGetValue(cueId, out float last) && Time.unscaledTime - last < 0.06f)
            {
                return ServiceResult.Success();
            }

            lastPlayedAt[cueId] = Time.unscaledTime;
            AudioSource channel = cueId.StartsWith("SFX_UI_", StringComparison.Ordinal) ? ui
                : cueId == "SFX_SHOP_AMBIENCE" ? ambience
                : sfx;
            if (!TryGetClip(cueId, out AudioClip clip))
            {
                return MissingCue(cueId);
            }

            channel.PlayOneShot(clip);
            return ServiceResult.Success();
        }

        public ServiceResult PlayMusic(string cueId)
        {
            if (string.IsNullOrWhiteSpace(cueId))
            {
                return ServiceResult.Failure(ServiceError.InvalidRequest, "Music cue ID is required.");
            }

            if (string.Equals(currentMusic, cueId, StringComparison.Ordinal) && music.isPlaying)
            {
                return ServiceResult.Success();
            }

            if (!TryGetClip(cueId, out AudioClip clip))
            {
                return MissingCue(cueId);
            }

            currentMusic = cueId;
            music.clip = clip;
            music.loop = cueId != GameAudioCueIds.GameOverMusic && cueId != GameAudioCueIds.VictoryMusic;
            music.Play();
            return ServiceResult.Success();
        }

        public ServiceResult PlayAmbience(string cueId)
        {
            if (string.IsNullOrWhiteSpace(cueId))
            {
                return ServiceResult.Failure(ServiceError.InvalidRequest, "Ambience cue ID is required.");
            }

            if (!TryGetClip(cueId, out AudioClip clip))
            {
                return MissingCue(cueId);
            }

            if (ambience.clip == clip && ambience.isPlaying)
            {
                return ServiceResult.Success();
            }

            ambience.clip = clip;
            ambience.loop = true;
            ambience.Play();
            return ServiceResult.Success();
        }

        public void StopMusic()
        {
            music.Stop();
            currentMusic = null;
        }

        public void StopAmbience()
        {
            ambience.Stop();
            ambience.clip = null;
        }

        public void ApplySettings(GameSettingsState value)
        {
            settings = value ?? GameSettingsState.Default;
            if (music == null) return;
            music.volume = settings.MasterVolume * settings.MusicVolume * 0.35f;
            sfx.volume = settings.MasterVolume * settings.SoundVolume * 0.75f;
            ambience.volume = settings.MasterVolume * settings.SoundVolume * 0.25f;
            ui.volume = settings.MasterVolume * settings.SoundVolume * 0.55f;
        }

        private bool TryGetClip(string cueId, out AudioClip clip)
        {
            return clipsById.TryGetValue(cueId, out clip) && clip != null;
        }

        private ServiceResult MissingCue(string cueId)
        {
            string message = "Authored audio cue is missing: " + cueId;
            Debug.LogError(message, this);
            return ServiceResult.Failure(ServiceError.OperationFailed, message);
        }
    }
}
