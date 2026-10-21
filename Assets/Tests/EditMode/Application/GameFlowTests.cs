using System.Collections.Generic;
using NUnit.Framework;
using Witch.Application.Flow;
using Witch.Domain;

namespace Witch.Application.Tests
{
    public sealed class GameFlowTests
    {
        [Test]
        public void StartNewRun_CreatesFirstDayAtShopPreparation()
        {
            StartNewRunResult result = StartTestRun();

            Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
            Assert.That(result.Error, Is.EqualTo(StartNewRunError.None));
            Assert.That(result.Session.CurrentPhase, Is.EqualTo(GamePhase.ShopPreparation));
            Assert.That(result.Session.RunState.CurrentDay.DayNumber, Is.EqualTo(1));
            Assert.That(result.Session.RunState.CurrentDay.CustomerSequence, Has.Count.EqualTo(1));
        }

        [Test]
        public void ValidTransition_ChangesPhase()
        {
            GameSession session = StartTestRun().Session;
            var useCase = new OpenShopUseCase();

            GameFlowResult result = useCase.Execute(session);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Error, Is.EqualTo(GameFlowError.None));
            Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.CustomerEncounter));
        }

        [Test]
        public void InvalidTransition_ReturnsErrorAndKeepsPhase()
        {
            GameSession session = StartTestRun().Session;
            var useCase = new OpenShopUseCase();
            useCase.Execute(session);

            GameFlowResult result = useCase.Execute(session);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(GameFlowError.InvalidTransition));
            Assert.That(result.Phase, Is.EqualTo(GamePhase.CustomerEncounter));
            Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.CustomerEncounter));
        }

        [Test]
        public void MissingStartCommand_ReturnsTypedError()
        {
            var useCase = new StartNewRunUseCase();

            StartNewRunResult result = useCase.Execute(null);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(StartNewRunError.MissingCommand));
            Assert.That(result.Session, Is.Null);
        }

        [Test]
        public void MissingSession_ReturnsErrorWithoutInventingCurrentPhase()
        {
            var useCase = new OpenShopUseCase();

            GameFlowResult result = useCase.Execute(null);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(GameFlowError.MissingSession));
            Assert.That(result.Phase, Is.Null);
        }

        [Test]
        public void ResultFactories_RejectContradictoryResults()
        {
            Assert.That(
                () => StartNewRunResult.Success(null),
                Throws.TypeOf<System.ArgumentNullException>());
            Assert.That(
                () => GameFlowResult.Failure(GameFlowError.None, GamePhase.ShopPreparation),
                Throws.TypeOf<System.ArgumentException>());
        }

        private static StartNewRunResult StartTestRun()
        {
            var useCase = new StartNewRunUseCase();
            var command = new StartNewRunCommand(
                "run.test",
                10,
                new Dictionary<string, int> { { "herb.test", 2 } },
                1,
                new[] { "customer.villager.test" });

            return useCase.Execute(command);
        }
    }
}
