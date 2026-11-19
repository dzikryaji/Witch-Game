using System;
using System.Collections.Generic;
using System.Threading;
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
    public sealed class DaySummaryStatsTests
    {
        [Test]
        public async Task CountersIncrementCorrectlyPerOutcomeKind_AndSummaryCapturedBeforeReset()
        {
            var clock = new FakeClock();
            GameSession session = CreateSession(startingMoney: 0, expense: 20, timerSeconds: 30);
            var saves = new FakeSaveRepository();
            var resolve = new ResolveExpenseUseCase(
                new ExpenseRules(3),
                CreateSetup(30),
                saves,
                new FakeRandomSource { NextValue = 901 },
                clock);
            var rules = new PotionServiceRules(20);

            new OpenShopUseCase().Execute(session);

            // Correct serve: +1 correct, +20 coins.
            ServeCustomer(session, clock, rules, 1, correct: true, closeAfter: false);
            Assert.That(session.AttemptMetadata.CorrectServiceCountToday, Is.EqualTo(1));
            Assert.That(session.AttemptMetadata.WrongServiceCountToday, Is.EqualTo(0));
            Assert.That(session.AttemptMetadata.WrongOrdersForSummary, Is.EqualTo(0));
            Assert.That(session.AttemptMetadata.ServiceCountToday, Is.EqualTo(1));
            Assert.That(session.AttemptMetadata.CoinsEarnedToday, Is.EqualTo(20));

            // Wrong serve: +1 wrong, +0 coins.
            ServeCustomer(session, clock, rules, 2, correct: false, closeAfter: false);
            Assert.That(session.AttemptMetadata.CorrectServiceCountToday, Is.EqualTo(1));
            Assert.That(session.AttemptMetadata.WrongServiceCountToday, Is.EqualTo(1));
            Assert.That(session.AttemptMetadata.WrongOrdersForSummary, Is.EqualTo(1));
            Assert.That(session.AttemptMetadata.ServiceCountToday, Is.EqualTo(2));
            Assert.That(session.AttemptMetadata.CoinsEarnedToday, Is.EqualTo(20));

            // Another correct serve and close day.
            ServeCustomer(session, clock, rules, 3, correct: true, closeAfter: true);
            Assert.That(session.AttemptMetadata.CorrectServiceCountToday, Is.EqualTo(2));
            Assert.That(session.AttemptMetadata.WrongServiceCountToday, Is.EqualTo(1));
            Assert.That(session.AttemptMetadata.WrongOrdersForSummary, Is.EqualTo(1));
            Assert.That(session.AttemptMetadata.ServiceCountToday, Is.EqualTo(3));
            Assert.That(session.AttemptMetadata.CoinsEarnedToday, Is.EqualTo(40));
            Assert.That(session.RunState.Money, Is.EqualTo(40));

            ExpenseResolutionResult result = await resolve.ExecuteAsync(session);
            Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
            Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.DaySummary));
            Assert.That(session.LastDaySummary, Is.Not.Null);
            Assert.That(session.LastDaySummary.CorrectOrders, Is.EqualTo(2));
            Assert.That(session.LastDaySummary.WrongOrders, Is.EqualTo(1));
            Assert.That(session.LastDaySummary.CoinsEarned, Is.EqualTo(40));
            Assert.That(session.LastDaySummary.TaxPaid, Is.EqualTo(20));
            Assert.That(session.LastDaySummary.CoinsRemaining, Is.EqualTo(20));
            Assert.That(session.LastDaySummary.DayNumber, Is.EqualTo(1));

            // Counters reset for next day after summary capture.
            Assert.That(session.AttemptMetadata.CorrectServiceCountToday, Is.EqualTo(0));
            Assert.That(session.AttemptMetadata.WrongServiceCountToday, Is.EqualTo(0));
            Assert.That(session.AttemptMetadata.WrongOrdersForSummary, Is.EqualTo(0));
            Assert.That(session.AttemptMetadata.ServiceCountToday, Is.EqualTo(0));
            Assert.That(session.AttemptMetadata.CoinsEarnedToday, Is.EqualTo(0));

            // Invariant for the summarized day: coins earned minus tax equals net change captured in summary.
            Assert.That(session.LastDaySummary.CoinsEarned - session.LastDaySummary.TaxPaid,
                Is.EqualTo(session.LastDaySummary.CoinsRemaining));
        }

        [Test]
        public async Task AdvanceDay_ResetsSessionAndPersistedDayCounters_KeepsFinishedDaySummary()
        {
            var clock = new FakeClock();
            GameSession session = CreateSession(startingMoney: 0, expense: 20, timerSeconds: 30);
            var saves = new FakeSaveRepository();
            var resolve = new ResolveExpenseUseCase(
                new ExpenseRules(3),
                CreateSetup(30),
                saves,
                new FakeRandomSource { NextValue = 901 },
                clock);
            var rules = new PotionServiceRules(20);

            new OpenShopUseCase().Execute(session);
            ServeCustomer(session, clock, rules, 1, correct: true, closeAfter: false);
            ServeCustomer(session, clock, rules, 2, correct: false, closeAfter: false);
            ServeCustomer(session, clock, rules, 3, correct: true, closeAfter: true);
            Assert.That(session.AttemptMetadata.CorrectServiceCountToday, Is.EqualTo(2));
            Assert.That(session.AttemptMetadata.WrongServiceCountToday, Is.EqualTo(1));
            Assert.That(session.AttemptMetadata.WrongOrdersForSummary, Is.EqualTo(1));
            Assert.That(session.AttemptMetadata.ServiceCountToday, Is.EqualTo(3));
            Assert.That(session.AttemptMetadata.CoinsEarnedToday, Is.EqualTo(40));
            Assert.That(session.AttemptMetadata.MinigamePlayedToday, Is.False);

            ExpenseResolutionResult result = await resolve.ExecuteAsync(session);

            Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
            Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.DaySummary));
            Assert.That(session.LastDaySummary, Is.Not.Null);
            Assert.That(session.LastDaySummary.CorrectOrders, Is.EqualTo(2));
            Assert.That(session.LastDaySummary.WrongOrders, Is.EqualTo(1));
            Assert.That(session.LastDaySummary.CoinsEarned, Is.EqualTo(40));
            Assert.That(session.LastDaySummary.TaxPaid, Is.EqualTo(20));
            Assert.That(session.LastDaySummary.CoinsRemaining, Is.EqualTo(20));

            Assert.That(session.AttemptMetadata.CorrectServiceCountToday, Is.EqualTo(0));
            Assert.That(session.AttemptMetadata.WrongServiceCountToday, Is.EqualTo(0));
            Assert.That(session.AttemptMetadata.WrongOrdersForSummary, Is.EqualTo(0));
            Assert.That(session.AttemptMetadata.ServiceCountToday, Is.EqualTo(0));
            Assert.That(session.AttemptMetadata.CoinsEarnedToday, Is.EqualTo(0));
            Assert.That(session.AttemptMetadata.MinigamePlayedToday, Is.False);

            Assert.That(saves.SavedAttemptMetadata, Is.Not.Null);
            Assert.That(saves.SavedAttemptMetadata.CorrectServiceCountToday, Is.EqualTo(session.AttemptMetadata.CorrectServiceCountToday));
            Assert.That(saves.SavedAttemptMetadata.WrongServiceCountToday, Is.EqualTo(session.AttemptMetadata.WrongServiceCountToday));
            Assert.That(saves.SavedAttemptMetadata.WrongOrdersForSummary, Is.EqualTo(session.AttemptMetadata.WrongOrdersForSummary));
            Assert.That(saves.SavedAttemptMetadata.ServiceCountToday, Is.EqualTo(session.AttemptMetadata.ServiceCountToday));
            Assert.That(saves.SavedAttemptMetadata.CoinsEarnedToday, Is.EqualTo(session.AttemptMetadata.CoinsEarnedToday));
            Assert.That(saves.SavedAttemptMetadata.MinigamePlayedToday, Is.EqualTo(session.AttemptMetadata.MinigamePlayedToday));
        }

        [Test]
        public async Task RetryResetsNewCountersAndSummaryWrong_LeavesLegacyCarryForward()
        {
            var clock = new FakeClock();
            GameSession session = CreateSession(startingMoney: 0, expense: 20, timerSeconds: 30);
            var rules = new PotionServiceRules(20);
            new OpenShopUseCase().Execute(session);

            ServeCustomer(session, clock, rules, 1, correct: false, closeAfter: true);
            Assert.That(session.AttemptMetadata.WrongServiceCountToday, Is.EqualTo(1));
            Assert.That(session.AttemptMetadata.WrongOrdersForSummary, Is.EqualTo(1));
            Assert.That(session.AttemptMetadata.CoinsEarnedToday, Is.EqualTo(0));

            var resolve = new ResolveExpenseUseCase(
                new ExpenseRules(3),
                CreateSetup(30),
                new FakeSaveRepository(),
                new FakeRandomSource(),
                clock);
            ExpenseResolutionResult failed = await resolve.ExecuteAsync(session);
            Assert.That(failed.Outcome.Kind, Is.EqualTo(ExpenseOutcomeKind.GameOver));

            var retry = new RetryDayUseCase(new FakeSaveRepository(), new FakeSceneLoader());
            ExpenseResolutionResult result = await retry.ExecuteAsync(session);
            Assert.That(result.Succeeded, Is.True, result.ErrorMessage);

            // Existing carry-forward left untouched (Clean Ledger depends on it).
            Assert.That(session.AttemptMetadata.WrongServiceCountToday, Is.EqualTo(1));
            Assert.That(session.AttemptMetadata.ServiceCountToday, Is.EqualTo(1));
            // Summary-only and new counters reset to match restored day-start money.
            Assert.That(session.AttemptMetadata.WrongOrdersForSummary, Is.EqualTo(0));
            Assert.That(session.AttemptMetadata.CorrectServiceCountToday, Is.EqualTo(0));
            Assert.That(session.AttemptMetadata.CoinsEarnedToday, Is.EqualTo(0));
            Assert.That(session.AttemptMetadata.CoinsEarnedToday,
                Is.EqualTo(session.RunState.Money - session.DayStartSnapshot.RunState.Money));
        }

        [Test]
        public async Task MinigameWin_QueueClosed_KeepsPostWrongServiceState_AndMovesToClosingWithTimerStoppedAtZero()
        {
            var clock = new FakeClock();
            GameSession session = CreateSession(startingMoney: 0, expense: 100, timerSeconds: 30);
            var rules = new PotionServiceRules(20);
            new OpenShopUseCase().Execute(session);

            AdmitAndServe(session, clock, rules, "c1", "o1", correct: true);
            TriggerMinigamePending(session, clock, rules);
            Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.MinigamePending));

            // Close the queue before winning the minigame.
            clock.UtcNow = clock.UtcNow.Add(session.TimerState.RemainingTime).Add(TimeSpan.FromSeconds(1));
            new UpdateShopTimerUseCase(clock).Execute(session);
            Assert.That(session.TimerState.RemainingTime, Is.EqualTo(TimeSpan.Zero));
            Assert.That(session.TimerState.QueueOpen, Is.False);

            (int money, int correct, int wrong, int wrongSummary, int coins, int serviceCount, int winCount, int suspicion) before = CaptureState(session);

            var saves = new FakeSaveRepository();
            var scenes = new FakeSceneLoader();
            await new EnterMinigameUseCase(saves, scenes, new FakeRandomSource { NextValue = 777 }).ExecuteAsync(session);
            MinigameTransactionResult completed = await new CompleteMinigameUseCase(saves, scenes, new FakeRandomSource { NextValue = 888 }).ExecuteAsync(session);

            Assert.That(completed.Succeeded, Is.True, completed.ErrorMessage);
            Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.ClosingEncounter));
            Assert.That(session.TimerState.RemainingTime, Is.EqualTo(TimeSpan.Zero));
            Assert.That(session.TimerState.QueueOpen, Is.False);
            Assert.That(session.TimerState.HasActiveEncounter, Is.False);
            Assert.That(session.TimerStopped, Is.True);
            Assert.That(session.TimerPaused, Is.False);
            Assert.That(session.AttemptState.ActiveOrder, Is.Null);
            Assert.That(session.RunState.Suspicion, Is.EqualTo(0));
            Assert.That(session.RunState.MinigameWinCount, Is.EqualTo(before.winCount + 1));
            Assert.That(session.AttemptMetadata.MinigamePlayedToday, Is.True);
            Assert.That(session.RunState.Money, Is.EqualTo(before.money));
            Assert.That(session.AttemptMetadata.CorrectServiceCountToday, Is.EqualTo(before.correct));
            Assert.That(session.AttemptMetadata.WrongServiceCountToday, Is.EqualTo(before.wrong));
            Assert.That(session.AttemptMetadata.WrongOrdersForSummary, Is.EqualTo(before.wrongSummary));
            Assert.That(session.AttemptMetadata.ServiceCountToday, Is.EqualTo(before.serviceCount));
            Assert.That(session.AttemptMetadata.CoinsEarnedToday, Is.EqualTo(before.coins));
        }

        [Test]
        public async Task MinigameWin_QueueOpen_KeepsPostWrongServiceState_RemainingTime_AndPausesForFarewellRelease()
        {
            var clock = new FakeClock();
            GameSession session = CreateSession(startingMoney: 0, expense: 100, timerSeconds: 60);
            var rules = new PotionServiceRules(20);
            new OpenShopUseCase().Execute(session);

            AdmitAndServe(session, clock, rules, "c1", "o1", correct: true);
            TriggerMinigamePending(session, clock, rules);
            Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.MinigamePending));
            Assert.That(session.TimerState.RemainingTime, Is.GreaterThan(TimeSpan.Zero));
            Assert.That(session.TimerState.QueueOpen, Is.True);

            (int money, int correct, int wrong, int wrongSummary, int coins, int serviceCount, int winCount, int suspicion) before = CaptureState(session);
            TimeSpan remaining = session.TimerState.RemainingTime;

            var saves = new FakeSaveRepository();
            var scenes = new FakeSceneLoader();
            await new EnterMinigameUseCase(saves, scenes, new FakeRandomSource { NextValue = 777 }).ExecuteAsync(session);
            MinigameTransactionResult completed = await new CompleteMinigameUseCase(saves, scenes, new FakeRandomSource { NextValue = 888 }).ExecuteAsync(session);

            Assert.That(completed.Succeeded, Is.True, completed.ErrorMessage);
            Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.CustomerArrival));
            Assert.That(session.TimerState.RemainingTime, Is.EqualTo(remaining));
            Assert.That(session.TimerState.QueueOpen, Is.True);
            Assert.That(session.TimerState.HasActiveEncounter, Is.False);
            Assert.That(session.TimerStopped, Is.False);
            Assert.That(session.TimerPaused, Is.True);
            Assert.That(session.AttemptState.ActiveOrder, Is.Null);
            Assert.That(session.RunState.Suspicion, Is.EqualTo(0));
            Assert.That(session.RunState.MinigameWinCount, Is.EqualTo(before.winCount + 1));
            Assert.That(session.AttemptMetadata.MinigamePlayedToday, Is.True);
            Assert.That(session.RunState.Money, Is.EqualTo(before.money));
            Assert.That(session.AttemptMetadata.CorrectServiceCountToday, Is.EqualTo(before.correct));
            Assert.That(session.AttemptMetadata.WrongServiceCountToday, Is.EqualTo(before.wrong));
            Assert.That(session.AttemptMetadata.WrongOrdersForSummary, Is.EqualTo(before.wrongSummary));
            Assert.That(session.AttemptMetadata.ServiceCountToday, Is.EqualTo(before.serviceCount));
            Assert.That(session.AttemptMetadata.CoinsEarnedToday, Is.EqualTo(before.coins));
        }

        [Test]
        public async Task MinigameWin_PersistedMetadataMatchesSession_SaveReloadDoesNotRouteToMinigame_AndFarewellMarkerSet()
        {
            var clock = new FakeClock();
            GameSession session = CreateSession(startingMoney: 0, expense: 100, timerSeconds: 60);
            var rules = new PotionServiceRules(20);
            new OpenShopUseCase().Execute(session);
            AdmitAndServe(session, clock, rules, "c1", "o1", correct: true);
            TriggerMinigamePending(session, clock, rules);
            int winCountBefore = session.RunState.MinigameWinCount;

            var saves = new FakeSaveRepository();
            var scenes = new FakeSceneLoader();
            await new EnterMinigameUseCase(saves, scenes, new FakeRandomSource { NextValue = 777 }).ExecuteAsync(session);
            MinigameTransactionResult completed = await new CompleteMinigameUseCase(saves, scenes, new FakeRandomSource { NextValue = 888 }).ExecuteAsync(session);

            Assert.That(completed.Succeeded, Is.True, completed.ErrorMessage);
            Assert.That(session.TryConsumeHunterFarewell(out int recordedWinCount), Is.True);
            Assert.That(recordedWinCount, Is.EqualTo(winCountBefore));

            Assert.That(saves.SavedAttemptMetadata, Is.Not.Null);
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
        }

        [Test]
        public async Task MinigameWin_WrongServiceAndWrongOrdersForSummary_RemainCommittedValues()
        {
            var clock = new FakeClock();
            GameSession session = CreateSession(startingMoney: 0, expense: 100, timerSeconds: 60);
            var rules = new PotionServiceRules(20);
            new OpenShopUseCase().Execute(session);
            AdmitAndServe(session, clock, rules, "c1", "o1", correct: true);
            TriggerMinigamePending(session, clock, rules);

            int wrongServiceBefore = session.AttemptMetadata.WrongServiceCountToday;
            int wrongOrdersBefore = session.AttemptMetadata.WrongOrdersForSummary;
            Assert.That(wrongServiceBefore, Is.GreaterThan(0));
            Assert.That(wrongOrdersBefore, Is.GreaterThan(0));

            var saves = new FakeSaveRepository();
            var scenes = new FakeSceneLoader();
            await new EnterMinigameUseCase(saves, scenes, new FakeRandomSource { NextValue = 777 }).ExecuteAsync(session);
            MinigameTransactionResult completed = await new CompleteMinigameUseCase(saves, scenes, new FakeRandomSource { NextValue = 888 }).ExecuteAsync(session);

            Assert.That(completed.Succeeded, Is.True, completed.ErrorMessage);
            Assert.That(session.AttemptMetadata.WrongServiceCountToday, Is.EqualTo(wrongServiceBefore));
            Assert.That(session.AttemptMetadata.WrongOrdersForSummary, Is.EqualTo(wrongOrdersBefore));
        }

        private static (int money, int correct, int wrong, int wrongSummary, int coins, int serviceCount, int winCount, int suspicion) CaptureState(GameSession session)
        {
            return (
                session.RunState.Money,
                session.AttemptMetadata.CorrectServiceCountToday,
                session.AttemptMetadata.WrongServiceCountToday,
                session.AttemptMetadata.WrongOrdersForSummary,
                session.AttemptMetadata.CoinsEarnedToday,
                session.AttemptMetadata.ServiceCountToday,
                session.RunState.MinigameWinCount,
                session.RunState.Suspicion);
        }

        private static void TriggerMinigamePending(GameSession session, FakeClock clock, PotionServiceRules rules)
        {
            int guard = 0;
            while (session.CurrentPhase != GamePhase.MinigamePending && guard++ < 10)
            {
                if (session.CurrentPhase == GamePhase.CustomerArrival)
                {
                    AdmitCustomerUseCase admit = new AdmitCustomerUseCase(clock);
                    AdmitCustomerResult admitted = admit.Execute(
                        session,
                        new AdmitCustomerCommand(
                            "cx" + guard,
                            new OrderRequest("ox" + guard, new HerbPair("herb.ginger", "herb.mint"))));
                    Assert.That(admitted.Succeeded, Is.True, admitted.ErrorMessage);
                }
                if (session.CurrentPhase != GamePhase.CustomerEncounter || session.ServiceCommitted)
                {
                    break;
                }
                var prep = new PotionPreparationUseCase(rules);
                prep.Grind(session, "herb.mint", "herb.sage");
                prep.AddToPot(session);
                prep.Brew(session);
                new ServePotionUseCase(rules).Execute(session);
            }
        }

        private static void AdmitAndServe(GameSession session, FakeClock clock, PotionServiceRules rules, string customerId, string orderId, bool correct)
        {
            if (session.CurrentPhase == GamePhase.ShopPreparation)
            {
                new OpenShopUseCase().Execute(session);
            }
            if (session.CurrentPhase == GamePhase.CustomerArrival)
            {
                AdmitCustomerUseCase admit = new AdmitCustomerUseCase(clock);
                AdmitCustomerResult admitted = admit.Execute(
                    session,
                    new AdmitCustomerCommand(customerId, new OrderRequest(orderId, new HerbPair("herb.ginger", "herb.mint"))));
                Assert.That(admitted.Succeeded, Is.True, admitted.ErrorMessage);
            }
            var prep = new PotionPreparationUseCase(rules);
            prep.Grind(session, correct ? "herb.ginger" : "herb.mint", correct ? "herb.mint" : "herb.sage");
            prep.AddToPot(session);
            prep.Brew(session);
            new ServePotionUseCase(rules).Execute(session);
        }

        private static void ServeCustomer(GameSession session, FakeClock clock, PotionServiceRules rules, int index, bool correct, bool closeAfter)
        {
            if (session.CurrentPhase == GamePhase.ShopPreparation)
            {
                new OpenShopUseCase().Execute(session);
            }
            if (session.CurrentPhase == GamePhase.CustomerArrival)
            {
                new AdmitCustomerUseCase(clock).Execute(
                    session,
                    new AdmitCustomerCommand(
                        "customer." + index,
                        new OrderRequest("order." + index, new HerbPair("herb.ginger", "herb.mint"))));
            }
            var prep = new PotionPreparationUseCase(rules);
            prep.Grind(session, correct ? "herb.ginger" : "herb.mint", correct ? "herb.mint" : "herb.sage");
            prep.AddToPot(session);
            prep.Brew(session);
            new ServePotionUseCase(rules).Execute(session);
            if (closeAfter)
            {
                clock.UtcNow = clock.UtcNow.Add(session.TimerState.RemainingTime).Add(TimeSpan.FromSeconds(1));
                new UpdateShopTimerUseCase(clock).Execute(session);
            }
        }

        private static DayProgressionSetup CreateSetup(int timerSeconds)
        {
            return new DayProgressionSetup(
                new Dictionary<int, TimeSpan>
                {
                    [1] = TimeSpan.FromSeconds(timerSeconds),
                    [2] = TimeSpan.FromSeconds(timerSeconds),
                    [3] = TimeSpan.FromSeconds(timerSeconds)
                },
                new Dictionary<int, int> { [1] = 20, [2] = 20, [3] = 20 },
                1);
        }

        private static GameSession CreateSession(int startingMoney, int expense, int timerSeconds)
        {
            return new StartNewRunUseCase().Execute(new StartNewRunCommand(
                "run.summary",
                startingMoney,
                new[] { "category.warm" },
                new[] { "herb.ginger", "herb.mint", "herb.sage" },
                new[] { "guidebook.warm" },
                100,
                TimeSpan.FromSeconds(timerSeconds),
                "expense.day.1",
                expense,
                1,
                DateTimeOffset.UtcNow)).Session;
        }
    }
}
