using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Witch.Application.Flow;
using Witch.Application.Ports;
using Witch.Domain;
using Witch.Domain.Orders;
using Witch.Domain.Potions;
using Witch.Domain.Progression;

namespace Witch.Application.Tests
{
    public sealed class MinigameTransactionUseCaseTests
    {
        [Test]
        public async Task Enter_PersistsPendingBeforeLoadingScene()
        {
            GameSession session = CreatePendingMinigameSession(9);
            var operations = new List<string>();
            var saves = new FakeSaveRepository { Operations = operations };
            var scenes = new FakeSceneLoader { Operations = operations };
            var useCase = new EnterMinigameUseCase(saves, scenes, new FakeRandomSource { NextValue = 707 });

            MinigameTransactionResult result = await useCase.ExecuteAsync(session);

            Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
            Assert.That(operations, Is.EqualTo(new[] { "save", "scene:MatchCardMinigame" }));
            Assert.That(saves.SavedAttemptMetadata.PendingMinigame, Is.True);
            Assert.That(saves.SavedAttemptMetadata.MinigameBoardSeed, Is.EqualTo(707));
            Assert.That(result.Board.PairCount, Is.EqualTo(8));
            Assert.That(session.TimerStopped, Is.True);
        }

        [Test]
        public async Task Enter_SaveFailureDoesNotRequestScene()
        {
            GameSession session = CreatePendingMinigameSession(0);
            var saves = new FakeSaveRepository
            {
                SaveResult = SaveStateResult.Failure(SaveStateError.WriteFailed, "disk full")
            };
            var scenes = new FakeSceneLoader();

            MinigameTransactionResult result = await new EnterMinigameUseCase(
                saves,
                scenes,
                new FakeRandomSource()).ExecuteAsync(session);

            Assert.That(result.Error, Is.EqualTo(MinigameTransactionError.SaveFailed));
            Assert.That(scenes.LoadCallCount, Is.Zero);
            Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.MinigamePending));
        }

        [Test]
        public async Task Enter_SceneFailureFallsBackToMenuAndCanRetry()
        {
            GameSession session = CreatePendingMinigameSession(0);
            var operations = new List<string>();
            var scenes = new FakeSceneLoader { Operations = operations };
            scenes.Results.Enqueue(SceneLoadResult.Failure(SceneLoadError.LoadFailed, "minigame unavailable"));
            scenes.Results.Enqueue(SceneLoadResult.Success());
            var useCase = new EnterMinigameUseCase(
                new FakeSaveRepository { Operations = operations },
                scenes,
                new FakeRandomSource { NextValue = 707 });

            MinigameTransactionResult failed = await useCase.ExecuteAsync(session);
            MinigameTransactionResult retry = await useCase.ExecuteAsync(session);

            Assert.That(failed.Error, Is.EqualTo(MinigameTransactionError.SceneLoadFailed));
            Assert.That(retry.Succeeded, Is.True, retry.ErrorMessage);
            Assert.That(retry.AlreadyApplied, Is.True);
            Assert.That(operations, Is.EqualTo(new[]
            {
                "save",
                "scene:MatchCardMinigame",
                "scene:MainMenu",
                "scene:MatchCardMinigame"
            }));
        }

        [Test]
        public async Task Complete_QueueOpen_AppliesLiveStateOnly_ResetsSuspicion_PreservesCommittedState_AndSetsArrivalPaused()
        {
            GameSession session = CreatePendingMinigameSession(9, timerSeconds: 60);
            (int money, int correct, int wrong, int wrongSummary, int coins, int serviceCount, int suspicionBefore, int winCountBefore, TimeSpan remaining, string[] usedOrders) before =
                CapturePostWrongServiceState(session);
            Assert.That(session.TimerState.QueueOpen, Is.True);

            var saves = new FakeSaveRepository();
            var scenes = new FakeSceneLoader();
            await new EnterMinigameUseCase(saves, scenes, new FakeRandomSource { NextValue = 707 }).ExecuteAsync(session);
            var complete = new CompleteMinigameUseCase(saves, scenes, new FakeRandomSource { NextValue = 808 });

            MinigameTransactionResult result = await complete.ExecuteAsync(session);

            Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
            Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.CustomerArrival));
            Assert.That(session.AttemptState.ActiveOrder, Is.Null);
            Assert.That(session.AttemptState.ActiveCustomerId, Is.Null);
            Assert.That(session.ServiceCommitted, Is.True);
            Assert.That(session.TimerState.RemainingTime, Is.EqualTo(before.remaining));
            Assert.That(session.TimerStopped, Is.False);
            Assert.That(session.TimerPaused, Is.True);
            Assert.That(session.RunState.Suspicion, Is.EqualTo(0));
            Assert.That(session.RunState.MinigameWinCount, Is.EqualTo(before.winCountBefore + 1));
            Assert.That(session.RunState.Money, Is.EqualTo(before.money));
            Assert.That(session.AttemptMetadata.CorrectServiceCountToday, Is.EqualTo(before.correct));
            Assert.That(session.AttemptMetadata.WrongServiceCountToday, Is.EqualTo(before.wrong));
            Assert.That(session.AttemptMetadata.WrongOrdersForSummary, Is.EqualTo(before.wrongSummary));
            Assert.That(session.AttemptMetadata.ServiceCountToday, Is.EqualTo(before.serviceCount));
            Assert.That(session.AttemptMetadata.CoinsEarnedToday, Is.EqualTo(before.coins));
            Assert.That(session.AttemptState.UsedOrderIds, Is.EqualTo(before.usedOrders));
            Assert.That(session.RunState.SeenOrderIds, Is.EquivalentTo(before.usedOrders));
            Assert.That(session.AttemptMetadata.PendingMinigame, Is.False);
            Assert.That(session.AttemptMetadata.MinigameBoardSeed, Is.EqualTo(0));
            Assert.That(session.AttemptMetadata.MinigamePlayedToday, Is.True);
        }

        [Test]
        public async Task Complete_QueueClosed_AppliesLiveStateOnly_LeavesTimerStoppedAtZero_AndSetsClosingEncounter()
        {
            GameSession session = CreatePendingMinigameSession(9, timerSeconds: 60);
            ForceTimerRemaining(session, TimeSpan.Zero);
            Assert.That(session.TimerState.RemainingTime, Is.EqualTo(TimeSpan.Zero));
            Assert.That(session.TimerState.QueueOpen, Is.False);
            (int money, int correct, int wrong, int wrongSummary, int coins, int serviceCount, int suspicionBefore, int winCountBefore, TimeSpan remaining, string[] usedOrders) before =
                CapturePostWrongServiceState(session);

            var saves = new FakeSaveRepository();
            var scenes = new FakeSceneLoader();
            await new EnterMinigameUseCase(saves, scenes, new FakeRandomSource { NextValue = 707 }).ExecuteAsync(session);
            var complete = new CompleteMinigameUseCase(saves, scenes, new FakeRandomSource { NextValue = 808 });

            MinigameTransactionResult result = await complete.ExecuteAsync(session);

            Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
            Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.ClosingEncounter));
            Assert.That(session.AttemptState.ActiveOrder, Is.Null);
            Assert.That(session.TimerState.RemainingTime, Is.EqualTo(TimeSpan.Zero));
            Assert.That(session.TimerState.QueueOpen, Is.False);
            Assert.That(session.TimerStopped, Is.True);
            Assert.That(session.TimerPaused, Is.False);
            Assert.That(session.RunState.Suspicion, Is.EqualTo(0));
            Assert.That(session.RunState.MinigameWinCount, Is.EqualTo(before.winCountBefore + 1));
            Assert.That(session.RunState.Money, Is.EqualTo(before.money));
            Assert.That(session.AttemptMetadata.CorrectServiceCountToday, Is.EqualTo(before.correct));
            Assert.That(session.AttemptMetadata.WrongServiceCountToday, Is.EqualTo(before.wrong));
            Assert.That(session.AttemptMetadata.WrongOrdersForSummary, Is.EqualTo(before.wrongSummary));
            Assert.That(session.AttemptMetadata.ServiceCountToday, Is.EqualTo(before.serviceCount));
            Assert.That(session.AttemptMetadata.CoinsEarnedToday, Is.EqualTo(before.coins));
            Assert.That(session.AttemptMetadata.PendingMinigame, Is.False);
            Assert.That(session.AttemptMetadata.MinigameBoardSeed, Is.EqualTo(0));
            Assert.That(session.AttemptMetadata.MinigamePlayedToday, Is.True);
        }

        [Test]
        public async Task Complete_PersistsSnapshotAsCompleteBuildsToday_MetadataFromSession_AndSaveReloadDoesNotRouteToMinigame()
        {
            GameSession session = CreatePendingMinigameSession(9, timerSeconds: 60);
            var saves = new FakeSaveRepository();
            var scenes = new FakeSceneLoader();
            await new EnterMinigameUseCase(saves, scenes, new FakeRandomSource { NextValue = 707 }).ExecuteAsync(session);
            int nextSeed = 808;
            MatchCardOutcome expectedOutcome = MatchCardRules.Complete(session.DayStartSnapshot, session.RunState, nextSeed);

            MinigameTransactionResult result = await new CompleteMinigameUseCase(saves, scenes, new FakeRandomSource { NextValue = nextSeed }).ExecuteAsync(session);

            Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
            Assert.That(saves.SavedSnapshot.RunState.Money, Is.EqualTo(expectedOutcome.RestoredRunState.Money));
            Assert.That(saves.SavedSnapshot.RunState.Suspicion, Is.EqualTo(0));
            Assert.That(saves.SavedSnapshot.RunState.MinigameWinCount, Is.EqualTo(expectedOutcome.RestoredRunState.MinigameWinCount));
            Assert.That(saves.SavedSnapshot.OrderSeed, Is.EqualTo(expectedOutcome.NextAttemptSeed));
            Assert.That(saves.SavedSnapshot.RunState.SeenOrderIds, Is.EquivalentTo(session.RunState.SeenOrderIds));
            Assert.That(saves.SavedAttemptMetadata.Suspicion, Is.EqualTo(0));
            Assert.That(saves.SavedAttemptMetadata.MinigameWinCount, Is.EqualTo(session.RunState.MinigameWinCount));
            Assert.That(saves.SavedAttemptMetadata.PendingMinigame, Is.False);
            Assert.That(saves.SavedAttemptMetadata.MinigameBoardSeed, Is.EqualTo(0));
            Assert.That(saves.SavedAttemptMetadata.MinigamePlayedToday, Is.True);
            Assert.That(saves.SavedAttemptMetadata.CorrectServiceCountToday, Is.EqualTo(session.AttemptMetadata.CorrectServiceCountToday));
            Assert.That(saves.SavedAttemptMetadata.WrongServiceCountToday, Is.EqualTo(session.AttemptMetadata.WrongServiceCountToday));
            Assert.That(saves.SavedAttemptMetadata.WrongOrdersForSummary, Is.EqualTo(session.AttemptMetadata.WrongOrdersForSummary));
            Assert.That(saves.SavedAttemptMetadata.ServiceCountToday, Is.EqualTo(session.AttemptMetadata.ServiceCountToday));
            Assert.That(saves.SavedAttemptMetadata.CoinsEarnedToday, Is.EqualTo(session.AttemptMetadata.CoinsEarnedToday));

            var loadRepo = new FakeSaveRepository
            {
                LatestLoadResult = LoadStateResult.Success(new GameSaveState(saves.SavedSnapshot, saves.SavedAttemptMetadata))
            };
            var loadScenes = new FakeSceneLoader();
            LoadRunResult loaded = await new LoadRunUseCase(loadRepo, loadRepo, new GameSessionStore(), loadScenes)
                .ExecuteAsync(LoadRunCommand.Latest());

            Assert.That(loaded.Succeeded, Is.True, loaded.ErrorMessage);
            Assert.That(loadScenes.LastRequestedScene, Is.EqualTo(SceneId.Shop));
            Assert.That(loaded.Session.AttemptMetadata.PendingMinigame, Is.False);
            Assert.That(loaded.Session.CurrentPhase, Is.EqualTo(GamePhase.ShopPreparation));
        }

        [Test]
        public async Task Complete_DuplicateRetry_DoesNotApplyTwice_ReportsMemoryMenderOnce_AndKeepsFarewellConsumedState()
        {
            GameSession session = CreatePendingMinigameSession(9, timerSeconds: 60);
            var saves = new FakeSaveRepository();
            var scenes = new FakeSceneLoader();
            var random = new FakeRandomSource { NextValue = 808 };
            await new EnterMinigameUseCase(saves, scenes, new FakeRandomSource { NextValue = 707 }).ExecuteAsync(session);
            var complete = new CompleteMinigameUseCase(saves, scenes, random);

            MinigameTransactionResult first = await complete.ExecuteAsync(session);
            Assert.That(first.Succeeded, Is.True, first.ErrorMessage);
            int saveCalls = saves.SaveCallCount;
            int randomCalls = random.CallCount;
            int winCount = session.RunState.MinigameWinCount;
            TimeSpan remaining = session.TimerState.RemainingTime;
            Assert.That(session.TryConsumeHunterFarewell(out int recorded), Is.True);
            Assert.That(recorded, Is.EqualTo(winCount - 1));

            MinigameTransactionResult duplicate = await complete.ExecuteAsync(session);

            Assert.That(duplicate.Succeeded, Is.True, duplicate.ErrorMessage);
            Assert.That(duplicate.AlreadyApplied, Is.True);
            Assert.That(saves.SaveCallCount, Is.EqualTo(saveCalls));
            Assert.That(random.CallCount, Is.EqualTo(randomCalls));
            Assert.That(session.RunState.MinigameWinCount, Is.EqualTo(winCount));
            Assert.That(session.TimerState.RemainingTime, Is.EqualTo(remaining));
            Assert.That(session.TryConsumeHunterFarewell(out _), Is.False);
        }

        [Test]
        public async Task Complete_SceneFailureFallsBackToMenu_AndRetryLoadsShopWithoutReapplying()
        {
            GameSession session = CreatePendingMinigameSession(9, timerSeconds: 60);
            var saves = new FakeSaveRepository();
            var scenes = new FakeSceneLoader();
            await new EnterMinigameUseCase(saves, scenes, new FakeRandomSource { NextValue = 707 }).ExecuteAsync(session);
            scenes.Results.Enqueue(SceneLoadResult.Failure(SceneLoadError.LoadFailed, "shop unavailable"));
            scenes.Results.Enqueue(SceneLoadResult.Success());
            var complete = new CompleteMinigameUseCase(saves, scenes, new FakeRandomSource { NextValue = 808 });

            MinigameTransactionResult failed = await complete.ExecuteAsync(session);
            int money = session.RunState.Money;
            int suspicion = session.RunState.Suspicion;
            int winCount = session.RunState.MinigameWinCount;
            int saveCalls = saves.SaveCallCount;
            MinigameTransactionResult retry = await complete.ExecuteAsync(session);

            Assert.That(failed.Error, Is.EqualTo(MinigameTransactionError.SceneLoadFailed));
            Assert.That(retry.Succeeded, Is.True, retry.ErrorMessage);
            Assert.That(retry.AlreadyApplied, Is.True);
            Assert.That(session.CurrentPhase == GamePhase.CustomerArrival || session.CurrentPhase == GamePhase.ClosingEncounter, Is.True);
            Assert.That(session.RunState.Money, Is.EqualTo(money));
            Assert.That(session.RunState.Suspicion, Is.EqualTo(suspicion));
            Assert.That(session.RunState.MinigameWinCount, Is.EqualTo(winCount));
            Assert.That(saves.SaveCallCount, Is.EqualTo(saveCalls));
            Assert.That(scenes.LoadCallCount, Is.EqualTo(4));
        }

        [Test]
        public async Task Complete_MarksHunterFarewellWithWinCountBeforeThisOne()
        {
            GameSession session = CreatePendingMinigameSession(9, timerSeconds: 60);
            var saves = new FakeSaveRepository();
            var scenes = new FakeSceneLoader();
            await new EnterMinigameUseCase(saves, scenes, new FakeRandomSource { NextValue = 707 }).ExecuteAsync(session);
            int before = session.RunState.MinigameWinCount;

            MinigameTransactionResult result = await new CompleteMinigameUseCase(saves, scenes, new FakeRandomSource { NextValue = 808 }).ExecuteAsync(session);

            Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
            Assert.That(session.TryConsumeHunterFarewell(out int recorded), Is.True);
            Assert.That(recorded, Is.EqualTo(before));
            Assert.That(session.RunState.MinigameWinCount, Is.EqualTo(before + 1));
        }

        [Test]
        public void ApplyTutorialFlag_SetsFlagOnRunStateAndDayStartSnapshot()
        {
            GameSession session = CreateInProgressSession(60);
            RunState dayStartBefore = session.DayStartSnapshot.RunState;
            int money = dayStartBefore.Money;
            int suspicion = dayStartBefore.Suspicion;
            IReadOnlyList<string> seenOrders = dayStartBefore.SeenOrderIds;

            session.ApplyTutorialFlag("shop.tutorial.day1.guided", true);

            Assert.That(session.RunState.TutorialFlags.TryGetValue("shop.tutorial.day1.guided", out bool live) && live, Is.True);
            Assert.That(session.DayStartSnapshot.RunState.TutorialFlags.TryGetValue("shop.tutorial.day1.guided", out bool snap) && snap, Is.True);
            Assert.That(session.DayStartSnapshot.RunState.Money, Is.EqualTo(money));
            Assert.That(session.DayStartSnapshot.RunState.Suspicion, Is.EqualTo(suspicion));
            Assert.That(session.DayStartSnapshot.RunState.SeenOrderIds, Is.EqualTo(seenOrders));
        }

        [Test]
        public async Task Complete_KeepsTutorialFlagOnLiveStateAndPersistedSnapshot()
        {
            GameSession session = CreatePendingMinigameSession(9, timerSeconds: 60);
            session.ApplyTutorialFlag("shop.tutorial.day1.guided", true);
            var saves = new FakeSaveRepository();
            var scenes = new FakeSceneLoader();
            await new EnterMinigameUseCase(saves, scenes, new FakeRandomSource { NextValue = 707 }).ExecuteAsync(session);

            MinigameTransactionResult result = await new CompleteMinigameUseCase(saves, scenes, new FakeRandomSource { NextValue = 808 }).ExecuteAsync(session);

            Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
            Assert.That(session.RunState.TutorialFlags.TryGetValue("shop.tutorial.day1.guided", out bool live) && live, Is.True);
            Assert.That(saves.SavedSnapshot.RunState.TutorialFlags.TryGetValue("shop.tutorial.day1.guided", out bool saved) && saved, Is.True);
        }

        [Test]
        public async Task Complete_DoesNotAffectMemoryMenderReportingStateOnDuplicateAlreadyApplied()
        {
            GameSession session = CreatePendingMinigameSession(9, timerSeconds: 60);
            var saves = new FakeSaveRepository();
            var scenes = new FakeSceneLoader();
            await new EnterMinigameUseCase(saves, scenes, new FakeRandomSource { NextValue = 707 }).ExecuteAsync(session);
            var complete = new CompleteMinigameUseCase(saves, scenes, new FakeRandomSource { NextValue = 808 });
            await complete.ExecuteAsync(session);
            MinigameTransactionResult duplicate = await complete.ExecuteAsync(session);
            Assert.That(duplicate.AlreadyApplied, Is.True);
        }

        [Test]
        public async Task Lose_TimeUp_LeavesPendingStateSavedAndContinueReturnsToMinigame()
        {
            GameSession session = CreatePendingMinigameSession(9, timerSeconds: 60);
            var saves = new FakeSaveRepository();
            var scenes = new FakeSceneLoader();
            var enter = new EnterMinigameUseCase(saves, scenes, new FakeRandomSource { NextValue = 707 });

            MinigameTransactionResult entered = await enter.ExecuteAsync(session);

            Assert.That(entered.Succeeded, Is.True, entered.ErrorMessage);
            Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.MinigameActive));
            Assert.That(session.AttemptMetadata.PendingMinigame, Is.True);
            Assert.That(session.AttemptMetadata.MinigameBoardSeed, Is.EqualTo(707));
            Assert.That(session.RunState.Suspicion, Is.EqualTo(3));
            Assert.That(session.RunState.MinigameWinCount, Is.EqualTo(0));
            Assert.That(saves.SavedAttemptMetadata, Is.Not.Null);
            Assert.That(saves.SavedAttemptMetadata.PendingMinigame, Is.True);
            Assert.That(saves.SavedAttemptMetadata.MinigameBoardSeed, Is.EqualTo(707));

            int suspicionBeforeLoss = session.RunState.Suspicion;
            int winCountBeforeLoss = session.RunState.MinigameWinCount;
            int moneyBeforeLoss = session.RunState.Money;
            int boardSeedBeforeLoss = session.AttemptMetadata.MinigameBoardSeed;

            // Losing the minigame (time up) is handled in presentation and does not apply any completion or state mutation.
            Assert.That(session.AttemptMetadata.PendingMinigame, Is.True);
            Assert.That(session.AttemptMetadata.MinigameBoardSeed, Is.EqualTo(boardSeedBeforeLoss));
            Assert.That(session.RunState.Suspicion, Is.EqualTo(suspicionBeforeLoss));
            Assert.That(session.RunState.MinigameWinCount, Is.EqualTo(winCountBeforeLoss));
            Assert.That(session.RunState.Money, Is.EqualTo(moneyBeforeLoss));

            DayStartSnapshot snapshotForLoad = saves.SavedSnapshot ?? session.DayStartSnapshot;
            Assert.That(saves.SavedAttemptMetadata, Is.Not.Null);
            var loadRepo = new FakeSaveRepository
            {
                LatestLoadResult = LoadStateResult.Success(new GameSaveState(snapshotForLoad, saves.SavedAttemptMetadata))
            };
            var loadScenes = new FakeSceneLoader();
            LoadRunResult loaded = await new LoadRunUseCase(loadRepo, loadRepo, new GameSessionStore(), loadScenes)
                .ExecuteAsync(LoadRunCommand.Latest());

            Assert.That(loaded.Succeeded, Is.True, loaded.ErrorMessage);
            Assert.That(loadScenes.LastRequestedScene, Is.EqualTo(SceneId.MatchCardMinigame));
            Assert.That(loaded.Session.CurrentPhase, Is.EqualTo(GamePhase.MinigameActive));
            Assert.That(loaded.Session.AttemptMetadata.PendingMinigame, Is.True);
            Assert.That(loaded.Session.AttemptMetadata.MinigameBoardSeed, Is.EqualTo(707));
        }

        private static void ForceTimerRemaining(GameSession session, TimeSpan remaining)
        {
            var timerStateType = session.TimerState.GetType();
            object timerState = Activator.CreateInstance(timerStateType, remaining, false);
            typeof(GameSession).GetProperty("TimerState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.SetValue(session, timerState);

            var attemptStateType = session.AttemptState.GetType();
            object attemptState = Activator.CreateInstance(
                attemptStateType,
                session.AttemptState.AttemptSeed,
                remaining,
                session.AttemptState.UsedOrderIds,
                session.AttemptState.ActiveCustomerId,
                session.AttemptState.ActiveOrder,
                session.AttemptState.EarnedMoney,
                session.AttemptState.PotionDraft,
                session.AttemptState.PreparedPotion);
            typeof(GameSession).GetProperty("AttemptState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.SetValue(session, attemptState);
        }

        private static (int Money, int Correct, int Wrong, int WrongSummary, int Coins, int ServiceCount, int Suspicion, int MinigameWinCount, TimeSpan Remaining, string[] UsedOrders) CapturePostWrongServiceState(GameSession session)
        {
            return (
                session.RunState.Money,
                session.AttemptMetadata.CorrectServiceCountToday,
                session.AttemptMetadata.WrongServiceCountToday,
                session.AttemptMetadata.WrongOrdersForSummary,
                session.AttemptMetadata.CoinsEarnedToday,
                session.AttemptMetadata.ServiceCountToday,
                session.RunState.Suspicion,
                session.RunState.MinigameWinCount,
                session.TimerState.RemainingTime,
                new System.Collections.Generic.List<string>(session.AttemptState.UsedOrderIds).ToArray());
        }

        private static GameSession CreatePendingMinigameSession(int startingMoney, int timerSeconds = 240)
        {
            var rules = CreateRules();
            GameSession session = new StartNewRunUseCase().Execute(new StartNewRunCommand(
                "run.minigame",
                startingMoney,
                new[] { "category.warm" },
                new[] { "herb.ginger", "herb.mint", "herb.sage" },
                new[] { "guidebook.warm" },
                101,
                TimeSpan.FromSeconds(timerSeconds),
                "expense.day.1",
                20,
                1,
                DateTimeOffset.UtcNow)).Session;
            new OpenShopUseCase().Execute(session);
            var arrival = new AdmitCustomerUseCase(new FakeClock());
            var prepare = new PotionPreparationUseCase(rules);
            var serve = new ServePotionUseCase(rules);

            for (int index = 0; index < 3; index++)
            {
                arrival.Execute(
                    session,
                    new AdmitCustomerCommand(
                        "customer." + index,
                        new OrderRequest(
                            "order." + index,
                            new HerbPair("herb.ginger", "herb.mint"))));
                prepare.Grind(session, "herb.mint", "herb.sage");
                prepare.AddToPot(session);
                prepare.Brew(session);
                serve.Execute(session);
            }

            Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.MinigamePending));
            return session;
        }

        private static PotionServiceRules CreateRules()
        {
            return new PotionServiceRules(10);
        }

        private static GameSession CreateInProgressSession(int timerSeconds)
        {
            GameSession session = new StartNewRunUseCase().Execute(new StartNewRunCommand(
                "run.progress",
                0,
                new[] { "category.warm" },
                new[] { "herb.ginger", "herb.mint" },
                new[] { "guidebook.warm" },
                101,
                TimeSpan.FromSeconds(timerSeconds),
                "expense.day.1",
                20,
                1,
                DateTimeOffset.UtcNow)).Session;
            new OpenShopUseCase().Execute(session);
            return session;
        }
    }
}
