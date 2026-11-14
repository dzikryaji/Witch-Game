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

namespace Witch.Bootstrap
{
    [DefaultExecutionOrder(-1000)]
    public sealed class GameCompositionRoot : MonoBehaviour
    {
        [SerializeField] private GameContentCatalogDefinition contentCatalog;
        [SerializeField] private int randomSeed = 20261024;
        [Header("Startup")]
        [SerializeField] private StartupScene startupScene = StartupScene.MainMenu;
#if UNITY_EDITOR
        [Header("Editor Manual Testing")]
        [SerializeField, Min(0)] private int developmentStartingMoney;
        [SerializeField, Range(0, 5)] private int developmentRewindCount;
        [SerializeField] private int developmentOrderSeed = 20261024;
        [SerializeField] private int developmentBoardSeed = 20261025;
#endif
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
        private ContinueToNextDayUseCase continueToNextDay;
        private MarkTutorialFlagUseCase markTutorialFlag;
        private IGameAudioService audioService;
        private IGameCenterService gameCenter;
        private SetShopTimerPausedUseCase setShopTimerPaused;
        private ISaveRepository runRepository;
        private GameRuntimeContent runtimeContent;
        private bool compositionSucceeded;

        public static GameCompositionRoot Instance { get; private set; }
        public bool IsReady { get; private set; }
        public string StartupError { get; private set; }
        public IGameSessionStore SessionStore => sessionStore;
        public DayTransitionController DayTransition { get; private set; }

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
                _ = InitializeGameCenterAsync();
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
            runRepository = startupScene == StartupScene.MainMenu
                ? new JsonRunRepository(content.NewRunSetup.SaveVersion)
                : new InMemoryRunRepository();
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
                new DeterministicRandomSource(randomSeed + 1));
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
            if (startupScene == StartupScene.Shop)
            {
                StartNewGameResult shopStart = await startNewGame.ExecuteAsync(
                    new StartNewGameCommand("development-shop-" + Guid.NewGuid().ToString("N")));
                if (!shopStart.Succeeded)
                {
                    FailStartup("Bootstrap could not open development Shop: " + shopStart.ErrorMessage);
                }
                return;
            }

            if (startupScene == StartupScene.RewindMinigame)
            {
                GameSession session = DevelopmentStartupSessionFactory.CreatePendingRewind(
                    runtimeContent.NewRunSetup,
                    developmentStartingMoney,
                    developmentRewindCount,
                    developmentOrderSeed,
                    developmentBoardSeed,
                    DateTimeOffset.UtcNow);
                SaveStateResult save = await runRepository.SaveRunStateAsync(
                    session.DayStartSnapshot,
                    session.AttemptMetadata);
                if (!save.Succeeded)
                {
                    FailStartup("Bootstrap could not prepare development Rewind Minigame: " + save.ErrorMessage);
                    return;
                }

                sessionStore.SetActive(session);
                SceneLoadResult rewindResult = await sceneLoader.LoadAsync(SceneId.RewindMinigame);
                if (!rewindResult.Succeeded)
                {
                    FailStartup("Bootstrap could not open development Rewind Minigame: " + rewindResult.ErrorMessage);
                }
                return;
            }
#endif

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
            }
        }

        private void FailStartup(string message)
        {
            StartupError = message;
            IsReady = false;
            compositionSucceeded = false;
            Debug.LogError(message, this);
        }

    }
}
