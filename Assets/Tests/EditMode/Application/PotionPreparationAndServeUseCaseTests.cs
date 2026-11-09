using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Witch.Application.Flow;
using Witch.Application.Ports;
using Witch.Domain;
using Witch.Domain.Orders;
using Witch.Domain.Potions;

namespace Witch.Application.Tests
{
    public sealed class PotionPreparationAndServeUseCaseTests
    {
        [Test]
        public void GrindAddAndBrew_TwoDifferentHerbsPreparePotion()
        {
            GameSession session = CreateEncounterSession();
            var preparation = new PotionPreparationUseCase(CreateRules());

            preparation.Grind(session, "herb.ginger", "herb.mint");
            preparation.AddToPot(session);
            PreparationResult result = preparation.Brew(session);

            Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
            Assert.That(result.Kind, Is.EqualTo(PreparationResultKind.Prepared));
            Assert.That(session.PreparedPotion.HerbPair, Is.EqualTo(new HerbPair("herb.ginger", "herb.mint")));
        }

        [Test]
        public void Brew_AnyDifferentPairPreparesPotion()
        {
            GameSession session = CreateEncounterSession();
            var preparation = new PotionPreparationUseCase(CreateRules());

            preparation.Grind(session, "herb.ginger", "herb.sage");
            preparation.AddToPot(session);
            PreparationResult result = preparation.Brew(session);

            Assert.That(result.Kind, Is.EqualTo(PreparationResultKind.Prepared));
            Assert.That(session.PotionDraft.Ingredients.Count, Is.EqualTo(2));
            Assert.That(session.PreparedPotion.HerbPair, Is.EqualTo(new HerbPair("herb.ginger", "herb.sage")));
            Assert.That(session.RunState.Suspicion, Is.Zero);
        }

        [Test]
        public void Serve_CorrectPotionCommitsOnceAndPaysTen()
        {
            GameSession session = CreateEncounterSession();
            var rules = CreateRules();
            var preparation = new PotionPreparationUseCase(rules);
            preparation.Grind(session, "herb.ginger", "herb.mint");
            preparation.AddToPot(session);
            preparation.Brew(session);
            var serve = new ServePotionUseCase(rules);

            ServePotionResult first = serve.Execute(session);
            ServePotionResult duplicate = serve.Execute(session);

            Assert.That(first.Succeeded, Is.True, first.ErrorMessage);
            Assert.That(first.Outcome.Kind, Is.EqualTo(ServiceOutcomeKind.Correct));
            Assert.That(session.RunState.Money, Is.EqualTo(10));
            Assert.That(duplicate.Succeeded, Is.False);
            Assert.That(session.RunState.Money, Is.EqualTo(10));
        }

        [Test]
        public async Task ServeAsync_WrongPotionPersistsSuspicionBeforeCommit()
        {
            GameSession session = CreateEncounterSession();
            var rules = CreateRules();
            var preparation = new PotionPreparationUseCase(rules);
            preparation.Grind(session, "herb.mint", "herb.sage");
            preparation.AddToPot(session);
            preparation.Brew(session);
            var repository = new FakeSaveRepository();

            ServePotionResult result = await new ServePotionUseCase(rules, repository).ExecuteAsync(session);

            Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
            Assert.That(repository.SavedAttemptMetadata.Suspicion, Is.EqualTo(1));
            Assert.That(session.RunState.Suspicion, Is.EqualTo(1));
        }

        [Test]
        public async Task ServeAsync_WhenSuspicionWriteFails_DoesNotCommitOutcome()
        {
            GameSession session = CreateEncounterSession();
            var rules = CreateRules();
            var preparation = new PotionPreparationUseCase(rules);
            preparation.Grind(session, "herb.mint", "herb.sage");
            preparation.AddToPot(session);
            preparation.Brew(session);
            var repository = new FakeSaveRepository
            {
                SaveResult = SaveStateResult.Failure(SaveStateError.WriteFailed, "disk full")
            };

            ServePotionResult result = await new ServePotionUseCase(rules, repository).ExecuteAsync(session);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(session.RunState.Suspicion, Is.Zero);
            Assert.That(session.ServiceCommitted, Is.False);
            Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.CustomerEncounter));
        }

        private static PotionServiceRules CreateRules()
        {
            return new PotionServiceRules(10);
        }

        private static GameSession CreateEncounterSession()
        {
            var start = new StartNewRunUseCase().Execute(new StartNewRunCommand(
                "run.test",
                0,
                new[] { "category.warm" },
                new[] { "herb.ginger", "herb.mint", "herb.sage" },
                new[] { "guidebook.warm" },
                1234,
                TimeSpan.FromMinutes(4),
                "expense.day.1",
                20,
                1,
                DateTimeOffset.UtcNow));
            new OpenShopUseCase().Execute(start.Session);
            new AdmitCustomerUseCase(new FakeClock()).Execute(
                start.Session,
                new AdmitCustomerCommand(
                    "customer.villager.one",
                    new OrderRequest(
                        "order.one",
                        new HerbPair("herb.ginger", "herb.mint"))));
            return start.Session;
        }
    }
}
