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
        public async Task Complete_RestoresSnapshotAndDuplicateDoesNotApplyTwice()
        {
            GameSession session = CreatePendingMinigameSession(9);
            var saves = new FakeSaveRepository();
            var scenes = new FakeSceneLoader();
            await new EnterMinigameUseCase(saves, scenes, new FakeRandomSource { NextValue = 707 })
                .ExecuteAsync(session);
            var random = new FakeRandomSource { NextValue = 808 };
            var complete = new CompleteMinigameUseCase(saves, scenes, random);

            MinigameTransactionResult first = await complete.ExecuteAsync(session);
            MinigameTransactionResult duplicate = await complete.ExecuteAsync(session);

            Assert.That(first.Succeeded, Is.True, first.ErrorMessage);
            Assert.That(duplicate.Succeeded, Is.True);
            Assert.That(duplicate.AlreadyApplied, Is.True);
            Assert.That(session.RunState.Money, Is.EqualTo(9));
            Assert.That(session.RunState.Suspicion, Is.Zero);
            Assert.That(session.RunState.MinigameWinCount, Is.EqualTo(1));
            Assert.That(session.AttemptMetadata.ActiveOrderSeed, Is.EqualTo(808));
            Assert.That(session.AttemptMetadata.PendingMinigame, Is.False);
            Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.CustomerEncounter));
            Assert.That(saves.SaveCallCount, Is.EqualTo(2));
            Assert.That(random.CallCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Complete_SceneFailureFallsBackToMenuAndCanRetry()
        {
            GameSession session = CreatePendingMinigameSession(9);
            var saves = new FakeSaveRepository();
            var scenes = new FakeSceneLoader();
            await new EnterMinigameUseCase(saves, scenes, new FakeRandomSource { NextValue = 707 })
                .ExecuteAsync(session);
            scenes.Results.Enqueue(SceneLoadResult.Failure(SceneLoadError.LoadFailed, "shop unavailable"));
            scenes.Results.Enqueue(SceneLoadResult.Success());
            var complete = new CompleteMinigameUseCase(saves, scenes, new FakeRandomSource { NextValue = 808 });

            MinigameTransactionResult failed = await complete.ExecuteAsync(session);
            MinigameTransactionResult retry = await complete.ExecuteAsync(session);

            Assert.That(failed.Error, Is.EqualTo(MinigameTransactionError.SceneLoadFailed));
            Assert.That(retry.Succeeded, Is.True, retry.ErrorMessage);
            Assert.That(retry.AlreadyApplied, Is.True);
            Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.CustomerEncounter));
            Assert.That(scenes.LoadCallCount, Is.EqualTo(4));
        }

        [Test]
        public void CompleteMinigameInPlace_KeepsTutorialFlagEvenWhenResumeRunStateLacksIt()
        {
            GameSession session = CreateInPlaceMinigameSessionWithFlagOnLiveStateButNotResume();
            var restoredSnapshot = new DayStartSnapshot(
                session.DayStartSnapshot.RunState.WithProgress(1, session.DayStartSnapshot.RunState.Money, 0, 1),
                808,
                session.DayStartSnapshot.TimerDuration,
                session.DayStartSnapshot.ExpenseId,
                session.DayStartSnapshot.ExpenseValue,
                session.DayStartSnapshot.SaveVersion,
                session.DayStartSnapshot.CapturedAtUtc);
            var metadata = new ActiveAttemptMetadata(
                session.RunState.RunId,
                1,
                0,
                1,
                false,
                0,
                808,
                session.AttemptMetadata.WrongServiceCountToday,
                session.AttemptMetadata.ServiceCountToday,
                true,
                0,
                0,
                0);

            session.CompleteMinigameInPlace(restoredSnapshot, metadata);

            Assert.That(session.RunState.TutorialFlags.TryGetValue("shop.tutorial.day1.guided", out bool completed) && completed, Is.True);
            Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.CustomerEncounter));
        }

        [Test]
        public void ApplyTutorialFlag_SetsFlagOnRunStateResumeAndDayStartSnapshotOnly()
        {
            GameSession session = CreateInPlaceCapableSession();
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

            var resumeField = typeof(GameSession).GetField("resumeRunState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var resume = (RunState)resumeField.GetValue(session);
            Assert.That(resume, Is.Not.Null);
            Assert.That(resume.TutorialFlags.TryGetValue("shop.tutorial.day1.guided", out bool resumeFlag) && resumeFlag, Is.True);
        }

        [Test]
        public async Task Complete_FullMinigameResetPath_KeepsTutorialFlag()
        {
            GameSession session = CreatePendingMinigameSession(9);
            session.ApplyTutorialFlag("shop.tutorial.day1.guided", true);
            var saves = new FakeSaveRepository();
            var scenes = new FakeSceneLoader();
            await new EnterMinigameUseCase(saves, scenes, new FakeRandomSource { NextValue = 707 })
                .ExecuteAsync(session);
            var complete = new CompleteMinigameUseCase(saves, scenes, new FakeRandomSource { NextValue = 808 });

            MinigameTransactionResult result = await complete.ExecuteAsync(session);

            Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
            Assert.That(session.RunState.TutorialFlags.TryGetValue("shop.tutorial.day1.guided", out bool completed) && completed, Is.True);
            Assert.That(saves.SavedSnapshot.RunState.TutorialFlags.TryGetValue("shop.tutorial.day1.guided", out bool saved) && saved, Is.True);
        }

        [Test]
        public async Task Complete_InPlacePath_RecordsPreIncrementMinigameWinCount()
        {
            GameSession session = CreatePendingMinigameSession(9);
            var saves = new FakeSaveRepository();
            var scenes = new FakeSceneLoader();
            await new EnterMinigameUseCase(saves, scenes, new FakeRandomSource { NextValue = 707 })
                .ExecuteAsync(session);

            MinigameTransactionResult result = await new CompleteMinigameUseCase(
                saves,
                scenes,
                new FakeRandomSource { NextValue = 808 }).ExecuteAsync(session);

            Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
            Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.CustomerEncounter));
            Assert.That(session.TryConsumeHunterFarewell(out int recordedMinigameWins) && recordedMinigameWins == 0, Is.True);
        }

        [TestCase(0)]
        [TestCase(2)]
        [TestCase(4)]
        [TestCase(5)]
        public async Task Complete_FullPath_RecordsPreIncrementMinigameWinCount(int completedMinigameWins)
        {
            GameSession session = CreatePendingMinigameSession(9);
            var saves = new FakeSaveRepository();
            var scenes = new FakeSceneLoader();
            await new EnterMinigameUseCase(saves, scenes, new FakeRandomSource { NextValue = 707 })
                .ExecuteAsync(session);
            SetMinigameWinCount(session, completedMinigameWins);
            ClearResumeSnapshot(session);

            MinigameTransactionResult result = await new CompleteMinigameUseCase(
                saves,
                scenes,
                new FakeRandomSource { NextValue = 808 }).ExecuteAsync(session);

            Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
            Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.ShopPreparation));
            Assert.That(session.RunState.MinigameWinCount, Is.EqualTo(completedMinigameWins + 1));
            Assert.That(session.TryConsumeHunterFarewell(out int recordedMinigameWins), Is.True);
            Assert.That(recordedMinigameWins, Is.EqualTo(completedMinigameWins));
        }

        [Test]
        public async Task Complete_DuplicateRetry_DoesNotSetOrClearMarker()
        {
            GameSession session = CreatePendingMinigameSession(9);
            var saves = new FakeSaveRepository();
            var scenes = new FakeSceneLoader();
            await new EnterMinigameUseCase(saves, scenes, new FakeRandomSource { NextValue = 707 })
                .ExecuteAsync(session);
            var complete = new CompleteMinigameUseCase(saves, scenes, new FakeRandomSource { NextValue = 808 });
            await complete.ExecuteAsync(session);

            Assert.That(session.TryConsumeHunterFarewell(out _), Is.True);

            MinigameTransactionResult duplicate = await complete.ExecuteAsync(session);

            Assert.That(duplicate.AlreadyApplied, Is.True);
            Assert.That(session.TryConsumeHunterFarewell(out _), Is.False);
        }

        [Test]
        public void TryConsumeHunterFarewell_ReturnsValueOnceThenClears()
        {
            GameSession session = CreateInPlaceCapableSession();
            session.MarkHunterFarewell(3);

            Assert.That(session.TryConsumeHunterFarewell(out int first), Is.True);
            Assert.That(first, Is.EqualTo(3));
            Assert.That(session.TryConsumeHunterFarewell(out _), Is.False);
        }

        [Test]
        public void RetryDay_ClearsHunterFarewell()
        {
            GameSession session = CreateInPlaceCapableSession();
            session.MarkHunterFarewell(1);

            session.RetryDay(session.DayStartSnapshot, session.AttemptMetadata);

            Assert.That(session.TryConsumeHunterFarewell(out _), Is.False);
        }

        [Test]
        public void AdvanceDay_ClearsHunterFarewell()
        {
            GameSession session = CreateInPlaceCapableSession();
            session.MarkHunterFarewell(1);

            session.AdvanceDay(
                session.DayStartSnapshot,
                session.AttemptMetadata,
                new DaySummaryData(0, 0, 0, 0, 0, 1));

            Assert.That(session.TryConsumeHunterFarewell(out _), Is.False);
        }

        [Test]
        public void EnterMinigame_ClearsHunterFarewell()
        {
            GameSession session = CreateInPlaceCapableSession();
            session.MarkHunterFarewell(1);

            session.EnterMinigame(session.AttemptMetadata);

            Assert.That(session.TryConsumeHunterFarewell(out _), Is.False);
        }

        private static void SetMinigameWinCount(GameSession session, int minigameWinCount)
        {
            RunState runState = session.RunState.WithProgress(
                session.RunState.CurrentDayNumber,
                session.RunState.Money,
                session.RunState.Suspicion,
                minigameWinCount);
            typeof(GameSession).GetProperty("RunState").SetValue(session, runState);
        }

        private static void ClearResumeSnapshot(GameSession session)
        {
            typeof(GameSession)
                .GetField("resumeRunState", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(session, null);
            Assert.That(session.CanResumeInPlace, Is.False);
        }

        private static GameSession CreatePendingMinigameSession(int startingMoney)
        {
            var rules = CreateRules();
            GameSession session = new StartNewRunUseCase().Execute(new StartNewRunCommand(
                "run.minigame",
                startingMoney,
                new[] { "category.warm" },
                new[] { "herb.ginger", "herb.mint", "herb.sage" },
                new[] { "guidebook.warm" },
                101,
                TimeSpan.FromMinutes(4),
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

        private static GameSession CreateInPlaceCapableSession()
        {
            var session = new StartNewRunUseCase().Execute(new StartNewRunCommand(
                "run.inplace",
                0,
                new[] { "category.warm" },
                new[] { "herb.ginger", "herb.mint" },
                new[] { "guidebook.warm" },
                101,
                TimeSpan.FromMinutes(4),
                "expense.day.1",
                20,
                1,
                DateTimeOffset.UtcNow)).Session;
            new OpenShopUseCase().Execute(session);
            new AdmitCustomerUseCase(new FakeClock()).Execute(
                session,
                new AdmitCustomerCommand(
                    "customer.1",
                    new OrderRequest("order.1", new HerbPair("herb.ginger", "herb.mint"))));
            Assert.That(session.CanResumeInPlace, Is.True);
            return session;
        }

        private static GameSession CreateInPlaceMinigameSessionWithFlagOnLiveStateButNotResume()
        {
            GameSession session = CreateInPlaceCapableSession();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var resumeField = typeof(GameSession).GetField("resumeRunState", flags);
            RunState resumeWithoutFlag = (RunState)resumeField.GetValue(session);
            typeof(GameSession).GetProperty("RunState").SetValue(session, session.RunState.WithTutorialFlag("shop.tutorial.day1.guided", true));
            typeof(GameSession).GetProperty("DayStartSnapshot").SetValue(session, new DayStartSnapshot(
                session.DayStartSnapshot.RunState.WithTutorialFlag("shop.tutorial.day1.guided", true),
                session.DayStartSnapshot.OrderSeed,
                session.DayStartSnapshot.TimerDuration,
                session.DayStartSnapshot.ExpenseId,
                session.DayStartSnapshot.ExpenseValue,
                session.DayStartSnapshot.SaveVersion,
                session.DayStartSnapshot.CapturedAtUtc));
            resumeField.SetValue(session, resumeWithoutFlag);
            Assert.That(session.CanResumeInPlace, Is.True);
            Assert.That(session.RunState.TutorialFlags.TryGetValue("shop.tutorial.day1.guided", out bool live) && live, Is.True);
            Assert.That(resumeWithoutFlag.TutorialFlags.TryGetValue("shop.tutorial.day1.guided", out bool resume) && resume, Is.False);
            return session;
        }
    }
}
