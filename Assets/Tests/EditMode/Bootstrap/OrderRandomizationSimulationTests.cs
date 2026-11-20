using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using Witch.Application.Flow;
using Witch.Bootstrap;
using Witch.Domain.Orders;
using Witch.Domain.Progression;
using Witch.Infrastructure.Content;
using Witch.Infrastructure.Persistence;
using Witch.Presentation.Shop;

namespace Witch.Tests.EditMode.Bootstrap
{
    public sealed class OrderRandomizationSimulationTests
    {
        private const string CatalogPath = "Assets/Content/GameContentCatalog.asset";
        private GameRuntimeContent content;

        [OneTimeSetUp]
        public void LoadContent()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalogDefinition>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, "Missing " + CatalogPath);
            GameContentLoadResult load = new GameContentAdapter().Load(catalog);
            Assert.That(load.Succeeded, string.Join("\n", load.Errors));
            content = load.Content;
        }

        [Test]
        public void SeededFourteenDaySimulation_HoldsEveryRule()
        {
            const int seedCount = 24;
            int totalDraws = 0;
            int newOrderRepeats = 0;
            int newRuleOneViolations = 0;
            int oldOrderRepeats = 0;
            int immediatePairRepeats = 0;
            int immediatePairViolations = 0;
            int cyclePairRepeats = 0;
            int cyclePairViolations = 0;
            int categoryRuns = 0;
            int categoryViolations = 0;
            int tierRuleViolations = 0;
            int introViolations = 0;
            int ruleTwoViolations = 0;
            int ruleEightClears = 0;
            int hunterDraws = 0;
            var relaxationCounts = new Dictionary<string, int>(StringComparer.Ordinal);

            for (int seedIndex = 0; seedIndex < seedCount; seedIndex++)
            {
                int runSeed = 1000 + (seedIndex * 7919);
                var seen = new HashSet<string>(StringComparer.Ordinal);
                var oldSeen = new HashSet<string>(StringComparer.Ordinal);
                OrderSelectionState carried = new OrderSelectionState();

                for (int day = 1; day <= 14; day++)
                {
                    int daySeed = unchecked(runSeed + (day * 101));
                    ShopPresentationSetup setup = CreateSetup(day, daySeed, out OrderSelectionState scheduled, out _);
                    DayContent dayContent = content.DaysByNumber[day];
                    string[] priorSeen = seen.ToArray();

                    string pending = carried.PendingIntroOrderId;
                    if (string.IsNullOrWhiteSpace(pending)) pending = scheduled.PendingIntroOrderId;
                    bool carriedPending = !string.IsNullOrWhiteSpace(carried.PendingIntroOrderId);
                    IReadOnlyList<string> scheduledIds = carriedPending || !string.IsNullOrWhiteSpace(pending)
                        ? new string[0]
                        : scheduled.ScheduledIntroOrderIds;
                    int introSlot = carriedPending || !string.IsNullOrWhiteSpace(pending) ? 0 : scheduled.IntroSlot;
                    string expectedIntro = carriedPending || !string.IsNullOrWhiteSpace(pending)
                        ? pending
                        : scheduledIds.FirstOrDefault();
                    int customerCount = 5 + PositiveModulo(runSeed + (day * 13), 21);

                    var warnings = new List<string>();
                    OrderSelectionState initial = new OrderSelectionState(
                        new string[0],
                        carried.GivenPairKeys,
                        scheduledIds,
                        pending,
                        introSlot,
                        carried.LastPairKey);
                    var deck = new ShopEncounterDeck(
                        setup.Encounters,
                        setup.EncounterSeed,
                        setup.RandomizeEncounters,
                        seen.ToArray(),
                        setup.IntroductionOrderId,
                        setup.OneTimeIntroduction,
                        setup.IntroductionCategoryOrderIds,
                        initial,
                        setup.DayDifficultyRanks,
                        setup.GuidedTutorialEnabled,
                        day,
                        dayOneCoolingCategoryId: "category.cool",
                        logWarning: warnings.Add);

                    if (setup.Encounters.Any(encounter =>
                            string.Equals(encounter.CustomerId, "customer.witchHunter", StringComparison.Ordinal)))
                    {
                        hunterDraws++;
                    }

                    var eligible = new HashSet<string>(
                        setup.Encounters.Select(encounter => encounter.Order.OrderId),
                        StringComparer.Ordinal);
                    var issuedToday = new HashSet<string>(StringComparer.Ordinal);
                    var dayPairKeys = new HashSet<string>(
                        setup.Encounters.Select(encounter => OrderPairKey.From(encounter.Order.RequestedPair)),
                        StringComparer.Ordinal);
                    var cyclePairs = new HashSet<string>(
                        dayPairKeys.Where(pair => carried.GivenPairKeys.Contains(pair, StringComparer.Ordinal)),
                        StringComparer.Ordinal);
                    OrderSelectionState current = initial;
                    string previousPair = carried.LastPairKey;
                    string previousCategory = null;
                    int categoryRun = 0;
                    int introDrawIndex = -1;
                    int firstNonIntroIndex = !string.IsNullOrWhiteSpace(pending)
                        ? 1
                        : scheduledIds.Count == 0 ? 0 : introSlot == 1 ? 1 : 0;
                    int easiestRank = dayContent.OrderDifficulties.Count == 2
                        ? dayContent.OrderDifficulties.Min(difficulty => (int)difficulty)
                        : -1;

                    for (int index = 0; index < customerCount; index++)
                    {
                        int warningStart = warnings.Count;
                        ShopEncounterViewData draw = deck.Draw(index, current);
                        current = deck.CurrentState;
                        totalDraws++;

                        string id = draw.Order.OrderId;
                        string pair = OrderPairKey.From(draw.Order.RequestedPair);

                        string[] drawWarnings = warnings.Skip(warningStart).ToArray();
                        bool warned(string prefix) => drawWarnings.Any(warning => warning.StartsWith(prefix, StringComparison.Ordinal));
                        bool ruleOneCleared = warned("rule1:");
                        bool ruleFiveCleared = warned("rule5:");
                        bool ruleSixCleared = warned("rule6:");
                        bool ruleSevenCleared = warned("rule7:");
                        bool ruleEightCleared = warned("rule8:");
                        bool dayOneWarm = day == 1 && index == 0 && setup.GuidedTutorialEnabled
                            && string.Equals(id, "order.tutorial.w01", StringComparison.Ordinal);
                        if (!eligible.Contains(id)) ruleTwoViolations++;
                        if (!IsAllowedDifficulty(draw, dayContent, expectedIntro, day, setup.GuidedTutorialEnabled)) ruleTwoViolations++;
                        if (seen.Contains(id))
                        {
                            newOrderRepeats++;
                            if (!ruleOneCleared) newRuleOneViolations++;
                        }
                        if (string.Equals(pair, previousPair, StringComparison.Ordinal))
                        {
                            immediatePairRepeats++;
                            if (!ruleFiveCleared) immediatePairViolations++;
                        }
                        if (string.Equals(draw.CategoryId, previousCategory, StringComparison.Ordinal)) categoryRun++;
                        else categoryRun = 1;
                        if (categoryRun >= 3)
                        {
                            categoryRuns++;
                            if (!ruleSixCleared && !ruleOneCleared) categoryViolations++;
                        }
                        if (string.Equals(id, expectedIntro, StringComparison.Ordinal)) introDrawIndex = index;
                        if (easiestRank >= 0 && index == firstNonIntroIndex
                            && draw.DifficultyRank != easiestRank
                            && !dayOneWarm
                            && !string.Equals(id, expectedIntro, StringComparison.Ordinal)
                            && !ruleSevenCleared
                            && !ruleOneCleared)
                        {
                            tierRuleViolations++;
                        }

                        if (deck.LastPairCycleCleared)
                        {
                            cyclePairs.Clear();
                            ruleEightClears++;
                        }
                        if (cyclePairs.Contains(pair))
                        {
                            cyclePairRepeats++;
                            if (!ruleEightCleared && !ruleOneCleared) cyclePairViolations++;
                        }
                        cyclePairs.Add(pair);

                        seen.Add(id);
                        issuedToday.Add(id);
                        previousPair = pair;
                        previousCategory = draw.CategoryId;

                        for (int warningIndex = warningStart; warningIndex < warnings.Count; warningIndex++)
                        {
                            string warning = warnings[warningIndex];
                            string bucket = warning.Contains(':') ? warning.Substring(0, warning.IndexOf(':')) : warning;
                            relaxationCounts.TryGetValue(bucket, out int count);
                            relaxationCounts[bucket] = count + 1;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(expectedIntro))
                    {
                        if (introDrawIndex < 0 && string.IsNullOrWhiteSpace(current.PendingIntroOrderId)) introViolations++;
                        if (introDrawIndex >= 0 && !string.IsNullOrWhiteSpace(pending) && introDrawIndex != 0) introViolations++;
                        if (introDrawIndex >= 0 && string.IsNullOrWhiteSpace(pending)
                            && introSlot >= 1 && introSlot <= 3 && introDrawIndex != introSlot - 1)
                        {
                            introViolations++;
                        }
                    }

                    string[] oldIds = SimulateOldSequence(
                        setup.Encounters,
                        setup.EncounterSeed,
                        priorSeen,
                        setup.OneTimeIntroduction,
                        setup.IntroductionOrderId,
                        new HashSet<string>(setup.IntroductionCategoryOrderIds ?? new string[0], StringComparer.Ordinal),
                        customerCount);
                    foreach (string oldId in oldIds)
                    {
                        if (oldSeen.Contains(oldId)) oldOrderRepeats++;
                        else oldSeen.Add(oldId);
                    }

                    carried = new OrderSelectionState(
                        new string[0],
                        current.GivenPairKeys,
                        new string[0],
                        current.PendingIntroOrderId,
                        0,
                        current.LastPairKey);
                }
            }

            string summary =                 "simulation draws=" + totalDraws
                + " oldOrderRepeatRate=" + oldOrderRepeats + "/" + totalDraws
                + " (" + Percent(oldOrderRepeats, totalDraws) + "%)"
                + " newOrderRepeatRate=" + newOrderRepeats + "/" + totalDraws
                + " (" + Percent(newOrderRepeats, totalDraws) + "%)"
                + " newRuleOneViolations=" + newRuleOneViolations
                + " immediatePairRepeats=" + immediatePairRepeats
                + " immediatePairViolations=" + immediatePairViolations
                + " cyclePairRepeats=" + cyclePairRepeats
                + " cyclePairViolations=" + cyclePairViolations
                + " categoryRuns=" + categoryRuns
                + " categoryViolations=" + categoryViolations
                + " tierRuleViolations=" + tierRuleViolations
                + " introViolations=" + introViolations
                + " ruleTwoViolations=" + ruleTwoViolations
                + " hunterDraws=" + hunterDraws
                + " ruleEightClears=" + ruleEightClears
                + " relaxations=" + string.Join(",", relaxationCounts.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => pair.Key + "=" + pair.Value));
            TestContext.WriteLine(summary);

            Assert.That(ruleTwoViolations, Is.Zero, "rule2: ineligible tier or herb pair drawn");
            Assert.That(newRuleOneViolations, Is.Zero, "rule1: order repeated before pool exhaustion");
            Assert.That(immediatePairViolations, Is.Zero, "rule5: same pair appeared back to back without a relaxation");
            Assert.That(categoryViolations, Is.Zero, "rule6: category appeared three times in a row without a relaxation");
            Assert.That(tierRuleViolations, Is.Zero, "rule7: first order used wrong tier");
            Assert.That(introViolations, Is.Zero, "rule3/rule4: introduction slot failed");
            Assert.That(cyclePairViolations, Is.Zero, "rule8: pair repeated before cycle clear");
            Assert.That(hunterDraws, Is.Zero, "hunter: witch hunter must not be in the normal draw pool");
        }

        [Test]
        public async Task SaveLoadAtAnyDrawPosition_ProducesIdenticalRemainingSequence()
        {
            for (int drawCount = 0; drawCount <= 6; drawCount++)
            {
                ShopPresentationSetup setup = CreateSetup(6, 4242, out _, out GameSession session);
                var seen = new HashSet<string>(StringComparer.Ordinal);
                var selection = new OrderSelectionState();

                for (int index = 0; index < drawCount; index++)
                {
                    Draw(setup, index, seen, ref selection);
                }

                string saveDirectory = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "witch-order-selection-" + Guid.NewGuid().ToString("N"));
                try
                {
                    var repository = new JsonRunRepository(saveDirectory, JsonRunRepository.CurrentSaveVersion);
                    var snapshot = new DayStartSnapshot(
                        session.RunState.WithSeenOrders(seen.ToArray()),
                        session.DayStartSnapshot.OrderSeed,
                        session.DayStartSnapshot.TimerDuration,
                        session.DayStartSnapshot.ExpenseId,
                        session.DayStartSnapshot.ExpenseValue,
                        session.DayStartSnapshot.SaveVersion,
                        session.DayStartSnapshot.CapturedAtUtc,
                        new OrderSelectionState());
                    var metadata = new ActiveAttemptMetadata(
                        session.AttemptMetadata.RunId,
                        session.AttemptMetadata.DayNumber,
                        session.AttemptMetadata.Suspicion,
                        session.AttemptMetadata.MinigameWinCount,
                        false,
                        0,
                        session.AttemptMetadata.ActiveOrderSeed,
                        selection: selection);
                    Assert.That((await repository.SaveRunStateAsync(snapshot, metadata)).Succeeded, Is.True);

                    var loadResult = await repository.LoadLatestAsync();
                    Assert.That(loadResult.Succeeded, loadResult.ErrorMessage);
                    OrderSelectionState loadedSelection = loadResult.SavedGame.AttemptMetadata.Selection;
                    IReadOnlyList<string> loadedSeen = loadResult.SavedGame.DayStartSnapshot.RunState.SeenOrderIds;
                    Assert.That(loadedSelection.UsedOrderIds, Is.EqualTo(selection.UsedOrderIds));
                    Assert.That(loadedSelection.GivenPairKeys, Is.EqualTo(selection.GivenPairKeys));
                    Assert.That(loadedSelection.LastPairKey, Is.EqualTo(selection.LastPairKey), "position " + drawCount);

                    var loadedSeenSet = new HashSet<string>(loadedSeen, StringComparer.Ordinal);
                    for (int index = drawCount; index < drawCount + 4; index++)
                    {
                        string originalId = Draw(setup, index, seen, ref selection).Order.OrderId;
                        string loadedSelectionId = Draw(setup, index, loadedSeenSet, ref loadedSelection).Order.OrderId;
                        Assert.That(loadedSelectionId, Is.EqualTo(originalId), "save/load changed draw " + index);
                    }
                }
                finally
                {
                    if (System.IO.Directory.Exists(saveDirectory)) System.IO.Directory.Delete(saveDirectory, true);
                }
            }
        }

        private static ShopEncounterViewData Draw(
            ShopPresentationSetup setup,
            int index,
            HashSet<string> seen,
            ref OrderSelectionState selection)
        {
            var deck = new ShopEncounterDeck(
                setup.Encounters,
                setup.EncounterSeed,
                setup.RandomizeEncounters,
                seen.ToArray(),
                setup.IntroductionOrderId,
                setup.OneTimeIntroduction,
                setup.IntroductionCategoryOrderIds,
                selection,
                setup.DayDifficultyRanks,
                setup.GuidedTutorialEnabled,
                setup.DayNumber,
                logWarning: _ => { });
            ShopEncounterViewData draw = deck.Draw(index, selection);
            selection = deck.CurrentState;
            seen.Add(draw.Order.OrderId);
            return draw;
        }

        private ShopPresentationSetup CreateSetup(
            int day,
            int seed,
            out OrderSelectionState scheduledSelection,
            out GameSession session)
        {
            session = DevelopmentStartupSessionFactory.CreateShopPreparationForDay(
                content.NewRunSetup,
                content.DayProgressionSetup,
                day,
                content.NewRunSetup.StartingMoney,
                seed,
                DateTimeOffset.UnixEpoch);
            ShopPresentationSetup setup = new ShopPresentationSetupFactory(content).Create(session);
            scheduledSelection = session.AttemptMetadata.Selection;
            return setup;
        }

        private static bool IsAllowedDifficulty(
            ShopEncounterViewData draw,
            DayContent day,
            string expectedIntro,
            int dayNumber,
            bool forceDayOneTutorial)
        {
            if (string.Equals(draw.Order.OrderId, expectedIntro, StringComparison.Ordinal)) return true;
            if (!Enum.TryParse<OrderDifficulty>(draw.DifficultyId, out OrderDifficulty difficulty)) return false;
            if (forceDayOneTutorial && dayNumber == 1 && difficulty == OrderDifficulty.Tutorial) return true;
            return day.OrderDifficulties.Contains(difficulty);
        }

        private static string[] SimulateOldSequence(
            IReadOnlyList<ShopEncounterViewData> templates,
            int seed,
            IReadOnlyList<string> seenOrderIds,
            bool oneTimeIntroduction,
            string introductionOrderId,
            HashSet<string> introductionCategoryOrderIds,
            int count)
        {
            var random = new System.Random(seed);
            var initiallySeen = new HashSet<string>(seenOrderIds, StringComparer.Ordinal);
            var sequence = new List<int>();
            string previousPair = null;
            int previousRunLength = 0;
            bool firstBag = true;

            while (sequence.Count < count)
            {
                var candidates = new List<int>();
                for (int index = 0; index < templates.Count; index++)
                {
                    string orderId = templates[index].Order.OrderId;
                    if (!firstBag && oneTimeIntroduction
                        && string.Equals(orderId, introductionOrderId, StringComparison.Ordinal))
                    {
                        continue;
                    }
                    if (!firstBag || !initiallySeen.Contains(orderId)) candidates.Add(index);
                }
                if (candidates.Count == 0)
                {
                    for (int index = 0; index < templates.Count; index++) candidates.Add(index);
                }

                List<int> bag = CreateValidShuffledBag(
                    random,
                    templates,
                    previousPair,
                    previousRunLength,
                    candidates);
                if (firstBag)
                {
                    PlaceOldIntroduction(
                        random,
                        templates,
                        bag,
                        initiallySeen,
                        introductionOrderId,
                        introductionCategoryOrderIds);
                }

                foreach (int index in bag)
                {
                    string pair = OrderPairKey.From(templates[index].Order.RequestedPair);
                    if (string.Equals(pair, previousPair, StringComparison.Ordinal)) previousRunLength++;
                    else
                    {
                        previousPair = pair;
                        previousRunLength = 1;
                    }
                    sequence.Add(index);
                }
                firstBag = false;
            }

            return sequence.Take(count).Select(index => templates[index].Order.OrderId).ToArray();
        }

        private static List<int> CreateValidShuffledBag(
            System.Random random,
            IReadOnlyList<ShopEncounterViewData> templates,
            string previousPair,
            int previousRunLength,
            IReadOnlyList<int> candidates)
        {
            const int attemptLimit = 1000;
            List<int> bag = null;
            for (int attempt = 0; attempt < attemptLimit; attempt++)
            {
                bag = new List<int>(candidates);
                for (int index = bag.Count - 1; index > 0; index--)
                {
                    int swapIndex = random.Next(index + 1);
                    int temp = bag[index];
                    bag[index] = bag[swapIndex];
                    bag[swapIndex] = temp;
                }

                if (HasMaximumPairRun(bag, templates, previousPair, previousRunLength, 2)) return bag;
            }
            return bag;
        }

        private static bool HasMaximumPairRun(
            IReadOnlyList<int> bag,
            IReadOnlyList<ShopEncounterViewData> templates,
            string previousPair,
            int previousRunLength,
            int maximumRun)
        {
            string pair = previousPair;
            int runLength = previousRunLength;
            foreach (int index in bag)
            {
                string next = OrderPairKey.From(templates[index].Order.RequestedPair);
                if (string.Equals(next, pair, StringComparison.Ordinal)) runLength++;
                else
                {
                    pair = next;
                    runLength = 1;
                }
                if (runLength > maximumRun) return false;
            }
            return true;
        }

        private static void PlaceOldIntroduction(
            System.Random random,
            IReadOnlyList<ShopEncounterViewData> templates,
            List<int> bag,
            HashSet<string> initiallySeen,
            string introductionOrderId,
            HashSet<string> introductionCategoryOrderIds)
        {
            if (string.IsNullOrEmpty(introductionOrderId) || initiallySeen.Contains(introductionOrderId)) return;
            int introductionIndex = bag.FindIndex(index =>
                string.Equals(templates[index].Order.OrderId, introductionOrderId, StringComparison.Ordinal));
            if (introductionIndex < 0) return;

            int introduction = bag[introductionIndex];
            bag.RemoveAt(introductionIndex);
            int normalCount = bag.Count(index =>
                !introductionCategoryOrderIds.Contains(templates[index].Order.OrderId));
            int target = random.Next(Math.Min(2, normalCount) + 1);
            for (int index = 0; index < target; index++)
            {
                if (!introductionCategoryOrderIds.Contains(templates[bag[index]].Order.OrderId)) continue;
                int replacement = bag.FindIndex(target, value =>
                    !introductionCategoryOrderIds.Contains(templates[value].Order.OrderId));
                int temp = bag[index];
                bag[index] = bag[replacement];
                bag[replacement] = temp;
            }
            bag.Insert(target, introduction);
        }

        private static string Percent(int count, int total)
        {
            if (total == 0) return "0.00";
            return (100d * count / total).ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static int PositiveModulo(int value, int count)
        {
            int result = value % count;
            return result < 0 ? result + count : result;
        }
    }
}
