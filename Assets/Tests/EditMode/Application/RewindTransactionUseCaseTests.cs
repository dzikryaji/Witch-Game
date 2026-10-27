using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Witch.Application.Flow;
using Witch.Application.Ports;
using Witch.Domain;
using Witch.Domain.Orders;
using Witch.Domain.Potions;

namespace Witch.Application.Tests
{
    public sealed class RewindTransactionUseCaseTests
    {
        [Test]
        public async Task Enter_PersistsPendingBeforeLoadingScene()
        {
            GameSession session = CreatePendingRewindSession(9);
            var operations = new List<string>();
            var saves = new FakeSaveRepository { Operations = operations };
            var scenes = new FakeSceneLoader { Operations = operations };
            var useCase = new EnterRewindUseCase(saves, scenes, new FakeRandomSource { NextValue = 707 });

            RewindTransactionResult result = await useCase.ExecuteAsync(session);

            Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
            Assert.That(operations, Is.EqualTo(new[] { "save", "scene:RewindMinigame" }));
            Assert.That(saves.SavedAttemptMetadata.PendingRewind, Is.True);
            Assert.That(saves.SavedAttemptMetadata.MinigameBoardSeed, Is.EqualTo(707));
            Assert.That(result.Board.PairCount, Is.EqualTo(3));
            Assert.That(session.TimerStopped, Is.True);
        }

        [Test]
        public async Task Enter_SaveFailureDoesNotRequestScene()
        {
            GameSession session = CreatePendingRewindSession(0);
            var saves = new FakeSaveRepository
            {
                SaveResult = SaveStateResult.Failure(SaveStateError.WriteFailed, "disk full")
            };
            var scenes = new FakeSceneLoader();

            RewindTransactionResult result = await new EnterRewindUseCase(
                saves,
                scenes,
                new FakeRandomSource()).ExecuteAsync(session);

            Assert.That(result.Error, Is.EqualTo(RewindTransactionError.SaveFailed));
            Assert.That(scenes.LoadCallCount, Is.Zero);
            Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.RewindPending));
        }

        [Test]
        public async Task Complete_RestoresSnapshotAndDuplicateDoesNotApplyTwice()
        {
            GameSession session = CreatePendingRewindSession(9);
            var saves = new FakeSaveRepository();
            var scenes = new FakeSceneLoader();
            await new EnterRewindUseCase(saves, scenes, new FakeRandomSource { NextValue = 707 })
                .ExecuteAsync(session);
            var random = new FakeRandomSource { NextValue = 808 };
            var complete = new CompleteRewindUseCase(saves, scenes, random);

            RewindTransactionResult first = await complete.ExecuteAsync(session);
            RewindTransactionResult duplicate = await complete.ExecuteAsync(session);

            Assert.That(first.Succeeded, Is.True, first.ErrorMessage);
            Assert.That(duplicate.Succeeded, Is.True);
            Assert.That(duplicate.AlreadyApplied, Is.True);
            Assert.That(session.RunState.Money, Is.EqualTo(9));
            Assert.That(session.RunState.Suspicion, Is.Zero);
            Assert.That(session.RunState.RewindCount, Is.EqualTo(1));
            Assert.That(session.AttemptMetadata.ActiveOrderSeed, Is.EqualTo(808));
            Assert.That(session.AttemptMetadata.PendingRewind, Is.False);
            Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.ShopPreparation));
            Assert.That(saves.SaveCallCount, Is.EqualTo(2));
            Assert.That(random.CallCount, Is.EqualTo(1));
        }

        private static GameSession CreatePendingRewindSession(int startingMoney)
        {
            var rules = CreateRules();
            GameSession session = new StartNewRunUseCase().Execute(new StartNewRunCommand(
                "run.rewind",
                startingMoney,
                new[] { "category.warm" },
                new[] { "herb.ginger", "herb.mint", "herb.sage" },
                new[] { "recipe.warming", "recipe.cooling" },
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
                prepare.Grind(session, "herb.mint");
                prepare.AddToPot(session);
                prepare.Grind(session, "herb.sage");
                prepare.AddToPot(session);
                prepare.Brew(session);
                serve.Execute(session);
            }

            Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.RewindPending));
            return session;
        }

        private static PotionServiceRules CreateRules()
        {
            return new PotionServiceRules(
                10,
                new RecipeCatalog(new[]
                {
                    new Recipe("recipe.warming", new HerbPair("herb.ginger", "herb.mint")),
                    new Recipe("recipe.cooling", new HerbPair("herb.mint", "herb.sage"))
                }));
        }
    }
}
