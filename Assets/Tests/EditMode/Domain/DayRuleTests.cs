using System;
using NUnit.Framework;
using Witch.Domain.Progression;

namespace Witch.Domain.Tests
{
    public sealed class DayRuleTests
    {
        [TestCase(1, 4, 0)]
        [TestCase(2, 4, 20)]
        [TestCase(3, 4, 40)]
        public void TimerDuration_IncreasesTwentySecondsEachDay(int day, int minutes, int seconds)
        {
            var rules = new DayTimingRules(TimeSpan.FromMinutes(4), TimeSpan.FromSeconds(20));

            Assert.That(rules.GetDuration(day), Is.EqualTo(new TimeSpan(0, 0, minutes, seconds)));
        }

        [Test]
        public void TimerAtZero_ClosesQueueButActiveEncounterCanFinish()
        {
            var timer = new ShopTimerState(TimeSpan.FromSeconds(1), hasActiveEncounter: true);

            ShopTimerState elapsed = timer.Elapse(TimeSpan.FromSeconds(1));

            Assert.That(elapsed.QueueOpen, Is.False);
            Assert.That(elapsed.CanAdmitCustomer, Is.False);
            Assert.That(elapsed.CanCompleteActiveEncounter, Is.True);
        }

        [Test]
        public void Expense_WhenAffordable_DeductsAndAdvancesDay()
        {
            var rules = new ExpenseRules(completionDay: 3);
            RunState run = CreateRun(day: 1, money: 30);

            ExpenseOutcome outcome = rules.Resolve(run, expenseValue: 20);

            Assert.That(outcome.Kind, Is.EqualTo(ExpenseOutcomeKind.NextDay));
            Assert.That(outcome.UpdatedRunState.Money, Is.EqualTo(10));
            Assert.That(outcome.UpdatedRunState.CurrentDayNumber, Is.EqualTo(2));
        }

        [Test]
        public void Expense_WhenUnaffordable_DeductsNothingAndEndsRun()
        {
            var rules = new ExpenseRules(completionDay: 3);
            RunState run = CreateRun(day: 2, money: 19);

            ExpenseOutcome outcome = rules.Resolve(run, expenseValue: 20);

            Assert.That(outcome.Kind, Is.EqualTo(ExpenseOutcomeKind.GameOver));
            Assert.That(outcome.UpdatedRunState, Is.SameAs(run));
            Assert.That(outcome.UpdatedRunState.Money, Is.EqualTo(19));
            Assert.That(outcome.UpdatedRunState.CurrentDayNumber, Is.EqualTo(2));
        }

        [Test]
        public void PrototypeCompletionDayThree_WinsAfterPayingExpense()
        {
            var rules = new ExpenseRules(completionDay: 3);

            ExpenseOutcome outcome = rules.Resolve(CreateRun(day: 3, money: 20), 20);

            Assert.That(outcome.Kind, Is.EqualTo(ExpenseOutcomeKind.Victory));
            Assert.That(outcome.UpdatedRunState.Money, Is.Zero);
            Assert.That(outcome.UpdatedRunState.CurrentDayNumber, Is.EqualTo(3));
        }

        [Test]
        public void PlannedFullGameCompletionDayFourteen_RemainsConfigurable()
        {
            var rules = new ExpenseRules(completionDay: 14);

            ExpenseOutcome dayThree = rules.Resolve(CreateRun(day: 3, money: 20), 20);
            ExpenseOutcome dayFourteen = rules.Resolve(CreateRun(day: 14, money: 20), 20);

            Assert.That(dayThree.Kind, Is.EqualTo(ExpenseOutcomeKind.NextDay));
            Assert.That(dayThree.UpdatedRunState.CurrentDayNumber, Is.EqualTo(4));
            Assert.That(dayFourteen.Kind, Is.EqualTo(ExpenseOutcomeKind.Victory));
        }

        [TestCase(0, 3)]
        [TestCase(1, 4)]
        [TestCase(5, 8)]
        [TestCase(20, 8)]
        public void RewindBoard_GrowsFromThreePairsAndCapsAtEight(int rewindCount, int expectedPairs)
        {
            Assert.That(RewindRules.GetPairCount(rewindCount), Is.EqualTo(expectedPairs));
        }

        [Test]
        public void RewindCompletion_RestoresDayStartAndClearsSuspicionWithNewSeed()
        {
            RunState dayStartRun = CreateRun(day: 2, money: 5);
            var snapshot = new DayStartSnapshot(
                dayStartRun,
                100,
                TimeSpan.FromMinutes(4),
                "expense.day.2",
                20,
                1,
                DateTimeOffset.UtcNow);
            RunState failedAttempt = dayStartRun.WithProgress(
                currentDayNumber: 2,
                money: 35,
                suspicion: 3,
                rewindCount: 1);

            RewindOutcome outcome = RewindRules.Complete(snapshot, failedAttempt, nextAttemptSeed: 999);

            Assert.That(outcome.RestoredRunState.CurrentDayNumber, Is.EqualTo(2));
            Assert.That(outcome.RestoredRunState.Money, Is.EqualTo(5));
            Assert.That(outcome.RestoredRunState.Suspicion, Is.Zero);
            Assert.That(outcome.RestoredRunState.RewindCount, Is.EqualTo(2));
            Assert.That(outcome.NextAttemptSeed, Is.EqualTo(999));
        }

        private static RunState CreateRun(int day, int money)
        {
            return new RunState(
                "run.test",
                day,
                money,
                0,
                0,
                new[] { "category.warm" },
                new[] { "herb.ginger", "herb.mint" },
                new[] { "recipe.warming-tonic" },
                new[] { "guidebook.warm" });
        }
    }
}
