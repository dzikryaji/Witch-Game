using System;
using UnityEngine;
using UnityEngine.UI;
using Witch.Application.Ports;

namespace Witch.Presentation.Settings
{
    public sealed class SettingsPanelPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Slider soundSlider;     // SoundVolume (sound effects)
        [SerializeField] private Slider musicSlider;     // MusicVolume
        [SerializeField] private Button saveButton;      // the back arrow / save button
        [SerializeField] private Text errorText;         // optional
        [SerializeField] private GameObject overlay;     // the dim background behind the settings panel

        private IGameSettingsService settingsService;
        private IGameAudioService audioService;
        private bool bound;
        private float nextSoundSample;

        public bool IsOpen => panel != null && panel.activeSelf;
        public event Action Closed;
        public event Action Opened;

        public void Initialize(IGameSettingsService service, IGameAudioService audio)
        {
            settingsService = service ?? throw new ArgumentNullException(nameof(service));
            audioService = audio;

            if (!bound)
            {
                soundSlider.onValueChanged.AddListener(_ => Preview(true));
                musicSlider.onValueChanged.AddListener(_ => Preview(false));
                saveButton.onClick.AddListener(OnSaveClicked);
                bound = true;
            }

            panel.SetActive(false);
            ShowError(null);
        }

        public void Open()
        {
            if (settingsService == null) return;

            GameSettingsState saved = settingsService.Current;
            soundSlider.SetValueWithoutNotify(saved.SoundVolume);
            musicSlider.SetValueWithoutNotify(saved.MusicVolume);

            SetOpen(true);
            Opened?.Invoke();
        }

        // Closes without saving and undoes the live preview.
        public void Cancel()
        {
            if (!IsOpen) return;

            audioService?.ApplySettings(settingsService.Current);
            SetOpen(false);
            Closed?.Invoke();
        }

        private GameSettingsState CurrentSelection()
        {
            GameSettingsState saved = settingsService.Current;
            return new GameSettingsState(
                musicSlider.value,
                soundSlider.value,
                saved.ReducedMismatchDelay);
        }

        private void Preview(bool playSample)
        {
            if (settingsService == null || !IsOpen) return;
            audioService?.ApplySettings(CurrentSelection());

            // Let the player hear the sound-effect level while dragging, but not on every tiny change.
            if (playSample && audioService != null && Time.unscaledTime >= nextSoundSample)
            {
                audioService.Play(GameAudioCueIds.UiClick);
                nextSoundSample = Time.unscaledTime + 0.25f;
            }
        }

        private async void OnSaveClicked()
        {
            try
            {
                GameSettingsState value = CurrentSelection();
                SaveStateResult result = await settingsService.SaveAsync(value);
                if (!result.Succeeded)
                {
                    ShowError(result.ErrorMessage);
                    return;
                }

                audioService?.ApplySettings(value);
                SetOpen(false);
                Closed?.Invoke();
            }
            catch (Exception exception)
            {
                ShowError(exception.Message);
                Debug.LogException(exception, this);
            }
        }

        private void ShowError(string message)
        {
            if (errorText == null) return;
            errorText.text = message ?? string.Empty;
            errorText.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
        }

        private void SetOpen(bool open)
        {
            if (overlay != null) overlay.SetActive(open);
            panel.SetActive(open);
        }
    }
}