using System;
using System.Collections.Generic;
using NUnit.Framework;
using Witch.Domain.Orders;
using Witch.Domain.Potions;
using Witch.Domain.Progression;

namespace Witch.Domain.Tests
{
    public sealed class DayRuntimeStateTests
    {
        [Test]
        public void DayStartSnapshot_KeepsStableBeginningOfDayData()
        {
            RunState runState = CreateRunState();
            var capturedAt = new DateTimeOffset(2026, 10, 23, 8, 0, 0, TimeSpan.Zero);

            var snapshot = new DayStartSnapshot(
                runState,
                orderSeed: 1234,
                timerDuration: TimeSpan.FromMinutes(4),
                expenseId: "expense.day.1",
                expenseValue: 20,
                saveVersion: 1,
                capturedAtUtc: capturedAt);

            Assert.That(snapshot.RunState, Is.SameAs(runState));
            Assert.That(snapshot.OrderSeed, Is.EqualTo(1234));
            Assert.That(snapshot.TimerDuration, Is.EqualTo(TimeSpan.FromMinutes(4)));
            Assert.That(snapshot.ExpenseId, Is.EqualTo("expense.day.1"));
            Assert.That(snapshot.ExpenseValue, Is.EqualTo(20));
            Assert.That(snapshot.SaveVersion, Is.EqualTo(1));
            Assert.That(snapshot.CapturedAtUtc, Is.EqualTo(capturedAt));
        }

        [Test]
        public void DayAttemptState_CopiesRuntimeProgressWithoutChangingSnapshot()
        {
            var usedOrderIds = new List<string> { "order.first" };
            var activeOrder = new OrderRequest(
                "order.second",
                new HerbPair("herb.ginger", "herb.mint"));
            var snapshot = new DayStartSnapshot(
                CreateRunState(),
                1234,
                TimeSpan.FromMinutes(4),
                "expense.day.1",
                20,
                1,
                DateTimeOffset.UtcNow);

            var attempt = new DayAttemptState(
                attemptSeed: 5678,
                remainingTime: TimeSpan.FromMinutes(2),
                usedOrderIds: usedOrderIds,
                activeCustomerId: "customer.villager.one",
                activeOrder: activeOrder,
                earnedMoney: 10,
                potionDraft: new PotionDraft(),
                preparedRecipe: null);
            usedOrderIds.Add("order.changed-after-construction");

            Assert.That(attempt.AttemptSeed, Is.EqualTo(5678));
            Assert.That(attempt.UsedOrderIds, Is.EqualTo(new[] { "order.first" }));
            Assert.That(attempt.ActiveCustomerId, Is.EqualTo("customer.villager.one"));
            Assert.That(attempt.ActiveOrder, Is.SameAs(activeOrder));
            Assert.That(attempt.EarnedMoney, Is.EqualTo(10));
            Assert.That(snapshot.RunState.Money, Is.Zero);
        }

        private static RunState CreateRunState()
        {
            return new RunState(
                "run.test",
                1,
                0,
                0,
                0,
                new[] { "category.warm" },
                new[] { "herb.ginger", "herb.mint" },
                new[] { "recipe.warming-tonic" },
                new[] { "guidebook.warm" });
        }
    }
}
