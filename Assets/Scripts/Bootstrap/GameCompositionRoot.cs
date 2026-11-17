using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Witch.Application.Flow;
using Witch.Application.Ports;
using Witch.Infrastructure.Content;
using Witch.Infrastructure.Audio;
using Witch.Infrastructure.Persistence;
using Witch.Infrastructure.Platform;
using Witch.Infrastructure.Scenes;
using Witch.Presentation;
using Witch.Presentation.MainMenu;
using Witch.Presentation.Rewind;
using Witch.Presentation.Shop;
using Witch.Presentation.Settings;
#if UNITY_EDITOR
using UnityEditor;
using Witch.Domain;
using Witch.Domain.Progression;
#endif

namespace Witch.Bootstrap
{
    [DefaultExecutionOrder(-1000)]
    public sealed class GameCompositionRoot : MonoBehaviour
    {
        [SerializeField] private GameContentCatalogDefinition contentCatalog;
        [SerializeField] private int randomSeed = 20261024;
        [Header("Performance")]
        [SerializeField, Min(30)] private int targetFrameRate = 60;

        private IGameSessionStore sessionStore;
        private IStartNewGameUseCase startNewGame;
        private ILoadRunUseCase loadRun;
        private IGameSettingsService settingsService;
        private IApplicationLifecycle applicationLifecycle;
        private ISceneLoader sceneLoader;
        private ShopPresentationSetupFactory shopPresentationFactory;
        private OpenShopUseCase openShop;
        private AdmitCustomerUseCase admitCustomer;
        private PotionPreparationUseCase potionPreparation;
        private ServePotionUseCase servePotion;
        private UpdateShopTimerUseCase updateShopTimer;
        private EnterRewindUseCase enterRewind;
        private CompleteRewindUseCase completeRewind;
        private ResolveExpenseUseCase resolveExpense;
        private RetryDayUseCase retryDay;
        private RestartDayUseCase restartDay;
        private ContinueToNextDayUseCase continueToNextDay;
        private MarkTutorialFlagUseCase markTutorialFlag;
        private IGameAudioService audioService;
        private IGameCenterService gameCenter;
        private SetShopTimerPausedUseCase setShopTimerPaused;
        private ISaveRepository runRepository;
        private GameRuntimeContent runtimeContent;
        private bool compositionSucceeded;
#if UNITY_EDITOR
        private bool debugSessionActive;
#endif

        public static GameCompositionRoot Instance { get; private set; }
        public bool IsReady { get; private set; }
        public string StartupError { get; private set; }
        public IGameSessionStore SessionStore => sessionStore;
        public DayTransitionController DayTransition { get; private set; }
#if UNITY_EDITOR
        public bool IsDebugSessionActive => debugSessionActive;
#endif

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            QualitySettings.vSyncCount = 0;
            UnityEngine.Application.targetFrameRate = targetFrameRate;
            DontDestroyOnLoad(gameObject);
            DayTransition = EnsureDayTransition();
            SceneManager.sceneLoaded += HandleSceneLoaded;
            Compose();
        }

        private async void Start()
        {
            if (!compositionSucceeded)
            {
                return;
            }

            try
            {
                await settingsService.LoadAsync();
                if (audioService is UnityAudioService authoredAudio
                    && !authoredAudio.TryValidateRequiredCues(out string audioError))
                {
                    FailStartup(audioError);
                    return;
                }
                audioService.ApplySettings(settingsService.Current);
                IsReady = true;
                Inject(SceneManager.GetActiveScene());
#if UNITY_EDITOR
                if (!debugSessionActive)
                {
                    _ = InitializeGameCenterAsync();
                }
#else
                _ = InitializeGameCenterAsync();
#endif
                await OpenStartupSceneAsync();
            }
            catch (Exception exception)
            {
                FailStartup("Bootstrap initialization failed: " + exception.Message);
            }
        }

        /// <summary>
        /// Initializes the local Game Center player without blocking startup.
        /// Apple recommends initializing at launch; a decline, restriction,
        /// or offline state keeps every other feature working.
        /// </summary>
        private async System.Threading.Tasks.Task InitializeGameCenterAsync()
        {
#if UNITY_EDITOR
            if (debugSessionActive)
            {
                return;
            }
#endif
            try
            {
                ServiceResult result = await gameCenter.InitializeAsync();
                if (!result.Succeeded)
                {
                    Debug.Log("[GameCenter] Unavailable at startup: " + result.ErrorMessage);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[GameCenter] Startup initialization failed: " + exception.Message);
            }
        }

        private void OnDestroy()
        {
            if (Instance != this)
            {
                return;
            }

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            (gameCenter as IDisposable)?.Dispose();
            Instance = null;
        }

        private void Compose()
        {
            if (contentCatalog == null)
            {
                FailStartup("Bootstrap requires a game content catalog.");
                return;
            }

            GameContentLoadResult contentResult = new GameContentAdapter().Load(contentCatalog);
            if (!contentResult.Succeeded)
            {
                FailStartup("Game content is invalid: " + string.Join(" | ", contentResult.Errors));
                return;
            }

            GameRuntimeContent content = contentResult.Content;
            runtimeContent = content;
            shopPresentationFactory = new ShopPresentationSetupFactory(content);
#if UNITY_EDITOR
            debugSessionActive = WitchDebugRuntime.HasActiveStartupOverride;
            runRepository = debugSessionActive
                ? new InMemoryRunRepository()
                : new JsonRunRepository(content.NewRunSetup.SaveVersion);
#else
            runRepository = new JsonRunRepository(content.NewRunSetup.SaveVersion);
#endif
            sessionStore = new GameSessionStore();
            sceneLoader = new UnitySceneLoader();
            settingsService = new GameSettingsService(new JsonSettingsRepository());
            applicationLifecycle = new UnityApplicationLifecycle();

            // Game Center is optional. The Apple adapter is composed only for
            // Apple player builds; the Editor and every unsupported platform
            // use the no-op service so startup never depends on GameKit.
#if UNITY_EDITOR
            gameCenter = new Witch.Infrastructure.GameCenter.NoOpGameCenterService();
#elif UNITY_STANDALONE_OSX
            gameCenter = new Witch.Infrastructure.GameCenter.AppleGameCenterService();
#else
            gameCenter = new Witch.Infrastructure.GameCenter.NoOpGameCenterService();
#endif

            UnityAudioService authoredAudio = gameObject.GetComponent<UnityAudioService>();
            if (authoredAudio == null || !authoredAudio.HasAuthoredChannels)
            {
                FailStartup("Bootstrap requires an authored UnityAudioService with Music, SFX, Ambience, and UI channels.");
                return;
            }
            audioService = authoredAudio;
            IClock clock = new UnityMonotonicClock();
            IRandomSource orderRandom = new SystemRandomSource();
            var potionRules = new Witch.Domain.Potions.PotionServiceRules(content.CorrectOrderPayment);
            openShop = new OpenShopUseCase();
            admitCustomer = new AdmitCustomerUseCase(clock);
            potionPreparation = new PotionPreparationUseCase(potionRules);
            var herbCategoryIds = content.HerbsById
                .Where(pair => pair.Value != null)
                .ToDictionary(pair => pair.Key, pair => pair.Value.CategoryId, StringComparer.Ordinal);
            servePotion = new ServePotionUseCase(
                potionRules,
                runRepository,
                gameCenter,
                herbCategoryIds);
            updateShopTimer = new UpdateShopTimerUseCase(clock);
            setShopTimerPaused = new SetShopTimerPausedUseCase(clock);
            enterRewind = new EnterRewindUseCase(
                runRepository,
                sceneLoader,
                orderRandom);
            completeRewind = new CompleteRewindUseCase(
                runRepository,
                sceneLoader,
                new DeterministicRandomSource(randomSeed + 2),
                gameCenter);
            resolveExpense = new ResolveExpenseUseCase(
                content.ExpenseRules,
                content.DayProgressionSetup,
                runRepository,
                orderRandom,
                clock,
                gameCenter);
            retryDay = new RetryDayUseCase(runRepository, sceneLoader);
            restartDay = new RestartDayUseCase(runRepository, sceneLoader);
            continueToNextDay = new ContinueToNextDayUseCase(sceneLoader);
            markTutorialFlag = new MarkTutorialFlagUseCase(runRepository);

            var startNewRun = new StartNewRunUseCase();
            startNewGame = new StartNewGameUseCase(
                startNewRun,
                content.NewRunSetup,
                runRepository,
                sessionStore,
                sceneLoader,
                clock,
                orderRandom);
            loadRun = new LoadRunUseCase(
                runRepository,
                runRepository,
                sessionStore,
                sceneLoader);
            compositionSucceeded = true;
        }

        private async System.Threading.Tasks.Task OpenStartupSceneAsync()
        {
            if (SceneManager.GetActiveScene().name != SceneId.Bootstrap.ToString())
            {
                return;
            }

#if UNITY_EDITOR
            if (debugSessionActive)
            {
                await OpenEditorDebugStartupAsync();
                return;
            }

            // Normal start from Main Menu without override: ensure debug state is clean.
            WitchDebugRuntime.ClearActiveJump();
#endif

            // Player builds (and normal editor starts) always enter Main Menu at Day 1.
            SceneLoadResult result = await sceneLoader.LoadAsync(SceneId.MainMenu);
            if (!result.Succeeded)
            {
                FailStartup("Bootstrap could not open MainMenu: " + result.ErrorMessage);
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (IsReady)
            {
                Inject(scene);
            }
        }

        private DayTransitionController EnsureDayTransition()
        {
            DayTransitionController existing = DayTransition;
            if (existing == null)
            {
                existing = GetComponentInChildren<DayTransitionController>(true);
            }
            if (existing == null)
            {
                existing = FindAnyObjectByType<DayTransitionController>(FindObjectsInactive.Include);
            }
            if (existing != null && existing.gameObject.scene.name != "DontDestroyOnLoad")
            {
                DontDestroyOnLoad(existing.gameObject);
            }
            if (existing == null)
            {
                existing = DayTransitionController.CreateRuntime(transform);
            }
            else
            {
            }
            return existing;
        }

        private void Inject(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MainMenuPresenter presenter in root.GetComponentsInChildren<MainMenuPresenter>(true))
                {
                    presenter.Initialize(
                        startNewGame,
                        loadRun,
                        settingsService,
                        applicationLifecycle,
                        audioService,
                        DayTransition,
                        runRepository as IDayStartSnapshotRepository,
                        runRepository as IActiveAttemptMetadataRepository);
                }

                foreach (ShopShellPresenter presenter in root.GetComponentsInChildren<ShopShellPresenter>(true))
                {
                    if (sessionStore.ActiveSession == null)
                    {
                        presenter.Initialize(sessionStore);
                    }
                    else
                    {
                        presenter.ConfigurePolish(setShopTimerPaused, audioService, settingsService.Current);
                        presenter.ConfigureNavigation(sceneLoader);
                        presenter.ConfigurePersistence(runRepository);
                        presenter.ConfigureTutorialFlag(markTutorialFlag);
                        presenter.ConfigureTransition(DayTransition);
                        presenter.ConfigureRestart(restartDay);
                        presenter.Initialize(
                            sessionStore,
                            openShop,
                            admitCustomer,
                            potionPreparation,
                            servePotion,
                            updateShopTimer,
                            enterRewind,
                            shopPresentationFactory.Create(sessionStore.ActiveSession));
                        presenter.ConfigureProgression(resolveExpense, retryDay, continueToNextDay);
                    }
                }

                foreach (RewindShellPresenter presenter in root.GetComponentsInChildren<RewindShellPresenter>(true))
                {
                    var symbols = new System.Collections.Generic.List<string>(runtimeContent.RewindCards.Count);
                    foreach (RewindCardContent card in runtimeContent.RewindCards)
                    {
                        symbols.Add(card.SymbolKey);
                    }
                    presenter.ConfigureNavigation(sceneLoader);
                    presenter.Initialize(
                        sessionStore,
                        completeRewind,
                        audioService,
                        settingsService.Current,
                        symbols,
                        runtimeContent.RewindDifficulty.StartingPairCount,
                        runtimeContent.RewindDifficulty.MaximumPairCount);
                }

                foreach (SettingsPanelPresenter presenter in root.GetComponentsInChildren<SettingsPanelPresenter>(true))
                {
                    presenter.Initialize(settingsService, audioService);
                }
            }
        }

        private void FailStartup(string message)
        {
            StartupError = message;
            IsReady = false;
            compositionSucceeded = false;
            Debug.LogError(message, this);
        }

        /// <summary>
        /// Startup safety rule: debug scene/day overrides may only affect Editor runs.
        /// Player builds must always follow the normal Main Menu/Day 1 path.
        /// </summary>
        public static bool ShouldUseEditorDebugStartup(bool isEditorBuild, bool hasActiveOverride)
        {
            return isEditorBuild && hasActiveOverride;
        }

#if UNITY_EDITOR
        private async System.Threading.Tasks.Task OpenEditorDebugStartupAsync()
        {
            WitchDebugRuntime.IsDebugJumpActive = true;
            string scene = WitchDebugRuntime.StartScene;
            int day = WitchDebugRuntime.StartDay;

            if (string.Equals(scene, WitchDebugRuntime.StartSceneRewind, System.StringComparison.Ordinal))
            {
                // Debug Rewind entry: playable in-memory session matching EnterRewind metadata/state
                // (PendingRewind, RewindMinigame phase, suspicion at threshold, board/order seeds).
                // Real Shop->Rewind does not cover with Day Transition; do not leave a cover over Rewind.
                if (DayTransition != null)
                {
                    DayTransition.CancelPendingReveal();
                    DayTransition.HideImmediate();
                }

                GameSession session = DevelopmentStartupSessionFactory.CreatePendingRewind(
                    runtimeContent.NewRunSetup,
                    runtimeContent.NewRunSetup.StartingMoney,
                    0,
                    randomSeed + 10,
                    randomSeed + 11,
                    DateTimeOffset.UtcNow);
                SaveStateResult save = await runRepository.SaveRunStateAsync(session.DayStartSnapshot, session.AttemptMetadata);
                if (!save.Succeeded)
                {
                    FailStartup("Bootstrap could not prepare debug Rewind Minigame: " + save.ErrorMessage);
                    return;
                }

                sessionStore.SetActive(session);
                SceneLoadResult rewindResult = await sceneLoader.LoadAsync(SceneId.RewindMinigame);
                if (!rewindResult.Succeeded)
                {
                    FailStartup("Bootstrap could not open debug Rewind Minigame: " + rewindResult.ErrorMessage);
                }
                return;
            }

            // Shop/start-day debug: start or jump directly into Shop at requested day.
            if (!TryBuildDebugShopSession(day, out GameSession debugSession, out string error))
            {
                FailStartup(error);
                return;
            }

            SaveStateResult debugSave = await runRepository.SaveRunStateAsync(debugSession.DayStartSnapshot, debugSession.AttemptMetadata);
            if (!debugSave.Succeeded)
            {
                FailStartup("Bootstrap could not prepare debug day start: " + debugSave.ErrorMessage);
                return;
            }

            sessionStore.SetActive(debugSession);
            if (DayTransition != null)
            {
                DayTransition.CoverInstant(day, revealShopOnInit: true);
            }

            SceneLoadResult shopResult = await sceneLoader.LoadAsync(SceneId.Shop);
            if (!shopResult.Succeeded)
            {
                FailStartup("Bootstrap could not open debug Shop: " + shopResult.ErrorMessage);
            }
        }

        private bool TryBuildDebugShopSession(int dayNumber, out GameSession session, out string error)
        {
            session = null;
            error = null;
            try
            {
                int day = WitchDebugRuntime.ClampDay(dayNumber);
                int orderSeed = UnityEngine.Random.Range(1, int.MaxValue);
                session = DevelopmentStartupSessionFactory.CreateShopPreparationForDay(
                    runtimeContent.NewRunSetup,
                    runtimeContent.DayProgressionSetup,
                    day,
                    runtimeContent.NewRunSetup.StartingMoney,
                    orderSeed,
                    DateTimeOffset.UtcNow);
                return true;
            }
            catch (Exception ex)
            {
                error = "Could not build debug day session: " + ex.Message;
                return false;
            }
        }

        public async void DebugJumpToDay(int dayNumber)
        {
            if (!IsReady || runtimeContent == null)
            {
                Debug.LogWarning("[WitchDebug] Cannot jump: bootstrap not ready.");
                return;
            }

            int day = WitchDebugRuntime.ClampDay(dayNumber);
            debugSessionActive = true;
            WitchDebugRuntime.IsDebugJumpActive = true;
            if (!TryBuildDebugShopSession(day, out GameSession debugSession, out string error))
            {
                Debug.LogError("[WitchDebug] " + error);
                return;
            }

            SaveStateResult debugSave = await runRepository.SaveRunStateAsync(debugSession.DayStartSnapshot, debugSession.AttemptMetadata);
            if (!debugSave.Succeeded)
            {
                Debug.LogError("[WitchDebug] Could not prepare debug jump: " + debugSave.ErrorMessage);
                return;
            }

            sessionStore.SetActive(debugSession);
            if (DayTransition != null)
            {
                DayTransition.CoverInstant(day, revealShopOnInit: true);
            }

            SceneLoadResult result = await sceneLoader.LoadAsync(SceneId.Shop);
            if (!result.Succeeded)
            {
                Debug.LogError("[WitchDebug] Could not load Shop for debug jump: " + result.ErrorMessage);
                return;
            }

            Debug.Log($"[WitchDebug] Jumped to Day {day} (in-memory debug session, no saves/achievements).");
        }

        public void DebugJumpToDayRelative(int delta)
        {
            int current = sessionStore != null && sessionStore.ActiveSession != null
                ? sessionStore.ActiveSession.RunState.CurrentDayNumber
                : WitchDebugRuntime.StartDay;
            DebugJumpToDay(current + delta);
        }

        private void OnGUI()
        {
            if (!debugSessionActive && !WitchDebugRuntime.IsDebugJumpActive)
            {
                return;
            }

            int day = sessionStore != null && sessionStore.ActiveSession != null
                ? sessionStore.ActiveSession.RunState.CurrentDayNumber
                : WitchDebugRuntime.StartDay;
            var rect = new Rect(10, 10, 360, 24);
            var prev = GUI.color;
            GUI.color = new Color(1f, 0.85f, 0.1f, 0.95f);
            GUI.Label(rect, $"DEBUG | Day {day} | in-memory (no saves/achievements)");
            GUI.color = prev;
        }
#endif

    }
}
