using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Witch.Application.Flow;
using Witch.Application.Ports;
using Witch.Domain;
using Witch.Domain.Orders;
using Witch.Domain.Potions;
using Witch.Infrastructure.Persistence;
using Witch.Presentation.Shop;
using Object = UnityEngine.Object;

namespace Witch.Bootstrap.PlayModeTests
{
    public sealed class ShopVerticalSlicePlayModeTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator RepeatedMintThenAloeGroundHerbsAndPotionsStayVisibleAndDraggable()
        {
            yield return LoadShop();
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            Harness harness = CreateHarness();
            ShopShellPresenter presenter = InitializeStressPresenter(harness, 3);

            RectTransform groundHomeParent = null;
            Vector2 groundHomePosition = default;
            RectTransform potionHomeParent = null;
            Vector2 potionHomePosition = default;
            yield return WaitUntil(
                () => presenter.ActiveSession.CurrentPhase == GamePhase.CustomerEncounter,
                "initial customer encounter");

            for (int cycle = 0; cycle < 3; cycle++)
            {
                HerbDragItem mint = FindHerb("herb.mint");
                HerbDragItem aloe = FindHerb("herb.aloe");
                Button grind = GameObject.Find("Grind").GetComponent<Button>();
                Button brew = GameObject.Find("Brew").GetComponent<Button>();
                Button discardPotion = GameObject.Find("Discard").GetComponent<Button>();
                GrinderDropZone grinder = Object.FindAnyObjectByType<GrinderDropZone>();
                CauldronDropZone cauldron = Object.FindAnyObjectByType<CauldronDropZone>();
                PotionServeDropZone customerSlot = Object.FindAnyObjectByType<PotionServeDropZone>();

                yield return DragUi(
                    mouse,
                    mint.GetComponent<RectTransform>(),
                    grinder.GetComponent<RectTransform>(),
                    () => AssertShelfHerbCopied(mint, "mint herb"));
                AssertDragVisualRemoved(mint, "mint herb");
                yield return ClickUi(mouse, grind.GetComponent<RectTransform>());

                GroundHerbDragItem ground = Object.FindAnyObjectByType<GroundHerbDragItem>();
                Assert.That(ground, Is.Not.Null, "Grinding must reveal a draggable ground herb.");
                AssertGroundHerbVisible(ground, "Mint");
                Assert.That(ground.GetComponent<CanvasGroup>().blocksRaycasts, Is.True);
                RememberOrAssertHome(
                    ground.GetComponent<RectTransform>(),
                    ref groundHomeParent,
                    ref groundHomePosition,
                    "ground herb");
                yield return DragUi(
                    mouse,
                    ground.GetComponent<RectTransform>(),
                    cauldron.GetComponent<RectTransform>(),
                    () => AssertRenderedInFront(ground.transform, "ground herb"));
                Assert.That(presenter.ActiveSession.PotionDraft.Ingredients.Count, Is.EqualTo(1));

                yield return DragUi(
                    mouse,
                    aloe.GetComponent<RectTransform>(),
                    grinder.GetComponent<RectTransform>(),
                    () => AssertShelfHerbCopied(aloe, "aloe herb"));
                AssertDragVisualRemoved(aloe, "aloe herb");
                yield return ClickUi(mouse, grind.GetComponent<RectTransform>());

                ground = Object.FindAnyObjectByType<GroundHerbDragItem>();
                Assert.That(ground, Is.Not.Null, "Second grinding must reveal the same draggable output.");
                AssertGroundHerbVisible(ground, "Aloe");
                Assert.That(ground.GetComponent<CanvasGroup>().blocksRaycasts, Is.True);
                RememberOrAssertHome(
                    ground.GetComponent<RectTransform>(),
                    ref groundHomeParent,
                    ref groundHomePosition,
                    "ground herb");
                yield return DragUi(
                    mouse,
                    ground.GetComponent<RectTransform>(),
                    cauldron.GetComponent<RectTransform>(),
                    () => AssertRenderedInFront(ground.transform, "second ground herb"));
                Assert.That(presenter.ActiveSession.PotionDraft.Ingredients.Count, Is.EqualTo(2));

                yield return ClickUi(mouse, brew.GetComponent<RectTransform>());
                PreparedPotionDragItem potion = Object.FindAnyObjectByType<PreparedPotionDragItem>();
                Assert.That(potion, Is.Not.Null, "Brewing must reveal a draggable potion.");
                Assert.That(potion.GetComponent<CanvasGroup>().blocksRaycasts, Is.True);
                RememberOrAssertHome(
                    potion.GetComponent<RectTransform>(),
                    ref potionHomeParent,
                    ref potionHomePosition,
                    "brewed potion");

                if (cycle < 2)
                {
                    yield return DragUiAway(
                        mouse,
                        potion.GetComponent<RectTransform>(),
                        () => AssertRenderedInFront(potion.transform, "brewed potion"));
                    Assert.That(presenter.ActiveSession.PreparedRecipe, Is.Not.Null,
                        "Dropping outside a valid zone must keep the brewed potion.");
                    RememberOrAssertHome(
                        potion.GetComponent<RectTransform>(),
                        ref potionHomeParent,
                        ref potionHomePosition,
                        "brewed potion");
                    Assert.That(potion.GetComponent<CanvasGroup>().blocksRaycasts, Is.True);
                    yield return ClickUi(mouse, discardPotion.GetComponent<RectTransform>());
                    Assert.That(presenter.ActiveSession.PreparedRecipe, Is.Null);
                }
                else
                {
                    yield return DragUi(
                        mouse,
                        potion.GetComponent<RectTransform>(),
                        customerSlot.GetComponent<RectTransform>(),
                        () => AssertRenderedInFront(potion.transform, "brewed potion"));
                    yield return WaitUntil(
                        () => presenter.ActiveSession.PreparedRecipe == null,
                        "potion serve drop");
                }
            }

            Assert.That(presenter.ActiveSession.RunState.Money, Is.EqualTo(10));
        }

        [UnityTest]
        public IEnumerator DragGuidebookAndCorrectOrderWorkThroughRealShopUi()
        {
            yield return LoadShop();
            Harness harness = CreateHarness();
            ShopShellPresenter presenter = InitializePresenter(harness);

            Assert.That(presenter.ActiveSession.CurrentPhase, Is.EqualTo(GamePhase.CustomerEncounter));
            Assert.That(presenter.ActiveSession.TimerStartedAtUtc, Is.Not.Null);
            Assert.That(GameObject.Find("Shop Canvas"), Is.Not.Null);
            Assert.That(presenter.HasAuthoredView, Is.True);

            presenter.ToggleGuidebook();
            Assert.That(presenter.GuidebookVisible, Is.True);
            harness.Clock.Advance(TimeSpan.FromSeconds(5));
            harness.UpdateTimer.Execute(presenter.ActiveSession);
            Assert.That(presenter.ActiveSession.TimerState.RemainingTime, Is.EqualTo(TimeSpan.FromSeconds(55)));

            HerbDragItem ginger = FindHerb("herb.ginger");
            GrinderDropZone grinder = Object.FindAnyObjectByType<GrinderDropZone>();
            ginger.GetComponent<Button>().onClick.Invoke();
            Assert.That(presenter.GrindLoadedHerb().Succeeded, Is.False, "Clicking a herb must not load the grinder.");
            ExecuteEvents.Execute<ISubmitHandler>(
                ginger.gameObject,
                new BaseEventData(EventSystem.current),
                ExecuteEvents.submitHandler);
            Assert.That(presenter.GrindLoadedHerb().Succeeded, Is.True, "Keyboard submit must load the grinder.");
            presenter.DiscardGroundHerb();
            var pointer = new PointerEventData(EventSystem.current) { pointerDrag = ginger.gameObject };
            grinder.OnDrop(pointer);
            Assert.That(presenter.ActiveSession.GroundHerbId, Is.Null);
            Assert.That(presenter.GrindLoadedHerb().Succeeded, Is.True);
            Assert.That(presenter.ActiveSession.GroundHerbId, Is.EqualTo("herb.ginger"));
            Assert.That(presenter.AddGroundHerbToPot().Succeeded, Is.True);
            Assert.That(presenter.GrindHerb("herb.mint").Succeeded, Is.True);
            Assert.That(presenter.AddGroundHerbToPot().Succeeded, Is.True);
            Assert.That(presenter.Brew().Kind, Is.EqualTo(PreparationResultKind.Prepared));

            Task<ServePotionResult> serve = presenter.ServeAsync();
            yield return WaitFor(serve);
            Assert.That(serve.Result.Succeeded, Is.True);
            Assert.That(serve.Result.Outcome.Kind, Is.EqualTo(ServiceOutcomeKind.Correct));
            Assert.That(presenter.ActiveSession.RunState.Money, Is.EqualTo(10));

            Task<ServePotionResult> duplicate = presenter.ServeAsync();
            yield return WaitFor(duplicate);
            Assert.That(duplicate.Result.Succeeded, Is.False);
        }

        [UnityTest]
        public IEnumerator WrongValidOrderPaysNothingAndAddsSuspicion()
        {
            yield return LoadShop();
            Harness harness = CreateHarness();
            ShopShellPresenter presenter = InitializePresenter(harness);

            PreparePair(presenter, "herb.ginger", "herb.sage");
            Assert.That(presenter.Brew().Kind, Is.EqualTo(PreparationResultKind.Prepared));
            Task<ServePotionResult> serve = presenter.ServeAsync();
            yield return WaitFor(serve);

            Assert.That(serve.Result.Outcome.Kind, Is.EqualTo(ServiceOutcomeKind.WrongValid));
            Assert.That(presenter.ActiveSession.RunState.Money, Is.Zero);
            Assert.That(presenter.ActiveSession.RunState.Suspicion, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator MixedPairBrewsAndTimerZeroClosingBehaveAsSpecified()
        {
            yield return LoadShop();
            Harness harness = CreateHarness();
            ShopShellPresenter presenter = InitializePresenter(harness);

            PreparePair(presenter, "herb.mint", "herb.lavender");
            Assert.That(presenter.Brew().Kind, Is.EqualTo(PreparationResultKind.Prepared));
            Assert.That(presenter.ActiveSession.PreparedRecipe, Is.Not.Null);

            presenter.GrindHerb("herb.ginger");
            Assert.That(presenter.Discard().Kind, Is.EqualTo(PreparationResultKind.Discarded));
            Assert.That(presenter.ActiveSession.GroundHerbId, Is.Null);

            harness.Clock.Advance(TimeSpan.FromSeconds(70));
            harness.UpdateTimer.Execute(presenter.ActiveSession);
            Assert.That(presenter.ActiveSession.TimerState.RemainingTime, Is.EqualTo(TimeSpan.Zero));
            Assert.That(presenter.ActiveSession.CurrentPhase, Is.EqualTo(GamePhase.CustomerEncounter));

            PreparePair(presenter, "herb.ginger", "herb.mint");
            presenter.Brew();
            Task<ServePotionResult> serve = presenter.ServeAsync();
            yield return WaitFor(serve);
            Assert.That(serve.Result.Succeeded, Is.True);
            Assert.That(presenter.ActiveSession.CurrentPhase, Is.EqualTo(GamePhase.ClosingEncounter));
        }

        [UnityTest]
        public IEnumerator AuthoredOrdersRefillAfterBagWhileQueueRemainsOpen()
        {
            yield return LoadShop();
            Harness harness = CreateHarness();
            ShopShellPresenter presenter = Object.FindAnyObjectByType<ShopShellPresenter>();
            presenter.Initialize(
                harness.Store,
                harness.OpenShop,
                harness.Admit,
                harness.Preparation,
                harness.Serve,
                harness.UpdateTimer,
                new ShopPresentationSetup(
                    new[]
                    {
                        new ShopEncounterViewData(
                            "customer.first",
                            "First Villager",
                            new OrderRequest("order.first", new HerbPair("herb.ginger", "herb.mint")),
                            "Ginger and mint."),
                        new ShopEncounterViewData(
                            "customer.second",
                            "Second Villager",
                            new OrderRequest("order.second", new HerbPair("herb.ginger", "herb.sage")),
                            "Ginger and sage.")
                    },
                    new[]
                    {
                        new ShopHerbViewData("herb.ginger", "Ginger"),
                        new ShopHerbViewData("herb.mint", "Mint"),
                        new ShopHerbViewData("herb.sage", "Sage")
                    }));

            Assert.That(presenter.ActiveSession.AttemptState.ActiveOrder.OrderId, Is.EqualTo("order.first"));
            PreparePair(presenter, "herb.ginger", "herb.mint");
            presenter.Brew();
            Task<ServePotionResult> first = presenter.ServeAsync();
            yield return WaitFor(first);

            Assert.That(presenter.ActiveSession.AttemptState.ActiveOrder.OrderId, Is.EqualTo("order.second"));
            Assert.That(presenter.ActiveSession.AttemptState.UsedOrderIds,
                Is.EqualTo(new[] { "order.first", "order.second" }));

            PreparePair(presenter, "herb.ginger", "herb.sage");
            presenter.Brew();
            Task<ServePotionResult> second = presenter.ServeAsync();
            yield return WaitFor(second);

            Assert.That(second.Result.Outcome.Kind, Is.EqualTo(ServiceOutcomeKind.Correct));
            Assert.That(presenter.ActiveSession.CurrentPhase, Is.EqualTo(GamePhase.CustomerEncounter));
            Assert.That(presenter.ActiveSession.AttemptState.ActiveOrder.OrderId, Is.EqualTo("order.first.repeat.1"));
            Assert.That(presenter.ActiveSession.AttemptState.UsedOrderIds,
                Is.EqualTo(new[] { "order.first", "order.second", "order.first.repeat.1" }));
        }

        [Test]
        public void RandomEncounterDeckUsesBalancedBagsAndNeverRunsOneRecipeMoreThanTwice()
        {
            HerbPair warm = new HerbPair("herb.ginger", "herb.cinnamon");
            HerbPair cool = new HerbPair("herb.mint", "herb.aloe");
            HerbPair soothe = new HerbPair("herb.lavender", "herb.chamomile");
            var encounters = new List<ShopEncounterViewData>();
            for (int index = 0; index < 5; index++)
            {
                encounters.Add(new ShopEncounterViewData(
                    "customer.test",
                    "Test Villager",
                    new OrderRequest("order.warm." + index, warm),
                    "Ginger and cinnamon."));
                encounters.Add(new ShopEncounterViewData(
                    "customer.test",
                    "Test Villager",
                    new OrderRequest("order.cool." + index, cool),
                    "Mint and aloe."));
                encounters.Add(new ShopEncounterViewData(
                    "customer.test",
                    "Test Villager",
                    new OrderRequest("order.soothe." + index, soothe),
                    "Lavender and chamomile."));
            }

            var deck = new ShopEncounterDeck(encounters, 90421, randomizeEncounters: true);
            HerbPair previous = null;
            int runLength = 0;
            for (int bag = 0; bag < 3; bag++)
            {
                int warmCount = 0;
                int coolCount = 0;
                int sootheCount = 0;
                var orderIds = new HashSet<string>();
                for (int offset = 0; offset < 15; offset++)
                {
                    ShopEncounterViewData encounter = deck.Draw((bag * 15) + offset);
                    Assert.That(orderIds.Add(encounter.Order.OrderId), Is.True);
                    if (encounter.Order.RequestedPair.Equals(warm)) warmCount++;
                    if (encounter.Order.RequestedPair.Equals(cool)) coolCount++;
                    if (encounter.Order.RequestedPair.Equals(soothe)) sootheCount++;

                    if (encounter.Order.RequestedPair.Equals(previous)) runLength++;
                    else
                    {
                        previous = encounter.Order.RequestedPair;
                        runLength = 1;
                    }
                    Assert.That(runLength, Is.LessThanOrEqualTo(2));
                }

                Assert.That(warmCount, Is.EqualTo(5));
                Assert.That(coolCount, Is.EqualTo(5));
                Assert.That(sootheCount, Is.EqualTo(5));
            }
        }

        [UnityTest]
        public IEnumerator TutorialGatesFirstBellAndPauseFreezesShopTime()
        {
            yield return LoadShop();
            Harness harness = CreateHarness();
            ShopShellPresenter presenter = Object.FindAnyObjectByType<ShopShellPresenter>();
            var audio = new FakeAudioService();
            presenter.ConfigurePolish(
                new SetShopTimerPausedUseCase(harness.Clock),
                audio,
                new GameSettingsState(1f, 0.5f, 0.8f, true));
            presenter.Initialize(
                harness.Store,
                harness.OpenShop,
                harness.Admit,
                harness.Preparation,
                harness.Serve,
                harness.UpdateTimer,
                new ShopPresentationSetup(
                    new[]
                    {
                        new ShopEncounterViewData(
                            "customer.tutorial",
                            "Tutorial Villager",
                            new OrderRequest("order.tutorial", new HerbPair("herb.ginger", "herb.mint")),
                            "Ginger and mint.")
                    },
                    new[]
                    {
                        new ShopHerbViewData("herb.ginger", "Ginger"),
                        new ShopHerbViewData("herb.mint", "Mint")
                    },
                    "Ginger and Mint make the requested remedy.",
                    1,
                    true));

            Assert.That(presenter.TutorialVisible, Is.True);
            Assert.That(presenter.ActiveSession.CurrentPhase, Is.EqualTo(GamePhase.ShopPreparation));
            Assert.That(presenter.ActiveSession.TimerStartedAtUtc, Is.Null);
            presenter.AdvanceTutorial();
            presenter.AdvanceTutorial();
            presenter.AdvanceTutorial();
            presenter.AdvanceTutorial();
            presenter.AdvanceTutorial();

            Assert.That(presenter.TutorialVisible, Is.False);
            Assert.That(presenter.ActiveSession.CurrentPhase, Is.EqualTo(GamePhase.CustomerEncounter));
            Assert.That(audio.Played, Does.Contain(GameAudioCueIds.CustomerBell));

            harness.Clock.Advance(TimeSpan.FromSeconds(5));
            harness.UpdateTimer.Execute(presenter.ActiveSession);
            presenter.TogglePause();
            harness.Clock.Advance(TimeSpan.FromSeconds(20));
            harness.UpdateTimer.Execute(presenter.ActiveSession);
            Assert.That(presenter.ActiveSession.TimerState.RemainingTime, Is.EqualTo(TimeSpan.FromSeconds(55)));
            presenter.TogglePause();
            harness.Clock.Advance(TimeSpan.FromSeconds(5));
            harness.UpdateTimer.Execute(presenter.ActiveSession);
            Assert.That(presenter.ActiveSession.TimerState.RemainingTime, Is.EqualTo(TimeSpan.FromSeconds(50)));
        }

        private static Harness CreateHarness()
        {
            var repository = new InMemoryRunRepository();
            var store = new GameSessionStore();
            var clock = new MutableClock();
            var setup = new NewRunSetup(
                0,
                new[] { "category.test" },
                new[] { "herb.ginger", "herb.mint", "herb.aloe", "herb.sage", "herb.lavender" },
                new[] { "recipe.correct", "recipe.cool", "recipe.wrong" },
                new[] { "guide.test" },
                TimeSpan.FromSeconds(60),
                "expense.day.1",
                20,
                1);
            var start = new StartNewGameUseCase(
                new StartNewRunUseCase(),
                setup,
                repository,
                store,
                new FakeSceneLoader(),
                clock,
                new FixedRandom());
            StartNewGameResult startResult = start.ExecuteAsync(new StartNewGameCommand("playmode-run")).Result;
            Assert.That(startResult.Succeeded, Is.True, startResult.ErrorMessage);

            var recipes = new RecipeCatalog(new[]
            {
                new Recipe("recipe.correct", new HerbPair("herb.ginger", "herb.mint")),
                new Recipe("recipe.cool", new HerbPair("herb.mint", "herb.aloe")),
                new Recipe("recipe.wrong", new HerbPair("herb.ginger", "herb.sage"))
            });
            var rules = new PotionServiceRules(10, recipes);
            return new Harness(
                store,
                clock,
                new OpenShopUseCase(),
                new AdmitCustomerUseCase(clock),
                new PotionPreparationUseCase(rules),
                new ServePotionUseCase(rules, repository),
                new UpdateShopTimerUseCase(clock));
        }

        private static ShopShellPresenter InitializePresenter(Harness harness)
        {
            ShopShellPresenter presenter = Object.FindAnyObjectByType<ShopShellPresenter>();
            Assert.That(presenter, Is.Not.Null);
            presenter.Initialize(
                harness.Store,
                harness.OpenShop,
                harness.Admit,
                harness.Preparation,
                harness.Serve,
                harness.UpdateTimer,
                new ShopPresentationSetup(
                    "customer.test",
                    "Nervous Villager",
                    new OrderRequest("order.correct", new HerbPair("herb.ginger", "herb.mint")),
                    "I need ginger and mint. Please do not improvise.",
                    new[]
                    {
                        new ShopHerbViewData("herb.ginger", "Ginger"),
                        new ShopHerbViewData("herb.mint", "Mint"),
                        new ShopHerbViewData("herb.sage", "Sage"),
                        new ShopHerbViewData("herb.lavender", "Lavender")
                    }));
            return presenter;
        }

        private static ShopShellPresenter InitializeStressPresenter(Harness harness, int encounterCount)
        {
            ShopShellPresenter presenter = Object.FindAnyObjectByType<ShopShellPresenter>();
            Assert.That(presenter, Is.Not.Null);
            var encounters = new ShopEncounterViewData[encounterCount];
            for (int index = 0; index < encounterCount; index++)
            {
                encounters[index] = new ShopEncounterViewData(
                    "customer.stress." + index,
                    "Stress Villager " + index,
                    new OrderRequest("order.stress." + index, new HerbPair("herb.mint", "herb.aloe")),
                    "Mint and aloe.");
            }

            presenter.Initialize(
                harness.Store,
                harness.OpenShop,
                harness.Admit,
                harness.Preparation,
                harness.Serve,
                harness.UpdateTimer,
                new ShopPresentationSetup(
                    encounters,
                    new[]
                    {
                        new ShopHerbViewData("herb.mint", "Mint"),
                        new ShopHerbViewData("herb.aloe", "Aloe")
                    }));
            return presenter;
        }

        private IEnumerator ClickUi(Mouse mouse, RectTransform target)
        {
            Move(mouse.position, ScreenCenter(target));
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);
            yield return null;
        }

        private IEnumerator DragUi(
            Mouse mouse,
            RectTransform source,
            RectTransform target,
            Action duringDrag = null)
        {
            Vector2 start = ScreenCenter(source);
            Vector2 end = ScreenCenter(target);
            yield return DragUi(mouse, start, end, duringDrag);
        }

        private IEnumerator DragUiAway(Mouse mouse, RectTransform source, Action duringDrag = null)
        {
            Vector2 start = ScreenCenter(source);
            yield return DragUi(mouse, start, start + Vector2.down * 120f, duringDrag);
        }

        private IEnumerator DragUi(Mouse mouse, Vector2 start, Vector2 end, Action duringDrag = null)
        {
            Move(mouse.position, start);
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            Move(mouse.position, Vector2.Lerp(start, end, 0.5f));
            yield return null;
            duringDrag?.Invoke();
            Move(mouse.position, end);
            yield return null;
            Release(mouse.leftButton);
            yield return null;
        }

        private static void AssertRenderedInFront(Transform item, string label)
        {
            Assert.That(item.GetSiblingIndex(), Is.EqualTo(item.parent.childCount - 1),
                label + " must render above its Workbench siblings while dragged.");
        }

        private static void AssertShelfHerbCopied(HerbDragItem item, string label)
        {
            Assert.That(item.GetComponentInParent<GridLayoutGroup>(), Is.Not.Null,
                label + " source must stay in the shelf grid while dragged.");
            GameObject copy = GameObject.Find(item.gameObject.name + " Drag Visual");
            Assert.That(copy, Is.Not.Null, label + " must drag a visual copy.");
            Assert.That(copy.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
        }

        private static void AssertDragVisualRemoved(HerbDragItem item, string label)
        {
            Assert.That(GameObject.Find(item.gameObject.name + " Drag Visual"), Is.Null,
                label + " drag visual must disappear after drop.");
        }

        private static Vector2 ScreenCenter(RectTransform target)
        {
            return RectTransformUtility.WorldToScreenPoint(null, target.TransformPoint(target.rect.center));
        }

        private static void RememberOrAssertHome(
            RectTransform item,
            ref RectTransform expectedParent,
            ref Vector2 expectedPosition,
            string label)
        {
            if (expectedParent == null)
            {
                expectedParent = item.parent as RectTransform;
                expectedPosition = item.anchoredPosition;
                return;
            }

            Assert.That(item.parent, Is.SameAs(expectedParent), label + " must return to its authored parent.");
            Assert.That(Vector2.Distance(item.anchoredPosition, expectedPosition), Is.LessThan(0.01f),
                label + " must reappear at the same coordinate.");
        }

        private static void AssertGroundHerbVisible(GroundHerbDragItem ground, string expectedName)
        {
            Assert.That(ground.gameObject.activeInHierarchy, Is.True, expectedName + " ground herb must be active.");
            var rect = ground.GetComponent<RectTransform>();
            Assert.That(rect.rect.width, Is.GreaterThan(0f), expectedName + " ground herb must have width.");
            Assert.That(rect.rect.height, Is.GreaterThan(0f), expectedName + " ground herb must have height.");

            CanvasGroup group = ground.GetComponent<CanvasGroup>();
            Assert.That(group.alpha, Is.GreaterThan(0.99f), expectedName + " ground herb must be opaque.");
            Assert.That(group.blocksRaycasts, Is.True, expectedName + " ground herb must receive pointer input.");

            UnityEngine.UI.Image image = ground.GetComponent<UnityEngine.UI.Image>();
            Assert.That(image, Is.Not.Null);
            Assert.That(image.enabled, Is.True);
            Assert.That(image.canvasRenderer.GetInheritedAlpha(), Is.GreaterThan(0.99f));

            Text text = ground.GetComponentInChildren<Text>(true);
            Assert.That(text, Is.Not.Null);
            Assert.That(text.text, Does.Contain(expectedName).IgnoreCase);

            Vector2 center = ScreenCenter(rect);
            Assert.That(center.x, Is.InRange(0f, (float)Screen.width));
            Assert.That(center.y, Is.InRange(0f, (float)Screen.height));
        }

        private static IEnumerator WaitUntil(Func<bool> condition, string description)
        {
            const int FrameLimit = 300;
            int frames = 0;
            while (!condition() && frames++ < FrameLimit)
            {
                yield return null;
            }

            Assert.That(condition(), Is.True, "Timed out waiting for " + description + ".");
        }

        private static void PreparePair(ShopShellPresenter presenter, string first, string second)
        {
            Assert.That(presenter.GrindHerb(first).Succeeded, Is.True);
            Assert.That(presenter.AddGroundHerbToPot().Succeeded, Is.True);
            Assert.That(presenter.GrindHerb(second).Succeeded, Is.True);
            Assert.That(presenter.AddGroundHerbToPot().Succeeded, Is.True);
        }

        private static HerbDragItem FindHerb(string herbId)
        {
            foreach (HerbDragItem item in Object.FindObjectsByType<HerbDragItem>())
            {
                if (item.HerbId == herbId)
                {
                    return item;
                }
            }

            Assert.Fail("Herb UI not found: " + herbId);
            return null;
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

        private sealed class Harness
        {
            public Harness(
                IGameSessionStore store,
                MutableClock clock,
                OpenShopUseCase openShop,
                AdmitCustomerUseCase admit,
                PotionPreparationUseCase preparation,
                ServePotionUseCase serve,
                UpdateShopTimerUseCase updateTimer)
            {
                Store = store;
                Clock = clock;
                OpenShop = openShop;
                Admit = admit;
                Preparation = preparation;
                Serve = serve;
                UpdateTimer = updateTimer;
            }

            public IGameSessionStore Store { get; }
            public MutableClock Clock { get; }
            public OpenShopUseCase OpenShop { get; }
            public AdmitCustomerUseCase Admit { get; }
            public PotionPreparationUseCase Preparation { get; }
            public ServePotionUseCase Serve { get; }
            public UpdateShopTimerUseCase UpdateTimer { get; }
        }

        private sealed class MutableClock : IClock
        {
            public DateTimeOffset UtcNow { get; private set; } = new DateTimeOffset(2026, 10, 24, 0, 0, 0, TimeSpan.Zero);
            public void Advance(TimeSpan value) => UtcNow += value;
        }

        private sealed class FixedRandom : IRandomSource
        {
            public int NextInt(int minimumInclusive, int maximumExclusive) => minimumInclusive;
        }

        private sealed class FakeSceneLoader : ISceneLoader
        {
            public Task<SceneLoadResult> LoadAsync(SceneId sceneId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(SceneLoadResult.Success());
            }
        }

        private sealed class FakeSettingsRepository : ISettingsRepository
        {
            public Task<SaveStateResult> SaveSettingsAsync(GameSettingsState settings, CancellationToken cancellationToken = default)
                => Task.FromResult(SaveStateResult.Success());
            public Task<SettingsLoadResult> LoadSettingsAsync(CancellationToken cancellationToken = default)
                => Task.FromResult(SettingsLoadResult.Success(GameSettingsState.Default));
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
