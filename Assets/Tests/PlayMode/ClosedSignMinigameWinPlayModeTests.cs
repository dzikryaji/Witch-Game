using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
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
using Witch.Presentation.MatchCard;
using Witch.Presentation.Shop;
using Object = UnityEngine.Object;

namespace Witch.Bootstrap.PlayModeTests
{
    public sealed class ClosedSignMinigameWinPlayModeTests
    {
        [UnityTest]
        public IEnumerator QueueClosedMinigameWin_KeepsClosedSignVisibleAtRestThroughShopReturnAndProgression()
        {
            MinigameHarness harness = CreatePendingHarnessWithExpiredTimer();
            Assert.That(harness.Session.CurrentPhase, Is.EqualTo(GamePhase.MinigamePending));
            Assert.That(harness.Session.TimerState.QueueOpen, Is.False, "Queue must be closed for this scenario.");
            Assert.That(harness.Session.TimerState.RemainingTime, Is.EqualTo(TimeSpan.Zero));

            var enter = new EnterMinigameUseCase(
                harness.Repository,
                new UnitySceneLoader(),
                new FixedRandom(707));
            Task<MinigameTransactionResult> enterTask = enter.ExecuteAsync(harness.Session);
            yield return WaitFor(enterTask);
            Assert.That(enterTask.Result.Succeeded, Is.True, enterTask.Result.ErrorMessage);
            yield return WaitForScene("MatchCardMinigame", 10f);

            MatchCardShellPresenter minigamePresenter = Object.FindAnyObjectByType<MatchCardShellPresenter>();
            Assert.That(minigamePresenter, Is.Not.Null, "MatchCard presenter is required.");
            minigamePresenter.Initialize(
                harness.Store,
                new CompleteMinigameUseCase(
                    harness.Repository,
                    new UnitySceneLoader(),
                    new FixedRandom(808)));

            yield return CompleteBoard(minigamePresenter);

            Button winContinue = GetPrivateField<Button>(minigamePresenter, "winContinueButton");
            Assert.That(winContinue, Is.Not.Null, "Win continue button is required.");
            yield return WaitForCondition(() => winContinue.gameObject.activeInHierarchy && winContinue.interactable, 5f,
                "Timed out waiting for win continue button.");
            winContinue.onClick.Invoke();

            yield return WaitForScene("Shop", 10f);
            yield return WaitForCondition(() => !harness.Session.AttemptMetadata.PendingMinigame, 5f,
                "Timed out waiting for minigame completion to apply.");

            ShopShellPresenter shopPresenter = InitializeShopAfterReturn(harness);
            Assert.That(shopPresenter, Is.Not.Null, "Shop presenter missing after return.");
            Assert.That(shopPresenter.ActiveSession, Is.SameAs(harness.Session));

            // Moment: right after Shop scene comes back (initial render after return/init).
            yield return null;
            yield return null;
            AssertClosedSignAtRest("right after Shop scene comes back");

            // Sample through post-return flow (farewell/progression) while letting real presenter flow run.
            float deadline = Time.realtimeSinceStartup + 20f;
            bool sawProgression = false;
            while (Time.realtimeSinceStartup < deadline)
            {
                AssertClosedSignAtRest("during post-return flow");
                if (IsProgressionPanelActive(shopPresenter))
                {
                    sawProgression = true;
                    break;
                }

                yield return null;
            }

            Assert.That(sawProgression, Is.True,
                "Timed out waiting for progression panel in queue-closed minigame win flow.");
            AssertClosedSignAtRest("during progression panel");
            yield return null;
            AssertClosedSignAtRest("during progression panel (next frame)");
        }

        private static ShopShellPresenter InitializeShopAfterReturn(MinigameHarness harness)
        {
            ShopShellPresenter presenter = Object.FindAnyObjectByType<ShopShellPresenter>();
            Assert.That(presenter, Is.Not.Null, "Shop presenter is required after returning to Shop.");

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
                new EnterMinigameUseCase(harness.Repository, new UnitySceneLoader(), new FixedRandom(707)),
                setup);
            presenter.ConfigurePolish(
                new SetShopTimerPausedUseCase(harness.Clock),
                new FakeAudioService(),
                GameSettingsState.Default);
            presenter.ConfigurePersistence(harness.Repository);
            presenter.ConfigureProgression(
                harness.ResolveExpense,
                new RetryDayUseCase(harness.Repository, new FakeSceneLoader()),
                new ContinueToNextDayUseCase(new FakeSceneLoader()));
            return presenter;
        }

        private static void AssertClosedSignAtRest(string moment)
        {
            ClosedSignView sign = Object.FindAnyObjectByType<ClosedSignView>(FindObjectsInactive.Include);
            Assert.That(sign, Is.Not.Null, $"Closed sign missing at moment: {moment}");

            ShopShellPresenter presenter = Object.FindAnyObjectByType<ShopShellPresenter>();
            GameSession session = presenter != null ? presenter.ActiveSession : null;
            string state = session == null
                ? "session=null"
                : $"phase={session.CurrentPhase},queueOpen={session.TimerState.QueueOpen},remaining={session.TimerState.RemainingTime}";

            bool selfActive = sign.gameObject.activeSelf;
            bool hierarchyActive = sign.gameObject.activeInHierarchy;
            string parentActive = sign.transform.parent != null
                ? sign.transform.parent.gameObject.activeInHierarchy.ToString()
                : "no-parent";

            Assert.That(hierarchyActive, Is.True,
                $"Closed sign GameObject inactive at moment: {moment}. selfActive={selfActive}, parentActiveInHierarchy={parentActive}, signIsVisible={sign.IsVisible}, {state}");
            Assert.That(sign.IsVisible, Is.True, $"Closed sign not visible at moment: {moment}. {state}");

            RectTransform rect = sign.transform as RectTransform;
            Assert.That(rect, Is.Not.Null, $"Closed sign RectTransform missing at moment: {moment}");
            FieldInfo restingField = typeof(ClosedSignView).GetField("restingAnchoredPosition", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(restingField, Is.Not.Null, "Closed sign resting position field missing.");
            Vector2 resting = (Vector2)restingField.GetValue(sign);
            Assert.That(rect.anchoredPosition.x, Is.EqualTo(resting.x).Within(0.01f), $"Closed sign not at resting X at moment: {moment}");
            Assert.That(rect.anchoredPosition.y, Is.EqualTo(resting.y).Within(0.01f), $"Closed sign not at resting Y at moment: {moment}");

            FieldInfo dropRoutineField = typeof(ClosedSignView).GetField("dropRoutine", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(dropRoutineField, Is.Not.Null, "Closed sign drop routine field missing.");
            Assert.That(dropRoutineField.GetValue(sign), Is.Null, $"Closed sign entrance animation running at moment: {moment}");
        }

        private static bool IsProgressionPanelActive(ShopShellPresenter presenter)
        {
            if (presenter == null) return false;
            FieldInfo panelField = typeof(ShopShellPresenter).GetField("progressionPanel", BindingFlags.Instance | BindingFlags.NonPublic);
            if (panelField == null) return false;
            GameObject panel = panelField.GetValue(presenter) as GameObject;
            return panel != null && panel.activeInHierarchy;
        }

        private static IEnumerator CompleteBoard(MatchCardShellPresenter presenter)
        {
            Dictionary<int, List<int>> pairs = GroupPairs(presenter.Board);
            foreach (List<int> pair in pairs.Values)
            {
                yield return WaitFor(presenter.FlipCardAsync(pair[0]));
                yield return WaitFor(presenter.FlipCardAsync(pair[1]));
            }

            Assert.That(presenter.IsComplete, Is.True, "Board should be complete.");
        }

        private static MinigameHarness CreatePendingHarnessWithExpiredTimer()
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
                new FixedRandom(101)).ExecuteAsync(new StartNewGameCommand("closed-sign-minigame")).Result;
            Assert.That(start.Succeeded, Is.True, start.ErrorMessage);

            var rules = new PotionServiceRules(10);
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
                prepare.Grind(start.Session, "herb.mint", "herb.sage");
                prepare.AddToPot(start.Session);
                prepare.Brew(start.Session);
                ServePotionResult result = serve.ExecuteAsync(start.Session).Result;
                Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
            }

            Assert.That(start.Session.CurrentPhase, Is.EqualTo(GamePhase.MinigamePending));
            clock.Advance(start.Session.TimerState.RemainingTime + TimeSpan.FromSeconds(1));
            new UpdateShopTimerUseCase(clock).Execute(start.Session);

            var daySetup = new DayProgressionSetup(
                new Dictionary<int, TimeSpan>
                {
                    { 1, TimeSpan.FromSeconds(60) },
                    { 2, TimeSpan.FromSeconds(60) }
                },
                new Dictionary<int, int>
                {
                    { 1, 20 },
                    { 2, 20 }
                },
                1);
            var resolveExpense = new ResolveExpenseUseCase(
                new ExpenseRules(14),
                daySetup,
                repository,
                new FixedRandom(202),
                clock);

            return new MinigameHarness(
                repository,
                store,
                clock,
                start.Session,
                rules,
                new UpdateShopTimerUseCase(clock),
                resolveExpense);
        }

        private static Dictionary<int, List<int>> GroupPairs(MatchCardBoard board)
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

        private static IEnumerator WaitFor(Task task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }

            Assert.That(task.IsFaulted, Is.False, task.Exception?.ToString());
        }

        private static IEnumerator WaitForCondition(Func<bool> condition, float timeoutSeconds, string message)
        {
            float timeout = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.That(condition(), Is.True, message);
        }

        private static IEnumerator WaitForScene(string sceneName, float timeoutSeconds)
        {
            float timeout = Time.realtimeSinceStartup + timeoutSeconds;
            while (SceneManager.GetActiveScene().name != sceneName && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(sceneName), "Timed out waiting for scene " + sceneName + ".");
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

        private sealed class MinigameHarness
        {
            public MinigameHarness(
                InMemoryRunRepository repository,
                IGameSessionStore store,
                MutableClock clock,
                GameSession session,
                PotionServiceRules rules,
                UpdateShopTimerUseCase updateTimer,
                ResolveExpenseUseCase resolveExpense)
            {
                Repository = repository;
                Store = store;
                Clock = clock;
                Session = session;
                Rules = rules;
                UpdateTimer = updateTimer;
                ResolveExpense = resolveExpense;
            }

            public InMemoryRunRepository Repository { get; }
            public IGameSessionStore Store { get; }
            public MutableClock Clock { get; }
            public GameSession Session { get; }
            public PotionServiceRules Rules { get; }
            public UpdateShopTimerUseCase UpdateTimer { get; }
            public ResolveExpenseUseCase ResolveExpense { get; }
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

        private sealed class FakeAudioService : IGameAudioService
        {
            public float MusicRemainingSeconds => 0f;

            public ServiceResult Play(string cueId) => ServiceResult.Success();

            public ServiceResult PlayMusic(string cueId) => ServiceResult.Success();

            public ServiceResult PlayAmbience(string cueId) => ServiceResult.Success();

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
    }
}
