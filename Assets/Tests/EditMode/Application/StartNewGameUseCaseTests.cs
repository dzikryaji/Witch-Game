using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Witch.Application.Flow;
using Witch.Application.Ports;

namespace Witch.Application.Tests
{
    public sealed class StartNewGameUseCaseTests
    {
        [Test]
        public async Task Execute_SavesActivatesSessionAndRequestsShopScene()
        {
            var repository = new FakeSaveRepository();
            var sessionStore = new GameSessionStore();
            var sceneLoader = new FakeSceneLoader();
            var useCase = CreateUseCase(repository, sessionStore, sceneLoader);

            StartNewGameResult result = await useCase.ExecuteAsync(new StartNewGameCommand("run.test"));

            Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
            Assert.That(repository.SavedGame.DayStartSnapshot, Is.SameAs(result.Session.DayStartSnapshot));
            Assert.That(repository.SavedGame.AttemptMetadata, Is.SameAs(result.Session.AttemptMetadata));
            Assert.That(repository.SavedGame.AttemptMetadata.Suspicion, Is.Zero);
            Assert.That(repository.SavedGame.AttemptMetadata.RewindCount, Is.Zero);
            Assert.That(repository.SavedGame.AttemptMetadata.PendingRewind, Is.False);
            Assert.That(sessionStore.ActiveSession, Is.SameAs(result.Session));
            Assert.That(sceneLoader.LastRequestedScene, Is.EqualTo(SceneId.Shop));
        }

        [Test]
        public async Task Execute_WhenSaveFails_DoesNotActivateOrLoadScene()
        {
            var repository = new FakeSaveRepository
            {
                SaveResult = SaveStateResult.Failure(SaveStateError.WriteFailed, "save failed")
            };
            var sessionStore = new GameSessionStore();
            var sceneLoader = new FakeSceneLoader();
            var useCase = CreateUseCase(repository, sessionStore, sceneLoader);

            StartNewGameResult result = await useCase.ExecuteAsync(new StartNewGameCommand("run.test"));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(StartNewGameError.SaveFailed));
            Assert.That(sessionStore.ActiveSession, Is.Null);
            Assert.That(sceneLoader.LoadCallCount, Is.Zero);
        }

        [Test]
        public void Execute_WhenCancelled_PropagatesCancellationWithoutActivatingSession()
        {
            var repository = new FakeSaveRepository();
            var sessionStore = new GameSessionStore();
            var sceneLoader = new FakeSceneLoader();
            var useCase = CreateUseCase(repository, sessionStore, sceneLoader);
            var cancellation = new CancellationToken(canceled: true);

            Assert.That(
                async () => await useCase.ExecuteAsync(
                    new StartNewGameCommand("run.test"),
                    cancellation),
                Throws.InstanceOf<OperationCanceledException>());
            Assert.That(sessionStore.ActiveSession, Is.Null);
            Assert.That(sceneLoader.LoadCallCount, Is.Zero);
        }

        private static StartNewGameUseCase CreateUseCase(
            FakeSaveRepository repository,
            GameSessionStore sessionStore,
            FakeSceneLoader sceneLoader)
        {
            return new StartNewGameUseCase(
                new StartNewRunUseCase(),
                new NewRunSetup(
                    0,
                    new[] { "category.warm" },
                    new[] { "herb.ginger", "herb.mint" },
                    new[] { "recipe.warming-tonic" },
                    new[] { "guidebook.warm" },
                    firstDayTimerDuration: TimeSpan.FromMinutes(4),
                    firstDayExpenseId: "expense.day.1",
                    firstDayExpenseValue: 20,
                    saveVersion: 1),
                repository,
                sessionStore,
                sceneLoader,
                new FakeClock(),
                new FakeRandomSource());
        }
    }
}
