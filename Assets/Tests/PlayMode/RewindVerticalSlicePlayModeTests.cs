using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Witch.Application.Flow;
using Witch.Application.Ports;
using Witch.Domain;
using Witch.Domain.Orders;
using Witch.Domain.Potions;
using Witch.Domain.Progression;
using Witch.Infrastructure.Persistence;
using Witch.Infrastructure.Scenes;
using Witch.Presentation.Rewind;
using Object = UnityEngine.Object;

namespace Witch.Bootstrap.PlayModeTests
{
    public sealed class RewindVerticalSlicePlayModeTests
    {
        [UnityTest]
        public IEnumerator ThirdWrongOrderEntersBoardAndAllMatchesResetSameDay()
        {
            RewindHarness harness = CreatePendingHarness();
            Assert.That(harness.Session.CurrentPhase, Is.EqualTo(GamePhase.RewindPending));
            Assert.That(harness.Session.RunState.Suspicion, Is.EqualTo(3));

            var enter = new EnterRewindUseCase(
                harness.Repository,
                new UnitySceneLoader(),
                new FixedRandom(707));
            Task<RewindTransactionResult> enterTask = enter.ExecuteAsync(harness.Session);
            yield return WaitFor(enterTask);
            Assert.That(enterTask.Result.Succeeded, Is.True, enterTask.Result.ErrorMessage);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("RewindMinigame"));
            Assert.That(harness.Session.AttemptMetadata.PendingRewind, Is.True);

            RewindShellPresenter presenter = Object.FindAnyObjectByType<RewindShellPresenter>();
            presenter.Initialize(
                harness.Store,
                new CompleteRewindUseCase(
                    harness.Repository,
                    new UnitySceneLoader(),
                    new FixedRandom(808)));
            Assert.That(presenter.Board.PairCount, Is.EqualTo(3));
            Assert.That(GameObject.Find("Guidebook"), Is.Null);

            FindMismatch(presenter.Board, out int mismatchA, out int mismatchB);
            yield return WaitFor(presenter.FlipCardAsync(mismatchA));
            yield return WaitFor(presenter.FlipCardAsync(mismatchB));
            Assert.That(presenter.IsCardVisible(mismatchA), Is.False);
            Assert.That(presenter.IsCardVisible(mismatchB), Is.False);

            Dictionary<int, List<int>> pairs = GroupPairs(presenter.Board);
            foreach (List<int> pair in pairs.Values)
            {
                yield return WaitFor(presenter.FlipCardAsync(pair[0]));
                yield return WaitFor(presenter.FlipCardAsync(pair[1]));
            }

            Assert.That(presenter.IsComplete, Is.True);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Shop"));
            Assert.That(harness.Session.CurrentPhase, Is.EqualTo(GamePhase.ShopPreparation));
            Assert.That(harness.Session.RunState.Money, Is.EqualTo(9));
            Assert.That(harness.Session.RunState.Suspicion, Is.Zero);
            Assert.That(harness.Session.RunState.RewindCount, Is.EqualTo(1));
            Assert.That(harness.Session.TimerState.RemainingTime, Is.EqualTo(TimeSpan.FromSeconds(60)));
            Assert.That(harness.Session.AttemptMetadata.ActiveOrderSeed, Is.EqualTo(808));
            Assert.That(RewindBoard.Create(909, harness.Session.RunState.RewindCount).PairCount, Is.EqualTo(4));
        }

        [UnityTest]
        public IEnumerator PendingRewindReloadReturnsToSameBoardDifficulty()
        {
            RewindHarness harness = CreatePendingHarness();
            var fakeScene = new RecordingSceneLoader();
            var enter = new EnterRewindUseCase(harness.Repository, fakeScene, new FixedRandom(515));
            Task<RewindTransactionResult> enterTask = enter.ExecuteAsync(harness.Session);
            yield return WaitFor(enterTask);
            int expectedSeed = harness.Session.AttemptMetadata.MinigameBoardSeed;
            harness.Store.Clear();

            var load = new LoadRunUseCase(
                harness.Repository,
                harness.Repository,
                harness.Store,
                new UnitySceneLoader());
            Task<LoadRunResult> loadTask = load.ExecuteAsync(LoadRunCommand.Latest());
            yield return WaitFor(loadTask);

            Assert.That(loadTask.Result.Succeeded, Is.True, loadTask.Result.ErrorMessage);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("RewindMinigame"));
            Assert.That(loadTask.Result.Session.CurrentPhase, Is.EqualTo(GamePhase.RewindMinigame));
            Assert.That(loadTask.Result.Session.AttemptMetadata.MinigameBoardSeed, Is.EqualTo(expectedSeed));
            Assert.That(
                RewindBoard.Create(expectedSeed, loadTask.Result.Session.AttemptMetadata.RewindCount).PairCount,
                Is.EqualTo(3));
        }

        private static RewindHarness CreatePendingHarness()
        {
            var repository = new InMemoryRunRepository();
            var store = new GameSessionStore();
            var clock = new FixedClock();
            var setup = new NewRunSetup(
                9,
                new[] { "category.test" },
                new[] { "herb.ginger", "herb.mint", "herb.sage" },
                new[] { "recipe.correct", "recipe.wrong" },
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
                new RecordingSceneLoader(),
                clock,
                new FixedRandom(101)).ExecuteAsync(new StartNewGameCommand("rewind-playmode")).Result;
            Assert.That(start.Succeeded, Is.True, start.ErrorMessage);

            var rules = new PotionServiceRules(
                10,
                new RecipeCatalog(new[]
                {
                    new Recipe("recipe.correct", new HerbPair("herb.ginger", "herb.mint")),
                    new Recipe("recipe.wrong", new HerbPair("herb.mint", "herb.sage"))
                }));
            var open = new OpenShopUseCase();
            var admit = new AdmitCustomerUseCase(clock);
            var prepare = new PotionPreparationUseCase(rules);
            var serve = new ServePotionUseCase(rules, repository);
            open.Execute(start.Session);

            for (int index = 0; index < 3; index++)
            {
                AdmitCustomerResult arrival = admit.Execute(
                    start.Session,
                    new AdmitCustomerCommand(
                        "customer." + index,
                        new OrderRequest("order." + index, new HerbPair("herb.ginger", "herb.mint"))));
                Assert.That(arrival.Succeeded, Is.True, arrival.ErrorMessage);
                prepare.Grind(start.Session, "herb.mint");
                prepare.AddToPot(start.Session);
                prepare.Grind(start.Session, "herb.sage");
                prepare.AddToPot(start.Session);
                prepare.Brew(start.Session);
                ServePotionResult result = serve.ExecuteAsync(start.Session).Result;
                Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
            }

            return new RewindHarness(repository, store, start.Session);
        }

        private static Dictionary<int, List<int>> GroupPairs(RewindBoard board)
        {
            var result = new Dictionary<int, List<int>>();
            for (int index = 0; index < board.Cards.Count; index++)
            {
                if (!result.TryGetValue(board.Cards[index], out List<int> values))
                {
                    values = new List<int>();
                    result.Add(board.Cards[index], values);
                }

                values.Add(index);
            }

            return result;
        }

        private static void FindMismatch(RewindBoard board, out int first, out int second)
        {
            first = 0;
            for (second = 1; second < board.Cards.Count; second++)
            {
                if (board.Cards[first] != board.Cards[second])
                {
                    return;
                }
            }

            Assert.Fail("Board did not contain a mismatch.");
        }

        private static IEnumerator WaitFor(Task task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }

            Assert.That(task.IsFaulted, Is.False, task.Exception?.ToString());
        }

        private sealed class RewindHarness
        {
            public RewindHarness(InMemoryRunRepository repository, IGameSessionStore store, GameSession session)
            {
                Repository = repository;
                Store = store;
                Session = session;
            }

            public InMemoryRunRepository Repository { get; }
            public IGameSessionStore Store { get; }
            public GameSession Session { get; }
        }

        private sealed class FixedClock : IClock
        {
            public DateTimeOffset UtcNow => new DateTimeOffset(2026, 10, 24, 0, 0, 0, TimeSpan.Zero);
        }

        private sealed class FixedRandom : IRandomSource
        {
            private readonly int value;
            public FixedRandom(int value) => this.value = value;
            public int NextInt(int minimumInclusive, int maximumExclusive) => value;
        }

        private sealed class RecordingSceneLoader : ISceneLoader
        {
            public Task<SceneLoadResult> LoadAsync(SceneId sceneId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(SceneLoadResult.Success());
            }
        }
    }
}
