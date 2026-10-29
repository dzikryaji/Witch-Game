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
using Witch.Domain.Potions;

namespace Witch.Presentation.Shop
{
    public sealed class ShopShellPresenter : MonoBehaviour
    {
        [Header("Authored View")]
        [SerializeField] private ShopHudView hudView;
        [SerializeField] private Text guidebookText;
        [SerializeField] private Button[] herbButtons;
        [SerializeField] private HerbDragItem herbButtonPrefab;
        [SerializeField] private RectTransform herbButtonContainer;
        [SerializeField] private GrinderDropZone grinderDropZone;
        [SerializeField] private CauldronDropZone cauldronDropZone;
        [SerializeField] private DiscardDropZone discardDropZone;
        [SerializeField] private PotionServeDropZone potionServeDropZone;
        [SerializeField] private Button grindButton;
        [SerializeField] private Button ejectGrinderButton;
        [SerializeField] private Button addToPotButton;
        [SerializeField] private Button discardGroundButton;
        [SerializeField] private Button undoLastIngredientButton;
        [SerializeField] private Button emptyPotButton;
        [SerializeField] private Button brewButton;
        [SerializeField] private Button discardButton;
        [SerializeField] private Button serveButton;
        [SerializeField] private Button guidebookButton;
        [SerializeField] private Button guidebookPreviousButton;
        [SerializeField] private Button guidebookNextButton;
        [SerializeField] private Button pauseButton;
        [SerializeField] private GameObject guidebookPanel;
        [SerializeField] private Text guidebookPageText;
        [SerializeField] private Text guidebookPageNumberText;
        [SerializeField] private Image guidebookIllustration;
        [SerializeField] private Text grinderInputText;
        [SerializeField] private GameObject groundHerbObject;
        [SerializeField] private Text groundHerbText;
        [SerializeField] private GameObject preparedPotionObject;
        [SerializeField] private Text preparedPotionText;
        [SerializeField] private GameObject badPotionIndicator;
        [SerializeField] private GameObject hunterWarningPanel;
        [SerializeField] private GameObject progressionPanel;
        [SerializeField] private Text progressionText;
        [SerializeField] private Button resolveExpenseButton;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private GameObject tutorialPanel;
        [SerializeField] private Text tutorialText;
        [SerializeField] private Button tutorialContinueButton;
        [SerializeField] private GameObject[] tutorialHighlights;
        [Header("Input Actions")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private MinigameIntroPopupView minigameIntroPopup;

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
        private int tutorialBeat;
        private bool viewBound;
        private CancellationTokenSource lifetimeCancellation;
        private bool actionInProgress;
        private InputAction pauseAction;
        private InputAction guidebookAction;
        private readonly List<HerbDragItem> herbSlots = new List<HerbDragItem>();
        private string loadedGrinderHerbId;
        private IReadOnlyList<GuidebookPageViewData> guidebookPages = Array.Empty<GuidebookPageViewData>();
        private int guidebookPageIndex;
        private Coroutine badPotionRoutine;

        public bool IsInitialized => sessionStore != null;
        public GameSession ActiveSession => sessionStore?.ActiveSession;
        public string LastStatus { get; private set; }
        public bool GuidebookVisible => guidebookPanel != null && guidebookPanel.activeSelf;
        public bool PauseVisible => pausePanel != null && pausePanel.activeSelf;
        public bool TutorialVisible => tutorialPanel != null && tutorialPanel.activeSelf;
        public bool MinigameIntroVisible => minigameIntroPopup != null && minigameIntroPopup.IsVisible;
        public bool HasAuthoredView => hudView != null
            && hudView.IsAuthored
            && guidebookText != null
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
            && addToPotButton != null
            && discardGroundButton != null
            && undoLastIngredientButton != null
            && emptyPotButton != null
            && brewButton != null
            && discardButton != null
            && serveButton != null
            && guidebookPreviousButton != null
            && guidebookNextButton != null
            && pauseButton != null
            && guidebookPanel != null
            && guidebookPageText != null
            && guidebookPageNumberText != null
            && guidebookIllustration != null
            && grinderInputText != null
            && groundHerbObject != null
            && groundHerbText != null
            && preparedPotionObject != null
            && preparedPotionText != null
            && badPotionIndicator != null
            && hunterWarningPanel != null
            && progressionPanel != null
            && progressionText != null
            && resolveExpenseButton != null
            && retryButton != null
            && continueButton != null
            && pausePanel != null
            && resumeButton != null
            && mainMenuButton != null
            && tutorialPanel != null
            && tutorialText != null
            && tutorialContinueButton != null
            && tutorialHighlights != null
            && tutorialHighlights.Length == 5
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
                setup.RandomizeEncounters);

            RequireAndBindAuthoredView();
            ConfigureHerbButtons();
            ConfigureGuidebook();
            guidebookPanel.SetActive(true);
            hunterWarningPanel.SetActive(false);
            progressionPanel.SetActive(false);
            pausePanel.SetActive(false);
            tutorialPanel.SetActive(false);
            groundHerbObject.SetActive(false);
            preparedPotionObject.SetActive(false);
            badPotionIndicator.SetActive(false);
            hudView.ClearCustomer();
            audioService?.PlayMusic(GameAudioCueIds.ShopMusic);
            audioService?.PlayAmbience(GameAudioCueIds.ShopAmbience);
            if (setup.ShowOpeningTutorial) ShowTutorialBeat();
            else BeginArrival();
            Render();
            if ((ActiveSession.CurrentPhase == Witch.Domain.GamePhase.RewindPending
                    || (ActiveSession.CurrentPhase == Witch.Domain.GamePhase.RewindMinigame
                        && ActiveSession.AttemptMetadata.PendingRewind))
                && enterRewind != null)
            {
                ResumePendingRewind();
            }
        }

        private void Update()
        {
            if (!IsInitialized || updateTimer == null || ActiveSession == null || Time.unscaledTime < nextRefresh)
            {
                return;
            }

            nextRefresh = Time.unscaledTime + 0.1f;
            updateTimer.Execute(ActiveSession);
            Render();
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
            if (setTimerPaused != null && !TutorialVisible && !MinigameIntroVisible) TogglePause();
        }

        private void HandleGuidebookAction(InputAction.CallbackContext context)
        {
            if (IsInitialized && !TutorialVisible && !MinigameIntroVisible) ToggleGuidebook();
        }

        public PreparationResult GrindHerb(string herbId)
        {
            PreparationResult result = preparation.Grind(ActiveSession, herbId);
            SetStatus(result.Succeeded
                ? setup.DayNumber == 1
                    ? "Ground " + DisplayName(herbId) + ". Add it to the pot, then prepare the second herb."
                    : "Ground " + DisplayName(herbId) + "."
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

            if (string.IsNullOrWhiteSpace(herbId)) return false;
            loadedGrinderHerbId = herbId;
            SetStatus(DisplayName(herbId) + " loaded. Press Grind, or Eject to change it.");
            Render();
            return true;
        }

        public PreparationResult GrindLoadedHerb()
        {
            if (string.IsNullOrWhiteSpace(loadedGrinderHerbId))
            {
                PreparationResult failure = PreparationResult.Failure("Drop a herb into the grinder first.");
                SetStatus(failure.ErrorMessage);
                return failure;
            }

            PreparationResult result = GrindHerb(loadedGrinderHerbId);
            if (result.Succeeded)
            {
                loadedGrinderHerbId = null;
                Render();
            }
            return result;
        }

        public void EjectLoadedHerb()
        {
            if (string.IsNullOrWhiteSpace(loadedGrinderHerbId))
            {
                SetStatus("Grinder is already empty.");
                return;
            }
            loadedGrinderHerbId = null;
            SetStatus("Herb returned to shelf.");
            Render();
        }

        public PreparationResult AddGroundHerbToPot()
        {
            PreparationResult result = preparation.AddToPot(ActiveSession);
            SetStatus(result.Succeeded
                ? setup.DayNumber == 1 ? "Ground herb added to pot. Brew when both exact herbs are inside." : "Ground herb added to pot."
                : result.ErrorMessage);
            if (result.Succeeded) audioService?.Play(GameAudioCueIds.Pot);
            Render();
            return result;
        }

        public PreparationResult DiscardGroundHerb()
        {
            PreparationResult result = preparation.DiscardGroundHerb(ActiveSession);
            SetStatus(result.Succeeded ? "Ground herb discarded." : result.ErrorMessage);
            if (result.Succeeded) audioService?.Play(GameAudioCueIds.Discard);
            Render();
            return result;
        }

        public PreparationResult UndoLastPotIngredient()
        {
            PreparationResult result = preparation.UndoLastPotIngredient(ActiveSession);
            SetStatus(result.Succeeded ? "Last ingredient removed from cauldron." : result.ErrorMessage);
            if (result.Succeeded) audioService?.Play(GameAudioCueIds.Discard);
            Render();
            return result;
        }

        public PreparationResult Brew()
        {
            PreparationResult result = preparation.Brew(ActiveSession);
            string message = !result.Succeeded
                ? result.ErrorMessage
                : "Potion brewed. Drag it into the customer Potion slot.";
            SetStatus(message);
            if (result.Succeeded)
            {
                audioService?.Play(GameAudioCueIds.BrewSuccess);
            }
            Render();
            return result;
        }

        public PreparationResult Discard()
        {
            loadedGrinderHerbId = null;
            PreparationResult result = preparation.Discard(ActiveSession);
            SetStatus(result.Succeeded ? "Workbench cleared." : result.ErrorMessage);
            if (result.Succeeded) audioService?.Play(GameAudioCueIds.Discard);
            Render();
            return result;
        }

        public async Task<ServePotionResult> ServeAsync()
        {
            CancellationToken cancellationToken = lifetimeCancellation?.Token ?? CancellationToken.None;
            ServePotionResult result = await servePotion.ExecuteAsync(ActiveSession, cancellationToken);
            if (!result.Succeeded)
            {
                SetStatus(result.ErrorMessage);
            }
            else if (result.Outcome.Kind == ServiceOutcomeKind.Correct)
            {
                SetStatus("Correct order. Coins received: " + result.Outcome.Payment + ". Save enough for the daily expense.");
                audioService?.Play(GameAudioCueIds.OrderCorrect);
                audioService?.Play(GameAudioCueIds.Payment);
            }
            else
            {
                SetStatus("Wrong potion. No coins. Suspicion increased. At 3, Hunters investigate the suspicious 'herbalist'.");
                audioService?.Play(GameAudioCueIds.OrderWrong);
            }

            Render();
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

        public async Task<ExpenseResolutionResult> ResolveExpenseAsync()
        {
            CancellationToken cancellationToken = lifetimeCancellation?.Token ?? CancellationToken.None;
            ExpenseResolutionResult result = await resolveExpense.ExecuteAsync(ActiveSession, cancellationToken);
            SetStatus(result.Succeeded ? "Daily expense resolved." : result.ErrorMessage);
            if (result.Succeeded && ActiveSession.CurrentPhase == Witch.Domain.GamePhase.GameOver)
            {
                audioService?.Play(GameAudioCueIds.ExpenseFail);
                audioService?.PlayMusic(GameAudioCueIds.GameOverMusic);
            }
            else if (result.Succeeded && ActiveSession.CurrentPhase == Witch.Domain.GamePhase.Victory)
            {
                audioService?.Play(GameAudioCueIds.ExpensePay);
                audioService?.PlayMusic(GameAudioCueIds.VictoryMusic);
            }
            else if (result.Succeeded)
            {
                audioService?.Play(GameAudioCueIds.ExpensePay);
            }
            Render();
            return result;
        }

        public async Task<ExpenseResolutionResult> RetryDayAsync()
        {
            CancellationToken cancellationToken = lifetimeCancellation?.Token ?? CancellationToken.None;
            ExpenseResolutionResult result = await retryDay.ExecuteAsync(ActiveSession, cancellationToken);
            SetStatus(result.Succeeded ? "Same day restored." : result.ErrorMessage);
            return result;
        }

        public async Task<ExpenseResolutionResult> ContinueNextDayAsync()
        {
            CancellationToken cancellationToken = lifetimeCancellation?.Token ?? CancellationToken.None;
            ExpenseResolutionResult result = await continueToNextDay.ExecuteAsync(ActiveSession, cancellationToken);
            SetStatus(result.Succeeded ? "Opening next day." : result.ErrorMessage);
            return result;
        }

        public void ToggleGuidebook()
        {
            if (guidebookPanel == null) return;
            guidebookPanel.SetActive(true);
            SetStatus("Guidebook stays on the table. Timer continues while you read.");
        }

        public void PreviousGuidebookPage()
        {
            if (guidebookPages.Count == 0) return;
            guidebookPageIndex = (guidebookPageIndex - 1 + guidebookPages.Count) % guidebookPages.Count;
            RenderGuidebookPage();
            audioService?.Play(GameAudioCueIds.GuidebookClose);
        }

        public void NextGuidebookPage()
        {
            if (guidebookPages.Count == 0) return;
            guidebookPageIndex = (guidebookPageIndex + 1) % guidebookPages.Count;
            RenderGuidebookPage();
            audioService?.Play(GameAudioCueIds.GuidebookOpen);
        }

        public void AdvanceTutorial()
        {
            if (!TutorialVisible) return;
            tutorialBeat++;
            if (tutorialBeat >= 5)
            {
                tutorialPanel.SetActive(false);
                SetTutorialHighlight(-1);
                audioService?.Play(GameAudioCueIds.UiConfirm);
                BeginArrival();
                Render();
                return;
            }
            ShowTutorialBeat();
        }

        public void TogglePause()
        {
            if (pausePanel == null || setTimerPaused == null || ActiveSession == null) return;
            bool paused = !pausePanel.activeSelf;
            setTimerPaused.Execute(ActiveSession, paused);
            pausePanel.SetActive(paused);
            audioService?.Play(paused ? GameAudioCueIds.UiConfirm : GameAudioCueIds.UiBack);
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
                setTimerPaused.Execute(ActiveSession, false);
                pausePanel.SetActive(false);
            }

            audioService?.Play(GameAudioCueIds.UiBack);
            SceneLoadResult result = await sceneLoader.LoadAsync(
                SceneId.MainMenu,
                lifetimeCancellation?.Token ?? CancellationToken.None);
            if (!result.Succeeded)
            {
                SetStatus("Could not return to Main Menu: " + result.ErrorMessage);
            }
        }

        private void BeginArrival()
        {
            if (ActiveSession == null)
            {
                SetStatus("No active run.");
                return;
            }

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
                ShopEncounterViewData encounter = FindNextEncounter();
                if (encounter == null)
                {
                    SetStatus("No more authored customers today. The clock continues to closing.");
                    return;
                }

                AdmitCustomerResult arrival = admitCustomer.Execute(
                    ActiveSession,
                    new AdmitCustomerCommand(encounter.CustomerId, encounter.Order));
                SetStatus(arrival.Succeeded
                    ? setup.DayNumber == 1
                        ? "DING! " + encounter.CustomerName + " enters. Read the order, consult the guidebook, and prepare the exact herb pair."
                        : "DING! " + encounter.CustomerName + " enters the shop."
                    : arrival.ErrorMessage);
                if (arrival.Succeeded) audioService?.Play(GameAudioCueIds.CustomerBell);
                if (arrival.Succeeded)
                {
                    hudView.SetCustomer(encounter.CustomerName, encounter.ClueText);
                }
            }
        }

        private void ShowTutorialBeat()
        {
            if (tutorialPanel == null) return;
            string[] beats =
            {
                "Orders and customer clues appear here after the bell.",
                "Guidebook stays on the table. Turn pages to compare remedies.",
                "Drag an unlocked herb from the shelf into the grinder.",
                "Press Grind. Drag the ground herb into the cauldron. Undo or empty it if needed.",
                "Brew two herbs, then drag the finished potion into the customer Potion slot."
            };
            tutorialText.text = beats[Math.Min(tutorialBeat, beats.Length - 1)];
            tutorialPanel.SetActive(true);
            SetTutorialHighlight(Math.Min(tutorialBeat, tutorialHighlights.Length - 1));
        }

        private Task EnterPendingRewindAsync(CancellationToken cancellationToken)
        {
            SetStatus("KNOCK. KNOCK. Hunters suspect poisoning. Cast the memory spell before they enter; the day will rewind.");
            audioService?.Play(GameAudioCueIds.HunterWarning);
            audioService?.Play(GameAudioCueIds.TimeStop);
            audioService?.StopMusic();
            audioService?.StopAmbience();

            if (minigameIntroPopup != null && minigameIntroPopup.IsAuthored)
            {
                minigameIntroPopup.Show();
                return Task.CompletedTask; // wait for the player to press the button
            }

            hunterWarningPanel.SetActive(true); // fallback when the popup isn't wired
            return StartRewindAsync(cancellationToken);
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

            hunterWarningPanel.SetActive(false);
            SetStatus(error + " Try again by serving or reloading the run.");
            audioService?.PlayMusic(GameAudioCueIds.ShopMusic);
        }

        private void OnMinigameIntroClicked()
        {
            if (actionInProgress) return;
            minigameIntroPopup.SetInteractable(false); // prevents double clicks
            audioService?.Play(GameAudioCueIds.UiConfirm);
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
            addToPotButton.onClick.AddListener(() => AddGroundHerbToPot());
            discardGroundButton.onClick.AddListener(() => DiscardGroundHerb());
            undoLastIngredientButton.onClick.AddListener(() => UndoLastPotIngredient());
            emptyPotButton.onClick.AddListener(() => Discard());
            brewButton.onClick.AddListener(() => Brew());
            discardButton.onClick.AddListener(() => Discard());
            serveButton.onClick.AddListener(OnServeClicked);
            if (guidebookButton != null) guidebookButton.onClick.AddListener(ToggleGuidebook);
            guidebookPreviousButton.onClick.AddListener(PreviousGuidebookPage);
            guidebookNextButton.onClick.AddListener(NextGuidebookPage);
            pauseButton.onClick.AddListener(TogglePause);
            resumeButton.onClick.AddListener(TogglePause);
            mainMenuButton.onClick.AddListener(OnMainMenuClicked);
            resolveExpenseButton.onClick.AddListener(OnResolveExpenseClicked);
            retryButton.onClick.AddListener(OnRetryClicked);
            continueButton.onClick.AddListener(OnContinueClicked);
            tutorialContinueButton.onClick.AddListener(AdvanceTutorial);
            viewBound = true;
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
                    continue;
                }

                ShopHerbViewData herb = setup.Herbs[index];
                button.interactable = true;
                button.gameObject.name = "Herb " + herb.StableId;
                Text label = button.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    label.text = herb.DisplayName;
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

        private void OnServeClicked() => RunUiAction(ServeAsync);

        public void RequestServe() => RunUiAction(ServeAsync);

        private void Render()
        {
            GameSession session = ActiveSession;
            if (session == null || hudView == null)
            {
                return;
            }

            hudView.Render(session, DisplayName);
            grinderInputText.text = string.IsNullOrWhiteSpace(loadedGrinderHerbId)
                ? "Drop herb here"
                : DisplayName(loadedGrinderHerbId);
            bool hasGroundHerb = !string.IsNullOrWhiteSpace(session.GroundHerbId);
            groundHerbObject.SetActive(hasGroundHerb);
            if (hasGroundHerb) groundHerbText.text = "Ground\n" + DisplayName(session.GroundHerbId);
            bool hasPotion = session.PreparedRecipe != null;
            preparedPotionObject.SetActive(hasPotion);
            if (hasPotion)
            {
                HerbPair pair = session.PreparedRecipe.HerbPair;
                preparedPotionText.text = "Brewed Potion\n"
                    + DisplayName(pair.FirstHerbId) + " + " + DisplayName(pair.SecondHerbId);
            }
            RenderProgression(session);
        }

        private void RenderProgression(GameSession session)
        {
            if (progressionPanel == null || resolveExpense == null)
            {
                return;
            }

            bool visible = session.CurrentPhase == Witch.Domain.GamePhase.ClosingEncounter
                || session.CurrentPhase == Witch.Domain.GamePhase.GameOver
                || session.CurrentPhase == Witch.Domain.GamePhase.DaySummary
                || session.CurrentPhase == Witch.Domain.GamePhase.Victory;
            progressionPanel.SetActive(visible);
            if (!visible)
            {
                return;
            }

            resolveExpenseButton.gameObject.SetActive(session.CurrentPhase == Witch.Domain.GamePhase.ClosingEncounter);
            retryButton.gameObject.SetActive(session.CurrentPhase == Witch.Domain.GamePhase.GameOver);
            continueButton.gameObject.SetActive(session.CurrentPhase == Witch.Domain.GamePhase.DaySummary);
            if (session.CurrentPhase == Witch.Domain.GamePhase.ClosingEncounter)
            {
                progressionText.text = "SHOP CLOSED\nCoins: " + session.RunState.Money + "\nExpense: " + session.DayStartSnapshot.ExpenseValue;
            }
            else if (session.CurrentPhase == Witch.Domain.GamePhase.GameOver)
            {
                progressionText.text = "GAME OVER\nNot enough coins. Nothing was deducted.\nYou may retry the same day.";
            }
            else if (session.CurrentPhase == Witch.Domain.GamePhase.DaySummary)
            {
                progressionText.text = "EXPENSE PAID\nRemaining coins: " + session.RunState.Money + "\nDay " + session.RunState.CurrentDayNumber + " awaits.";
            }
            else
            {
                progressionText.text = "THREE DAYS COMPLETE\nYou survived the current prototype.\nThe full game is planned for 14 days.";
            }
        }

        private void OnResolveExpenseClicked() => RunUiAction(ResolveExpenseAsync);
        private void OnRetryClicked() => RunUiAction(RetryDayAsync);
        private void OnContinueClicked() => RunUiAction(ContinueNextDayAsync);
        private void OnMainMenuClicked() => RunUiAction(ReturnToMainMenuAsync);

        private async void RunUiAction(Func<Task> action)
        {
            if (actionInProgress || action == null)
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

        private void SetStatus(string message)
        {
            LastStatus = message;
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
            GuidebookPageViewData page = guidebookPages[Math.Min(guidebookPageIndex, guidebookPages.Count - 1)];
            guidebookText.text = page.Text;
            guidebookPageText.text = page.Text;
            int totalPages = guidebookPages.Max(value => value.PageNumber);
            guidebookPageNumberText.text = page.PageNumber + " / " + totalPages;
            if (guidebookIllustration != null)
            {
                guidebookIllustration.sprite = page.Illustration;
                guidebookIllustration.gameObject.SetActive(page.Illustration != null);
            }
        }

        private void SetTutorialHighlight(int activeIndex)
        {
            for (int index = 0; index < tutorialHighlights.Length; index++)
            {
                if (tutorialHighlights[index] != null)
                {
                    tutorialHighlights[index].SetActive(index == activeIndex);
                }
            }
        }

        private void ShowBadPotionBriefly()
        {
            if (badPotionRoutine != null) StopCoroutine(badPotionRoutine);
            badPotionRoutine = StartCoroutine(BadPotionRoutine());
        }

        private System.Collections.IEnumerator BadPotionRoutine()
        {
            badPotionIndicator.SetActive(true);
            yield return new WaitForSecondsRealtime(0.9f);
            badPotionIndicator.SetActive(false);
            badPotionRoutine = null;
        }

    }

}
