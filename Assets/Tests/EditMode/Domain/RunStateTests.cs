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
                unlockedRecipeIds: new[] { "recipe.warming-tonic" },
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
            Assert.That(run.UnlockedRecipeIds, Is.EqualTo(new[] { "recipe.warming-tonic" }));
            Assert.That(run.UnlockedGuidebookEntryIds, Is.EqualTo(new[] { "guidebook.warm" }));
            Assert.That(run.TutorialFlags["tutorial.arrival"], Is.False);
            Assert.That(run.NarrativeFlags["narrative.intro"], Is.True);
        }
    }
}
