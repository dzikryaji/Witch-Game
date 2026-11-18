using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Witch.Application.Flow;
using Witch.Application.Ports;
using Witch.Domain;
using Witch.Domain.Orders;
using Witch.Domain.Potions;
using Witch.Domain.Progression;
using Witch.Infrastructure.Persistence;
using Witch.Infrastructure.Scenes;
using Witch.Presentation.Settings;
using Witch.Presentation.Shop;
using Object = UnityEngine.Object;

namespace Witch.Bootstrap.PlayModeTests
{
    public sealed class ShopPauseSettingsUiClickPlayModeTests
    {
        [UnityTest]
        public IEnumerator PausePanelButtons_PlayUiClickOnce_WhenPressed()
        {
            yield return LoadShop();
            var harness = CreateHarness();
            ShopShellPresenter presenter = InitializeShopPresenter(harness);
            Button pauseSettingsButton = GetPrivateField<Button>(presenter, "pauseSettingsButton");
            Button restartButton = GetPrivateField<Button>(presenter, "restartButton");
            Button mainMenuButton = GetPrivateField<Button>(presenter, "mainMenuButton");
            Button confirmYesButton = GetPrivateField<Button>(presenter, "confirmYesButton");
            Button cancelNoButton = GetPrivateField<Button>(presenter, "cancelNoButton");

            Assert.That(pauseSettingsButton, Is.Not.Null);
            Assert.That(restartButton, Is.Not.Null);
            Assert.That(mainMenuButton, Is.Not.Null);
            Assert.That(confirmYesButton, Is.Not.Null);
            Assert.That(cancelNoButton, Is.Not.Null);

            harness.Audio.Played.Clear();
            pauseSettingsButton.onClick.Invoke();
            Assert.That(CountUiClicks(harness.Audio), Is.EqualTo(1), "Pause Settings button should play UiClick once.");

            harness.Audio.Played.Clear();
            restartButton.onClick.Invoke();
            Assert.That(CountUiClicks(harness.Audio), Is.EqualTo(1), "Restart button should play UiClick once.");

            harness.Audio.Played.Clear();
            cancelNoButton.onClick.Invoke();
            Assert.That(CountUiClicks(harness.Audio), Is.EqualTo(1), "Confirmation No button should play UiClick once.");

            harness.Audio.Played.Clear();
            mainMenuButton.onClick.Invoke();
            Assert.That(CountUiClicks(harness.Audio), Is.EqualTo(1), "Main Menu button should play UiClick once.");

            // Confirmation Yes for Quit path: click should play once on press (scene load may be a no-op in fake loader).
            harness.Audio.Played.Clear();
            confirmYesButton.onClick.Invoke();
            Assert.That(CountUiClicks(harness.Audio), Is.EqualTo(1), "Confirmation Yes button should play UiClick once.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator SettingsSaveButton_PlaysUiClickOnce_WhenPressed()
        {
            yield return LoadShop();
            var harness = CreateHarness();
            InitializeShopPresenter(harness);

            SettingsPanelPresenter settings = Object.FindAnyObjectByType<SettingsPanelPresenter>(FindObjectsInactive.Include);
            Assert.That(settings, Is.Not.Null, "Settings panel presenter is required in Shop scene.");

            var settingsAudio = new CountingAudioService();
            settings.Initialize(new FakeSettingsService(), settingsAudio);
            settings.Open();

            Button saveButton = GetPrivateField<Button>(settings, "saveButton");
            Assert.That(saveButton, Is.Not.Null, "Settings save button is required.");

            settingsAudio.Played.Clear();
            saveButton.onClick.Invoke();
            Assert.That(CountUiClicks(settingsAudio), Is.EqualTo(1), "Settings save button should play UiClick once.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator NonInteractablePauseButton_PlaysNothing_WhenClickedViaUi()
        {
            yield return LoadShop();
            var harness = CreateHarness();
            ShopShellPresenter presenter = InitializeShopPresenter(harness);
            Button restartButton = GetPrivateField<Button>(presenter, "restartButton");
            Assert.That(restartButton, Is.Not.Null);

            restartButton.interactable = false;
            harness.Audio.Played.Clear();

            EventSystem eventSystem = Object.FindAnyObjectByType<EventSystem>();
            Assert.That(eventSystem, Is.Not.Null, "EventSystem is required for UI click simulation.");
            var pointer = new PointerEventData(eventSystem);
            ExecuteEvents.Execute(restartButton.gameObject, pointer, ExecuteEvents.pointerClickHandler);

            Assert.That(CountUiClicks(harness.Audio), Is.EqualTo(0), "Non-interactable button should not play UiClick.");
            yield return null;
        }

        private static ShopShellPresenter InitializeShopPresenter(TestHarness harness)
        {
            ShopShellPresenter presenter = Object.FindAnyObjectByType<ShopShellPresenter>();
            Assert.That(presenter, Is.Not.Null, "Shop presenter is required.");

            var setup = new ShopPresentationSetup(
                "customer.test",
                "Test Villager",
                new OrderRequest("order.correct", new HerbPair("herb.ginger", "herb.mint")),
                "Ginger and mint, please.",
                new[]
                {
                    new ShopHerbViewData("herb.ginger", "Ginger"),
                    new ShopHerbViewData("herb.mint", "Mint"),
                    new ShopHerbViewData("herb.sage", "Sage")
                });

            presenter.Initialize(
                harness.Store,
                new OpenShopUseCase(),
                new AdmitCustomerUseCase(harness.Clock),
                new PotionPreparationUseCase(harness.Rules),
                new ServePotionUseCase(harness.Rules, harness.Repository),
                harness.UpdateTimer,
                new EnterMinigameUseCase(harness.Repository, new FakeSceneLoader(), new FixedRandom(707)),
                setup);
            presenter.ConfigurePolish(
                new SetShopTimerPausedUseCase(harness.Clock),
                harness.Audio,
                GameSettingsState.Default);
            presenter.ConfigurePersistence(harness.Repository);
            presenter.ConfigureProgression(
                harness.ResolveExpense,
                new RetryDayUseCase(harness.Repository, new FakeSceneLoader()),
                new ContinueToNextDayUseCase(new FakeSceneLoader()));
            presenter.ConfigureRestart(new RestartDayUseCase(harness.Repository, new FakeSceneLoader()));
            presenter.ConfigureNavigation(new FakeSceneLoader());
            return presenter;
        }

        private static TestHarness CreateHarness()
        {
            var repository = new InMemoryRunRepository();
            var store = new GameSessionStore();
            var clock = new MutableClock();
            var setup = new NewRunSetup(
                20,
                new[] { "category.test" },
                new[] { "herb.ginger", "herb.mint", "herb.sage" },
                new[] { "guide.test" },
                TimeSpan.FromSeconds(60),
                "expense.day.1",
                20,
                1);

            StartNewGameResult start = new StartNewGameUseCase(
                new StartNewRunUseCase(),
                setup,
                repository,
                store,
                new FakeSceneLoader(),
                clock,
                new FixedRandom(101)).ExecuteAsync(new StartNewGameCommand("pause-audio")).Result;
            Assert.That(start.Succeeded, Is.True, start.ErrorMessage);

            var rules = new PotionServiceRules(10);
            var daySetup = new DayProgressionSetup(
                new Dictionary<int, TimeSpan> { { 1, TimeSpan.FromSeconds(60) }, { 2, TimeSpan.FromSeconds(60) } },
                new Dictionary<int, int> { { 1, 20 }, { 2, 20 } },
                1);
            var resolveExpense = new ResolveExpenseUseCase(
                new ExpenseRules(14),
                daySetup,
                repository,
                new FixedRandom(202),
                clock);

            return new TestHarness(
                repository,
                store,
                clock,
                start.Session,
                rules,
                new UpdateShopTimerUseCase(clock),
                resolveExpense,
                new CountingAudioService());
        }

        private static int CountUiClicks(CountingAudioService audio)
        {
            int count = 0;
            foreach (string cue in audio.Played)
            {
                if (string.Equals(cue, GameAudioCueIds.UiClick, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private static IEnumerator LoadShop()
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync("Shop", LoadSceneMode.Single);
            while (operation != null && !operation.isDone)
            {
                yield return null;
            }
            yield return null;
        }

        private static T GetPrivateField<T>(object target, string fieldName) where T : class
        {
            Type type = target.GetType();
            while (type != null)
            {
                FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    return field.GetValue(target) as T;
                }

                type = type.BaseType;
            }

            return null;
        }

        private sealed class TestHarness
        {
            public TestHarness(
                InMemoryRunRepository repository,
                IGameSessionStore store,
                MutableClock clock,
                GameSession session,
                PotionServiceRules rules,
                UpdateShopTimerUseCase updateTimer,
                ResolveExpenseUseCase resolveExpense,
                CountingAudioService audio)
            {
                Repository = repository;
                Store = store;
                Clock = clock;
                Session = session;
                Rules = rules;
                UpdateTimer = updateTimer;
                ResolveExpense = resolveExpense;
                Audio = audio;
            }

            public InMemoryRunRepository Repository { get; }
            public IGameSessionStore Store { get; }
            public MutableClock Clock { get; }
            public GameSession Session { get; }
            public PotionServiceRules Rules { get; }
            public UpdateShopTimerUseCase UpdateTimer { get; }
            public ResolveExpenseUseCase ResolveExpense { get; }
            public CountingAudioService Audio { get; }
        }

        private sealed class MutableClock : IClock
        {
            public DateTimeOffset UtcNow { get; private set; } = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

            public void Advance(TimeSpan value)
            {
                UtcNow += value;
            }
        }

        private sealed class FixedRandom : IRandomSource
        {
            private readonly int value;

            public FixedRandom(int value)
            {
                this.value = value;
            }

            public int NextInt(int minInclusive, int maxExclusive)
            {
                if (maxExclusive <= minInclusive)
                {
                    return minInclusive;
                }

                int range = maxExclusive - minInclusive;
                int normalized = ((value - minInclusive) % range + range) % range;
                return minInclusive + normalized;
            }
        }

        private sealed class FakeSceneLoader : ISceneLoader
        {
            public Task<SceneLoadResult> LoadAsync(SceneId sceneId, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(SceneLoadResult.Success());
            }
        }

        private sealed class CountingAudioService : IGameAudioService
        {
            public readonly List<string> Played = new List<string>();
            public float MusicRemainingSeconds => 0f;

            public ServiceResult Play(string cueId)
            {
                if (!string.IsNullOrWhiteSpace(cueId))
                {
                    Played.Add(cueId);
                }
                return ServiceResult.Success();
            }

            public ServiceResult PlayMusic(string cueId)
            {
                if (!string.IsNullOrWhiteSpace(cueId))
                {
                    Played.Add(cueId);
                }
                return ServiceResult.Success();
            }

            public ServiceResult PlayAmbience(string cueId)
            {
                if (!string.IsNullOrWhiteSpace(cueId))
                {
                    Played.Add(cueId);
                }
                return ServiceResult.Success();
            }

            public void StopMusic()
            {
            }

            public void StopAmbience()
            {
            }

            public void ApplySettings(GameSettingsState settings)
            {
            }
        }

        private sealed class FakeSettingsService : IGameSettingsService
        {
            public GameSettingsState Current { get; private set; } = GameSettingsState.Default;

            public bool IsLoaded => true;

            public Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(SettingsLoadResult.Success(Current));
            }

            public Task<SaveStateResult> SaveAsync(GameSettingsState settings, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Current = settings ?? GameSettingsState.Default;
                return Task.FromResult(SaveStateResult.Success());
            }
        }
    }
}
