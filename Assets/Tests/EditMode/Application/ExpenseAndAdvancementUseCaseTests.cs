using System;
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
    public sealed class ExpenseAndAdvancementUseCaseTests
    {
        [Test]
        public async Task ThreeDayFlow_AdvancesTwiceThenReturnsVictory()
        {
            var clock = new FakeClock();
            GameSession session = CreateSession();
            var saves = new FakeSaveRepository();
            var scenes = new FakeSceneLoader();
            var resolve = new ResolveExpenseUseCase(
                new ExpenseRules(3),
                CreateSetup(),
                saves,
                new FakeRandomSource { NextValue = 900 },
                clock);
            var continueDay = new ContinueToNextDayUseCase(scenes);

            for (int day = 1; day <= 3; day++)
            {
                CompletePaidClosingCustomer(session, clock, day);
                ExpenseResolutionResult result = await resolve.ExecuteAsync(session);

                Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
                if (day < 3)
                {
                    Assert.That(result.Outcome.Kind, Is.EqualTo(ExpenseOutcomeKind.NextDay));
                    Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.DaySummary));
                    Assert.That(session.RunState.CurrentDayNumber, Is.EqualTo(day + 1));
                    Assert.That(session.RunState.Money, Is.Zero);
                    Assert.That(session.DayStartSnapshot.TimerDuration, Is.EqualTo(TimeSpan.FromSeconds(day + 1)));
                    Assert.That((await continueDay.ExecuteAsync(session)).Succeeded, Is.True);
                }
                else
                {
                    Assert.That(result.Outcome.Kind, Is.EqualTo(ExpenseOutcomeKind.Victory));
                    Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.Victory));
                    Assert.That(session.RunState.Money, Is.Zero);
                }
            }

            Assert.That(saves.SaveCallCount, Is.EqualTo(2));
        }

        [Test]
        public async Task FailureDeductsNothingAndRetryRestoresSameSeedWithSuspicion()
        {
            var clock = new FakeClock();
            GameSession session = CreateSession();
            CompleteUnpaidClosingCustomer(session, clock);
            int originalSeed = session.DayStartSnapshot.OrderSeed;
            var saves = new FakeSaveRepository();
            var scenes = new FakeSceneLoader();
            var resolve = new ResolveExpenseUseCase(
                new ExpenseRules(3),
                CreateSetup(),
                saves,
                new FakeRandomSource(),
                clock);

            ExpenseResolutionResult failedExpense = await resolve.ExecuteAsync(session);
            ExpenseResolutionResult retry = await new RetryDayUseCase(saves, scenes).ExecuteAsync(session);

            Assert.That(failedExpense.Outcome.Kind, Is.EqualTo(ExpenseOutcomeKind.GameOver));
            Assert.That(failedExpense.Outcome.UpdatedRunState.Money, Is.Zero);
            Assert.That(retry.Succeeded, Is.True, retry.ErrorMessage);
            Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.ShopPreparation));
            Assert.That(session.DayStartSnapshot.OrderSeed, Is.EqualTo(originalSeed));
            Assert.That(session.RunState.Suspicion, Is.EqualTo(1));
            Assert.That(session.RunState.Money, Is.Zero);
        }

        [Test]
        public async Task NextDaySaveFailureLeavesClosingEncounterAvailableForRetry()
        {
            var clock = new FakeClock();
            GameSession session = CreateSession();
            CompletePaidClosingCustomer(session, clock, 1);
            var saves = new FakeSaveRepository
            {
                SaveResult = SaveStateResult.Failure(SaveStateError.WriteFailed, "disk full")
            };
            var resolve = new ResolveExpenseUseCase(
                new ExpenseRules(3),
                CreateSetup(),
                saves,
                new FakeRandomSource(),
                clock);

            ExpenseResolutionResult result = await resolve.ExecuteAsync(session);

            Assert.That(result.Error, Is.EqualTo(ExpenseResolutionError.SaveFailed));
            Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.ClosingEncounter));
            Assert.That(session.RunState.CurrentDayNumber, Is.EqualTo(1));
            Assert.That(session.RunState.Money, Is.EqualTo(20));
        }

        [Test]
        public void CancellationStopsNextDayPersistence()
        {
            var clock = new FakeClock();
            GameSession session = CreateSession();
            CompletePaidClosingCustomer(session, clock, 1);
            var resolve = new ResolveExpenseUseCase(
                new ExpenseRules(3),
                CreateSetup(),
                new FakeSaveRepository(),
                new FakeRandomSource(),
                clock);
            var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.ThrowsAsync<TaskCanceledException>(
                async () => await resolve.ExecuteAsync(session, cancellation.Token));
            Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.ClosingEncounter));
            Assert.That(session.RunState.CurrentDayNumber, Is.EqualTo(1));
        }

        private static DayProgressionSetup CreateSetup()
        {
            return new DayProgressionSetup(
                new DayTimingRules(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)),
                20,
                "expense.day.",
                1);
        }

        private static GameSession CreateSession()
        {
            return new StartNewRunUseCase().Execute(new StartNewRunCommand(
                "run.three-days",
                0,
                new[] { "category.warm" },
                new[] { "herb.ginger", "herb.mint", "herb.sage" },
                new[] { "recipe.warming", "recipe.cooling" },
                new[] { "guidebook.warm" },
                100,
                TimeSpan.FromSeconds(1),
                "expense.day.1",
                20,
                1,
                DateTimeOffset.UtcNow)).Session;
        }

        private static void CompletePaidClosingCustomer(GameSession session, FakeClock clock, int day)
        {
            CompleteClosingCustomer(session, clock, day, correct: true);
        }

        private static void CompleteUnpaidClosingCustomer(GameSession session, FakeClock clock)
        {
            CompleteClosingCustomer(session, clock, 1, correct: false);
        }

        private static void CompleteClosingCustomer(
            GameSession session,
            FakeClock clock,
            int day,
            bool correct)
        {
            var rules = CreatePotionRules();
            new OpenShopUseCase().Execute(session);
            new AdmitCustomerUseCase(clock).Execute(
                session,
                new AdmitCustomerCommand(
                    "customer.day." + day,
                    new OrderRequest(
                        "order.day." + day,
                        new HerbPair("herb.ginger", "herb.mint"))));
            clock.UtcNow = clock.UtcNow.Add(session.TimerState.RemainingTime).Add(TimeSpan.FromSeconds(1));
            new UpdateShopTimerUseCase(clock).Execute(session);
            var prepare = new PotionPreparationUseCase(rules);
            prepare.Grind(session, correct ? "herb.ginger" : "herb.mint");
            prepare.AddToPot(session);
            prepare.Grind(session, correct ? "herb.mint" : "herb.sage");
            prepare.AddToPot(session);
            prepare.Brew(session);
            new ServePotionUseCase(rules).Execute(session);
            Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.ClosingEncounter));
        }

        private static PotionServiceRules CreatePotionRules()
        {
            return new PotionServiceRules(
                20,
                new RecipeCatalog(new[]
                {
                    new Recipe("recipe.warming", new HerbPair("herb.ginger", "herb.mint")),
                    new Recipe("recipe.cooling", new HerbPair("herb.mint", "herb.sage"))
                }));
        }
    }
}
