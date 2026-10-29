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
using Witch.Presentation.Shop;
using Object = UnityEngine.Object;

namespace Witch.Bootstrap.PlayModeTests
{
    public sealed class ThreeDayProgressionPlayModeTests
    {
        [UnityTest]
        public IEnumerator ShopPresenterRunsThreePaidDaysToPrototypeVictory()
        {
            yield return LoadShop();
            ProgressionHarness harness = CreateHarness();

            for (int day = 1; day <= 3; day++)
            {
                ShopShellPresenter presenter = InitializeDay(harness);
                harness.Clock.Advance(presenter.ActiveSession.TimerState.RemainingTime + TimeSpan.FromSeconds(1));
                harness.UpdateTimer.Execute(presenter.ActiveSession);
                PreparePair(presenter, "herb.ginger", "herb.mint");
                presenter.Brew();
                Task<ServePotionResult> serve = presenter.ServeAsync();
                yield return WaitFor(serve);
                Assert.That(presenter.ActiveSession.CurrentPhase, Is.EqualTo(GamePhase.ClosingEncounter));

                Task<ExpenseResolutionResult> expense = presenter.ResolveExpenseAsync();
                yield return WaitFor(expense);
                Assert.That(expense.Result.Succeeded, Is.True, expense.Result.ErrorMessage);
                if (day < 3)
                {
                    Assert.That(expense.Result.Outcome.Kind, Is.EqualTo(ExpenseOutcomeKind.NextDay));
                    Assert.That(presenter.ActiveSession.CurrentPhase, Is.EqualTo(GamePhase.DaySummary));
                    Assert.That(presenter.ActiveSession.RunState.Money, Is.Zero);
                    Assert.That(presenter.ActiveSession.TimerState.RemainingTime, Is.EqualTo(TimeSpan.FromSeconds(day + 1)));
                    Task<ExpenseResolutionResult> next = presenter.ContinueNextDayAsync();
                    yield return WaitFor(next);
                }
                else
                {
                    Assert.That(expense.Result.Outcome.Kind, Is.EqualTo(ExpenseOutcomeKind.Victory));
                    Assert.That(presenter.ActiveSession.CurrentPhase, Is.EqualTo(GamePhase.Victory));
                    Assert.That(GameObject.Find("Progression Text").GetComponent<UnityEngine.UI.Text>().text, Does.Contain("THREE DAYS COMPLETE"));
                }
            }
        }

        [UnityTest]
        public IEnumerator FailedExpenseDeductsNothingAndRetryKeepsDayAndSeed()
        {
            yield return LoadShop();
            ProgressionHarness harness = CreateHarness();
            ShopShellPresenter presenter = InitializeDay(harness);
            int startingSeed = presenter.ActiveSession.DayStartSnapshot.OrderSeed;
            harness.Clock.Advance(TimeSpan.FromSeconds(2));
            harness.UpdateTimer.Execute(presenter.ActiveSession);
            PreparePair(presenter, "herb.mint", "herb.sage");
            presenter.Brew();
            Task<ServePotionResult> serve = presenter.ServeAsync();
            yield return WaitFor(serve);

            Task<ExpenseResolutionResult> expense = presenter.ResolveExpenseAsync();
            yield return WaitFor(expense);
            Assert.That(expense.Result.Outcome.Kind, Is.EqualTo(ExpenseOutcomeKind.GameOver));
            Assert.That(presenter.ActiveSession.RunState.Money, Is.Zero);
            Assert.That(presenter.ActiveSession.CurrentPhase, Is.EqualTo(GamePhase.GameOver));
            Assert.That(harness.Audio.Played, Does.Contain(GameAudioCueIds.ExpenseFail));
            Assert.That(harness.Audio.Played, Does.Contain(GameAudioCueIds.GameOverMusic));
            Assert.That(harness.Audio.Played, Does.Not.Contain(GameAudioCueIds.ExpensePay));

            Task<ExpenseResolutionResult> retry = presenter.RetryDayAsync();
            yield return WaitFor(retry);
            Assert.That(retry.Result.Succeeded, Is.True, retry.Result.ErrorMessage);
            Assert.That(presenter.ActiveSession.CurrentPhase, Is.EqualTo(GamePhase.ShopPreparation));
            Assert.That(presenter.ActiveSession.RunState.CurrentDayNumber, Is.EqualTo(1));
            Assert.That(presenter.ActiveSession.DayStartSnapshot.OrderSeed, Is.EqualTo(startingSeed));
            Assert.That(presenter.ActiveSession.RunState.Suspicion, Is.EqualTo(1));
        }

        private static ProgressionHarness CreateHarness()
        {
            var repository = new InMemoryRunRepository();
            var store = new GameSessionStore();
            var clock = new MutableClock();
            var scenes = new FakeSceneLoader();
            var runSetup = new NewRunSetup(
                0,
                new[] { "category.test" },
                new[] { "herb.ginger", "herb.mint", "herb.sage" },
                new[] { "recipe.correct", "recipe.wrong" },
                new[] { "guide.test" },
                TimeSpan.FromSeconds(1),
                "expense.day.1",
                20,
                1);
            StartNewGameResult start = new StartNewGameUseCase(
                new StartNewRunUseCase(),
                runSetup,
                repository,
                store,
                scenes,
                clock,
                new IncreasingRandom()).ExecuteAsync(new StartNewGameCommand("three-day-playmode")).Result;
            Assert.That(start.Succeeded, Is.True, start.ErrorMessage);

            var rules = new PotionServiceRules(
                20,
                new RecipeCatalog(new[]
                {
                    new Recipe("recipe.correct", new HerbPair("herb.ginger", "herb.mint")),
                    new Recipe("recipe.wrong", new HerbPair("herb.mint", "herb.sage"))
                }));
            var daySetup = new DayProgressionSetup(
                new DayTimingRules(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)),
                20,
                "expense.day.",
                1);
            return new ProgressionHarness(
                repository,
                store,
                clock,
                scenes,
                rules,
                new FakeAudioService(),
                new UpdateShopTimerUseCase(clock),
                new ResolveExpenseUseCase(new ExpenseRules(3), daySetup, repository, new IncreasingRandom(), clock));
        }

        private static ShopShellPresenter InitializeDay(ProgressionHarness harness)
        {
            ShopShellPresenter presenter = Object.FindAnyObjectByType<ShopShellPresenter>();
            presenter.Initialize(
                harness.Store,
                new OpenShopUseCase(),
                new AdmitCustomerUseCase(harness.Clock),
                new PotionPreparationUseCase(harness.Rules),
                new ServePotionUseCase(harness.Rules, harness.Repository),
                harness.UpdateTimer,
                new ShopPresentationSetup(
                    "customer.test",
                    "Rent-Fearing Villager",
                    new OrderRequest("order.correct", new HerbPair("herb.ginger", "herb.mint")),
                    "Ginger and mint, before the landlord develops object permanence.",
                    new[]
                    {
                        new ShopHerbViewData("herb.ginger", "Ginger"),
                        new ShopHerbViewData("herb.mint", "Mint"),
                        new ShopHerbViewData("herb.sage", "Sage")
                    }));
            presenter.ConfigureProgression(
                harness.ResolveExpense,
                new RetryDayUseCase(harness.Repository, harness.Scenes),
                new ContinueToNextDayUseCase(harness.Scenes));
            presenter.ConfigurePolish(
                new SetShopTimerPausedUseCase(harness.Clock),
                harness.Audio,
                GameSettingsState.Default);
            return presenter;
        }

        private static void PreparePair(ShopShellPresenter presenter, string first, string second)
        {
            presenter.GrindHerb(first);
            presenter.AddGroundHerbToPot();
            presenter.GrindHerb(second);
            presenter.AddGroundHerbToPot();
        }

        private static IEnumerator LoadShop()
        {
            AsyncOperation operation = SceneManager.LoadSceneAsync("Shop", LoadSceneMode.Single);
            while (!operation.isDone)
            {
                yield return null;
            }
        }

        private static IEnumerator WaitFor(Task task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }
            Assert.That(task.IsFaulted, Is.False, task.Exception?.ToString());
        }

        private sealed class ProgressionHarness
        {
            public ProgressionHarness(InMemoryRunRepository repository, IGameSessionStore store, MutableClock clock, ISceneLoader scenes, PotionServiceRules rules, FakeAudioService audio, UpdateShopTimerUseCase updateTimer, ResolveExpenseUseCase resolveExpense)
            {
                Repository = repository; Store = store; Clock = clock; Scenes = scenes; Rules = rules; Audio = audio; UpdateTimer = updateTimer; ResolveExpense = resolveExpense;
            }
            public InMemoryRunRepository Repository { get; }
            public IGameSessionStore Store { get; }
            public MutableClock Clock { get; }
            public ISceneLoader Scenes { get; }
            public PotionServiceRules Rules { get; }
            public FakeAudioService Audio { get; }
            public UpdateShopTimerUseCase UpdateTimer { get; }
            public ResolveExpenseUseCase ResolveExpense { get; }
        }

        private sealed class MutableClock : IClock
        {
            public DateTimeOffset UtcNow { get; private set; } = new DateTimeOffset(2026, 10, 24, 0, 0, 0, TimeSpan.Zero);
            public void Advance(TimeSpan value) => UtcNow += value;
        }

        private sealed class IncreasingRandom : IRandomSource
        {
            private int value = 100;
            public int NextInt(int minimumInclusive, int maximumExclusive) => value++;
        }

        private sealed class FakeSceneLoader : ISceneLoader
        {
            public Task<SceneLoadResult> LoadAsync(SceneId sceneId, CancellationToken cancellationToken = default) => Task.FromResult(SceneLoadResult.Success());
        }

        private sealed class FakeAudioService : IGameAudioService
        {
            public readonly List<string> Played = new List<string>();
            public ServiceResult Play(string cueId) { Played.Add(cueId); return ServiceResult.Success(); }
            public ServiceResult PlayMusic(string cueId) { Played.Add(cueId); return ServiceResult.Success(); }
            public ServiceResult PlayAmbience(string cueId) { Played.Add(cueId); return ServiceResult.Success(); }
            public void StopMusic() { }
            public void StopAmbience() { }
            public void ApplySettings(GameSettingsState settings) { }
        }
    }
}
