using System;
using System.Collections.Generic;
using System.Reflection;
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
        public async Task FullMinigameResetResetsNewCountersAndSummaryWrongToDayStart()
        {
            var clock = new FakeClock();
            GameSession session = CreateSession(startingMoney: 0, expense: 100, timerSeconds: 60);
            var rules = new PotionServiceRules(20);
            new OpenShopUseCase().Execute(session);

            AdmitAndServe(session, clock, rules, "c1", "o1", correct: true);
            AdmitAndServe(session, clock, rules, "c2", "o2", correct: false);
            Assert.That(session.AttemptMetadata.CorrectServiceCountToday, Is.EqualTo(1));
            Assert.That(session.AttemptMetadata.WrongOrdersForSummary, Is.EqualTo(1));
            Assert.That(session.AttemptMetadata.CoinsEarnedToday, Is.EqualTo(20));

            TriggerMinigamePending(session, clock, rules);
            Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.MinigamePending));

            // Full minigame reset path when not resumable in place: expire remaining timer via public behavior.
            if (session.TimerState.RemainingTime > TimeSpan.Zero)
            {
                clock.UtcNow = clock.UtcNow.Add(session.TimerState.RemainingTime).Add(TimeSpan.FromSeconds(1));
                new UpdateShopTimerUseCase(clock).Execute(session);
            }
            Assert.That(session.TimerState.RemainingTime, Is.EqualTo(TimeSpan.Zero));
            Assert.That(session.CanResumeInPlace, Is.False, "Test must exercise full minigame reset path.");

            var enter = new EnterMinigameUseCase(new FakeSaveRepository(), new FakeSceneLoader(), new FakeRandomSource { NextValue = 777 });
            await enter.ExecuteAsync(session);
            var complete = new CompleteMinigameUseCase(new FakeSaveRepository(), new FakeSceneLoader(), new FakeRandomSource { NextValue = 888 });
            MinigameTransactionResult completed = await complete.ExecuteAsync(session);

            Assert.That(completed.Succeeded, Is.True, completed.ErrorMessage);
            Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.ShopPreparation));
            Assert.That(session.RunState.Money, Is.EqualTo(session.DayStartSnapshot.RunState.Money));
            Assert.That(session.AttemptMetadata.CorrectServiceCountToday, Is.EqualTo(0));
            Assert.That(session.AttemptMetadata.CoinsEarnedToday, Is.EqualTo(0));
            Assert.That(session.AttemptMetadata.WrongOrdersForSummary, Is.EqualTo(0));
            // Legacy carry-forward preserved (Clean Ledger depends on it).
            Assert.That(session.AttemptMetadata.WrongServiceCountToday, Is.GreaterThan(0));
            Assert.That(session.AttemptMetadata.CoinsEarnedToday,
                Is.EqualTo(session.RunState.Money - session.DayStartSnapshot.RunState.Money));
        }

        [Test]
        public async Task InPlaceMinigameRollsBackNewCountersAndSummaryWrongToPreServiceSnapshot()
        {
            var clock = new FakeClock();
            GameSession session = CreateSession(startingMoney: 0, expense: 100, timerSeconds: 60);
            var rules = new PotionServiceRules(20);
            new OpenShopUseCase().Execute(session);

            // Correct serve before the minigame-causing customer: baseline money 20, correct 1, summary wrong 0.
            AdmitAndServe(session, clock, rules, "c1", "o1", correct: true);
            Assert.That(session.AttemptMetadata.CorrectServiceCountToday, Is.EqualTo(1));
            Assert.That(session.AttemptMetadata.WrongOrdersForSummary, Is.EqualTo(0));
            Assert.That(session.AttemptMetadata.CoinsEarnedToday, Is.EqualTo(20));

            // Reach minigame pending with time remaining so in-place resume is possible.
            TriggerMinigamePending(session, clock, rules);
            Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.MinigamePending));
            Assert.That(session.TimerState.RemainingTime, Is.GreaterThan(TimeSpan.Zero));
            Assert.That(session.CanResumeInPlace, Is.True, "Test must exercise in-place minigame resume path.");

            var enter = new EnterMinigameUseCase(new FakeSaveRepository(), new FakeSceneLoader(), new FakeRandomSource { NextValue = 777 });
            await enter.ExecuteAsync(session);
            var complete = new CompleteMinigameUseCase(new FakeSaveRepository(), new FakeSceneLoader(), new FakeRandomSource { NextValue = 888 });
            MinigameTransactionResult completed = await complete.ExecuteAsync(session);

            Assert.That(completed.Succeeded, Is.True, completed.ErrorMessage);
            Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.CustomerEncounter));
            // Rolled back to pre-service snapshot captured at encounter begin.
            Assert.That(session.RunState.Money, Is.EqualTo(20));
            Assert.That(session.AttemptMetadata.CorrectServiceCountToday, Is.EqualTo(1));
            Assert.That(session.AttemptMetadata.CoinsEarnedToday, Is.EqualTo(20));
            // Pre-service snapshot for this encounter is before the 3rd wrong serve (two prior wrong serves remain in summary).
            Assert.That(session.AttemptMetadata.WrongOrdersForSummary, Is.EqualTo(2));
            // Legacy carry-forward preserved.
            Assert.That(session.AttemptMetadata.WrongServiceCountToday, Is.GreaterThan(0));
            Assert.That(session.AttemptMetadata.CoinsEarnedToday,
                Is.EqualTo(session.RunState.Money - session.DayStartSnapshot.RunState.Money));
        }

        [Test]
        public async Task InPlaceMinigameSave_MatchesSessionAndReloadKeepsPreServiceCounters()
        {
            var clock = new FakeClock();
            GameSession session = CreateSession(startingMoney: 0, expense: 100, timerSeconds: 60);
            var rules = new PotionServiceRules(20);
            var saves = new FakeSaveRepository();
            new OpenShopUseCase().Execute(session);

            AdmitAndServe(session, clock, rules, "c1", "o1", correct: true);
            TriggerMinigamePending(session, clock, rules);
            Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.MinigamePending));
            Assert.That(session.CanResumeInPlace, Is.True, "Test must exercise in-place minigame resume path.");

            var enter = new EnterMinigameUseCase(saves, new FakeSceneLoader(), new FakeRandomSource { NextValue = 777 });
            await enter.ExecuteAsync(session);
            var complete = new CompleteMinigameUseCase(saves, new FakeSceneLoader(), new FakeRandomSource { NextValue = 888 });
            MinigameTransactionResult completed = await complete.ExecuteAsync(session);

            Assert.That(completed.Succeeded, Is.True, completed.ErrorMessage);
            Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.CustomerEncounter));

            // Saved data must match running session for counters restored by in-place minigame.
            Assert.That(saves.SavedAttemptMetadata, Is.Not.Null);
            Assert.That(saves.SavedAttemptMetadata.CorrectServiceCountToday, Is.EqualTo(session.AttemptMetadata.CorrectServiceCountToday));
            Assert.That(saves.SavedAttemptMetadata.CoinsEarnedToday, Is.EqualTo(session.AttemptMetadata.CoinsEarnedToday));
            Assert.That(saves.SavedAttemptMetadata.WrongOrdersForSummary, Is.EqualTo(session.AttemptMetadata.WrongOrdersForSummary));

            // Simulated reload from saved run: rebuilt session keeps pre-service counter values.
            var ctor = typeof(GameSession).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(DayStartSnapshot), typeof(ActiveAttemptMetadata), typeof(GamePhase) },
                null);
            Assert.That(ctor, Is.Not.Null, "GameSession save-state constructor is required for reload simulation.");
            var reloaded = (GameSession)ctor.Invoke(new object[] { saves.SavedSnapshot, saves.SavedAttemptMetadata, GamePhase.ShopPreparation });
            Assert.That(reloaded.AttemptMetadata.CorrectServiceCountToday, Is.EqualTo(1));
            Assert.That(reloaded.AttemptMetadata.CoinsEarnedToday, Is.EqualTo(20));
            Assert.That(reloaded.AttemptMetadata.WrongOrdersForSummary, Is.EqualTo(2));
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
