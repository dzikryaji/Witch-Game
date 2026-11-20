using System;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;
using Witch.Application.Flow;
using Witch.Application.Ports;
using Witch.Domain.Progression;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Witch.Presentation;
using Witch.Presentation.Settings;

namespace Witch.Presentation.MatchCard
{
    public sealed class MatchCardShellPresenter : MonoBehaviour
    {
        [Header("Authored View")]
        [SerializeField] private MatchCardView[] cardViews;
        [SerializeField] private MatchCardBoardLayout boardLayout;

        [Header("Timer")]
        [SerializeField] private Text timerText;
        [SerializeField] private MatchCardTimeUpPopupView timeUpPopup;
        [SerializeField, Min(5f)] private float timeLimitSeconds = 60f;
        [SerializeField] private MatchCardHeaderView header;
        [SerializeField] private MatchCardFailCutsceneView failCutscene;

        [Header("Pause")]
        [SerializeField] private Button pauseButton;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject pauseOverlay;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private SettingsPanelPresenter settingsPanelPresenter;
        [SerializeField] private ConfirmPopupView quitConfirm;

        [Header("Win Popup")]
        [SerializeField] private GameObject winPopup;
        [SerializeField] private GameObject popUpOverlay;
        [SerializeField] private Button winContinueButton;

        private float remainingSeconds;
        private bool timerRunning;
        private int boardGeneration;
        public bool IsTimedOut { get; private set; }
        private ISceneLoader sceneLoader;
        private bool returningToMenu;

        private IGameSessionStore sessionStore;
        private CompleteMinigameUseCase completeMinigameUseCase;
        private AbandonMinigameUseCase abandonMinigameUseCase;
        private bool[] revealed;
        private bool[] matched;
        private int firstIndex = -1;
        private IGameAudioService audioService;
        private GameSettingsState settings = GameSettingsState.Default;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly List<MatchCardView> cardSlots = new List<MatchCardView>();
        private IReadOnlyList<string> symbols;
        private const int CompletionHoldMs = 500;
        private int FlipDelayMs(int index) => Mathf.CeilToInt(cardSlots[index].FlipDuration * 1000f);
        private bool completing;
        private bool gameOverMusicStarted;
        private bool gameOverAmbienceStarted;

        public bool IsPaused { get; private set; }
        public bool IsInitialized => sessionStore != null;
        public GameSession ActiveSession => sessionStore?.ActiveSession;
        public MatchCardBoard Board { get; private set; }
        public bool IsResolvingMismatch { get; private set; }
        public bool IsComplete { get; private set; }
        public bool HasAuthoredView
        {
            get
            {
                if (cardViews == null || cardViews.Length == 0)
                {
                    return false;
                }

                foreach (MatchCardView view in cardViews)
                {
                    if (view == null || !view.IsAuthored)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public void Initialize(IGameSessionStore store)
        {
            sessionStore = store ?? throw new ArgumentNullException(nameof(store));
        }

        public void Initialize(IGameSessionStore store, CompleteMinigameUseCase completeUseCase)
        {
            Initialize(store, completeUseCase, null, GameSettingsState.Default);
        }

        public void Initialize(
            IGameSessionStore store,
            CompleteMinigameUseCase completeUseCase,
            IGameAudioService audio,
            GameSettingsState currentSettings)
        {
            Initialize(
                store,
                completeUseCase,
                audio,
                currentSettings,
                null,
                MatchCardRules.StartingPairCount,
                MatchCardRules.MaximumPairCount);
        }

        public void Initialize(
            IGameSessionStore store,
            CompleteMinigameUseCase completeUseCase,
            IGameAudioService audio,
            GameSettingsState currentSettings,
            IReadOnlyList<string> symbolKeys,
            int startingPairCount,
            int maximumPairCount)
        {
            Initialize(store);
            completeMinigameUseCase = completeUseCase ?? throw new ArgumentNullException(nameof(completeUseCase));
            audioService = audio;
            settings = currentSettings ?? GameSettingsState.Default;
            audioService?.ApplySettings(settings);
            if (ActiveSession == null || !ActiveSession.AttemptMetadata.PendingMinigame)
            {
                return;
            }

            RequireAuthoredView();
            Board = MatchCardBoard.Create(
                ActiveSession.AttemptMetadata.MinigameBoardSeed,
                ActiveSession.AttemptMetadata.MinigameWinCount,
                startingPairCount,
                maximumPairCount);
            if (symbolKeys != null && symbolKeys.Count < Board.PairCount)
            {
                throw new InvalidOperationException("Match Card content has fewer symbols than board pairs.");
            }
            symbols = ShuffleSymbols(symbolKeys, Board.Seed);
            revealed = new bool[Board.Cards.Count];
            matched = new bool[Board.Cards.Count];
            EnsureCardViews(Board.Cards.Count);
            BindCards();
            BindPause();
            header?.Render(ActiveSession);
            if (timeUpPopup != null)
            {
                timeUpPopup.Hide();
                timeUpPopup.Bind(ReturnToMainMenu);
            }
            ResetTimer();
            if (boardLayout != null) boardLayout.Apply(Board.Cards.Count);
            RenderCards();
            audioService?.StopAmbience();
            audioService?.PlayMusic(GameAudioCueIds.MinigameMusic);
        }

        public async Task<bool> FlipCardAsync(int index)
        {
            if (Board == null || IsResolvingMismatch || index < 0 || index >= Board.Cards.Count || matched[index] || revealed[index] || IsTimedOut || IsComplete || IsPaused)
            {
                return false;
            }

            int generation = boardGeneration;

            revealed[index] = true;
            audioService?.Play(GameAudioCueIds.PageFlip);
            if (firstIndex < 0)
            {
                firstIndex = index;
                RenderCards();
                return true;
            }

            int previous = firstIndex;
            firstIndex = -1;
            IsResolvingMismatch = true;
            RenderCards();
            await Task.Delay(FlipDelayMs(index), lifetime.Token);
            if (generation != boardGeneration) return false;

            if (Board.Cards[previous] == Board.Cards[index])
            {
                matched[previous] = true;
                matched[index] = true;
                IsResolvingMismatch = false;
                audioService?.Play(GameAudioCueIds.CardMatch);
                if (AllMatched())
                {
                    IsComplete = true;
                    timerRunning = false;
                    header?.DrainSuspicion(0.5f);
                    RenderCards();
                    audioService?.Play(GameAudioCueIds.MinigameComplete);
                    audioService?.StopMusic();
                    await Task.Delay(CompletionHoldMs, lifetime.Token);
                    if (generation != boardGeneration) return false;
                    ShowWinPopup();
                    return true;
                }

                RenderCards();
                return true;
            }

            audioService?.Play(GameAudioCueIds.CardMismatch);
            await Task.Delay(settings.ReducedMismatchDelay ? 100 : 450, lifetime.Token);
            revealed[previous] = false;
            revealed[index] = false;
            IsResolvingMismatch = false;
            RenderCards();
            return true;
        }

        public bool IsCardVisible(int index) => revealed[index] || matched[index];
        public bool IsCardMatched(int index) => matched[index];

        private void RequireAuthoredView()
        {
            if (!HasAuthoredView)
            {
                throw new InvalidOperationException("MatchCardMinigame scene is missing authored card views.");
            }
        }

        private void BindCards()
        {
            for (int index = 0; index < cardSlots.Count; index++)
            {
                MatchCardView view = cardSlots[index];
                bool active = index < Board.Cards.Count;
                view.gameObject.SetActive(active);
                view.Unbind();
                if (!active)
                {
                    continue;
                }

                int captured = index;
                view.Bind(() => OnCardClicked(captured));
            }
        }

        private bool AllMatched()
        {
            foreach (bool value in matched)
            {
                if (!value)
                {
                    return false;
                }
            }

            return true;
        }

        private async void OnCardClicked(int index)
        {
            try
            {
                await FlipCardAsync(index);
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private void OnDestroy()
        {
            StopGameOverAudio();
            lifetime.Cancel();
            lifetime.Dispose();
            foreach (MatchCardView view in cardSlots)
            {
                view?.Unbind();
            }
        }

        private void RenderCards()
        {
            for (int index = 0; index < Board.Cards.Count; index++)
            {
                bool visible = revealed[index];
                bool isMatched = matched[index];
                cardSlots[index].Render(
                    symbols == null
                        ? "SIGIL " + (Board.Cards[index] + 1)
                        : symbols[Board.Cards[index]],
                    visible,
                    isMatched,
                    !isMatched && !IsResolvingMismatch && !IsComplete && !IsTimedOut && !IsPaused);
            }
        }

        private void EnsureCardViews(int count)
        {
            cardSlots.Clear();
            foreach (MatchCardView view in cardViews)
            {
                if (view != null) cardSlots.Add(view);
            }

            if (cardSlots.Count < count)
            {
                throw new InvalidOperationException(
                    $"MatchCardMinigame requires {count} authored card views but only {cardSlots.Count} are assigned.");
            }
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) HandleEscape();

            if (!timerRunning || IsPaused) return;

            remainingSeconds -= Time.unscaledDeltaTime;
            if (remainingSeconds <= 0f)
            {
                remainingSeconds = 0f;
                RenderTimer();
                OnTimeUp();
                return;
            }
            RenderTimer();
        }

        private void RenderTimer()
        {
            if (timerText == null) return;
            int seconds = Mathf.CeilToInt(remainingSeconds);
            timerText.text = (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
            timerText.fontStyle = seconds <= 10 ? FontStyle.Bold : FontStyle.Normal;
        }

        private void ResetTimer()
        {
            remainingSeconds = timeLimitSeconds;
            timerRunning = true;
            IsTimedOut = false;
            RenderTimer();
        }

        private async void OnTimeUp()
        {
            if (IsTimedOut || IsComplete || gameOverMusicStarted) return;

            timerRunning = false;
            IsTimedOut = true;
            await SaveFailedMinigameAsync();
            boardGeneration++;
            IsResolvingMismatch = false;
            firstIndex = -1;
            RenderCards();
            audioService?.Play(GameAudioCueIds.CardMismatch);
            audioService?.StopMusic();
            PlayGameOverMusicOnce();

            try
            {
                if (failCutscene != null && failCutscene.IsAuthored)
                {
                    await failCutscene.PlayAsync(lifetime.Token, TryStartGameOverAmbienceOnce);
                }
                else
                {
                    TryStartGameOverAmbienceOnce();
                }

                timeUpPopup?.Show();
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                TryStartGameOverAmbienceOnce();
                timeUpPopup?.Show();
            }
        }

        private async Task SaveFailedMinigameAsync()
        {
            if (abandonMinigameUseCase == null || ActiveSession == null)
            {
                Debug.LogWarning("Cannot abandon minigame: use case or session is missing.", this);
                return;
            }

            try
            {
                MinigameTransactionResult result =
                    await abandonMinigameUseCase.ExecuteAsync(
                        ActiveSession,
                        lifetime.Token);

                if (!result.Succeeded)
                {
                    Debug.LogWarning(
                        $"Failed to save minigame abandonment: {result.ErrorMessage}",
                        this);
                }
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private void PlayGameOverMusicOnce()
        {
            if (gameOverMusicStarted || audioService == null) return;
            gameOverMusicStarted = true;
            audioService.PlayMusic(GameAudioCueIds.GameOverMusic);
        }

        private void TryStartGameOverAmbienceOnce()
        {
            if (gameOverAmbienceStarted || audioService == null) return;
            if (lifetime.IsCancellationRequested) return;
            if (!IsTimedOut || IsComplete) return;

            gameOverAmbienceStarted = true;
            audioService.PlayAmbience(GameAudioCueIds.StakeAmbience);
        }

        private void StopGameOverAudio()
        {
            if (audioService == null)
            {
                gameOverMusicStarted = false;
                gameOverAmbienceStarted = false;
                return;
            }

            if (gameOverMusicStarted)
            {
                audioService.StopMusic();
                gameOverMusicStarted = false;
            }

            if (gameOverAmbienceStarted)
            {
                audioService.StopAmbience();
                gameOverAmbienceStarted = false;
            }
        }

        public void ConfigureNavigation(ISceneLoader loader)
        {
            sceneLoader = loader ?? throw new ArgumentNullException(nameof(loader));
        }

        public void ConfigureFailure(AbandonMinigameUseCase abandonUseCase)
        {
            abandonMinigameUseCase = abandonUseCase
                ?? throw new ArgumentNullException(nameof(abandonUseCase));
        }

        private async void ReturnToMainMenu()
        {
            if (returningToMenu) return;
            if (sceneLoader == null)
            {
                Debug.LogWarning("MatchCardShellPresenter has no scene loader. Bootstrap must call ConfigureNavigation.", this);
                return;
            }

            returningToMenu = true;
            timeUpPopup?.SetInteractable(false);
            StopGameOverAudio();
            audioService?.Play(GameAudioCueIds.UiClick);

            try
            {
                SceneLoadResult result = await sceneLoader.LoadAsync(SceneId.MainMenu, lifetime.Token);
                if (!result.Succeeded)
                {
                    returningToMenu = false;
                    timeUpPopup?.Show("Could not open the menu: " + result.ErrorMessage);
                }
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                returningToMenu = false;
                timeUpPopup?.SetInteractable(true);
                Debug.LogException(exception, this);
            }
        }

        private void BindPause()
        {
            pausePanel?.SetActive(false);
            pauseOverlay?.SetActive(false);
            quitConfirm?.Hide();

            Bind(pauseButton, TogglePause);
            Bind(resumeButton, TogglePause);
            Bind(quitButton, AskToQuit);
            if (settingsButton != null && settingsPanelPresenter != null)
            {
                Bind(settingsButton, settingsPanelPresenter.Open);
                settingsPanelPresenter.Opened -= OnSettingsOpened;
                settingsPanelPresenter.Opened += OnSettingsOpened;
                settingsPanelPresenter.Closed -= OnSettingsClosed;
                settingsPanelPresenter.Closed += OnSettingsClosed;
            }

            winPopup?.SetActive(false);
            popUpOverlay?.SetActive(false);

            Bind(winContinueButton, OnWinContinueClicked);
        }

        private static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        public void TogglePause()
        {
            if (Board == null || IsComplete || IsTimedOut || IsResolvingMismatch || returningToMenu) return;
            if (quitConfirm != null && quitConfirm.IsVisible) return;
            if (settingsPanelPresenter != null && settingsPanelPresenter.IsOpen) return;

            IsPaused = !IsPaused;
            pausePanel?.SetActive(IsPaused);
            pauseOverlay?.SetActive(IsPaused);
            if (!IsPaused) SetPauseContentVisible(true);
            audioService?.Play(GameAudioCueIds.UiClick);
            RenderCards();
        }

        private void AskToQuit()
        {
            if (quitConfirm == null) return;

            quitConfirm.Show(
                "Quit",
                "Return to the main menu? Any progress you've made today won't be saved.",
                ReturnToMainMenu
            );
        }

        private void HandleEscape()
        {
            if (quitConfirm != null && quitConfirm.IsVisible) { quitConfirm.Hide(); return; }
            if (settingsPanelPresenter != null && settingsPanelPresenter.IsOpen) { settingsPanelPresenter.Cancel(); return; }
            TogglePause();
        }

        private void OnSettingsOpened() => SetPauseContentVisible(false);
        private void OnSettingsClosed() => SetPauseContentVisible(true);

        private void SetPauseContentVisible(bool visible)
        {
            if (pausePanel == null) return;
            CanvasGroup group = pausePanel.GetComponent<CanvasGroup>();
            if (group == null) group = pausePanel.AddComponent<CanvasGroup>();
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        private void ShowWinPopup()
        {
            popUpOverlay?.SetActive(true);
            winPopup?.SetActive(true);
        }

        private async void OnWinContinueClicked()
        {
            if (completing) return;
            completing = true;
            if (winContinueButton != null) winContinueButton.interactable = false;
            audioService?.Play(GameAudioCueIds.UiClick);

            try
            {
                MinigameTransactionResult result = await completeMinigameUseCase.ExecuteAsync(ActiveSession, lifetime.Token);
                if (!result.Succeeded)
                {
                    Debug.LogWarning("Could not finish the minigame: " + result.ErrorMessage, this);
                    completing = false;
                    if (winContinueButton != null) winContinueButton.interactable = true;
                }
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                completing = false;
                if (winContinueButton != null) winContinueButton.interactable = true;
                Debug.LogException(exception, this);
            }
        }

        private static IReadOnlyList<string> ShuffleSymbols(IReadOnlyList<string> keys, int seed)
        {
            if (keys == null) return null;

            var copy = new List<string>(keys);
            var random = new System.Random(seed ^ 0x5bd1e995);
            for (int i = copy.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                string temp = copy[i];
                copy[i] = copy[j];
                copy[j] = temp;
            }
            return copy;
        }
    }
}
