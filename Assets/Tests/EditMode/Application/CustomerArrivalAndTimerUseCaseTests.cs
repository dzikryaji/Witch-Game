using System;
using NUnit.Framework;
using Witch.Application.Flow;
using Witch.Domain;
using Witch.Domain.Orders;
using Witch.Domain.Potions;

namespace Witch.Application.Tests
{
    public sealed class CustomerArrivalAndTimerUseCaseTests
    {
        [Test]
        public void Admit_FirstCustomerStartsTimerAndDuplicateDoesNotRestartIt()
        {
            GameSession session = CreateOpenSession();
            var clock = new FakeClock();
            var useCase = new AdmitCustomerUseCase(clock);
            var command = new AdmitCustomerCommand(
                "customer.villager.one",
                new OrderRequest("order.one", new HerbPair("herb.ginger", "herb.mint")));

            AdmitCustomerResult first = useCase.Execute(session, command);
            DateTimeOffset firstStart = session.TimerStartedAtUtc.Value;
            clock.UtcNow = clock.UtcNow.AddSeconds(10);
            AdmitCustomerResult duplicate = useCase.Execute(session, command);

            Assert.That(first.Succeeded, Is.True, first.ErrorMessage);
            Assert.That(first.AlreadyAdmitted, Is.False);
            Assert.That(duplicate.Succeeded, Is.True, duplicate.ErrorMessage);
            Assert.That(duplicate.AlreadyAdmitted, Is.True);
            Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.CustomerEncounter));
            Assert.That(session.TimerStartedAtUtc, Is.EqualTo(firstStart));
        }

        [Test]
        public void Tick_DuringEncounterPreparationReducesRemainingTime()
        {
            GameSession session = CreateOpenSession();
            var clock = new FakeClock();
            AdmitFirstCustomer(session, clock);
            var timer = new UpdateShopTimerUseCase(clock);

            clock.UtcNow = clock.UtcNow.AddSeconds(15);
            timer.Execute(session);

            Assert.That(session.TimerState.RemainingTime, Is.EqualTo(TimeSpan.FromMinutes(4) - TimeSpan.FromSeconds(15)));
        }

        [Test]
        public void Pause_FreezesTimerUntilResumed()
        {
            GameSession session = CreateOpenSession();
            var clock = new FakeClock();
            AdmitFirstCustomer(session, clock);
            var pause = new SetShopTimerPausedUseCase(clock);
            var timer = new UpdateShopTimerUseCase(clock);

            pause.Execute(session, paused: true);
            clock.UtcNow = clock.UtcNow.AddSeconds(30);
            timer.Execute(session);
            pause.Execute(session, paused: false);
            clock.UtcNow = clock.UtcNow.AddSeconds(5);
            timer.Execute(session);

            Assert.That(session.TimerState.RemainingTime, Is.EqualTo(TimeSpan.FromMinutes(4) - TimeSpan.FromSeconds(5)));
        }

        [Test]
        public void StopForRewind_PreventsFurtherTimerProgress()
        {
            GameSession session = CreateOpenSession();
            var clock = new FakeClock();
            AdmitFirstCustomer(session, clock);
            var stop = new StopShopTimerUseCase(clock);
            var timer = new UpdateShopTimerUseCase(clock);

            clock.UtcNow = clock.UtcNow.AddSeconds(10);
            stop.Execute(session);
            TimeSpan stoppedAt = session.TimerState.RemainingTime;
            clock.UtcNow = clock.UtcNow.AddSeconds(30);
            timer.Execute(session);

            Assert.That(session.TimerStopped, Is.True);
            Assert.That(session.TimerState.RemainingTime, Is.EqualTo(stoppedAt));
        }

        [Test]
        public void Tick_WhenQueueIsIdleAtZero_ClosesDayWithoutAnotherCustomer()
        {
            GameSession session = CreateOpenSession();
            var clock = new FakeClock();
            AdmitFirstCustomer(session, clock);
            var rules = new PotionServiceRules(
                10,
                new RecipeCatalog(new[]
                {
                    new Recipe("recipe.warming-tonic", new HerbPair("herb.ginger", "herb.mint"))
                }));
            var preparation = new PotionPreparationUseCase(rules);
            preparation.Grind(session, "herb.ginger");
            preparation.AddToPot(session);
            preparation.Grind(session, "herb.mint");
            preparation.AddToPot(session);
            preparation.Brew(session);
            new ServePotionUseCase(rules).Execute(session);

            Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.CustomerArrival));
            clock.UtcNow = clock.UtcNow.AddMinutes(5);
            new UpdateShopTimerUseCase(clock).Execute(session);

            Assert.That(session.TimerState.RemainingTime, Is.EqualTo(TimeSpan.Zero));
            Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.ClosingEncounter));
        }

        private static void AdmitFirstCustomer(GameSession session, FakeClock clock)
        {
            var useCase = new AdmitCustomerUseCase(clock);
            useCase.Execute(
                session,
                new AdmitCustomerCommand(
                    "customer.villager.one",
                    new OrderRequest("order.one", new HerbPair("herb.ginger", "herb.mint"))));
        }

        private static GameSession CreateOpenSession()
        {
            var useCase = new StartNewRunUseCase();
            StartNewRunResult start = useCase.Execute(new StartNewRunCommand(
                "run.test",
                0,
                new[] { "category.warm" },
                new[] { "herb.ginger", "herb.mint" },
                new[] { "recipe.warming-tonic" },
                new[] { "guidebook.warm" },
                1234,
                TimeSpan.FromMinutes(4),
                "expense.day.1",
                20,
                1,
                DateTimeOffset.UtcNow));
            var openShop = new OpenShopUseCase();
            openShop.Execute(start.Session);
            return start.Session;
        }
    }
}
