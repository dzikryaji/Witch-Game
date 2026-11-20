using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Witch.Domain.Orders;
using Witch.Domain.Potions;
using Witch.Presentation.Shop;

namespace Witch.Presentation.Tests
{
    public sealed class ShopEncounterDeckTests
    {
        [Test]
        public void Rule1_NoOrderRepeatsUntilEligiblePoolIsExhausted()
        {
            ShopEncounterViewData[] encounters =
            {
                Encounter("order.a", "herb.a1", "herb.a2"),
                Encounter("order.b", "herb.b1", "herb.b2"),
                Encounter("order.c", "herb.c1", "herb.c2")
            };
            var deck = Deck(encounters, 17);

            string[] ids = Enumerable.Range(0, 4).Select(index => deck.Draw(index).Order.OrderId).ToArray();

            Assert.That(ids.Take(3).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(3));
            Assert.That(ids.Take(3), Is.EquivalentTo(new[] { "order.a", "order.b", "order.c" }));
            Assert.That(deck.LastRelaxations.Any(message => message.StartsWith("rule1:", StringComparison.Ordinal)), Is.True);
        }

        [Test]
        public void Rule3_IntroductionReservesHerbPairUntilItIsGiven()
        {
            ShopEncounterViewData[] encounters =
            {
                Encounter("order.intro", "herb.i1", "herb.i2"),
                Encounter("order.intro.duplicate", "herb.i2", "herb.i1"),
                Encounter("order.other.a", "herb.a1", "herb.a2"),
                Encounter("order.other.b", "herb.b1", "herb.b2")
            };
            var selection = new OrderSelectionState(
                null,
                null,
                new[] { "order.intro" },
                null,
                2);
            var deck = Deck(encounters, 23, selection);

            string[] ids = Enumerable.Range(0, 5).Select(index => deck.Draw(index).Order.OrderId).ToArray();

            Assert.That(ids[1], Is.EqualTo("order.intro"));
            Assert.That(ids[0], Is.Not.EqualTo("order.intro.duplicate"));
            Assert.That(ids, Does.Contain("order.intro.duplicate"));
        }

        [Test]
        public void Rule4_ScheduledIntroductionUsesSeededSlotWithinFirstThree()
        {
            ShopEncounterViewData[] encounters =
            {
                Encounter("order.intro", "herb.i1", "herb.i2"),
                Encounter("order.other.a", "herb.a1", "herb.a2"),
                Encounter("order.other.b", "herb.b1", "herb.b2"),
                Encounter("order.other.c", "herb.c1", "herb.c2")
            };
            var selection = new OrderSelectionState(null, null, new[] { "order.intro" }, null, 3);
            var deck = Deck(encounters, 31, selection);

            string[] ids = Enumerable.Range(0, 4).Select(index => deck.Draw(index).Order.OrderId).ToArray();

            Assert.That(ids[2], Is.EqualTo("order.intro"));
            Assert.That(ids.Take(2), Does.Not.Contain("order.intro"));
        }

        [Test]
        public void Rule4_IntroductionIsNotDrawnEarlyWhenEveryOrderSharesItsPair()
        {
            ShopEncounterViewData[] encounters =
            {
                Encounter("order.a", "herb.x", "herb.y"),
                Encounter("order.intro", "herb.x", "herb.y"),
                Encounter("order.b", "herb.x", "herb.y")
            };
            var selection = new OrderSelectionState(null, null, new[] { "order.intro" }, null, 2);
            var deck = Deck(encounters, 33, selection);

            string[] ids = Enumerable.Range(0, 3).Select(index => deck.Draw(index).Order.OrderId).ToArray();

            Assert.That(ids[0], Is.Not.EqualTo("order.intro"));
            Assert.That(ids[1], Is.EqualTo("order.intro"));
            Assert.That(ids.Count(id => id == "order.intro"), Is.EqualTo(1));
        }

        [Test]
        public void Rule4_CarriedIntroductionIsForcedIntoFirstSlot()
        {
            ShopEncounterViewData[] encounters =
            {
                Encounter("order.intro", "herb.i1", "herb.i2"),
                Encounter("order.other.a", "herb.a1", "herb.a2"),
                Encounter("order.other.b", "herb.b1", "herb.b2")
            };
            var selection = new OrderSelectionState(null, null, null, "order.intro", 0);
            var deck = Deck(encounters, 37, selection);

            Assert.That(deck.Draw(0).Order.OrderId, Is.EqualTo("order.intro"));
        }

        [Test]
        public void Rule5_NoHerbPairAppearsInTwoConsecutiveDraws()
        {
            ShopEncounterViewData[] encounters = MakePairs(4, 3);
            var deck = Deck(encounters, 41);
            string previous = null;

            for (int index = 0; index < 16; index++)
            {
                string pair = Pair(deck.Draw(index));
                Assert.That(pair, Is.Not.EqualTo(previous), "pair repeated at draw " + index);
                previous = pair;
            }
        }

        [Test]
        public void Rule6_NoCategoryAppearsThreeTimesInARow()
        {
            var encounters = new List<ShopEncounterViewData>();
            for (int pairIndex = 0; pairIndex < 6; pairIndex++)
            {
                string category = pairIndex % 2 == 0 ? "category.warm" : "category.cool";
                for (int orderIndex = 0; orderIndex < 3; orderIndex++)
                {
                    encounters.Add(Encounter(
                        "order." + pairIndex + "." + orderIndex,
                        "herb.p" + pairIndex + "a",
                        "herb.p" + pairIndex + "b",
                        category));
                }
            }

            var deck = Deck(encounters, 43);
            string previousCategory = null;
            int run = 0;

            for (int index = 0; index < 18; index++)
            {
                string category = deck.Draw(index).CategoryId;
                run = string.Equals(category, previousCategory, StringComparison.Ordinal) ? run + 1 : 1;
                Assert.That(run, Is.LessThanOrEqualTo(2), "category run of three at draw " + index);
                previousCategory = category;
            }
        }

        [Test]
        public void Rule7_FirstNonIntroDrawUsesEasierTierUnlessIntroductionOwnsSlotOne()
        {
            ShopEncounterViewData[] encounters =
            {
                Encounter("order.easy.a", "herb.a1", "herb.a2", "category.warm", "Simple", 1),
                Encounter("order.easy.b", "herb.b1", "herb.b2", "category.warm", "Simple", 1),
                Encounter("order.mid.a", "herb.c1", "herb.c2", "category.warm", "Mid", 2),
                Encounter("order.mid.b", "herb.d1", "herb.d2", "category.warm", "Mid", 2)
            };
            var deck = Deck(encounters, 47, dayDifficultyRanks: new[] { 1, 2 });

            Assert.That(deck.Draw(0).DifficultyRank, Is.EqualTo(1));

            var introEncounters = encounters.Concat(new[]
            {
                Encounter("order.intro", "herb.i1", "herb.i2", "category.warm", "Mid", 2)
            }).ToArray();
            var introSelection = new OrderSelectionState(null, null, new[] { "order.intro" }, null, 1);
            var introDeck = Deck(introEncounters, 47, introSelection, new[] { 1, 2 });

            Assert.That(introDeck.Draw(0).Order.OrderId, Is.EqualTo("order.intro"));
        }

        [Test]
        public void Rule8_NoPairRepeatsUntilCycleClearsAndBackToBackBanSurvives()
        {
            ShopEncounterViewData[] encounters = MakePairs(3, 3);
            var deck = Deck(encounters, 53);
            var pairs = new List<string>();

            for (int index = 0; index < 3; index++) pairs.Add(Pair(deck.Draw(index)));

            Assert.That(pairs.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(3));

            ShopEncounterViewData fourth = deck.Draw(3);

            Assert.That(deck.LastPairCycleCleared, Is.True);
            Assert.That(deck.LastRelaxations.Any(message => message.StartsWith("rule8", StringComparison.Ordinal)), Is.True);
            Assert.That(Pair(fourth), Is.Not.EqualTo(pairs[2]));
        }

        [Test]
        public void DayOne_ForcesWarmTutorialThenSeededCoolingTutorialWithoutConsumingWarmPairAgain()
        {
            const string warmOrder = "order.tutorial.w01";
            const string warmPair = "herb.ginger|herb.mustardSeed";
            ShopEncounterViewData[] encounters =
            {
                Encounter(warmOrder, "herb.ginger", "herb.mustardSeed", "category.warm", "Tutorial", 0),
                Encounter("order.tutorial.c01", "herb.peppermint", "herb.borage", "category.cool", "Tutorial", 0),
                Encounter("order.tutorial.c02", "herb.spearmint", "herb.yarrow", "category.cool", "Tutorial", 0),
                Encounter("order.tutorial.c03", "herb.sennaLeaf", "herb.arnicaFlower", "category.cool", "Tutorial", 0),
                Encounter("order.normal.a", "herb.lavender", "herb.chamomile", "category.soothe", "Simple", 1),
                Encounter("order.normal.b", "herb.sage", "herb.thyme", "category.warm", "Simple", 1)
            };
            var deck = Deck(
                encounters,
                59,
                forceDayOneTutorial: true,
                dayNumber: 1,
                dayOneCoolingCategoryId: "category.cool");

            ShopEncounterViewData first = deck.Draw(0);
            ShopEncounterViewData second = deck.Draw(1);
            ShopEncounterViewData third = deck.Draw(2);

            Assert.That(first.Order.OrderId, Is.EqualTo(warmOrder));
            Assert.That(second.DifficultyId, Is.EqualTo("Tutorial"));
            Assert.That(second.CategoryId, Is.EqualTo("category.cool"));
            Assert.That(Pair(second), Is.Not.EqualTo(warmPair));
            Assert.That(third.Order.OrderId, Is.Not.EqualTo(warmOrder));
            Assert.That(third.Order.OrderId, Is.Not.EqualTo(second.Order.OrderId));
            Assert.That(deck.CurrentState.UsedOrderIds.Count, Is.EqualTo(3));
            Assert.That(deck.CurrentState.UsedOrderIds.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(3));

            var resumed = new HashSet<string>(StringComparer.Ordinal)
            {
                first.Order.OrderId,
                second.Order.OrderId
            };
            var resumedDeck = Deck(
                encounters,
                59,
                selection: deck.CurrentState,
                forceDayOneTutorial: true,
                dayNumber: 1,
                dayOneCoolingCategoryId: "category.cool");

            Assert.That(resumed.Contains(resumedDeck.Draw(2, deck.CurrentState).Order.OrderId), Is.False);
        }

        private static ShopEncounterViewData[] MakePairs(int pairCount, int ordersPerPair)
        {
            var encounters = new List<ShopEncounterViewData>();
            for (int pairIndex = 0; pairIndex < pairCount; pairIndex++)
            {
                for (int orderIndex = 0; orderIndex < ordersPerPair; orderIndex++)
                {
                    encounters.Add(Encounter(
                        "order.p" + pairIndex + "." + orderIndex,
                        "herb.p" + pairIndex + "a",
                        "herb.p" + pairIndex + "b"));
                }
            }

            return encounters.ToArray();
        }

        private static ShopEncounterViewData Encounter(
            string id,
            string firstHerb,
            string secondHerb,
            string categoryId = "category.warm",
            string difficultyId = "Simple",
            int difficultyRank = 1)
        {
            return new ShopEncounterViewData(
                "customer.test",
                "Test Villager",
                null,
                new OrderRequest(id, new HerbPair(firstHerb, secondHerb)),
                new[] { "Please mix " + firstHerb + " and " + secondHerb + "." },
                difficultyId,
                difficultyRank,
                categoryId);
        }

        private static ShopEncounterDeck Deck(
            IReadOnlyList<ShopEncounterViewData> encounters,
            int seed,
            OrderSelectionState selection = null,
            IReadOnlyList<int> dayDifficultyRanks = null,
            bool forceDayOneTutorial = false,
            int dayNumber = 2,
            string dayOneCoolingCategoryId = "category.cool")
        {
            return new ShopEncounterDeck(
                encounters,
                seed,
                randomizeEncounters: true,
                selection: selection,
                dayDifficultyRanks: dayDifficultyRanks,
                forceDayOneTutorial: forceDayOneTutorial,
                dayNumber: dayNumber,
                dayOneCoolingCategoryId: dayOneCoolingCategoryId,
                logWarning: _ => { });
        }

        private static string Pair(ShopEncounterViewData encounter)
        {
            return OrderPairKey.From(encounter.Order.RequestedPair);
        }
    }
}
