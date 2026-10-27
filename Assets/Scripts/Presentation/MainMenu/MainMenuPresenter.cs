using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Witch.Application.Flow;
using Witch.Application.Ports;

namespace Witch.Presentation.MainMenu
{
    public sealed class MainMenuPresenter : MonoBehaviour
    {
        [Header("Authored View")]
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider soundSlider;
        [SerializeField] private Toggle reducedDelayToggle;
        [SerializeField] private Button applySettingsButton;
        [SerializeField] private Text errorText;

        private IStartNewGameUseCase startNewGame;
        private ILoadRunUseCase loadRun;
        private IGameSettingsService settingsService;
        private IApplicationLifecycle applicationLifecycle;
        private IGameAudioService audioService;
        private bool viewBound;

        public bool IsInitialized { get; private set; }
        public bool IsBusy { get; private set; }
        public string LastErrorMessage { get; private set; }
        public GameSettingsState LoadedSettings { get; private set; }
        public bool HasAuthoredView => newGameButton != null
            && continueButton != null
            && settingsButton != null
            && quitButton != null
            && settingsPanel != null
            && masterSlider != null
            && musicSlider != null
            && soundSlider != null
            && reducedDelayToggle != null
            && applySettingsButton != null
            && errorText != null;

        public void Initialize(
            IStartNewGameUseCase startNewGameUseCase,
            ILoadRunUseCase loadRunUseCase,
            ISettingsRepository settings,
            IApplicationLifecycle lifecycle)
        {
            Initialize(
                startNewGameUseCase,
                loadRunUseCase,
                new GameSettingsService(settings),
                lifecycle,
                null);
        }

        public void Initialize(
            IStartNewGameUseCase startNewGameUseCase,
            ILoadRunUseCase loadRunUseCase,
            ISettingsRepository settings,
            IApplicationLifecycle lifecycle,
            IGameAudioService audio)
        {
            Initialize(
                startNewGameUseCase,
                loadRunUseCase,
                new GameSettingsService(settings),
                lifecycle,
                audio);
        }

        public void Initialize(
            IStartNewGameUseCase startNewGameUseCase,
            ILoadRunUseCase loadRunUseCase,
            IGameSettingsService settings,
            IApplicationLifecycle lifecycle,
            IGameAudioService audio)
        {
            startNewGame = startNewGameUseCase ?? throw new ArgumentNullException(nameof(startNewGameUseCase));
            loadRun = loadRunUseCase ?? throw new ArgumentNullException(nameof(loadRunUseCase));
            settingsService = settings ?? throw new ArgumentNullException(nameof(settings));
            applicationLifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
            audioService = audio;
            RequireAndBindAuthoredView();
            IsInitialized = true;
            LastErrorMessage = null;
            RenderError();
            settingsPanel.SetActive(false);
            _ = LoadSettingsOrDefaultsAsync();
            audioService?.StopAmbience();
            audioService?.PlayMusic(GameAudioCueIds.MainMenuMusic);
            newGameButton.Select();
        }

        public async Task<bool> NewGameAsync()
        {
            EnsureInitialized();
            if (IsBusy)
            {
                return false;
            }

            SetBusy(true);
            LastErrorMessage = null;
            RenderError();
            try
            {
                StartNewGameResult result = await startNewGame.ExecuteAsync(
                    new StartNewGameCommand(Guid.NewGuid().ToString("N")));
                LastErrorMessage = result.Succeeded ? null : result.ErrorMessage;
                RenderError();
                return result.Succeeded;
            }
            finally
            {
                SetBusy(false);
            }
        }

        public async Task<bool> ContinueAsync()
        {
            EnsureInitialized();
            if (IsBusy)
            {
                return false;
            }

            SetBusy(true);
            LastErrorMessage = null;
            RenderError();
            try
            {
                LoadRunResult result = await loadRun.ExecuteAsync(LoadRunCommand.Latest());
                LastErrorMessage = result.Succeeded ? null : result.ErrorMessage;
                RenderError();
                return result.Succeeded;
            }
            finally
            {
                SetBusy(false);
            }
        }

        public async Task<bool> LoadSettingsAsync()
        {
            EnsureInitialized();
            SettingsLoadResult result = await settingsService.LoadAsync();
            LoadedSettings = result.Succeeded ? result.Settings : null;
            LastErrorMessage = result.Succeeded ? null : result.ErrorMessage;
            RenderError();
            return result.Succeeded;
        }

        public async Task<bool> SaveSettingsAsync(
            float masterVolume,
            float musicVolume,
            float soundVolume,
            bool reducedMismatchDelay)
        {
            EnsureInitialized();
            var value = new GameSettingsState(masterVolume, musicVolume, soundVolume, reducedMismatchDelay);
            SaveStateResult result = await settingsService.SaveAsync(value);
            if (result.Succeeded)
            {
                LoadedSettings = value;
                audioService?.ApplySettings(value);
                LastErrorMessage = null;
                RenderError();
                return true;
            }

            LastErrorMessage = result.ErrorMessage;
            RenderError();
            return false;
        }

        public void ToggleSettings()
        {
            if (settingsPanel == null)
            {
                return;
            }

            settingsPanel.SetActive(!settingsPanel.activeSelf);
            audioService?.Play(settingsPanel.activeSelf ? GameAudioCueIds.UiConfirm : GameAudioCueIds.UiBack);
        }

        public ServiceResult Quit()
        {
            EnsureInitialized();
            return applicationLifecycle.RequestQuit();
        }

        private void RequireAndBindAuthoredView()
        {
            if (!HasAuthoredView)
            {
                throw new InvalidOperationException("MainMenu scene is missing authored UI references.");
            }

            if (viewBound)
            {
                return;
            }

            newGameButton.onClick.AddListener(OnNewGameClicked);
            continueButton.onClick.AddListener(OnContinueClicked);
            settingsButton.onClick.AddListener(ToggleSettings);
            quitButton.onClick.AddListener(OnQuitClicked);
            applySettingsButton.onClick.AddListener(OnApplySettingsClicked);
            viewBound = true;
        }

        private void OnNewGameClicked() => RunUiAction(NewGameAsync);
        private void OnContinueClicked() => RunUiAction(ContinueAsync);
        private void OnQuitClicked() => Quit();

        private void OnApplySettingsClicked() => RunUiAction(ApplySettingsAsync);

        private async Task<bool> ApplySettingsAsync()
        {
            bool saved = await SaveSettingsAsync(
                masterSlider.value,
                musicSlider.value,
                soundSlider.value,
                reducedDelayToggle.isOn);
            if (saved)
            {
                ToggleSettings();
            }
            return saved;
        }

        private async void RunUiAction(Func<Task<bool>> action)
        {
            try
            {
                await action();
            }
            catch (Exception exception)
            {
                LastErrorMessage = exception.Message;
                RenderError();
                Debug.LogException(exception, this);
                SetBusy(false);
            }
        }

        private void EnsureInitialized()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("Main menu presenter has not been initialized by Bootstrap.");
            }
        }

        private async Task LoadSettingsOrDefaultsAsync()
        {
            if (!settingsService.IsLoaded)
            {
                await settingsService.LoadAsync();
            }

            LoadedSettings = settingsService.Current;
            audioService?.ApplySettings(LoadedSettings);
            masterSlider.SetValueWithoutNotify(LoadedSettings.MasterVolume);
            musicSlider.SetValueWithoutNotify(LoadedSettings.MusicVolume);
            soundSlider.SetValueWithoutNotify(LoadedSettings.SoundVolume);
            reducedDelayToggle.SetIsOnWithoutNotify(LoadedSettings.ReducedMismatchDelay);
        }

        private void SetBusy(bool busy)
        {
            IsBusy = busy;
            if (newGameButton != null) newGameButton.interactable = !busy;
            if (continueButton != null) continueButton.interactable = !busy;
            if (applySettingsButton != null) applySettingsButton.interactable = !busy;
        }

        private void RenderError()
        {
            if (errorText == null) return;
            errorText.text = LastErrorMessage ?? string.Empty;
            errorText.gameObject.SetActive(!string.IsNullOrWhiteSpace(LastErrorMessage));
        }
    }
}
