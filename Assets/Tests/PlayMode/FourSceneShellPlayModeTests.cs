using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Witch.Application.Flow;
using Witch.Application.Ports;
using Witch.Bootstrap;
using Witch.Infrastructure.Persistence;
using Witch.Infrastructure.Scenes;
using Witch.Presentation.MainMenu;
using Witch.Presentation.Rewind;
using Witch.Presentation.Shop;
using Object = UnityEngine.Object;

namespace Witch.Bootstrap.PlayModeTests
{
    public sealed class FourSceneShellPlayModeTests
    {
        [Test]
        public void FixedAspectRatio_UsesFullLandscapeViewportAndPillarboxesWiderScreens()
        {
            Rect landscape = FixedAspectRatioController.CalculateViewport(1600, 900);
            Rect wide = FixedAspectRatioController.CalculateViewport(1700, 900);

            Assert.That(landscape, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
            Assert.That(wide.height, Is.EqualTo(1f));
            Assert.That(wide.width, Is.EqualTo((16f / 9f) / (1700f / 900f)).Within(0.0001f));
            Assert.That(wide.x, Is.EqualTo((1f - wide.width) * 0.5f).Within(0.0001f));
        }

        [Test]
        public void BuildSettingsStartWithFourProductionScenesAndExcludeSampleScene()
        {
            string[] expectedPaths =
            {
                "Assets/Scenes/Bootstrap.unity",
                "Assets/Scenes/MainMenu.unity",
                "Assets/Scenes/Shop.unity",
                "Assets/Scenes/RewindMinigame.unity"
            };

            // Unity Test Runner appends its own temporary scene in Play Mode.
            Assert.That(SceneManager.sceneCountInBuildSettings, Is.GreaterThanOrEqualTo(expectedPaths.Length));
            for (int index = 0; index < expectedPaths.Length; index++)
            {
                Assert.That(SceneUtility.GetScenePathByBuildIndex(index), Is.EqualTo(expectedPaths[index]));
            }

            for (int index = 0; index < SceneManager.sceneCountInBuildSettings; index++)
            {
                Assert.That(
                    SceneUtility.GetScenePathByBuildIndex(index),
                    Is.Not.EqualTo("Assets/Scenes/SampleScene.unity"));
            }
        }

        [UnityTest]
        public IEnumerator BootstrapSupportsNewGameContinueAndAllSceneShells()
        {
            yield return LoadScene("Bootstrap");
            yield return WaitForActiveScene("MainMenu");

            GameCompositionRoot composition = Object.FindAnyObjectByType<GameCompositionRoot>();
            Assert.That(composition, Is.Not.Null);
            Assert.That(composition.IsReady, Is.True, composition.StartupError);

            MainMenuPresenter menu = Object.FindAnyObjectByType<MainMenuPresenter>();
            Assert.That(menu, Is.Not.Null);
            Assert.That(menu.IsInitialized, Is.True);
            Assert.That(menu.HasAuthoredView, Is.True);
            Assert.That(Object.FindAnyObjectByType<Camera>(), Is.Not.Null);
            Canvas mainMenuCanvas = GameObject.Find("Main Menu Canvas").GetComponent<Canvas>();
            Assert.That(mainMenuCanvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceCamera));
            Assert.That(mainMenuCanvas.worldCamera, Is.Not.Null);
            AssertNoMissingScripts(SceneManager.GetActiveScene());

            Object.Destroy(composition.gameObject);
            yield return null;
            yield return LoadScene("MainMenu");

            var repository = new InMemoryRunRepository();
            var sessionStore = new GameSessionStore();
            var sceneLoader = new UnitySceneLoader();
            var setup = new NewRunSetup(
                0,
                new[] { "category.test" },
                new[] { "herb.first", "herb.second" },
                new[] { "recipe.test" },
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

            menu = Object.FindAnyObjectByType<MainMenuPresenter>();
            menu.Initialize(startNewGame, loadRun, new FakeSettingsRepository(), new FakeApplicationLifecycle());
            Task<bool> newGameTask = menu.NewGameAsync();
            yield return WaitForTask(newGameTask);
            Assert.That(newGameTask.Result, Is.True, menu.LastErrorMessage);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Shop"));

            ShopShellPresenter shop = Object.FindAnyObjectByType<ShopShellPresenter>();
            Assert.That(shop, Is.Not.Null);
            Assert.That(shop.HasAuthoredView, Is.True);
            Assert.That(Object.FindAnyObjectByType<Camera>(), Is.Not.Null);
            Assert.That(GameObject.Find("Shop Canvas"), Is.Not.Null);
            shop.Initialize(sessionStore);
            Assert.That(shop.IsInitialized, Is.True);
            Assert.That(shop.ActiveSession, Is.Not.Null);
            AssertNoMissingScripts(SceneManager.GetActiveScene());

            yield return LoadScene("MainMenu");
            menu = Object.FindAnyObjectByType<MainMenuPresenter>();
            menu.Initialize(startNewGame, loadRun, new FakeSettingsRepository(), new FakeApplicationLifecycle());

            Task<bool> continueTask = menu.ContinueAsync();
            yield return WaitForTask(continueTask);
            Assert.That(continueTask.Result, Is.True, menu.LastErrorMessage);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Shop"));

            yield return LoadScene("RewindMinigame");
            RewindShellPresenter rewind = Object.FindAnyObjectByType<RewindShellPresenter>();
            Assert.That(rewind, Is.Not.Null);
            Assert.That(rewind.HasAuthoredView, Is.True);
            Assert.That(Object.FindAnyObjectByType<Camera>(), Is.Not.Null);
            Assert.That(GameObject.Find("Rewind Canvas"), Is.Not.Null);
            rewind.Initialize(sessionStore);
            Assert.That(rewind.IsInitialized, Is.True);
            Assert.That(rewind.ActiveSession, Is.Not.Null);
            AssertNoMissingScripts(SceneManager.GetActiveScene());

        }

        private static IEnumerator LoadScene(string sceneName)
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            Assert.That(operation, Is.Not.Null, "Scene is missing from build settings: " + sceneName);
            while (!operation.isDone)
            {
                yield return null;
            }
        }

        private static IEnumerator WaitForActiveScene(string expectedName)
        {
            const int maximumFrames = 120;
            for (int frame = 0; frame < maximumFrames; frame++)
            {
                if (SceneManager.GetActiveScene().name == expectedName)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("Active scene did not become " + expectedName + ".");
        }

        private static IEnumerator WaitForTask(Task task)
        {
            const int maximumFrames = 120;
            for (int frame = 0; frame < maximumFrames && !task.IsCompleted; frame++)
            {
                yield return null;
            }

            Assert.That(task.IsCompleted, Is.True, "Async operation did not finish.");
            Assert.That(task.IsFaulted, Is.False, task.Exception?.ToString());
        }

        private static void AssertNoMissingScripts(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Component component in root.GetComponentsInChildren<Component>(true))
                {
                    Assert.That(component, Is.Not.Null, "Scene has a missing script: " + scene.name);
                }
            }
        }

        private sealed class FixedClock : IClock
        {
            public DateTimeOffset UtcNow => new DateTimeOffset(2026, 10, 24, 0, 0, 0, TimeSpan.Zero);
            public TimeSpan MonotonicElapsed => TimeSpan.Zero;
        }

        private sealed class FixedRandomSource : IRandomSource
        {
            public int NextInt(int minimumInclusive, int maximumExclusive)
            {
                return minimumInclusive;
            }
        }

        private sealed class FakeSettingsRepository : ISettingsRepository
        {
            public Task<SaveStateResult> SaveSettingsAsync(
                GameSettingsState settings,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(SaveStateResult.Success());
            }

            public Task<SettingsLoadResult> LoadSettingsAsync(
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(SettingsLoadResult.Failure(LoadStateError.NotFound, "No test settings."));
            }
        }

        private sealed class FakeApplicationLifecycle : IApplicationLifecycle
        {
            public ServiceResult RequestQuit()
            {
                return ServiceResult.Success();
            }
        }
    }
}
