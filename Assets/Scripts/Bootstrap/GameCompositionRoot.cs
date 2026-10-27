using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Witch.Application.Flow;
using Witch.Application.Ports;
using Witch.Infrastructure.Content;
using Witch.Infrastructure.Audio;
using Witch.Infrastructure.Persistence;
using Witch.Infrastructure.Platform;
using Witch.Infrastructure.Scenes;
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
        private IGameAudioService audioService;
        private SetShopTimerPausedUseCase setShopTimerPaused;
        private ISaveRepository runRepository;
        private GameRuntimeContent runtimeContent;
        private bool compositionSucceeded;

        public static GameCompositionRoot Instance { get; private set; }
        public bool IsReady { get; private set; }
        public string StartupError { get; private set; }
        public IGameSessionStore SessionStore => sessionStore;

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
                await OpenStartupSceneAsync();
            }
            catch (Exception exception)
            {
                FailStartup("Bootstrap initialization failed: " + exception.Message);
            }
        }

        private void OnDestroy()
        {
            if (Instance != this)
            {
                return;
            }

            SceneManager.sceneLoaded -= HandleSceneLoaded;
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
            UnityAudioService authoredAudio = gameObject.GetComponent<UnityAudioService>();
            if (authoredAudio == null || !authoredAudio.HasAuthoredChannels)
            {
                FailStartup("Bootstrap requires an authored UnityAudioService with Music, SFX, Ambience, and UI channels.");
                return;
            }
            audioService = authoredAudio;
            IClock clock = new UnityMonotonicClock();
            var potionRules = new Witch.Domain.Potions.PotionServiceRules(
                content.CorrectOrderPayment,
                content.Recipes);
            openShop = new OpenShopUseCase();
            admitCustomer = new AdmitCustomerUseCase(clock);
            potionPreparation = new PotionPreparationUseCase(potionRules);
            servePotion = new ServePotionUseCase(potionRules, runRepository);
            updateShopTimer = new UpdateShopTimerUseCase(clock);
            setShopTimerPaused = new SetShopTimerPausedUseCase(clock);
            enterRewind = new EnterRewindUseCase(
                runRepository,
                sceneLoader,
                new DeterministicRandomSource(randomSeed + 1));
            completeRewind = new CompleteRewindUseCase(
                runRepository,
                sceneLoader,
                new DeterministicRandomSource(randomSeed + 2));
            resolveExpense = new ResolveExpenseUseCase(
                content.ExpenseRules,
                content.DayProgressionSetup,
                runRepository,
                new DeterministicRandomSource(randomSeed + 3),
                clock);
            retryDay = new RetryDayUseCase(runRepository, sceneLoader);
            continueToNextDay = new ContinueToNextDayUseCase(sceneLoader);

            var startNewRun = new StartNewRunUseCase();
            startNewGame = new StartNewGameUseCase(
                startNewRun,
                content.NewRunSetup,
                runRepository,
                sessionStore,
                sceneLoader,
                clock,
                new DeterministicRandomSource(randomSeed));
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

        private void Inject(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (MainMenuPresenter presenter in root.GetComponentsInChildren<MainMenuPresenter>(true))
                {
                    presenter.Initialize(startNewGame, loadRun, settingsService, applicationLifecycle, audioService);
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
                    presenter.Initialize(sessionStore, completeRewind, audioService, settingsService.Current);
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
