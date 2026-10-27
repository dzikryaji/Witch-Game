using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Witch.Application.Flow;
using Witch.Application.Ports;
using Witch.Domain.Progression;

namespace Witch.Application.Tests
{
    public sealed class LoadRunUseCaseTests
    {
        [Test]
        public async Task Execute_WithLatestRun_ActivatesSessionAndRequestsShopScene()
        {
            GameSaveState savedState = CreateSavedGame("run.saved");
            var repository = new FakeSaveRepository
            {
                LatestLoadResult = LoadStateResult.Success(savedState)
            };
            var sessionStore = new GameSessionStore();
            var sceneLoader = new FakeSceneLoader();
            var useCase = new LoadRunUseCase(repository, repository, sessionStore, sceneLoader);

            LoadRunResult result = await useCase.ExecuteAsync(LoadRunCommand.Latest());

            Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
            Assert.That(result.Session.DayStartSnapshot, Is.SameAs(savedState.DayStartSnapshot));
            Assert.That(sessionStore.ActiveSession, Is.SameAs(result.Session));
            Assert.That(sceneLoader.LastRequestedScene, Is.EqualTo(SceneId.Shop));
        }

        [Test]
        public async Task Execute_WhenSceneLoadFails_ClearsActiveSession()
        {
            var repository = new FakeSaveRepository
            {
                LatestLoadResult = LoadStateResult.Success(CreateSavedGame("run.saved"))
            };
            var sessionStore = new GameSessionStore();
            var sceneLoader = new FakeSceneLoader
            {
                Result = SceneLoadResult.Failure(SceneLoadError.LoadFailed, "scene failed")
            };
            var useCase = new LoadRunUseCase(repository, repository, sessionStore, sceneLoader);

            LoadRunResult result = await useCase.ExecuteAsync(LoadRunCommand.Latest());

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(LoadRunError.SceneLoadFailed));
            Assert.That(sessionStore.ActiveSession, Is.Null);
        }

        [Test]
        public async Task Execute_WithRunId_ForwardsStableRunIdToRepository()
        {
            var repository = new FakeSaveRepository
            {
                SpecificLoadResult = LoadStateResult.Success(CreateSavedGame("run.selected"))
            };
            var useCase = new LoadRunUseCase(
                repository,
                repository,
                new GameSessionStore(),
                new FakeSceneLoader());

            LoadRunResult result = await useCase.ExecuteAsync(LoadRunCommand.ByRunId("run.selected"));

            Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
            Assert.That(repository.LastRequestedRunId, Is.EqualTo("run.selected"));
        }

        [Test]
        public async Task Execute_WithPendingRewind_RoutesToRewindMinigame()
        {
            var repository = new FakeSaveRepository
            {
                LatestLoadResult = LoadStateResult.Success(CreateSavedGame("run.rewind", pendingRewind: true))
            };
            var sessionStore = new GameSessionStore();
            var sceneLoader = new FakeSceneLoader();
            var useCase = new LoadRunUseCase(repository, repository, sessionStore, sceneLoader);

            LoadRunResult result = await useCase.ExecuteAsync(LoadRunCommand.Latest());

            Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
            Assert.That(result.Session.CurrentPhase, Is.EqualTo(Witch.Domain.GamePhase.RewindMinigame));
            Assert.That(sceneLoader.LastRequestedScene, Is.EqualTo(SceneId.RewindMinigame));
        }

        [Test]
        public async Task Execute_WhenNoSaveExists_ReturnsTypedFailureWithoutLoadingScene()
        {
            var repository = new FakeSaveRepository();
            var sessionStore = new GameSessionStore();
            var sceneLoader = new FakeSceneLoader();
            var useCase = new LoadRunUseCase(repository, repository, sessionStore, sceneLoader);

            LoadRunResult result = await useCase.ExecuteAsync(LoadRunCommand.Latest());

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(LoadRunError.LoadFailed));
            Assert.That(sessionStore.ActiveSession, Is.Null);
            Assert.That(sceneLoader.LoadCallCount, Is.Zero);
        }

        [Test]
        public void Execute_WhenCancelled_PropagatesCancellationWithoutActivatingSession()
        {
            var sessionStore = new GameSessionStore();
            var sceneLoader = new FakeSceneLoader();
            var repository = new FakeSaveRepository();
            var useCase = new LoadRunUseCase(repository, repository, sessionStore, sceneLoader);
            var cancellation = new CancellationToken(canceled: true);

            Assert.That(
                async () => await useCase.ExecuteAsync(LoadRunCommand.Latest(), cancellation),
                Throws.InstanceOf<OperationCanceledException>());
            Assert.That(sessionStore.ActiveSession, Is.Null);
            Assert.That(sceneLoader.LoadCallCount, Is.Zero);
        }

        private static GameSaveState CreateSavedGame(string runId, bool pendingRewind = false)
        {
            var runState = new RunState(
                runId,
                1,
                0,
                pendingRewind ? RunState.RewindSuspicionThreshold : 0,
                pendingRewind ? 1 : 0,
                new[] { "category.warm" },
                new[] { "herb.ginger", "herb.mint" },
                new[] { "recipe.warming-tonic" },
                new[] { "guidebook.warm" });
            var snapshot = new DayStartSnapshot(
                runState.WithProgress(1, 0, 0, pendingRewind ? 0 : runState.RewindCount),
                1234,
                System.TimeSpan.FromMinutes(4),
                "expense.day.1",
                20,
                1,
                System.DateTimeOffset.UtcNow);
            var metadata = new ActiveAttemptMetadata(
                runId,
                1,
                runState.Suspicion,
                runState.RewindCount,
                pendingRewind,
                pendingRewind ? 777 : 0,
                1234);
            return new GameSaveState(snapshot, metadata);
        }
    }
}
