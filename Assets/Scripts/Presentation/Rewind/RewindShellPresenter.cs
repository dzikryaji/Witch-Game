using System;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;
using Witch.Application.Flow;
using Witch.Application.Ports;
using Witch.Domain.Progression;
using UnityEngine.UI;

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

        private float remainingSeconds;
        private bool timerRunning;
        private int boardGeneration;
        public bool IsTimedOut { get; private set; }

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
            symbols = symbolKeys;
            revealed = new bool[Board.Cards.Count];
            matched = new bool[Board.Cards.Count];
            EnsureCardViews(Board.Cards.Count);
            BindCards();
            if (timeUpPopup != null)
            {
                timeUpPopup.Hide();
                timeUpPopup.Bind(RetryBoard);
            }
            ResetTimer();
            if (boardLayout != null) boardLayout.Apply(Board.Cards.Count);
            RenderCards();
            audioService?.StopAmbience();
            audioService?.PlayMusic(GameAudioCueIds.RewindMusic);
        }

        public async Task<bool> FlipCardAsync(int index)
        {
            if (Board == null || IsResolvingMismatch || index < 0 || index >= Board.Cards.Count || matched[index] || revealed[index] || IsTimedOut || IsComplete)
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
                    RenderCards();
                    audioService?.Play(GameAudioCueIds.MinigameComplete);
                    audioService?.StopMusic();
                    await Task.Delay(CompletionHoldMs, lifetime.Token);   // show the final match before leaving
                    RewindTransactionResult result = await completeRewind.ExecuteAsync(ActiveSession, lifetime.Token);
                    return result.Succeeded;
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
                    !isMatched && !IsResolvingMismatch && !IsComplete && !IsTimedOut);
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
            if (!timerRunning) return;

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

        private void RetryBoard()
        {
            boardGeneration++;
            Array.Clear(revealed, 0, revealed.Length);
            Array.Clear(matched, 0, matched.Length);
            firstIndex = -1;
            IsResolvingMismatch = false;
            IsComplete = false;
            ResetTimer();
            timeUpPopup?.Hide();
            RenderCards();
            audioService?.Play(GameAudioCueIds.UiClick);
        }
    }
}
