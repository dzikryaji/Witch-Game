using System.Collections.Generic;
using NUnit.Framework;
using Witch.Domain.Progression;

namespace Witch.Domain.Tests
{
    public sealed class RunStateTests
    {
        [Test]
        public void NewRun_HasExpectedInitialState()
        {
            var run = new RunState(
                "run.test",
                currentDayNumber: 1,
                money: 0,
                suspicion: 0,
                rewindCount: 0,
                unlockedCategoryIds: new[] { "category.warm" },
                unlockedHerbIds: new[] { "herb.ginger", "herb.mint" },
                unlockedGuidebookEntryIds: new[] { "guidebook.warm" },
                tutorialFlags: new Dictionary<string, bool> { { "tutorial.arrival", false } },
                narrativeFlags: new Dictionary<string, bool> { { "narrative.intro", true } });

            Assert.That(run.RunId, Is.EqualTo("run.test"));
            Assert.That(run.CurrentDayNumber, Is.EqualTo(1));
            Assert.That(run.Money, Is.Zero);
            Assert.That(run.Suspicion, Is.Zero);
            Assert.That(run.RewindCount, Is.Zero);
            Assert.That(run.UnlockedCategoryIds, Is.EqualTo(new[] { "category.warm" }));
            Assert.That(run.UnlockedHerbIds, Is.EqualTo(new[] { "herb.ginger", "herb.mint" }));
            Assert.That(run.UnlockedGuidebookEntryIds, Is.EqualTo(new[] { "guidebook.warm" }));
            Assert.That(run.TutorialFlags["tutorial.arrival"], Is.False);
            Assert.That(run.NarrativeFlags["narrative.intro"], Is.True);
        }

        [Test]
        public void MarkOrderSeen_TracksUniquePoolAndResetsWhenOrderRepeats()
        {
            var run = new RunState("run.test", 1, 0, 0, 0);

            RunState firstCycle = run.MarkOrderSeen("order.one").MarkOrderSeen("order.two");
            RunState nextCycle = firstCycle.MarkOrderSeen("order.one");

            Assert.That(firstCycle.SeenOrderIds, Is.EqualTo(new[] { "order.one", "order.two" }));
            Assert.That(nextCycle.SeenOrderIds, Is.EqualTo(new[] { "order.one" }));
        }
    }
}
