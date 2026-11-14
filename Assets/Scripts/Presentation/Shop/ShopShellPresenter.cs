using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Witch.Application.Flow;
using Witch.Application.Ports;
using Witch.Domain;
using Witch.Domain.Potions;
using Witch.Domain.Progression;
using Witch.Presentation;

namespace Witch.Presentation.Shop
{
    public sealed class ShopShellPresenter : MonoBehaviour
    {
        [Header("Authored View")]
        [SerializeField] private ShopHudView hudView;
        [SerializeField] private Button[] herbButtons;
        [SerializeField] private HerbDragItem herbButtonPrefab;
        [SerializeField] private RectTransform herbButtonContainer;
        [SerializeField] private GrinderDropZone grinderDropZone;
        [SerializeField] private CauldronDropZone cauldronDropZone;
        [SerializeField] private DiscardDropZone discardDropZone;
        [SerializeField] private PotionServeDropZone potionServeDropZone;
        [SerializeField] private Button grindButton;
        [SerializeField] private Button ejectGrinderButton;
        [SerializeField] private Button discardGroundButton;
        [SerializeField] private Button emptyPotButton;
        [SerializeField] private Button brewButton;
        [SerializeField] private Button guidebookButton;
        [SerializeField] private Button guidebookPreviousButton;
        [SerializeField] private Button guidebookNextButton;
        [SerializeField] private Button pauseButton;
        [SerializeField] private GameObject guidebookPanel;
        [SerializeField] private GuidebookPageView guidebookLeftPage;
        [SerializeField] private GuidebookPageView guidebookRightPage;
        [Tooltip("Optional. Without it the guidebook changes pages instantly.")]
        [SerializeField] private PageFlipAnimator guidebookFlipAnimator;
        [SerializeField] private GuidebookPageView guidebookFlipFront;
        [SerializeField] private GuidebookPageView guidebookFlipBack;
        [Tooltip("Optional. Jumps back to the Contents spread; hidden while the Contents spread is open.")]
        [SerializeField] private Button guidebookContentsButton;
        [Tooltip("Optional. Coins a happy customer leaves on the counter after a correct order; the player taps them to collect.")]
        [SerializeField] private CounterCoinsView counterCoins;
        [Tooltip("Optional. The \"+10\" that floats up next to the header money when coins are collected.")]
        [SerializeField] private MoneyGainPopUp moneyGainPopUp;
        [Tooltip("Optional. Highlights the suspicion bar when an order is wrong.")]
        [SerializeField] private SuspicionFocusView suspicionFocus;
        [SerializeField] private Text grinderInputText;
        [SerializeField] private GameObject groundHerbObject;
        [SerializeField] private Text groundHerbText;
        [SerializeField] private GameObject preparedPotionObject;
        [SerializeField] private Text preparedPotionText;
        [SerializeField] private Image preparedPotionHerbIcon1;
        [SerializeField] private Image preparedPotionHerbIcon2;

        [SerializeField] private GameObject progressionPanel;
        [SerializeField] private Text progressionText;
        [SerializeField] private Text dayTitleText;
        [SerializeField] private Text correctOrdersText;
        [SerializeField] private Text wrongOrdersText;
        [SerializeField] private Text coinsEarnedText;
        [SerializeField] private Text taxPaidText;
        [SerializeField] private Text coinsRemainingText;
        [SerializeField] private Button continueButton;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private GameObject tutorialPanel;
        [SerializeField] private Text tutorialText;
        [SerializeField] private Button tutorialContinueButton;
        [SerializeField] private Button tutorialSkipButton;
        [SerializeField] private TutorialMaskView tutorialMaskView;
        [SerializeField] private GameObject[] tutorialHighlights; // legacy, unused by guided tutorial
        [Header("Input Actions")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private MinigameIntroPopupView minigameIntroPopup;
        [SerializeField] private PotionRevealAnimator potionRevealAnimator;
        [SerializeField] private Image[] grinderFirstHerbImages;
        [SerializeField] private Image[] grinderSecondHerbImages;

        private IGameSessionStore sessionStore;
        private OpenShopUseCase openShop;
        private AdmitCustomerUseCase admitCustomer;
        private PotionPreparationUseCase preparation;
        private ServePotionUseCase servePotion;
        private UpdateShopTimerUseCase updateTimer;
        private EnterRewindUseCase enterRewind;
        private ResolveExpenseUseCase resolveExpense;
        private RetryDayUseCase retryDay;
        private ContinueToNextDayUseCase continueToNextDay;
        private ShopPresentationSetup setup;
        private ShopEncounterDeck encounterDeck;
        private float nextRefresh;
        private SetShopTimerPausedUseCase setTimerPaused;
        private ISceneLoader sceneLoader;
        private IGameAudioService audioService;
        private GameSettingsState settings;
        private DayTransitionController dayTransition;
        private bool waitingForTransitionRelease;
        private bool pendingStartAfterReveal;
        private bool pendingStartIsResume;
        private int tutorialBeat;
        private bool viewBound;
        private CancellationTokenSource lifetimeCancellation;
        private bool actionInProgress;
        private bool autoResolveExpenseInFlight;
        private bool autoResolveExpenseAttempted;
        private string autoResolveExpenseError;
        private InputAction pauseAction;
        private InputAction guidebookAction;
        private bool guidedTutorialActive;
        private bool guidedTutorialSeenMarked;
        private ShopGuidedTutorialStep guidedTutorialStep;
        private string guidedTutorialOrderId;
        private bool guidedTutorialTimerFrozen;
        private bool guidedTutorialWaitingForServe;
        private IRunStateTransactionRepository saveRepository;
        private MarkTutorialFlagUseCase markTutorialFlagUseCase;
        private readonly List<HerbDragItem> herbSlots = new List<HerbDragItem>();
        private string grinderFirstHerbId;
        private string grinderSecondHerbId;
        private bool GrinderIsFull => grinderFirstHerbId != null && grinderSecondHerbId != null;
        private IReadOnlyList<GuidebookPageViewData> guidebookPages = Array.Empty<GuidebookPageViewData>();
        private int guidebookPageIndex;
        private int guidebookSpreadCount => (guidebookPages.Count + 1) / 2;

        public bool IsInitialized => sessionStore != null;
        public GameSession ActiveSession => sessionStore?.ActiveSession;
        public string LastStatus { get; private set; }
        public bool GuidebookVisible => guidebookPanel != null && guidebookPanel.activeSelf;
        public bool PauseVisible => pausePanel != null && pausePanel.activeSelf;
        public bool TutorialVisible => (tutorialPanel != null && tutorialPanel.activeSelf) || guidedTutorialActive;
        public bool GuidedTutorialActive => guidedTutorialActive;
        public bool MinigameIntroVisible => minigameIntroPopup != null && minigameIntroPopup.IsVisible;
        public bool HasAuthoredView => hudView != null
            && hudView.IsAuthored
            && herbButtons != null
            && herbButtons.Length == 25
            && Array.TrueForAll(herbButtons, button => button != null)
            && herbButtonContainer != null
            && grinderDropZone != null
            && cauldronDropZone != null
            && discardDropZone != null
            && potionServeDropZone != null
            && grindButton != null
            && ejectGrinderButton != null
            && discardGroundButton != null
            && emptyPotButton != null
            && brewButton != null
            && guidebookPreviousButton != null
            && guidebookNextButton != null
            && pauseButton != null
            && guidebookPanel != null
            && guidebookLeftPage != null
            && guidebookLeftPage.IsAuthored
            && guidebookRightPage != null
            && guidebookRightPage.IsAuthored
            && grinderInputText != null
            && groundHerbObject != null
            && groundHerbText != null
            && preparedPotionObject != null
            && preparedPotionText != null
            && progressionPanel != null
            && progressionText != null
            && dayTitleText != null
            && correctOrdersText != null
            && wrongOrdersText != null
            && coinsEarnedText != null
            && taxPaidText != null
            && coinsRemainingText != null
            && continueButton != null
            && pausePanel != null
            && resumeButton != null
            && mainMenuButton != null
            && tutorialPanel != null
            && tutorialText != null
            && tutorialContinueButton != null
            && inputActions != null;

        public void ConfigurePolish(
            SetShopTimerPausedUseCase pauseUseCase,
            IGameAudioService audio,
            GameSettingsState currentSettings)
        {
            setTimerPaused = pauseUseCase ?? throw new ArgumentNullException(nameof(pauseUseCase));
            audioService = audio;
            settings = currentSettings ?? GameSettingsState.Default;
            audioService?.ApplySettings(settings);
        }

        public void ConfigureNavigation(ISceneLoader loader)
        {
            sceneLoader = loader ?? throw new ArgumentNullException(nameof(loader));
        }

        public void ConfigurePersistence(IRunStateTransactionRepository repository)
        {
            saveRepository = repository;
        }

        public void ConfigureTutorialFlag(MarkTutorialFlagUseCase useCase)
        {
            markTutorialFlagUseCase = useCase;
        }

        public void ConfigureTransition(DayTransitionController transition)
        {
            dayTransition = transition;
        }

        public void Initialize(IGameSessionStore store)
        {
            sessionStore = store ?? throw new ArgumentNullException(nameof(store));
        }

        public void Initialize(
            IGameSessionStore store,
            OpenShopUseCase openShopUseCase,
            AdmitCustomerUseCase admitCustomerUseCase,
            PotionPreparationUseCase preparationUseCase,
            ServePotionUseCase servePotionUseCase,
            UpdateShopTimerUseCase updateTimerUseCase,
            ShopPresentationSetup presentationSetup)
        {
            Initialize(
                store,
                openShopUseCase,
                admitCustomerUseCase,
                preparationUseCase,
                servePotionUseCase,
                updateTimerUseCase,
                null,
                presentationSetup);
        }

        public void Initialize(
            IGameSessionStore store,
            OpenShopUseCase openShopUseCase,
            AdmitCustomerUseCase admitCustomerUseCase,
            PotionPreparationUseCase preparationUseCase,
            ServePotionUseCase servePotionUseCase,
            UpdateShopTimerUseCase updateTimerUseCase,
            EnterRewindUseCase enterRewindUseCase,
            ShopPresentationSetup presentationSetup)
        {
            Initialize(store);
            openShop = openShopUseCase ?? throw new ArgumentNullException(nameof(openShopUseCase));
            admitCustomer = admitCustomerUseCase ?? throw new ArgumentNullException(nameof(admitCustomerUseCase));
            preparation = preparationUseCase ?? throw new ArgumentNullException(nameof(preparationUseCase));
            servePotion = servePotionUseCase ?? throw new ArgumentNullException(nameof(servePotionUseCase));
            updateTimer = updateTimerUseCase ?? throw new ArgumentNullException(nameof(updateTimerUseCase));
            enterRewind = enterRewindUseCase;
            setup = presentationSetup ?? throw new ArgumentNullException(nameof(presentationSetup));
            encounterDeck = new ShopEncounterDeck(
                setup.Encounters,
                setup.EncounterSeed,
                setup.RandomizeEncounters,
                setup.SeenOrderIds,
                setup.IntroductionOrderId,
                setup.OneTimeIntroduction,
                setup.IntroductionCategoryOrderIds);
            guidedTutorialOrderId = setup.GuidedTutorialOrderId;
            guidedTutorialActive = false;
            guidedTutorialSeenMarked = ActiveSession != null
                && ActiveSession.RunState.TutorialFlags.TryGetValue(ShopTutorialFlags.Day1GuidedCompleted, out bool completed)
                && completed;
            guidedTutorialWaitingForServe = false;
            guidedTutorialTimerFrozen = false;

            RequireAndBindAuthoredView();
            ConfigureHerbButtons();
            ConfigureGuidebook();
            ValidateGuidedTutorialMasksIfEnabled();
            progressionPanel.SetActive(false);
            pausePanel.SetActive(false);
            tutorialPanel.SetActive(false);
            if (tutorialMaskView != null) tutorialMaskView.Hide();
            groundHerbObject.SetActive(false);
            preparedPotionObject.SetActive(false);
            if (counterCoins != null) counterCoins.Clear();
            if (moneyGainPopUp != null) moneyGainPopUp.Hide();
            if (suspicionFocus != null) suspicionFocus.Hide();
            hudView.ClearCustomer();
            audioService?.PlayMusic(GameAudioCueIds.ShopMusic);
            audioService?.PlayAmbience(GameAudioCueIds.ShopAmbience);
            Exception initException = null;
            try
            {
                bool deferStart = dayTransition != null && dayTransition.HasPendingShopReveal;
                pendingStartAfterReveal = false;
                pendingStartIsResume = false;
                if (deferStart)
                {
                    hudView.ResetDayCycleToMorning();
                    hudView.ClearCustomer();
                    pendingStartAfterReveal = true;
                    pendingStartIsResume = IsResumingEncounter();
                    if (setTimerPaused != null)
                    {
                        setTimerPaused.Execute(ActiveSession, true);
                        waitingForTransitionRelease = true;
                    }
                }
                else if (IsResumingEncounter())
                {
                    ResumeEncounter();
                    if (ShouldRunGuidedTutorial() && ActiveSession != null && ActiveSession.CurrentPhase == Witch.Domain.GamePhase.CustomerEncounter)
                    {
                        TryStartGuidedTutorialForCurrentEncounter();
                    }
                }
                else
                {
                    BeginArrival();
                }

                Render();
            }
            catch (Exception ex)
            {
                initException = ex;
                Debug.LogException(ex, this);
            }
            finally
            {
                // Always attempt reveal so a pending cover cannot trap the player.
                HandleDayTransitionReveal();
            }

            if (initException == null)
            {
                try
                {
                    if ((ActiveSession.CurrentPhase == Witch.Domain.GamePhase.RewindPending
                            || (ActiveSession.CurrentPhase == Witch.Domain.GamePhase.RewindMinigame
                                && ActiveSession.AttemptMetadata.PendingRewind))
                        && enterRewind != null)
                    {
                        ResumePendingRewind();
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex, this);
                }
            }
        }

        private void ValidateGuidedTutorialMasksIfEnabled()
        {
            if (setup == null || !setup.GuidedTutorialEnabled) return;
            if (tutorialMaskView == null)
            {
                throw new InvalidOperationException("Guided tutorial is enabled but TutorialMaskView is missing.");
            }
            var missing = new System.Collections.Generic.List<string>();
            foreach (ShopGuidedTutorialStep step in Enum.GetValues(typeof(ShopGuidedTutorialStep)))
            {
                if (!tutorialMaskView.TryGetMask(step, out TutorialStepMask mask) || mask == null || !mask.HasHoleRect() || !mask.HasDimPanel())
                {
                    missing.Add(step.ToString());
                }
            }
            if (missing.Count > 0)
            {
                throw new InvalidOperationException("Guided tutorial masks missing or incomplete: " + string.Join(", ", missing));
            }
        }

        private void Update()
        {
            if (!IsInitialized || updateTimer == null || ActiveSession == null)
            {
                return;
            }

            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + 0.1f;
                updateTimer.Execute(ActiveSession);
                Render();
            }

            if (guidedTutorialActive)
            {
                // Poll for async completions (e.g. order bubble entrance).
                EvaluateGuidedTutorialProgress();
            }

            TryAutoResolveExpense();
        }

        private void OnEnable()
        {
            lifetimeCancellation = new CancellationTokenSource();
            pauseAction = inputActions?.FindAction("Shop/Pause", false);
            guidebookAction = inputActions?.FindAction("Shop/Guidebook", false);
            if (pauseAction != null)
            {
                pauseAction.performed += HandlePauseAction;
                pauseAction.Enable();
            }
            if (guidebookAction != null)
            {
                guidebookAction.performed += HandleGuidebookAction;
                guidebookAction.Enable();
            }
        }

        private void OnDisable()
        {
            lifetimeCancellation?.Cancel();
            lifetimeCancellation?.Dispose();
            lifetimeCancellation = null;
            if (pauseAction != null)
            {
                pauseAction.performed -= HandlePauseAction;
                pauseAction.Disable();
            }
            if (guidebookAction != null)
            {
                guidebookAction.performed -= HandleGuidebookAction;
                guidebookAction.Disable();
            }
            pauseAction = null;
            guidebookAction = null;
        }

        private void HandlePauseAction(InputAction.CallbackContext context)
        {
            if (setTimerPaused == null || MinigameIntroVisible) return;
            if (guidedTutorialActive) return; // pause hotkey blocked during guided tutorial
            if (TutorialVisible) return;
            TogglePause();
        }

        private void HandleGuidebookAction(InputAction.CallbackContext context)
        {
            if (!IsInitialized || MinigameIntroVisible) return;
            if (guidedTutorialActive)
            {
                if (guidedTutorialStep == ShopGuidedTutorialStep.OpenGuidebook)
                {
                    if (!GuidebookVisible) OpenGuidebook();
                    EvaluateGuidedTutorialProgress();
                }
                else if (guidedTutorialStep == ShopGuidedTutorialStep.CloseGuidebook)
                {
                    if (GuidebookVisible) CloseGuidebook();
                    else EvaluateGuidedTutorialProgress();
                }
                // NavigateToMustardSeed: page navigation via UI buttons only, do not close/open here
                return;
            }
            if (TutorialVisible) return;
            ToggleGuidebook();
        }

        public PreparationResult GrindHerb(string firstHerbId, string secondHerbId)
        {
            PreparationResult result = preparation.Grind(ActiveSession, firstHerbId, secondHerbId);
            SetStatus(result.Succeeded
                ? "Ground " + DisplayName(firstHerbId) + " and " + DisplayName(secondHerbId) + ". Drag the bowl into the pot."
                : result.ErrorMessage);
            if (result.Succeeded) audioService?.Play(GameAudioCueIds.Grind);
            Render();
            return result;
        }

        public bool LoadHerbIntoGrinder(string herbId)
        {
            if (ActiveSession == null || ActiveSession.CurrentPhase != Witch.Domain.GamePhase.CustomerEncounter)
            {
                SetStatus("Wait for a customer before preparing medicine.");
                return false;
            }

            if (guidedTutorialActive && !GuidedTutorialAllowsHerb(herbId))
            {
                SetStatus("Follow the tutorial step and use the highlighted herbs.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(herbId)) return false;

            if (GrinderIsFull)
            {
                SetStatus("The grinder is full. Press Grind, or Reset to start over.");
                return false;
            }

            if (herbId == grinderFirstHerbId)
            {
                SetStatus(DisplayName(herbId) + " is already in the grinder.");
                return false;
            }

            if (grinderFirstHerbId == null) grinderFirstHerbId = herbId;
            else grinderSecondHerbId = herbId;

            audioService?.Play(GameAudioCueIds.HerbPluck);
            SetStatus(GrinderIsFull
                ? "Both herbs loaded. Press Grind."
                : DisplayName(herbId) + " loaded. Add a second herb.");
            Render();
            EvaluateGuidedTutorialProgress();
            return true;
        }

        public PreparationResult GrindLoadedHerb()
        {
            if (guidedTutorialActive && guidedTutorialStep != ShopGuidedTutorialStep.Grind && guidedTutorialStep != ShopGuidedTutorialStep.LoadHerbs)
            {
                PreparationResult blocked = PreparationResult.Failure("Follow the current tutorial step.");
                SetStatus(blocked.ErrorMessage);
                return blocked;
            }
            if (!GrinderIsFull)
            {
                PreparationResult failure = PreparationResult.Failure("Put two herbs into the grinder first.");
                SetStatus(failure.ErrorMessage);
                return failure;
            }

            PreparationResult result = GrindHerb(grinderFirstHerbId, grinderSecondHerbId);
            if (result.Succeeded)
            {
                ClearGrinder();
                Render();
                EvaluateGuidedTutorialProgress();
            }
            return result;
        }

        public void EjectLoadedHerb()
        {
            if (!GuidedTutorialAllowsUndo())
            {
                SetStatus("Undo is disabled during the tutorial.");
                return;
            }
            if (grinderFirstHerbId == null && grinderSecondHerbId == null)
            {
                SetStatus("Grinder is already empty.");
                return;
            }
            ClearGrinder();
            SetStatus("Grinder reset.");
            Render();
        }

        private void ClearGrinder()
        {
            grinderFirstHerbId = null;
            grinderSecondHerbId = null;
        }

        public PreparationResult AddGroundHerbToPot()
        {
            if (guidedTutorialActive && !GuidedTutorialAllowsAddToPot())
            {
                PreparationResult blocked = PreparationResult.Failure("Follow the current tutorial step.");
                SetStatus(blocked.ErrorMessage);
                return blocked;
            }
            PreparationResult result = preparation.AddToPot(ActiveSession);

            if (result.Succeeded && ActiveSession.PotionDraft.IsReadyToBrew)
            {
                return Brew();
            }

            SetStatus(result.Succeeded
                ? setup != null && setup.DayNumber == 1
                ? "Ground herb added to pot. Brew when both exact herbs are inside."
                : "Ground herb added to pot."
                : result.ErrorMessage);
            Render();
            EvaluateGuidedTutorialProgress();

            return result;
        }

        public PreparationResult DiscardGroundHerb()
        {
            if (!GuidedTutorialAllowsUndo())
            {
                PreparationResult blocked = PreparationResult.Failure("Undo is disabled during the tutorial.");
                SetStatus(blocked.ErrorMessage);
                return blocked;
            }
            PreparationResult result = preparation.DiscardGroundHerb(ActiveSession);
            SetStatus(result.Succeeded ? "Ground herb discarded." : result.ErrorMessage);
            if (result.Succeeded) audioService?.Play(GameAudioCueIds.Discard);
            Render();
            return result;
        }

        public PreparationResult Brew()
        {
            PreparationResult result = preparation.Brew(ActiveSession);
            string message = !result.Succeeded
                ? result.ErrorMessage
                : "Potion brewed. Drag it onto the customer counter.";
            SetStatus(message);
            if (result.Succeeded)
            {
                audioService?.Play(GameAudioCueIds.BrewSuccess);
            }
            Render();

            if (result.Succeeded && potionRevealAnimator != null)
            {
                potionRevealAnimator.Play();
            }
            if (result.Succeeded)
            {
                EvaluateGuidedTutorialProgress();
            }

            return result;
        }

        public PreparationResult Discard()
        {
            if (!GuidedTutorialAllowsUndo())
            {
                PreparationResult blocked = PreparationResult.Failure("Undo is disabled during the tutorial.");
                SetStatus(blocked.ErrorMessage);
                return blocked;
            }
            PreparationResult result = preparation.Discard(ActiveSession);
            SetStatus(result.Succeeded ? "Workbench cleared." : result.ErrorMessage);
            if (result.Succeeded) audioService?.Play(GameAudioCueIds.Discard);
            Render();
            return result;
        }

        public PreparationResult DiscardPotion()
        {
            if (!GuidedTutorialAllowsUndo())
            {
                PreparationResult blocked = PreparationResult.Failure("Undo is disabled during the tutorial.");
                SetStatus(blocked.ErrorMessage);
                return blocked;
            }
            PreparationResult result = preparation.DiscardPotion(ActiveSession);
            SetStatus(result.Succeeded ? "Potion discarded." : result.ErrorMessage);
            if (result.Succeeded) audioService?.Play(GameAudioCueIds.Discard);
            Render();
            return result;
        }

        public async Task<ServePotionResult> ServeAsync()
        {
            CancellationToken cancellationToken = lifetimeCancellation?.Token ?? CancellationToken.None;
            bool servingTutorial = guidedTutorialActive && guidedTutorialStep == ShopGuidedTutorialStep.Serve && IsTutorialOrderActive();
            ServePotionResult result = await servePotion.ExecuteAsync(ActiveSession, cancellationToken);
            if (!result.Succeeded)
            {
                SetStatus(result.ErrorMessage);
            }
            else if (result.Outcome.Kind == ServiceOutcomeKind.Correct)
            {
                audioService?.Play(GameAudioCueIds.OrderCorrect);
                if (counterCoins != null && result.Outcome.Payment > 0)
                {
                    // The payment is already saved; the coins only delay when the header shows it.
                    counterCoins.Add(result.Outcome.Payment);
                    SetStatus("Correct order. The customer left " + result.Outcome.Payment + " coins on the counter. Tap them to collect.");
                }
                else
                {
                    SetStatus("Correct order. Coins received: " + result.Outcome.Payment + ". Save enough for the daily expense.");
                    audioService?.Play(GameAudioCueIds.Payment);
                }
            }
            else
            {
                SetStatus("Wrong potion. No coins. Suspicion increased. At 3, Hunters investigate the suspicious 'herbalist'.");
                audioService?.Play(GameAudioCueIds.OrderWrong);
                if (suspicionFocus != null) suspicionFocus.Play();
            }

            Render();

            if (servingTutorial && result.Succeeded)
            {
                EndGuidedTutorial(skipped: false);
            }

            if (suspicionFocus != null && suspicionFocus.IsPlaying)
            {
                await suspicionFocus.Finished;
                if (cancellationToken.IsCancellationRequested) return result;
            }

            if (result.Succeeded
                && ActiveSession.CurrentPhase == Witch.Domain.GamePhase.RewindPending
                && enterRewind != null)
            {
                await EnterPendingRewindAsync(cancellationToken);
            }
            else if (result.Succeeded
                && (ActiveSession.CurrentPhase == Witch.Domain.GamePhase.CustomerArrival
                    || ActiveSession.CurrentPhase == Witch.Domain.GamePhase.ClosingEncounter))
            {
                await hudView.PlayCustomerExitAsync(cancellationToken);
                if (ActiveSession.CurrentPhase == Witch.Domain.GamePhase.CustomerArrival) BeginArrival();
                Render();
            }

            return result;
        }

        public void ConfigureProgression(
            ResolveExpenseUseCase resolveUseCase,
            RetryDayUseCase retryUseCase,
            ContinueToNextDayUseCase continueUseCase)
        {
            resolveExpense = resolveUseCase ?? throw new ArgumentNullException(nameof(resolveUseCase));
            retryDay = retryUseCase ?? throw new ArgumentNullException(nameof(retryUseCase));
            continueToNextDay = continueUseCase ?? throw new ArgumentNullException(nameof(continueUseCase));
            Render();
        }

        private void TryAutoResolveExpense()
        {
            if (resolveExpense == null || ActiveSession == null || autoResolveExpenseInFlight || autoResolveExpenseAttempted)
            {
                return;
            }

            if (ActiveSession.CurrentPhase != Witch.Domain.GamePhase.ClosingEncounter || PendingCounterCoins > 0)
            {
                return;
            }

            autoResolveExpenseAttempted = true;
            autoResolveExpenseInFlight = true;
            RunUiAction(ResolveExpenseAutomaticallyAsync);
        }

        private async Task ResolveExpenseAutomaticallyAsync()
        {
            try
            {
                CancellationToken cancellationToken = lifetimeCancellation?.Token ?? CancellationToken.None;
                ExpenseResolutionResult result = await resolveExpense.ExecuteAsync(ActiveSession, cancellationToken);
                if (!result.Succeeded)
                {
                    autoResolveExpenseError = string.IsNullOrWhiteSpace(result.ErrorMessage)
                        ? "Daily expense could not be resolved."
                        : result.ErrorMessage;
                    SetStatus("Auto-resolve failed: " + autoResolveExpenseError);
                    Debug.LogError("Shop auto-resolve failed: " + autoResolveExpenseError, this);
                    Render();
                    return;
                }

                autoResolveExpenseError = null;
                SetStatus("Daily expense resolved.");
                if (ActiveSession.CurrentPhase == Witch.Domain.GamePhase.GameOver)
                {
                    audioService?.Play(GameAudioCueIds.ExpenseFail);
                    audioService?.PlayMusic(GameAudioCueIds.GameOverMusic);
                }
                else if (ActiveSession.CurrentPhase == Witch.Domain.GamePhase.Victory)
                {
                    audioService?.Play(GameAudioCueIds.ExpensePay);
                    audioService?.PlayMusic(GameAudioCueIds.VictoryMusic);
                }
                else
                {
                    audioService?.Play(GameAudioCueIds.ExpensePay);
                }

                Render();
            }
            finally
            {
                autoResolveExpenseInFlight = false;
            }
        }

        private void ResetAutoResolveExpenseState()
        {
            autoResolveExpenseInFlight = false;
            autoResolveExpenseAttempted = false;
            autoResolveExpenseError = null;
        }

        public async Task<ExpenseResolutionResult> ContinueNextDayAsync()
        {
            CancellationToken cancellationToken = lifetimeCancellation?.Token ?? CancellationToken.None;
            if (dayTransition == null || ActiveSession == null || ActiveSession.CurrentPhase != GamePhase.DaySummary)
            {
                ExpenseResolutionResult direct = await continueToNextDay.ExecuteAsync(ActiveSession, cancellationToken);
                SetStatus(direct.Succeeded ? "Opening next day." : direct.ErrorMessage);
                return direct;
            }

            if (dayTransition.IsPlaying)
            {
                return ExpenseResolutionResult.Failure(ExpenseResolutionError.InvalidPhase, "Day transition is already running.");
            }

            int nextDay = ActiveSession.RunState.CurrentDayNumber;
            var completion = new TaskCompletionSource<ExpenseResolutionResult>();
            dayTransition.PlayDayToDayTransition(nextDay, async () =>
            {
                ExpenseResolutionResult advanced = continueToNextDay.AdvanceFromSummary(ActiveSession);
                if (!advanced.Succeeded)
                {
                    dayTransition.CancelPendingReveal();
                    completion.TrySetResult(advanced);
                    return;
                }

                // Old presenter can be destroyed/cancelled during load; do not cancel the handoff load with it.
                SceneLoadResult scene = await sceneLoader.LoadAsync(SceneId.Shop, CancellationToken.None);
                if (!scene.Succeeded)
                {
                    dayTransition.CancelPendingReveal();
                    await sceneLoader.LoadAsync(SceneId.MainMenu, CancellationToken.None);
                    completion.TrySetResult(ExpenseResolutionResult.Failure(ExpenseResolutionError.SceneLoadFailed, scene.ErrorMessage));
                    return;
                }

                completion.TrySetResult(ExpenseResolutionResult.Success());
            });

            ExpenseResolutionResult result = await completion.Task;
            SetStatus(result.Succeeded ? "Opening next day." : result.ErrorMessage);
            return result;
        }

        public void ToggleGuidebook()
        {
            if (guidebookPanel == null) return;
            if (guidebookPanel.activeSelf)
            {
                CloseGuidebook();
            }
            else
            {
                OpenGuidebook();
            }
        }

        public void OpenGuidebook()
        {
            if (guidebookPanel == null) return;
            guidebookPanel.SetActive(true);
            guidebookPanel.GetComponent<Animator>()?.SetTrigger("Open");
            // A turn interrupted by hiding the book leaves the two pages out of step; redraw them.
            if (!GuidebookTurning && guidebookPages.Count > 0) RenderGuidebookPage();
            SetStatus("Guidebook stays on the table. Timer continues while you read.");
            EvaluateGuidedTutorialProgress();
        }

        public void CloseGuidebook()
        {
            if (guidebookPanel == null) return;
            if (guidedTutorialActive && guidedTutorialStep == ShopGuidedTutorialStep.NavigateToMustardSeed)
            {
                return;
            }
            guidebookPanel.SetActive(false);
            EvaluateGuidedTutorialProgress();
        }

        public void PreviousGuidebookPage()
        {
            if (guidebookPages.Count == 0 || GuidebookTurning) return;
            int spread = guidebookPageIndex / 2;
            if (spread <= 0) return; // already on the first spread
            TurnGuidebookTo((spread - 1) * 2, FlipDirection.Backward);
            audioService?.Play(GameAudioCueIds.PageFlip);
        }

        public void NextGuidebookPage()
        {
            if (guidebookPages.Count == 0 || GuidebookTurning) return;
            int spread = guidebookPageIndex / 2;
            if (spread + 1 >= guidebookSpreadCount) return; // already on the last spread
            TurnGuidebookTo((spread + 1) * 2, FlipDirection.Forward);
            audioService?.Play(GameAudioCueIds.PageFlip);
        }

        /// <summary>Opens the spread that contains <paramref name="pageIndex"/> (zero-based), e.g. from a Contents link.</summary>
        public void GoToGuidebookPage(int pageIndex)
        {
            if (guidebookPages.Count == 0 || GuidebookTurning) return;
            int page = Math.Max(0, Math.Min(pageIndex, guidebookPages.Count - 1));
            int target = page - (page % 2);
            int current = guidebookPageIndex - (guidebookPageIndex % 2);
            TurnGuidebookTo(target, target >= current ? FlipDirection.Forward : FlipDirection.Backward);
            audioService?.Play(GameAudioCueIds.PageFlip);
        }

        /// <summary>Turns straight back to the Contents page (always the first page of the guidebook).</summary>
        public void OpenGuidebookContents()
        {
            GoToGuidebookPage(0);
        }

        private bool GuidebookTurning => guidebookFlipAnimator != null && guidebookFlipAnimator.IsPlaying;

        /// <summary>
        /// Moves to the spread whose left page is <paramref name="targetLeftIndex"/>. With a page-turn
        /// animator the turning page shows the old page on its front and the new page on its back;
        /// without one (or while the book is hidden) the pages change instantly.
        /// </summary>
        private void TurnGuidebookTo(int targetLeftIndex, FlipDirection direction)
        {
            int currentLeftIndex = guidebookPageIndex - (guidebookPageIndex % 2);
            guidebookPageIndex = targetLeftIndex;

            bool canAnimate = targetLeftIndex != currentLeftIndex
                && guidebookFlipAnimator != null
                && guidebookFlipAnimator.isActiveAndEnabled
                && guidebookFlipFront != null
                && guidebookFlipBack != null;
            if (!canAnimate)
            {
                RenderGuidebookPage();
                EvaluateGuidedTutorialProgress();
                return;
            }

            SetGuidebookContentsButtonVisible(targetLeftIndex > 0); // sits outside the pages, so it stays put while they turn
            UpdateGuidebookNavigation();
            // Everything is rendered in the same frame as Play, so nothing flashes.
            if (direction == FlipDirection.Forward)
            {
                RenderGuidebookSide(guidebookFlipFront, currentLeftIndex + 1, GuidebookPageSide.Right); // front of the turning page
                RenderGuidebookSide(guidebookFlipBack, targetLeftIndex, GuidebookPageSide.Left);         // its back = new left page
                RenderGuidebookSide(guidebookRightPage, targetLeftIndex + 1, GuidebookPageSide.Right);   // revealed underneath
            }
            else
            {
                RenderGuidebookSide(guidebookFlipFront, currentLeftIndex, GuidebookPageSide.Left);
                RenderGuidebookSide(guidebookFlipBack, targetLeftIndex + 1, GuidebookPageSide.Right);
                RenderGuidebookSide(guidebookLeftPage, targetLeftIndex, GuidebookPageSide.Left);
            }

            if (!guidebookFlipAnimator.Play(direction, () => { RenderGuidebookPage(); EvaluateGuidedTutorialProgress(); })) { RenderGuidebookPage(); EvaluateGuidedTutorialProgress(); }
        }

        public void AdvanceTutorial()
        {
            // Legacy API kept for compatibility; guided tutorial uses OnTutorialContinueClicked.
            if (guidedTutorialActive)
            {
                OnTutorialContinueClicked();
                return;
            }
        }

        public void TogglePause()
        {
            if (pausePanel == null || setTimerPaused == null || ActiveSession == null) return;
            if (guidedTutorialActive && guidedTutorialTimerFrozen)
            {
                // During guided tutorial the clock must remain frozen; pause UI is blocked by input.
                return;
            }
            bool paused = !pausePanel.activeSelf;
            // Never unfreeze while guided tutorial timer is frozen.
            if (!paused && guidedTutorialTimerFrozen)
            {
                pausePanel.SetActive(false);
                return;
            }
            setTimerPaused.Execute(ActiveSession, paused);
            pausePanel.SetActive(paused);
            audioService?.Play(GameAudioCueIds.UiClick);
            SetStatus(paused ? "Paused. Shop time is frozen." : "Back to work. Shop time is moving again.");
        }

        public async Task ReturnToMainMenuAsync()
        {
            if (sceneLoader == null || ActiveSession == null)
            {
                SetStatus("Main Menu is not available yet.");
                return;
            }

            if (pausePanel != null && pausePanel.activeSelf && setTimerPaused != null)
            {
                pausePanel.SetActive(false);
                if (!guidedTutorialTimerFrozen)
                {
                    setTimerPaused.Execute(ActiveSession, false);
                }
            }

            ClearCounterCoins();
            audioService?.Play(GameAudioCueIds.UiClick);
            SceneLoadResult result = await sceneLoader.LoadAsync(
                SceneId.MainMenu,
                lifetimeCancellation?.Token ?? CancellationToken.None);
            if (!result.Succeeded)
            {
                SetStatus("Could not return to Main Menu: " + result.ErrorMessage);
            }
        }

        private bool ShouldRunGuidedTutorial()
        {
            if (guidedTutorialSeenMarked) return false;
            if (setup == null || ActiveSession == null) return false;
            if (!setup.GuidedTutorialEnabled || string.IsNullOrWhiteSpace(setup.GuidedTutorialOrderId)) return false;
            if (ActiveSession.RunState.CurrentDayNumber != 1) return false;
            if (ActiveSession.RunState.TutorialFlags.TryGetValue(ShopTutorialFlags.Day1GuidedCompleted, out bool completed) && completed) return false;
            // Only for the tutorial-eligible run; retries are marked seen by RetryDayUseCase.
            return true;
        }

        private ShopEncounterViewData FindGuidedTutorialEncounter()
        {
            if (setup == null || string.IsNullOrWhiteSpace(guidedTutorialOrderId)) return null;
            for (int i = 0; i < setup.Encounters.Count; i++)
            {
                if (setup.Encounters[i].Order != null
                    && string.Equals(setup.Encounters[i].Order.OrderId, guidedTutorialOrderId, StringComparison.Ordinal))
                {
                    return setup.Encounters[i];
                }
            }
            return null;
        }

        private bool IsTutorialOrderActive()
        {
            return ActiveSession != null
                && ActiveSession.AttemptState.ActiveOrder != null
                && !string.IsNullOrWhiteSpace(guidedTutorialOrderId)
                && string.Equals(ActiveSession.AttemptState.ActiveOrder.OrderId, guidedTutorialOrderId, StringComparison.Ordinal);
        }

        private void TryStartGuidedTutorialForCurrentEncounter()
        {
            if (guidedTutorialActive || guidedTutorialSeenMarked) return;
            if (!ShouldRunGuidedTutorial() || !IsTutorialOrderActive()) return;
            if (ActiveSession == null
                || ActiveSession.AttemptState.UsedOrderIds.Count != 1
                || !string.Equals(ActiveSession.AttemptState.UsedOrderIds[0], guidedTutorialOrderId, StringComparison.Ordinal))
            {
                return;
            }
            guidedTutorialActive = true;
            guidedTutorialStep = ShopGuidedTutorialStep.ReadOrder;
            guidedTutorialWaitingForServe = false;
            FreezeGuidedTutorialTimer(true);
            if (pausePanel != null && pausePanel.activeSelf)
            {
                pausePanel.SetActive(false);
            }
            if (tutorialPanel != null) tutorialPanel.SetActive(true);
            UpdateGuidedTutorialUi();
        }

        private void FreezeGuidedTutorialTimer(bool freeze)
        {
            if (setTimerPaused == null || ActiveSession == null) return;
            if (freeze)
            {
                if (!guidedTutorialTimerFrozen)
                {
                    setTimerPaused.Execute(ActiveSession, true);
                    guidedTutorialTimerFrozen = true;
                }
                return;
            }
            if (guidedTutorialTimerFrozen)
            {
                setTimerPaused.Execute(ActiveSession, false);
                guidedTutorialTimerFrozen = false;
            }
        }

        private void EndGuidedTutorial(bool skipped)
        {
            if (!guidedTutorialActive && !guidedTutorialWaitingForServe && tutorialPanel != null && !tutorialPanel.activeSelf) return;
            bool wasActive = guidedTutorialActive || guidedTutorialWaitingForServe || (tutorialPanel != null && tutorialPanel.activeSelf);
            guidedTutorialActive = false;
            guidedTutorialWaitingForServe = false;
            if (tutorialPanel != null) tutorialPanel.SetActive(false);
            if (tutorialMaskView != null) tutorialMaskView.Hide();
            FreezeGuidedTutorialTimer(false);
            if (wasActive && !guidedTutorialSeenMarked)
            {
                MarkGuidedTutorialSeen();
            }
            Render();
        }

        private async void MarkGuidedTutorialSeen()
        {
            if (ActiveSession == null || guidedTutorialSeenMarked) return;
            if (ActiveSession.RunState.TutorialFlags.TryGetValue(ShopTutorialFlags.Day1GuidedCompleted, out bool completed) && completed)
            {
                guidedTutorialSeenMarked = true;
                return;
            }

            guidedTutorialSeenMarked = true;

            if (markTutorialFlagUseCase != null)
            {
                try
                {
                    SaveStateResult result = await markTutorialFlagUseCase.ExecuteAsync(
                        ActiveSession,
                        ShopTutorialFlags.Day1GuidedCompleted,
                        lifetimeCancellation?.Token ?? CancellationToken.None);
                    if (!result.Succeeded)
                    {
                        Debug.LogWarning("Failed to persist guided tutorial flag: " + result.ErrorMessage, this);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("Failed to persist guided tutorial flag: " + ex.Message, this);
                }
                return;
            }

            // Fallback: apply to session and persist day-start snapshot (keeps money/seen orders at day start).
            try
            {
                ActiveSession.ApplyTutorialFlag(ShopTutorialFlags.Day1GuidedCompleted, true);
                if (saveRepository != null)
                {
                    SaveStateResult save = await saveRepository.SaveRunStateAsync(
                        ActiveSession.DayStartSnapshot,
                        ActiveSession.AttemptMetadata,
                        lifetimeCancellation?.Token ?? CancellationToken.None);
                    if (!save.Succeeded)
                    {
                        Debug.LogWarning("Failed to persist guided tutorial flag: " + save.ErrorMessage, this);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Failed to persist guided tutorial flag: " + ex.Message, this);
            }
        }

        private void OnTutorialSkipClicked()
        {
            if (!guidedTutorialActive && !(tutorialPanel != null && tutorialPanel.activeSelf)) return;
            audioService?.Play(GameAudioCueIds.UiClick);
            EndGuidedTutorial(skipped: true);
            SetStatus("Tutorial skipped. The same customer is waiting.");
        }

        private void OnTutorialContinueClicked()
        {
            if (!guidedTutorialActive) return;
            if (guidedTutorialStep != ShopGuidedTutorialStep.ReadOrder) return;
            audioService?.Play(GameAudioCueIds.UiClick);
            if (!IsGuidedTutorialStepComplete()) return;
            AdvanceGuidedTutorialStep();
        }

        private bool IsGuidedTutorialStepComplete()
        {
            switch (guidedTutorialStep)
            {
                case ShopGuidedTutorialStep.ReadOrder:
                    return hudView != null && hudView.EntranceComplete.IsCompleted && hudView.EntranceComplete.Result;
                case ShopGuidedTutorialStep.OpenGuidebook:
                    return GuidebookVisible;
                case ShopGuidedTutorialStep.NavigateToMustardSeed:
                    return GuidebookVisible && IsMustardSeedSpreadVisible();
                case ShopGuidedTutorialStep.CloseGuidebook:
                    return !GuidebookVisible;
                case ShopGuidedTutorialStep.LoadHerbs:
                    return string.Equals(grinderFirstHerbId, "herb.ginger", StringComparison.Ordinal)
                        && string.Equals(grinderSecondHerbId, "herb.mustardSeed", StringComparison.Ordinal)
                        || (string.Equals(grinderFirstHerbId, "herb.mustardSeed", StringComparison.Ordinal)
                            && string.Equals(grinderSecondHerbId, "herb.ginger", StringComparison.Ordinal));
                case ShopGuidedTutorialStep.Grind:
                    return ActiveSession != null && ActiveSession.GroundMixture != null;
                case ShopGuidedTutorialStep.AddToCauldron:
                    return ActiveSession != null && ActiveSession.PreparedPotion != null;
                case ShopGuidedTutorialStep.Serve:
                    return false; // serve ends tutorial directly
                default:
                    return false;
            }
        }

        private bool IsMustardSeedSpreadVisible()
        {
            if (guidebookPages == null || guidebookPages.Count == 0) return false;
            int left = guidebookPageIndex - (guidebookPageIndex % 2);
            int right = left + 1;
            return PageContainsHerb(left, "Mustard Seed") || PageContainsHerb(right, "Mustard Seed")
                || PageContainsScientific(left, "Brassica") || PageContainsScientific(right, "Brassica");
        }

        private bool PageContainsHerb(int index, string title)
        {
            GuidebookPageViewData page = PageAt(index);
            return page != null && string.Equals(page.Title, title, StringComparison.Ordinal);
        }

        private bool PageContainsScientific(int index, string token)
        {
            GuidebookPageViewData page = PageAt(index);
            return page != null && !string.IsNullOrEmpty(page.ScientificName)
                && page.ScientificName.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void AdvanceGuidedTutorialStep()
        {
            if (guidedTutorialStep == ShopGuidedTutorialStep.Serve) return;
            guidedTutorialStep = (ShopGuidedTutorialStep)((int)guidedTutorialStep + 1);
            UpdateGuidedTutorialUi();
        }

        private void EvaluateGuidedTutorialProgress()
        {
            if (!guidedTutorialActive) return;
            UpdateTutorialInteractionState();
            bool complete = IsGuidedTutorialStepComplete();
            if (tutorialContinueButton != null)
            {
                bool showContinue = guidedTutorialStep == ShopGuidedTutorialStep.ReadOrder;
                tutorialContinueButton.gameObject.SetActive(showContinue);
                tutorialContinueButton.interactable = showContinue && complete;
            }
            UpdateGuidedTutorialMask();
            if (complete && guidedTutorialStep != ShopGuidedTutorialStep.ReadOrder && guidedTutorialStep != ShopGuidedTutorialStep.Serve)
            {
                AdvanceGuidedTutorialStep();
            }
        }

        private void UpdateGuidedTutorialUi()
        {
            if (!guidedTutorialActive || tutorialText == null) return;
            tutorialText.text = GetGuidedTutorialStepText(guidedTutorialStep);
            if (tutorialContinueButton != null)
            {
                bool showContinue = guidedTutorialStep == ShopGuidedTutorialStep.ReadOrder;
                tutorialContinueButton.gameObject.SetActive(showContinue);
                tutorialContinueButton.interactable = showContinue && IsGuidedTutorialStepComplete();
            }
            if (tutorialSkipButton != null) tutorialSkipButton.gameObject.SetActive(true);
            UpdateGuidedTutorialMask();
        }

        private string GetGuidedTutorialStepText(ShopGuidedTutorialStep step)
        {
            switch (step)
            {
                case ShopGuidedTutorialStep.ReadOrder:
                    return "Read the customer's order bubbles. Continue when you are ready.";
                case ShopGuidedTutorialStep.OpenGuidebook:
                    return "Open the guidebook to check Ginger and Mustard Seed.";
                case ShopGuidedTutorialStep.NavigateToMustardSeed:
                    return "Turn to the spread that shows Mustard Seed.";
                case ShopGuidedTutorialStep.CloseGuidebook:
                    return "Click outside the guidebook to close it and return to the counter.";
                case ShopGuidedTutorialStep.LoadHerbs:
                    return "Drag Ginger and Mustard Seed into the grinder.";
                case ShopGuidedTutorialStep.Grind:
                    return "Press Grind to prepare the herbs.";
                case ShopGuidedTutorialStep.AddToCauldron:
                    return "Drag the ground herbs into the cauldron to brew the potion.";
                case ShopGuidedTutorialStep.Serve:
                    return "Drag the brewed potion onto the customer counter to serve it.";
                default:
                    return string.Empty;
            }
        }

        private void UpdateGuidedTutorialMask()
        {
            if (tutorialMaskView == null || !guidedTutorialActive) return;
            tutorialMaskView.ShowStep(guidedTutorialStep);
        }

        private bool GuidedTutorialAllowsHerb(string herbId)
        {
            if (!guidedTutorialActive) return true;
            if (guidedTutorialStep != ShopGuidedTutorialStep.LoadHerbs) return false;
            return string.Equals(herbId, "herb.ginger", StringComparison.Ordinal)
                || string.Equals(herbId, "herb.mustardSeed", StringComparison.Ordinal);
        }

        private bool GuidedTutorialAllowsGrindAction() => !guidedTutorialActive || guidedTutorialStep == ShopGuidedTutorialStep.Grind || guidedTutorialStep == ShopGuidedTutorialStep.LoadHerbs;
        private bool GuidedTutorialAllowsAddToPot() => !guidedTutorialActive || guidedTutorialStep == ShopGuidedTutorialStep.AddToCauldron;
        private bool GuidedTutorialAllowsServe() => !guidedTutorialActive || guidedTutorialStep == ShopGuidedTutorialStep.Serve;
        private bool GuidedTutorialAllowsUndo() => !guidedTutorialActive; // undo blocked during tutorial

        private void UpdateTutorialInteractionState()
        {
            if (guidedTutorialActive)
            {
                // Restrict shelf interaction to the two tutorial herbs during the load step.
                for (int i = 0; i < herbSlots.Count; i++)
                {
                    HerbDragItem drag = herbSlots[i];
                    if (drag == null) continue;
                    Button button = drag.GetComponent<Button>();
                    if (button == null) continue;
                    bool activeSlot = !string.IsNullOrWhiteSpace(drag.HerbId);
                    button.interactable = activeSlot && (guidedTutorialStep != ShopGuidedTutorialStep.LoadHerbs || GuidedTutorialAllowsHerb(drag.HerbId));
                }
                if (grindButton != null) grindButton.interactable = GrinderIsFull && (guidedTutorialStep == ShopGuidedTutorialStep.Grind || guidedTutorialStep == ShopGuidedTutorialStep.LoadHerbs);
                if (ejectGrinderButton != null) ejectGrinderButton.interactable = false;
                if (discardGroundButton != null) discardGroundButton.interactable = false;
                if (emptyPotButton != null) emptyPotButton.interactable = false;
                if (brewButton != null) brewButton.interactable = false; // brewing is automatic on add in this flow
                return;
            }

            for (int i = 0; i < herbSlots.Count; i++)
            {
                HerbDragItem drag = herbSlots[i];
                if (drag == null) continue;
                Button button = drag.GetComponent<Button>();
                if (button == null) continue;
                button.interactable = !string.IsNullOrWhiteSpace(drag.HerbId);
            }
            if (grindButton != null) grindButton.interactable = GrinderIsFull;
            if (ejectGrinderButton != null) ejectGrinderButton.interactable = true;
            if (discardGroundButton != null) discardGroundButton.interactable = true;
            if (emptyPotButton != null) emptyPotButton.interactable = true;
            if (brewButton != null) brewButton.interactable = true;
        }


        private void BeginArrival()
        {
            if (ActiveSession == null)
            {
                SetStatus("No active run.");
                return;
            }

            ResetAutoResolveExpenseState();

            if (ActiveSession.CurrentPhase == Witch.Domain.GamePhase.ShopPreparation)
            {
                PhaseTransitionResult opened = openShop.Execute(ActiveSession);
                if (!opened.Succeeded)
                {
                    SetStatus("Shop could not open: " + opened.Error);
                    return;
                }
            }

            if (ActiveSession.CurrentPhase == Witch.Domain.GamePhase.CustomerArrival)
            {
                ShopEncounterViewData encounter = null;
                if (ShouldRunGuidedTutorial() && ActiveSession.AttemptState.UsedOrderIds.Count == 0)
                {
                    encounter = FindGuidedTutorialEncounter() ?? FindNextEncounter();
                }
                else
                {
                    encounter = FindNextEncounter();
                }
                if (encounter == null)
                {
                    SetStatus("No more authored customers today. The clock continues to closing.");
                    return;
                }

                AdmitCustomerResult arrival = admitCustomer.Execute(
                    ActiveSession,
                    new AdmitCustomerCommand(encounter.CustomerId, encounter.Order));
                SetStatus(arrival.Succeeded
                    ? setup != null && setup.DayNumber == 1 && (!guidedTutorialActive || string.Equals(encounter.Order?.OrderId, guidedTutorialOrderId, StringComparison.Ordinal))
                        ? "DING! " + encounter.CustomerName + " enters. Read the order, consult the guidebook, and prepare the exact herb pair."
                        : "DING! " + encounter.CustomerName + " enters the shop."
                    : arrival.ErrorMessage);
                if (arrival.Succeeded) audioService?.Play(GameAudioCueIds.CustomerBell);
                if (arrival.Succeeded)
                {
                    hudView.SetCustomer(
                        encounter.CustomerName,
                        encounter.OrderBubbles,
                        encounter.CustomerIllustration);
                }
                if (arrival.Succeeded && ShouldRunGuidedTutorial() && IsTutorialOrderActive())
                {
                    TryStartGuidedTutorialForCurrentEncounter();
                }
                else if (arrival.Succeeded && guidedTutorialActive && !IsTutorialOrderActive())
                {
                    // If somehow a different customer appears mid-tutorial context, do not start guided mode.
                }
            }
        }

        private void ShowTutorialBeat()
        {
            // Legacy pre-customer tutorial removed in favor of guided tutorial on first real customer.
        }

        private async Task EnterPendingRewindAsync(CancellationToken cancellationToken)
        {
            ClearCounterCoins();
            SetStatus("KNOCK. KNOCK. Hunters suspect poisoning. Cast the memory spell before they enter; the day will rewind.");
            audioService?.StopMusic();

            ShopCustomerViewData witchHunter = setup.WitchHunter
                ?? throw new InvalidOperationException("Witch Hunter customer data is missing from Shop setup.");
            hudView.SetCustomer(
                witchHunter.DisplayName,
                new[] {
                    "Three harmful remedies. Carelessness no longer explains this pattern.",
                    "Again, your work leaves questions ordinary mistakes cannot answer.",
                    "Your neighbors forget quickly. Written patterns do not.",
                    "Something about this shop refuses to remain remembered.",
                    "This examination ends now. Step away from counter."
                },
                witchHunter.Illustration);
            bool entranceFinished = await hudView.EntranceComplete;
            if (!entranceFinished) return;
            setTimerPaused?.Execute(ActiveSession, true);
            minigameIntroPopup.Show();
        }

        private async Task StartRewindAsync(CancellationToken cancellationToken)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();

            RewindTransactionResult rewind;
            try
            {
                rewind = await enterRewind.ExecuteAsync(ActiveSession, cancellationToken);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                if (MinigameIntroVisible) minigameIntroPopup.Show("Rewind could not start. Please try again.");
                return;
            }

            if (rewind.Succeeded) return;

            string error = "Rewind could not start. " + rewind.ErrorMessage;
            if (MinigameIntroVisible)
            {
                minigameIntroPopup.Show(error); // keeps the popup and re-enables the button
                return;
            }

            SetStatus(error + " Try again by serving or reloading the run.");
            audioService?.PlayMusic(GameAudioCueIds.ShopMusic);
        }

        private void OnMinigameIntroClicked()
        {
            if (actionInProgress) return;
            minigameIntroPopup.SetInteractable(false); // prevents double clicks
            RunUiAction(() => StartRewindAsync(lifetimeCancellation?.Token ?? CancellationToken.None));
        }

        private void ResumePendingRewind() => RunUiAction(
            () => EnterPendingRewindAsync(lifetimeCancellation?.Token ?? CancellationToken.None));

        private ShopEncounterViewData FindNextEncounter()
        {
            if (!ActiveSession.TimerState.QueueOpen || encounterDeck == null) return null;
            return encounterDeck.Draw(ActiveSession.AttemptState.UsedOrderIds.Count);
        }


        private void RequireAndBindAuthoredView()
        {
            if (!HasAuthoredView)
            {
                throw new InvalidOperationException("Shop scene is missing authored UI references.");
            }

            if (viewBound)
            {
                return;
            }

            if (minigameIntroPopup != null)
            {
                minigameIntroPopup.Hide();
                minigameIntroPopup.Bind(OnMinigameIntroClicked);
            }

            grinderDropZone.Initialize(this);
            cauldronDropZone.Initialize(this);
            discardDropZone.Initialize(this);
            potionServeDropZone.Initialize(this);
            grindButton.onClick.AddListener(() => GrindLoadedHerb());
            ejectGrinderButton.onClick.AddListener(EjectLoadedHerb);
            discardGroundButton.onClick.AddListener(() => DiscardGroundHerb());
            emptyPotButton.onClick.AddListener(() => Discard());
            brewButton.onClick.AddListener(() => Brew());
            if (guidebookButton != null) guidebookButton.onClick.AddListener(ToggleGuidebook);
            guidebookPreviousButton.onClick.AddListener(PreviousGuidebookPage);
            guidebookNextButton.onClick.AddListener(NextGuidebookPage);
            if (guidebookContentsButton != null) guidebookContentsButton.onClick.AddListener(OpenGuidebookContents);
            if (counterCoins != null)
            {
                counterCoins.Collected -= OnCounterCoinsCollected;
                counterCoins.Collected += OnCounterCoinsCollected;
            }
            guidebookLeftPage.BindPageLinks(GoToGuidebookPage);
            guidebookRightPage.BindPageLinks(GoToGuidebookPage);
            pauseButton.onClick.AddListener(TogglePause);
            resumeButton.onClick.AddListener(TogglePause);
            mainMenuButton.onClick.AddListener(OnMainMenuClicked);
            continueButton.onClick.AddListener(OnContinueClicked);
            tutorialContinueButton.onClick.RemoveListener(AdvanceTutorial);
            tutorialContinueButton.onClick.AddListener(OnTutorialContinueClicked);
            if (tutorialSkipButton != null) tutorialSkipButton.onClick.AddListener(OnTutorialSkipClicked);
            BindGuidebookBackdrop();
            viewBound = true;
        }

        private void BindGuidebookBackdrop()
        {
            if (guidebookPanel == null) return;
            Transform backdrop = guidebookPanel.transform.Find("Read Backdrop");
            if (backdrop == null && guidebookPanel.transform.parent != null)
                backdrop = guidebookPanel.transform.parent.Find("Read Backdrop");
            if (backdrop == null) return;
            Button button = backdrop.GetComponent<Button>();
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(CloseGuidebook);
        }

        private void ConfigureHerbButtons()
        {
            herbSlots.Clear();
            foreach (Button button in herbButtons)
            {
                if (button == null) continue;
                HerbDragItem drag = button.GetComponent<HerbDragItem>();
                if (drag == null)
                {
                    throw new InvalidOperationException("Authored herb slot is missing HerbDragItem.");
                }
                herbSlots.Add(drag);
            }

            for (int index = 0; index < herbSlots.Count; index++)
            {
                HerbDragItem drag = herbSlots[index];
                Button button = drag.GetComponent<Button>();
                if (button == null)
                {
                    throw new InvalidOperationException("HerbButton prefab is missing Button.");
                }
                bool active = index < setup.Herbs.Count;
                button.gameObject.SetActive(true);
                Image illustration = button.image;
                if (!active)
                {
                    drag.UnbindSubmit();
                    drag.Initialize(null);
                    button.interactable = false;
                    Text lockedLabel = button.GetComponentInChildren<Text>(true);
                    if (lockedLabel != null)
                    {
                        lockedLabel.text = string.Empty;
                    }
                    if (illustration != null)
                    {
                        illustration.sprite = null;
                        illustration.color = Color.clear;
                    }
                    continue;
                }

                ShopHerbViewData herb = setup.Herbs[index];
                button.interactable = true;
                button.gameObject.name = "Herb " + herb.StableId;
                Text label = button.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    label.text = herb.HerbIllustration == null ? herb.DisplayName : string.Empty;
                }
                if (illustration != null)
                {
                    illustration.sprite = herb.HerbIllustration;
                    illustration.preserveAspect = herb.HerbIllustration != null;
                    illustration.color = herb.HerbIllustration == null ? Color.clear : Color.white;
                }

                drag.Initialize(herb.StableId);
                string herbId = herb.StableId;
                drag.BindSubmit(() => LoadHerbIntoGrinder(herbId));
            }

            if (herbSlots.Count > 0)
            {
                herbSlots[0].GetComponent<Button>().Select();
            }
        }

        public void RequestServe()
        {
            if (guidedTutorialActive && !GuidedTutorialAllowsServe())
            {
                SetStatus("Follow the current tutorial step before serving.");
                return;
            }
            RunUiAction(ServeAsync);
        }

        private void Render()
        {
            GameSession session = ActiveSession;
            if (session == null || hudView == null)
            {
                return;
            }

            hudView.Render(session, DisplayName, PendingCounterCoins);
            grinderInputText.text = GrinderIsFull
                ? "Ready to grind"
                : grinderFirstHerbId == null
                ? "Drop two herbs here"
                : "Drop one more herb";
            grindButton.interactable = GrinderIsFull;
            ShowHerbImages(grinderFirstHerbImages, grinderFirstHerbId);
            ShowHerbImages(grinderSecondHerbImages, grinderSecondHerbId);
            bool hasGroundHerb = session.GroundMixture != null;
            groundHerbObject.SetActive(hasGroundHerb);
            if (hasGroundHerb)
            {
                groundHerbText.text = "Ground\n" + DisplayName(session.GroundMixture.FirstHerbId)
                    + " + " + DisplayName(session.GroundMixture.SecondHerbId);
            }
            bool hasPotion = session.PreparedPotion != null;
            if (!hasPotion && preparedPotionObject.activeSelf)
            {
                preparedPotionObject.GetComponent<PreparedPotionDragItem>()?.RestoreHome(false);
            }
            preparedPotionObject.SetActive(hasPotion);
            if (hasPotion)
            {
                HerbPair pair = session.PreparedPotion.HerbPair;
                preparedPotionText.text = "Brewed Potion\n"
                    + DisplayName(pair.FirstHerbId) + " + " + DisplayName(pair.SecondHerbId);

                if (preparedPotionHerbIcon1 != null)
                {
                    Sprite icon1 = DisplayImage(pair.FirstHerbId);
                    preparedPotionHerbIcon1.sprite = icon1;
                    preparedPotionHerbIcon1.color = icon1 == null ? Color.clear : Color.white;
                }

                if (preparedPotionHerbIcon2 != null)
                {
                    Sprite icon2 = DisplayImage(pair.SecondHerbId);
                    preparedPotionHerbIcon2.sprite = icon2;
                    preparedPotionHerbIcon2.color = icon2 == null ? Color.clear : Color.white;
                }

            }
            RenderProgression(session);
            UpdateTutorialInteractionState();
        }

        private void RenderProgression(GameSession session)
        {
            if (progressionPanel == null || resolveExpense == null)
            {
                return;
            }

            // The last customer's coins must be collected by hand before the day closes.
            bool waitingForCoins = session.CurrentPhase == Witch.Domain.GamePhase.ClosingEncounter
                && PendingCounterCoins > 0;
            if (counterCoins != null) counterCoins.SetWaiting(waitingForCoins);
            if (waitingForCoins)
            {
                SetStatus("Collect the coins on the counter before closing the shop.");
                autoResolveExpenseAttempted = false;
                autoResolveExpenseError = null;
            }

            bool focusingSuspicion = suspicionFocus != null && suspicionFocus.IsPlaying;
            bool showSummary = session.CurrentPhase == Witch.Domain.GamePhase.DaySummary
                || session.CurrentPhase == Witch.Domain.GamePhase.Victory;
            bool showGameOver = session.CurrentPhase == Witch.Domain.GamePhase.GameOver;
            bool showClosingError = session.CurrentPhase == Witch.Domain.GamePhase.ClosingEncounter
                && !waitingForCoins
                && !string.IsNullOrWhiteSpace(autoResolveExpenseError);
            bool visible = !waitingForCoins
                && !focusingSuspicion
                && (showSummary || showGameOver || showClosingError);
            progressionPanel.SetActive(visible);
            if (!visible)
            {
                return;
            }

            continueButton.gameObject.SetActive(session.CurrentPhase == Witch.Domain.GamePhase.DaySummary);

            SetSummaryRowsVisible(showSummary);
            progressionText.gameObject.SetActive(showGameOver || showClosingError);

            if (showClosingError)
            {
                progressionText.text = "SHOP CLOSED\n" + autoResolveExpenseError;
                return;
            }

            if (showGameOver)
            {
                progressionText.text = "GAME OVER\nNot enough coins. Nothing was deducted.";
                return;
            }

            if (showSummary)
            {
                DaySummaryData summary = session.LastDaySummary;
                int dayNumber = summary == null ? session.RunState.CurrentDayNumber : summary.DayNumber;
                dayTitleText.text = session.CurrentPhase == Witch.Domain.GamePhase.Victory
                    ? dayNumber + " DAYS COMPLETE"
                    : "DAY " + dayNumber + " COMPLETE";
                correctOrdersText.text = "Correct Orders									.................................." + (summary == null ? 0 : summary.CorrectOrders);
                wrongOrdersText.text = "Wrong Orders									.................................." + (summary == null ? 0 : summary.WrongOrders);
                coinsEarnedText.text = "Your Today's Income		    		    ................................$" + (summary == null ? 0 : summary.CoinsEarned);
                taxPaidText.text = "Tax													................................$" + (summary == null ? 0 : summary.TaxPaid);
                coinsRemainingText.text = "Current Total Coin												   $" + (summary == null ? 0 : summary.CoinsRemaining);
            }
        }

        private void SetSummaryRowsVisible(bool visible)
        {
            if (dayTitleText != null) dayTitleText.gameObject.SetActive(visible);
            if (correctOrdersText != null) correctOrdersText.gameObject.SetActive(visible);
            if (wrongOrdersText != null) wrongOrdersText.gameObject.SetActive(visible);
            if (coinsEarnedText != null) coinsEarnedText.gameObject.SetActive(visible);
            if (taxPaidText != null) taxPaidText.gameObject.SetActive(visible);
            if (coinsRemainingText != null) coinsRemainingText.gameObject.SetActive(visible);
        }

        private void OnContinueClicked() => RunUiAction(ContinueNextDayAsync);
        private void OnMainMenuClicked() => RunUiAction(ReturnToMainMenuAsync);

        private async void RunUiAction(Func<Task> action)
        {
            if (actionInProgress || action == null)
            {
                return;
            }

            if (dayTransition != null && dayTransition.IsPlaying)
            {
                return;
            }

            actionInProgress = true;
            try
            {
                await action();
            }
            catch (OperationCanceledException) when (lifetimeCancellation == null || lifetimeCancellation.IsCancellationRequested)
            {
                // Scene closed while operation was in flight.
            }
            catch (Exception exception)
            {
                SetStatus("Action failed: " + exception.Message);
                Debug.LogException(exception, this);
            }
            finally
            {
                actionInProgress = false;
            }
        }

        private string DisplayName(string herbId)
        {
            foreach (ShopHerbViewData herb in setup.Herbs)
            {
                if (string.Equals(herb.StableId, herbId, StringComparison.Ordinal))
                {
                    return herb.DisplayName;
                }
            }

            return herbId;
        }

        private Sprite DisplayImage(string herbId)
        {
            foreach (ShopHerbViewData herb in setup.Herbs)
            {
                if (string.Equals(herb.StableId, herbId, StringComparison.Ordinal))
                {
                    return herb.HerbIllustration;
                }
            }

            return null;
        }

        private void ShowHerbImages(Image[] images, string herbId)
        {
            if (images == null) return;
            Sprite sprite = herbId == null ? null : DisplayImage(herbId);
            foreach (Image image in images)
            {
                if (image == null) continue;
                image.sprite = sprite;
                image.enabled = sprite != null;
            }
        }

        private void SetStatus(string message)
        {
            LastStatus = message;
        }

        /// <summary>Coins on the counter the player has not tapped yet (already counted in the run state).</summary>
        public int PendingCounterCoins => counterCoins != null ? counterCoins.Amount : 0;

        /// <summary>Collects the counter coins as if the player tapped them.</summary>
        public void CollectCounterCoins()
        {
            if (counterCoins != null) counterCoins.Collect();
        }

        private void OnCounterCoinsCollected(int amount)
        {
            audioService?.Play(GameAudioCueIds.Payment);
            if (moneyGainPopUp != null) moneyGainPopUp.Show(amount);
            SetStatus("Coins collected.");
            Render();
        }

        // Leaving the shop (Main Menu, Rewind): the money is already saved, so just tidy the counter.
        private void ClearCounterCoins()
        {
            if (counterCoins != null && counterCoins.HasCoins) counterCoins.Clear();
        }

        private void ConfigureGuidebook()
        {
            guidebookPages = setup.GuidebookPages;
            if (guidebookPages.Count == 0)
            {
                guidebookPages = new[] { new GuidebookPageViewData(1, "No remedies unlocked.") };
            }
            guidebookPageIndex = 0;
            RenderGuidebookPage();
        }

        private void RenderGuidebookPage()
        {
            int leftIndex = guidebookPageIndex - (guidebookPageIndex % 2);
            int rightIndex = leftIndex + 1;

            RenderGuidebookSide(guidebookLeftPage, leftIndex, GuidebookPageSide.Left);
            RenderGuidebookSide(guidebookRightPage, rightIndex, GuidebookPageSide.Right);

            SetGuidebookContentsButtonVisible(leftIndex > 0);
            UpdateGuidebookNavigation();
        }

        /// <summary>Renders one side of the book together with its page number (left = odd, right = even).</summary>
        private void RenderGuidebookSide(GuidebookPageView view, int index, GuidebookPageSide side)
        {
            view.Render(PageAt(index), side);
            view.SetPageNumber(PageNumberLabel(index));
        }

        /// <summary>Hides Previous on the first spread and Next on the last spread.</summary>
        private void UpdateGuidebookNavigation()
        {
            int leftIndex = guidebookPageIndex - (guidebookPageIndex % 2);
            guidebookPreviousButton.gameObject.SetActive(leftIndex > 0);
            guidebookNextButton.gameObject.SetActive(leftIndex + 2 < guidebookPages.Count);
        }

        private void SetGuidebookContentsButtonVisible(bool visible)
        {
            if (guidebookContentsButton != null) guidebookContentsButton.gameObject.SetActive(visible);
        }

        private string PageNumberLabel(int index)
        {
            return index >= 0 && index < guidebookPages.Count
                ? (index + 1) + " / " + guidebookPages.Count
                : string.Empty;
        }

        private GuidebookPageViewData PageAt(int index)
        {
            return index >= 0 && index < guidebookPages.Count ? guidebookPages[index] : null;
        }

        private void SetTutorialHighlight(int activeIndex)
        {
            // Legacy highlight system replaced by TutorialMaskView in guided tutorial.
            if (tutorialHighlights == null) return;
            for (int index = 0; index < tutorialHighlights.Length; index++)
            {
                if (tutorialHighlights[index] != null)
                {
                    tutorialHighlights[index].SetActive(false);
                }
            }
        }

        private bool IsResumingEncounter() =>
            ActiveSession.CurrentPhase == Witch.Domain.GamePhase.CustomerEncounter
            && ActiveSession.AttemptState.ActiveOrder != null;

        private void ResumeEncounter()
        {
            ResetAutoResolveExpenseState();
            string orderId = ActiveSession.AttemptState.ActiveOrder.OrderId;
            ShopEncounterViewData encounter = setup.Encounters
                .FirstOrDefault(e => e.Order != null && e.Order.OrderId == orderId);

            Debug.Log($"[Resume] order={orderId} found={encounter != null}");
            if (encounter != null)
            {
                hudView.SetCustomer(
                    encounter.CustomerName,
                    encounter.OrderBubbles,
                    encounter.CustomerIllustration);
            }

            if (!guidedTutorialTimerFrozen && !waitingForTransitionRelease)
            {
                setTimerPaused?.Execute(ActiveSession, false); // clock continues from the remaining time
            }
            SetStatus("The spell worked. Time is back. Serve the order again.");
        }

        private void HandleDayTransitionReveal()
        {
            try
            {
                if (dayTransition == null || ActiveSession == null || !dayTransition.HasPendingShopReveal)
                {
                    waitingForTransitionRelease = waitingForTransitionRelease && pendingStartAfterReveal;
                    return;
                }

                int day = ActiveSession.RunState.CurrentDayNumber;
                dayTransition.SetDayNumber(day);
                hudView.ResetDayCycleToMorning();
                if (setTimerPaused != null)
                {
                    setTimerPaused.Execute(ActiveSession, true);
                    waitingForTransitionRelease = true;
                }
                dayTransition.RevealIfPending(ReleaseTimerAfterTransition);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, this);
                if (dayTransition != null)
                {
                    dayTransition.ForceReveal(ReleaseTimerAfterTransition, "HandleDayTransitionReveal-exception");
                }
            }
        }

        private void ReleaseTimerAfterTransition()
        {
            try
            {
                if (ActiveSession == null)
                {
                    waitingForTransitionRelease = false;
                    pendingStartAfterReveal = false;
                    return;
                }

                if (pendingStartAfterReveal)
                {
                    if (pendingStartIsResume)
                    {
                        StartResumedEncounterAfterReveal();
                    }
                    else
                    {
                        BeginArrival();
                    }
                    pendingStartAfterReveal = false;
                    pendingStartIsResume = false;
                    Render();
                }

                waitingForTransitionRelease = false;
                if (setTimerPaused == null) return;

                // Day 1 guided tutorial keeps timer frozen until serve/skip (existing behavior).
                if (guidedTutorialActive && guidedTutorialTimerFrozen)
                {
                    return;
                }

                if (ShouldRunGuidedTutorial() && ActiveSession.CurrentPhase == Witch.Domain.GamePhase.CustomerEncounter && IsTutorialOrderActive())
                {
                    return;
                }

                setTimerPaused.Execute(ActiveSession, false);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex, this);
            }
        }

        private void StartResumedEncounterAfterReveal()
        {
            ResetAutoResolveExpenseState();
            string orderId = ActiveSession.AttemptState.ActiveOrder.OrderId;
            ShopEncounterViewData encounter = setup.Encounters
                .FirstOrDefault(e => e.Order != null && e.Order.OrderId == orderId);

            Debug.Log($"[Resume] order={orderId} found={encounter != null}");
            if (encounter != null)
            {
                hudView.SetCustomer(
                    encounter.CustomerName,
                    encounter.OrderBubbles,
                    encounter.CustomerIllustration);
            }

            SetStatus("The spell worked. Time is back. Serve the order again.");
            // Timer release handled by ReleaseTimerAfterTransition (frozen only for guided tutorial).
        }

    }

}
