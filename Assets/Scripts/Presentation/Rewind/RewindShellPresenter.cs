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

namespace Witch.Presentation.Rewind
{
    public sealed class RewindShellPresenter : MonoBehaviour
    {
        [Header("Authored View")]
        [SerializeField] private RewindCardView[] cardViews;
        [SerializeField] private RewindBoardLayout boardLayout;

        [Header("Timer")]
        [SerializeField] private Text timerText;
        [SerializeField] private RewindTimeUpPopupView timeUpPopup;
        [SerializeField, Min(5f)] private float timeLimitSeconds = 60f;
        [SerializeField] private RewindHeaderView header;

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
        private CompleteRewindUseCase completeRewind;
        private bool[] revealed;
        private bool[] matched;
        private int firstIndex = -1;
        private IGameAudioService audioService;
        private GameSettingsState settings = GameSettingsState.Default;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly List<RewindCardView> cardSlots = new List<RewindCardView>();
        private IReadOnlyList<string> symbols;
        private const int CompletionHoldMs = 500;
        private int FlipDelayMs(int index) => Mathf.CeilToInt(cardSlots[index].FlipDuration * 1000f);
        private bool completing;

        public bool IsPaused { get; private set; }
        public bool IsInitialized => sessionStore != null;
        public GameSession ActiveSession => sessionStore?.ActiveSession;
        public RewindBoard Board { get; private set; }
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

                foreach (RewindCardView view in cardViews)
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

        public void Initialize(IGameSessionStore store, CompleteRewindUseCase completeUseCase)
        {
            Initialize(store, completeUseCase, null, GameSettingsState.Default);
        }

        public void Initialize(
            IGameSessionStore store,
            CompleteRewindUseCase completeUseCase,
            IGameAudioService audio,
            GameSettingsState currentSettings)
        {
            Initialize(
                store,
                completeUseCase,
                audio,
                currentSettings,
                null,
                RewindRules.StartingPairCount,
                RewindRules.MaximumPairCount);
        }

        public void Initialize(
            IGameSessionStore store,
            CompleteRewindUseCase completeUseCase,
            IGameAudioService audio,
            GameSettingsState currentSettings,
            IReadOnlyList<string> symbolKeys,
            int startingPairCount,
            int maximumPairCount)
        {
            Initialize(store);
            completeRewind = completeUseCase ?? throw new ArgumentNullException(nameof(completeUseCase));
            audioService = audio;
            settings = currentSettings ?? GameSettingsState.Default;
            audioService?.ApplySettings(settings);
            if (ActiveSession == null || !ActiveSession.AttemptMetadata.PendingRewind)
            {
                return;
            }

            RequireAuthoredView();
            Board = RewindBoard.Create(
                ActiveSession.AttemptMetadata.MinigameBoardSeed,
                ActiveSession.AttemptMetadata.RewindCount,
                startingPairCount,
                maximumPairCount);
            if (symbolKeys != null && symbolKeys.Count < Board.PairCount)
            {
                throw new InvalidOperationException("Rewind content has fewer symbols than board pairs.");
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
            audioService?.PlayMusic(GameAudioCueIds.RewindMusic);
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
                    await Task.Delay(CompletionHoldMs, lifetime.Token);   // let the last match be seen
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
                throw new InvalidOperationException("RewindMinigame scene is missing authored card views.");
            }
        }

        private void BindCards()
        {
            for (int index = 0; index < cardSlots.Count; index++)
            {
                RewindCardView view = cardSlots[index];
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
            lifetime.Cancel();
            lifetime.Dispose();
            foreach (RewindCardView view in cardSlots)
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
            foreach (RewindCardView view in cardViews)
            {
                if (view != null) cardSlots.Add(view);
            }

            if (cardSlots.Count < count)
            {
                throw new InvalidOperationException(
                    $"RewindMinigame requires {count} authored card views but only {cardSlots.Count} are assigned.");
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

        private void OnTimeUp()
        {
            timerRunning = false;
            IsTimedOut = true;
            boardGeneration++;          
            IsResolvingMismatch = false;
            firstIndex = -1;
            RenderCards();
            audioService?.Play(GameAudioCueIds.CardMismatch);
            timeUpPopup?.Show();
        }

        public void ConfigureNavigation(ISceneLoader loader)
        {
            sceneLoader = loader ?? throw new ArgumentNullException(nameof(loader));
        }

        private async void ReturnToMainMenu()
        {
            if (returningToMenu) return;
            if (sceneLoader == null)
            {
                Debug.LogWarning("RewindShellPresenter has no scene loader. Bootstrap must call ConfigureNavigation.", this);
                return;
            }

            returningToMenu = true;
            timeUpPopup?.SetInteractable(false);
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
                // scene closed while loading
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
            button.onClick.RemoveListener(action);   // safe if Initialize runs twice
            button.onClick.AddListener(action);
        }

        public void TogglePause()
        {
            // Not while a pair is resolving, the popup or settings are open, or the board is finished.
            if (Board == null || IsComplete || IsTimedOut || IsResolvingMismatch || returningToMenu) return;
            if (quitConfirm != null && quitConfirm.IsVisible) return;
            if (settingsPanelPresenter != null && settingsPanelPresenter.IsOpen) return;

            IsPaused = !IsPaused;
            pausePanel?.SetActive(IsPaused);
            pauseOverlay?.SetActive(IsPaused);
            if (!IsPaused) SetPauseContentVisible(true);
            audioService?.Play(GameAudioCueIds.UiClick);
            RenderCards();                           // greys out or restores the cards
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
            if (winContinueButton != null) winContinueButton.interactable = false;   // blocks double clicks
            audioService?.Play(GameAudioCueIds.UiClick);

            try
            {
                RewindTransactionResult result = await completeRewind.ExecuteAsync(ActiveSession, lifetime.Token);
                if (!result.Succeeded)
                {
                    Debug.LogWarning("Could not finish the rewind: " + result.ErrorMessage, this);
                    completing = false;
                    if (winContinueButton != null) winContinueButton.interactable = true;   // let the player try again
                }
                // On success the use case has already saved and loaded the Shop, so the popup stays up until then.
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
            {
                // scene closed while loading
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
