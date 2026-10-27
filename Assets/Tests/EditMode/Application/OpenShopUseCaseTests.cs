using System;
using NUnit.Framework;
using Witch.Application.Flow;
using Witch.Domain;

namespace Witch.Application.Tests
{
    public sealed class OpenShopUseCaseTests
    {
        [Test]
        public void Execute_FromShopPreparation_StartsCustomerArrival()
        {
            GameSession session = StartTestRun().Session;
            var useCase = new OpenShopUseCase();

            PhaseTransitionResult result = useCase.Execute(session);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Error, Is.EqualTo(PhaseTransitionError.None));
            Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.CustomerArrival));
        }

        [Test]
        public void Execute_FromCustomerArrival_ReturnsErrorAndKeepsPhase()
        {
            GameSession session = StartTestRun().Session;
            var useCase = new OpenShopUseCase();
            useCase.Execute(session);

            PhaseTransitionResult result = useCase.Execute(session);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(PhaseTransitionError.InvalidTransition));
            Assert.That(result.Phase, Is.EqualTo(GamePhase.CustomerArrival));
            Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.CustomerArrival));
        }

        [Test]
        public void Execute_WithoutSession_ReturnsErrorWithoutPhase()
        {
            var useCase = new OpenShopUseCase();

            PhaseTransitionResult result = useCase.Execute(null);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(PhaseTransitionError.MissingSession));
            Assert.That(result.Phase, Is.Null);
        }

        [Test]
        public void Failure_WithNoError_IsRejected()
        {
            Assert.That(
                () => PhaseTransitionResult.Failure(
                    PhaseTransitionError.None,
                    GamePhase.ShopPreparation),
                Throws.TypeOf<System.ArgumentException>());
        }

        private static StartNewRunResult StartTestRun()
        {
            var useCase = new StartNewRunUseCase();
            var command = new StartNewRunCommand(
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
                DateTimeOffset.UtcNow);

            return useCase.Execute(command);
        }
    }
}
