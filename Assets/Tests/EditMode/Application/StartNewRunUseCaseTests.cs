using System;
using NUnit.Framework;
using Witch.Application.Flow;
using Witch.Domain;

namespace Witch.Application.Tests
{
    public sealed class StartNewRunUseCaseTests
    {
        [Test]
        public void Execute_CreatesFirstDayAtShopPreparation()
        {
            StartNewRunResult result = StartTestRun();

            Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
            Assert.That(result.Error, Is.EqualTo(StartNewRunError.None));
            Assert.That(result.Session.CurrentPhase, Is.EqualTo(GamePhase.ShopPreparation));
            Assert.That(result.Session.RunState.CurrentDayNumber, Is.EqualTo(1));
            Assert.That(result.Session.RunState.Money, Is.Zero);
            Assert.That(result.Session.RunState.Suspicion, Is.Zero);
            Assert.That(result.Session.RunState.RewindCount, Is.Zero);
            Assert.That(result.Session.DayStartSnapshot.OrderSeed, Is.EqualTo(1234));
            Assert.That(result.Session.DayStartSnapshot.TimerDuration, Is.EqualTo(TimeSpan.FromMinutes(4)));
            Assert.That(result.Session.AttemptMetadata.ActiveOrderSeed, Is.EqualTo(1234));
            Assert.That(result.Session.AttemptMetadata.PendingRewind, Is.False);
        }

        [Test]
        public void Execute_WithoutCommand_ReturnsTypedError()
        {
            var useCase = new StartNewRunUseCase();

            StartNewRunResult result = useCase.Execute(null);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(StartNewRunError.MissingCommand));
            Assert.That(result.Session, Is.Null);
        }

        [Test]
        public void Success_WithoutSession_IsRejected()
        {
            Assert.That(
                () => StartNewRunResult.Success(null),
                Throws.TypeOf<System.ArgumentNullException>());
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
                firstDayOrderSeed: 1234,
                firstDayTimerDuration: TimeSpan.FromMinutes(4),
                firstDayExpenseId: "expense.day.1",
                firstDayExpenseValue: 20,
                saveVersion: 1,
                capturedAtUtc: new DateTimeOffset(2026, 10, 23, 8, 0, 0, TimeSpan.Zero));

            return useCase.Execute(command);
        }
    }
}
