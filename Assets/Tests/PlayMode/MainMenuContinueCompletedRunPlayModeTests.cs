using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Witch.Application.Flow;
using Witch.Application.Ports;
using Witch.Domain;
using Witch.Domain.Progression;
using Witch.Infrastructure.Persistence;
using Witch.Infrastructure.Scenes;
using Witch.Presentation.MainMenu;
using Object = UnityEngine.Object;

namespace Witch.Bootstrap.PlayModeTests
{
    public sealed class MainMenuContinueCompletedRunPlayModeTests
    {
        [UnityTest]
        public IEnumerator Continue_OnCompletedRun_StartsFreshDay1LikeNewGame()
        {
            yield return LoadScene("MainMenu");

            var repository = new InMemoryRunRepository();
            var sessionStore = new GameSessionStore();
            var sceneLoader = new UnitySceneLoader();
            var setup = new NewRunSetup(
                50,
                new[] { "category.test" },
                new[] { "herb.first", "herb.second" },
                new[] { "guide.test" },
                TimeSpan.FromSeconds(60),
                "expense.day.1",
                20,
                1);
            var startNewGame = new StartNewGameUseCase(
                new StartNewRunUseCase(),
                setup,
                repository,
                sessionStore,
                sceneLoader,
                new FixedClock(),
                new FixedRandomSource());
            var loadRun = new LoadRunUseCase(repository, repository, sessionStore, sceneLoader);

            // Seed a completed saved run (latest).
            RunState completedRun = new RunState(
                "run.completed",
                14,
                123,
                1,
                2,
                new[] { "category.test" },
                new[] { "herb.first", "herb.second" },
                new[] { "guide.test" },
                new Dictionary<string, bool> { { "shop.tutorial.day1.guided", true } },
                new Dictionary<string, bool> { { RunState.RunCompletedFlagId, true } },
                new[] { "order.seen" },
                new[] { "category.test" }).MarkCompleted();
            var snapshot = new DayStartSnapshot(
                completedRun,
                999,
                TimeSpan.FromSeconds(60),
                "expense.day.14",
                0,
                1,
                DateTimeOffset.UtcNow);
            var metadata = new ActiveAttemptMetadata("run.completed", 14, 1, 2, false, 0, 999);
            Task<SaveStateResult> seed = repository.SaveRunStateAsync(snapshot, metadata);
            yield return WaitForTask(seed);
            Assert.That(seed.Result.Succeeded, Is.True, seed.Result.ErrorMessage);

            MainMenuPresenter menu = CreatePresenter();
            menu.Initialize(startNewGame, loadRun, new FakeSettingsRepository(), new FakeApplicationLifecycle(),
                null, null, repository, repository);

            InvokePrivate(menu, "OnContinueClicked");
            yield return WaitFrames(240);

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Shop"), "Completed Continue should start new game into Shop.");

            LoadStateResult latest = null;
            Task<LoadStateResult> load = repository.LoadLatestAsync();
            yield return WaitForTask(load);
            latest = load.Result;
            Assert.That(latest.Succeeded, Is.True, latest.ErrorMessage);

            RunState run = latest.SavedGame.DayStartSnapshot.RunState;
            Assert.That(run.RunCompleted, Is.False, "run.completed must be cleared");
            Assert.That(run.CurrentDayNumber, Is.EqualTo(1));
            Assert.That(run.Money, Is.EqualTo(50));
            Assert.That(run.Suspicion, Is.EqualTo(0));
            Assert.That(run.RewindCount, Is.EqualTo(0));
            Assert.That(run.SeenOrderIds, Is.Empty);
            Assert.That(run.ServedCategoryIds, Is.Empty);
            Assert.That(run.TutorialFlags, Is.Empty);
            Assert.That(run.NarrativeFlags, Is.Empty);

            Object.Destroy(menu.gameObject);
        }

        [UnityTest]
        public IEnumerator Continue_OnNonCompletedRun_ResumesNormally()
        {
            yield return LoadScene("MainMenu");

            var repository = new InMemoryRunRepository();
            var sessionStore = new GameSessionStore();
            var sceneLoader = new UnitySceneLoader();
            var setup = new NewRunSetup(
                50,
                new[] { "category.test" },
                new[] { "herb.first", "herb.second" },
                new[] { "guide.test" },
                TimeSpan.FromSeconds(60),
                "expense.day.1",
                20,
                1);
            var startNewGame = new StartNewGameUseCase(
                new StartNewRunUseCase(),
                setup,
                repository,
                sessionStore,
                sceneLoader,
                new FixedClock(),
                new FixedRandomSource());
            var loadRun = new LoadRunUseCase(repository, repository, sessionStore, sceneLoader);

            RunState midRun = new RunState(
                "run.mid",
                3,
                77,
                1,
                0,
                new[] { "category.test" },
                new[] { "herb.first", "herb.second" },
                new[] { "guide.test" },
                new Dictionary<string, bool> { { "shop.tutorial.day1.guided", true } },
                new Dictionary<string, bool>(),
                new[] { "order.a" },
                new string[0]);
            var snapshot = new DayStartSnapshot(midRun, 555, TimeSpan.FromSeconds(60), "expense.day.3", 30, 1, DateTimeOffset.UtcNow);
            var metadata = new ActiveAttemptMetadata("run.mid", 3, 1, 0, false, 0, 555);
            Task<SaveStateResult> seed = repository.SaveRunStateAsync(snapshot, metadata);
            yield return WaitForTask(seed);
            Assert.That(seed.Result.Succeeded, Is.True);

            MainMenuPresenter menu = CreatePresenter();
            menu.Initialize(startNewGame, loadRun, new FakeSettingsRepository(), new FakeApplicationLifecycle(),
                null, null, repository, repository);

            InvokePrivate(menu, "OnContinueClicked");
            yield return WaitFrames(240);

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Shop"));
            LoadStateResult latest = null;
            Task<LoadStateResult> load = repository.LoadLatestAsync();
            yield return WaitForTask(load);
            latest = load.Result;
            Assert.That(latest.Succeeded, Is.True);
            RunState run = latest.SavedGame.DayStartSnapshot.RunState;
            Assert.That(run.RunCompleted, Is.False);
            Assert.That(run.CurrentDayNumber, Is.EqualTo(3));
            Assert.That(run.Money, Is.EqualTo(77));
            Assert.That(run.TutorialFlags.ContainsKey("shop.tutorial.day1.guided"), Is.True);
            Assert.That(run.SeenOrderIds.Count, Is.EqualTo(1));

            Object.Destroy(menu.gameObject);
        }

        private static MainMenuPresenter CreatePresenter()
        {
            var root = new GameObject("TestMainMenu", typeof(RectTransform), typeof(Canvas));
            var settingsPanel = new GameObject("Settings Panel", typeof(RectTransform));
            settingsPanel.transform.SetParent(root.transform, false);
            var settingsOverlay = new GameObject("Settings Overlay", typeof(RectTransform));
            settingsOverlay.transform.SetParent(root.transform, false);

            Button newGame = CreateButton("NewGame", root.transform);
            Button cont = CreateButton("Continue", root.transform);
            Button settings = CreateButton("Settings", root.transform);
            Button quit = CreateButton("Quit", root.transform);
            Button apply = CreateButton("Apply", settingsPanel.transform);
            Slider master = CreateSlider("Master", settingsPanel.transform);
            Slider music = CreateSlider("Music", settingsPanel.transform);
            var toggleGo = new GameObject("ReducedDelay", typeof(RectTransform), typeof(Toggle));
            toggleGo.transform.SetParent(settingsPanel.transform, false);
            Toggle reduced = toggleGo.GetComponent<Toggle>();
            var textGo = new GameObject("Error", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textGo.transform.SetParent(root.transform, false);
            Text error = textGo.GetComponent<Text>();

            MainMenuPresenter presenter = root.AddComponent<MainMenuPresenter>();
            SetPrivate(presenter, "newGameButton", newGame);
            SetPrivate(presenter, "continueButton", cont);
            SetPrivate(presenter, "settingsButton", settings);
            SetPrivate(presenter, "quitButton", quit);
            SetPrivate(presenter, "settingsPanel", settingsPanel);
            SetPrivate(presenter, "settingsOverlay", settingsOverlay);
            SetPrivate(presenter, "masterSlider", master);
            SetPrivate(presenter, "musicSlider", music);
            SetPrivate(presenter, "reducedDelayToggle", reduced);
            SetPrivate(presenter, "applySettingsButton", apply);
            SetPrivate(presenter, "errorText", error);
            return presenter;
        }

        private static Button CreateButton(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            return go.GetComponent<Button>();
        }

        private static Slider CreateSlider(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);
            return go.GetComponent<Slider>();
        }

        private static void SetPrivate(object obj, string field, object value)
        {
            FieldInfo f = obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(f, Is.Not.Null, "Missing field " + field);
            f.SetValue(obj, value);
        }

        private static void InvokePrivate(object obj, string method)
        {
            MethodInfo m = obj.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(m, Is.Not.Null, "Missing method " + method);
            m.Invoke(obj, null);
        }

        private static IEnumerator LoadScene(string sceneName)
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            Assert.That(operation, Is.Not.Null, "Scene is missing from build settings: " + sceneName);
            while (!operation.isDone) yield return null;
        }

        private static IEnumerator WaitForTask(Task task)
        {
            const int maximumFrames = 300;
            for (int frame = 0; frame < maximumFrames && !task.IsCompleted; frame++) yield return null;
            Assert.That(task.IsCompleted, Is.True, "Async operation did not finish.");
            Assert.That(task.IsFaulted, Is.False, task.Exception?.ToString());
        }

        private static IEnumerator WaitFrames(int count)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        private sealed class FixedClock : IClock
        {
            public DateTimeOffset UtcNow => new DateTimeOffset(2026, 11, 17, 0, 0, 0, TimeSpan.Zero);
            public TimeSpan MonotonicElapsed => TimeSpan.Zero;
        }

        private sealed class FixedRandomSource : IRandomSource
        {
            public int NextInt(int minimumInclusive, int maximumExclusive) => minimumInclusive;
        }

        private sealed class FakeSettingsRepository : ISettingsRepository
        {
            public Task<SaveStateResult> SaveSettingsAsync(GameSettingsState settings, CancellationToken cancellationToken = default)
                => Task.FromResult(SaveStateResult.Success());
            public Task<SettingsLoadResult> LoadSettingsAsync(CancellationToken cancellationToken = default)
                => Task.FromResult(SettingsLoadResult.Failure(LoadStateError.NotFound, "No test settings."));
        }

        private sealed class FakeApplicationLifecycle : IApplicationLifecycle
        {
            public ServiceResult RequestQuit() => ServiceResult.Success();
        }
    }
}
