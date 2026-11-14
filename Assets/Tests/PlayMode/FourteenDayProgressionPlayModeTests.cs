using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    public sealed class FourteenDayProgressionPlayModeTests
    {
        [UnityTest]
        public IEnumerator ClosingDayAutoResolvesToDaySummaryWithoutManualStep()
        {
            yield return LoadShop();
            ProgressionHarness harness = CreateHarness();
            ShopShellPresenter presenter = InitializeDay(harness);
            harness.Clock.Advance(presenter.ActiveSession.TimerState.RemainingTime + TimeSpan.FromSeconds(1));
            harness.UpdateTimer.Execute(presenter.ActiveSession);
            PreparePair(presenter, "herb.ginger", "herb.mint");
            Task<ServePotionResult> serve = presenter.ServeAsync();
            yield return WaitFor(serve);
            Assert.That(presenter.ActiveSession.CurrentPhase, Is.EqualTo(GamePhase.ClosingEncounter));

            yield return WaitForAutoResolvedExpense(presenter);

            Assert.That(presenter.ActiveSession.CurrentPhase, Is.EqualTo(GamePhase.DaySummary));
            Assert.That(presenter.ActiveSession.LastDaySummary, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator ShopPresenterRunsFourteenPaidDaysToVictory()
        {
            yield return LoadShop();
            ProgressionHarness harness = CreateHarness();

            for (int day = 1; day <= 14; day++)
            {
                ShopShellPresenter presenter = InitializeDay(harness);
                harness.Clock.Advance(presenter.ActiveSession.TimerState.RemainingTime + TimeSpan.FromSeconds(1));
                harness.UpdateTimer.Execute(presenter.ActiveSession);
                PreparePair(presenter, "herb.ginger", "herb.mint");
                Task<ServePotionResult> serve = presenter.ServeAsync();
                yield return WaitFor(serve);
                Assert.That(presenter.ActiveSession.CurrentPhase, Is.EqualTo(GamePhase.ClosingEncounter));

                yield return WaitForAutoResolvedExpense(presenter);
                if (day < 14)
                {
                    Assert.That(presenter.ActiveSession.CurrentPhase, Is.EqualTo(GamePhase.DaySummary));
                    Assert.That(presenter.ActiveSession.RunState.Money, Is.Zero);
                    Assert.That(presenter.ActiveSession.TimerState.RemainingTime, Is.EqualTo(TimeSpan.FromSeconds(day + 1)));
                    Task<ExpenseResolutionResult> next = presenter.ContinueNextDayAsync();
                    yield return WaitFor(next);
                }
                else
                {
                    Assert.That(presenter.ActiveSession.CurrentPhase, Is.EqualTo(GamePhase.Victory));
                    Assert.That(GameObject.Find("Day Title Text").GetComponent<UnityEngine.UI.Text>().text, Does.Contain("14 DAYS COMPLETE"));
                }
            }
        }

        [UnityTest]
        public IEnumerator FailedExpenseReachesGameOverWithNoRetryButton()
        {
            yield return LoadShop();
            ProgressionHarness harness = CreateHarness();
            ShopShellPresenter presenter = InitializeDay(harness);
            harness.Clock.Advance(TimeSpan.FromSeconds(2));
            harness.UpdateTimer.Execute(presenter.ActiveSession);
            PreparePair(presenter, "herb.mint", "herb.sage");
            Task<ServePotionResult> serve = presenter.ServeAsync();
            yield return WaitFor(serve);

            yield return WaitForAutoResolvedExpense(presenter);
            Assert.That(presenter.ActiveSession.CurrentPhase, Is.EqualTo(GamePhase.GameOver));
            Assert.That(presenter.ActiveSession.RunState.Money, Is.Zero);
            Assert.That(harness.Audio.Played, Does.Contain(GameAudioCueIds.ExpenseFail));
            Assert.That(harness.Audio.Played, Does.Contain(GameAudioCueIds.GameOverMusic));
            Assert.That(harness.Audio.Played, Does.Not.Contain(GameAudioCueIds.ExpensePay));
            // Retry Day button removed: GameOver currently has no progression-panel exit.
            Assert.That(GameObject.Find("Retry Day"), Is.Null);
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
                new IncreasingRandom()).ExecuteAsync(new StartNewGameCommand("fourteen-day-playmode")).Result;
            Assert.That(start.Succeeded, Is.True, start.ErrorMessage);

            var rules = new PotionServiceRules(20);
            var daySetup = new DayProgressionSetup(
                Enumerable.Range(1, 14).ToDictionary(day => day, day => TimeSpan.FromSeconds(day)),
                Enumerable.Range(1, 14).ToDictionary(day => day, day => 20),
                1);
            return new ProgressionHarness(
                repository,
                store,
                clock,
                scenes,
                rules,
                new FakeAudioService(),
                new UpdateShopTimerUseCase(clock),
                new ResolveExpenseUseCase(new ExpenseRules(14), daySetup, repository, new IncreasingRandom(), clock));
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
            // Both herbs are ground together; adding the mixture to the pot brews automatically.
            presenter.GrindHerb(first, second);
            presenter.AddGroundHerbToPot();
        }

        private static IEnumerator WaitForPhase(ShopShellPresenter presenter, GamePhase phase)
        {
            float timeout = Time.realtimeSinceStartup + 5f;
            while (presenter.ActiveSession.CurrentPhase != phase && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }
            Assert.That(presenter.ActiveSession.CurrentPhase, Is.EqualTo(phase),
                "Timed out waiting for phase " + phase + ".");
        }

        private static IEnumerator WaitForAutoResolvedExpense(ShopShellPresenter presenter)
        {
            float timeout = Time.realtimeSinceStartup + 5f;
            while (presenter.ActiveSession.CurrentPhase == GamePhase.ClosingEncounter
                && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }
            Assert.That(presenter.ActiveSession.CurrentPhase, Is.Not.EqualTo(GamePhase.ClosingEncounter),
                "Timed out waiting for automatic expense resolution.");
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
