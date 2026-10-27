#if UNITY_EDITOR
using System;
using NUnit.Framework;
using Witch.Application.Flow;
using Witch.Domain;
using Witch.Domain.Progression;

namespace Witch.Application.Tests
{
    public sealed class DevelopmentStartupSessionFactoryTests
    {
        [Test]
        public void PendingRewindDefaultsCreateImmediatelyPlayableBoardState()
        {
            var setup = new NewRunSetup(
                0,
                new[] { "category.warm" },
                new[] { "herb.ginger", "herb.cinnamon" },
                new[] { "recipe.warm" },
                new[] { "guide.warm" },
                TimeSpan.FromMinutes(4),
                "expense.day.1",
                20,
                1);

            GameSession session = DevelopmentStartupSessionFactory.CreatePendingRewind(
                setup,
                7,
                2,
                101,
                202,
                new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero));

            Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.RewindMinigame));
            Assert.That(session.RunState.Money, Is.EqualTo(7));
            Assert.That(session.RunState.Suspicion, Is.EqualTo(RunState.RewindSuspicionThreshold));
            Assert.That(session.RunState.RewindCount, Is.EqualTo(2));
            Assert.That(session.AttemptMetadata.PendingRewind, Is.True);
            Assert.That(session.AttemptMetadata.ActiveOrderSeed, Is.EqualTo(101));
            Assert.That(session.AttemptMetadata.MinigameBoardSeed, Is.EqualTo(202));
            Assert.That(RewindBoard.Create(202, 2).PairCount, Is.EqualTo(5));
        }
    }
}
#endif
