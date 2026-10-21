using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Witch.Application.Flow;
using Witch.Application.Ports;
using Witch.Domain.Inventory;
using Witch.Domain.Progression;

namespace Witch.Application.Tests
{
    public sealed class MainMenuFlowTests
    {
        [Test]
        public async Task StartNewGame_SavesActivatesSessionAndRequestsShopScene()
        {
            var repository = new FakeSaveRepository();
            var sessionStore = new GameSessionStore();
            var sceneLoader = new FakeSceneLoader();
            var useCase = new StartNewGameUseCase(
                new StartNewRunUseCase(),
                CreateSetup(),
                repository,
                sessionStore,
                sceneLoader);

            StartNewGameResult result = await useCase.ExecuteAsync(new StartNewGameCommand("run.test"));

            Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
            Assert.That(repository.SavedState, Is.SameAs(result.Session.RunState));
            Assert.That(sessionStore.ActiveSession, Is.SameAs(result.Session));
            Assert.That(sceneLoader.LastRequestedScene, Is.EqualTo(SceneId.Shop));
        }

        [Test]
        public async Task StartNewGame_WhenSaveFails_DoesNotActivateOrLoadScene()
        {
            var repository = new FakeSaveRepository
            {
                SaveResult = SaveStateResult.Failure(SaveStateError.WriteFailed, "save failed")
            };
            var sessionStore = new GameSessionStore();
            var sceneLoader = new FakeSceneLoader();
            var useCase = new StartNewGameUseCase(
                new StartNewRunUseCase(),
                CreateSetup(),
                repository,
                sessionStore,
                sceneLoader);

            StartNewGameResult result = await useCase.ExecuteAsync(new StartNewGameCommand("run.test"));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(StartNewGameError.SaveFailed));
            Assert.That(sessionStore.ActiveSession, Is.Null);
            Assert.That(sceneLoader.LoadCallCount, Is.Zero);
        }

        [Test]
        public async Task LoadLatestRun_ActivatesLoadedSessionAndRequestsShopScene()
        {
            RunState savedState = CreateRunState("run.saved");
            var repository = new FakeSaveRepository
            {
                LatestLoadResult = LoadStateResult.Success(savedState)
            };
            var sessionStore = new GameSessionStore();
            var sceneLoader = new FakeSceneLoader();
            var useCase = new LoadRunUseCase(repository, sessionStore, sceneLoader);

            LoadRunResult result = await useCase.ExecuteAsync(LoadRunCommand.Latest());

            Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
            Assert.That(result.Session.RunState, Is.SameAs(savedState));
            Assert.That(sessionStore.ActiveSession, Is.SameAs(result.Session));
            Assert.That(sceneLoader.LastRequestedScene, Is.EqualTo(SceneId.Shop));
        }

        [Test]
        public async Task LoadRun_WhenSceneLoadFails_ClearsActiveSession()
        {
            var repository = new FakeSaveRepository
            {
                LatestLoadResult = LoadStateResult.Success(CreateRunState("run.saved"))
            };
            var sessionStore = new GameSessionStore();
            var sceneLoader = new FakeSceneLoader
            {
                Result = SceneLoadResult.Failure(SceneLoadError.LoadFailed, "scene failed")
            };
            var useCase = new LoadRunUseCase(repository, sessionStore, sceneLoader);

            LoadRunResult result = await useCase.ExecuteAsync(LoadRunCommand.Latest());

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(LoadRunError.SceneLoadFailed));
            Assert.That(sessionStore.ActiveSession, Is.Null);
        }

        [Test]
        public async Task LoadSpecificRun_ForwardsStableRunIdToRepository()
        {
            var repository = new FakeSaveRepository
            {
                SpecificLoadResult = LoadStateResult.Success(CreateRunState("run.selected"))
            };
            var useCase = new LoadRunUseCase(
                repository,
                new GameSessionStore(),
                new FakeSceneLoader());

            LoadRunResult result = await useCase.ExecuteAsync(LoadRunCommand.ByRunId("run.selected"));

            Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
            Assert.That(repository.LastRequestedRunId, Is.EqualTo("run.selected"));
        }

        private static NewRunSetup CreateSetup()
        {
            return new NewRunSetup(
                10,
                new Dictionary<string, int> { { "herb.test", 2 } },
                1,
                new[] { "customer.villager.test" });
        }

        private static RunState CreateRunState(string runId)
        {
            return new RunState(
                runId,
                10,
                new InventoryState(new Dictionary<string, int> { { "herb.test", 2 } }),
                1,
                new DayState(1, new[] { "customer.villager.test" }));
        }

        private sealed class FakeSaveRepository : ISaveRepository
        {
            public SaveStateResult SaveResult { get; set; } = SaveStateResult.Success();

            public LoadStateResult LatestLoadResult { get; set; } =
                LoadStateResult.Failure(LoadStateError.NotFound, "no save");

            public LoadStateResult SpecificLoadResult { get; set; } =
                LoadStateResult.Failure(LoadStateError.NotFound, "no save");

            public RunState SavedState { get; private set; }

            public string LastRequestedRunId { get; private set; }

            public Task<SaveStateResult> SaveAsync(
                RunState snapshot,
                CancellationToken cancellationToken = default)
            {
                SavedState = snapshot;
                return Task.FromResult(SaveResult);
            }

            public Task<LoadStateResult> LoadLatestAsync(
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(LatestLoadResult);
            }

            public Task<LoadStateResult> LoadAsync(
                string runId,
                CancellationToken cancellationToken = default)
            {
                LastRequestedRunId = runId;
                return Task.FromResult(SpecificLoadResult);
            }
        }

        private sealed class FakeSceneLoader : ISceneLoader
        {
            public SceneLoadResult Result { get; set; } = SceneLoadResult.Success();

            public int LoadCallCount { get; private set; }

            public SceneId? LastRequestedScene { get; private set; }

            public Task<SceneLoadResult> LoadAsync(
                SceneId sceneId,
                CancellationToken cancellationToken = default)
            {
                LoadCallCount++;
                LastRequestedScene = sceneId;
                return Task.FromResult(Result);
            }
        }
    }
}
