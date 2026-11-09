using System;
using System.Collections.Generic;
using System.Linq;
using Witch.Domain.Orders;
using Witch.Domain.Potions;

namespace Witch.Presentation.Shop
{
    public sealed class ShopEncounterDeck
    {
        private readonly IReadOnlyList<ShopEncounterViewData> templates;
        private readonly int seed;
        private readonly bool randomize;
        private readonly HashSet<string> initiallySeenOrderIds;
        private readonly string introductionOrderId;
        private readonly bool oneTimeIntroduction;
        private readonly HashSet<string> introductionCategoryOrderIds;

        public ShopEncounterDeck(
            IReadOnlyList<ShopEncounterViewData> encounterTemplates,
            int encounterSeed,
            bool randomizeEncounters,
            IReadOnlyList<string> seenOrderIds = null,
            string priorityIntroductionOrderId = null,
            bool removeIntroductionAfterFirstBag = false,
            IReadOnlyList<string> priorityCategoryOrderIds = null)
        {
            templates = encounterTemplates ?? throw new ArgumentNullException(nameof(encounterTemplates));
            if (templates.Count == 0)
            {
                throw new ArgumentException("At least one encounter template is required.", nameof(encounterTemplates));
            }

            seed = encounterSeed;
            randomize = randomizeEncounters;
            initiallySeenOrderIds = new HashSet<string>(seenOrderIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            introductionOrderId = priorityIntroductionOrderId;
            oneTimeIntroduction = removeIntroductionAfterFirstBag;
            introductionCategoryOrderIds = new HashSet<string>(priorityCategoryOrderIds ?? Array.Empty<string>(), StringComparer.Ordinal);
        }

        public ShopEncounterViewData Draw(int admissionIndex)
        {
            if (admissionIndex < 0) throw new ArgumentOutOfRangeException(nameof(admissionIndex));

            int templateIndex = randomize
                ? DrawRandomizedTemplateIndex(admissionIndex)
                : admissionIndex % templates.Count;
            ShopEncounterViewData template = templates[templateIndex];
            return new ShopEncounterViewData(
                template.CustomerId,
                template.CustomerName,
                template.CustomerIllustration,
                template.Order,
                template.OrderBubbles);
        }

        private int DrawRandomizedTemplateIndex(int admissionIndex)
        {
            var random = new Random(seed);
            var sequence = new List<int>();
            HerbPair previousPair = null;
            int previousRunLength = 0;
            bool firstBag = true;

            while (sequence.Count <= admissionIndex)
            {
                var candidates = new List<int>();
                for (int index = 0; index < templates.Count; index++)
                {
                    string orderId = templates[index].Order.OrderId;
                    if (!firstBag && oneTimeIntroduction && string.Equals(orderId, introductionOrderId, StringComparison.Ordinal)) continue;
                    if (!firstBag || !initiallySeenOrderIds.Contains(orderId)) candidates.Add(index);
                }
                if (candidates.Count == 0)
                {
                    for (int index = 0; index < templates.Count; index++) candidates.Add(index);
                }

                List<int> bag = CreateValidShuffledBag(random, previousPair, previousRunLength, candidates);
                if (firstBag) PlaceIntroductionInFirstThree(random, bag);
                foreach (int index in bag)
                {
                    HerbPair pair = templates[index].Order.RequestedPair;
                    if (pair.Equals(previousPair)) previousRunLength++;
                    else
                    {
                        previousPair = pair;
                        previousRunLength = 1;
                    }
                    sequence.Add(index);
                }
                firstBag = false;
            }

            return sequence[admissionIndex];
        }

        private void PlaceIntroductionInFirstThree(Random random, List<int> bag)
        {
            if (string.IsNullOrEmpty(introductionOrderId)
                || initiallySeenOrderIds.Contains(introductionOrderId)) return;

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
                (bag[index], bag[replacement]) = (bag[replacement], bag[index]);
            }
            bag.Insert(target, introduction);
        }

        private List<int> CreateValidShuffledBag(
            Random random,
            HerbPair previousPair,
            int previousRunLength,
            IReadOnlyList<int> candidates)
        {
            const int AttemptLimit = 1000;
            List<int> bag = null;
            for (int attempt = 0; attempt < AttemptLimit; attempt++)
            {
                bag = new List<int>(candidates);
                for (int index = bag.Count - 1; index > 0; index--)
                {
                    int swapIndex = random.Next(index + 1);
                    (bag[index], bag[swapIndex]) = (bag[swapIndex], bag[index]);
                }

                if (HasMaximumPairRun(bag, previousPair, previousRunLength, 2)) return bag;
            }

            return bag;
        }

        private bool HasMaximumPairRun(
            IReadOnlyList<int> bag,
            HerbPair previousPair,
            int previousRunLength,
            int maximumRun)
        {
            HerbPair pair = previousPair;
            int runLength = previousRunLength;
            foreach (int index in bag)
            {
                HerbPair next = templates[index].Order.RequestedPair;
                if (next.Equals(pair)) runLength++;
                else
                {
                    pair = next;
                    runLength = 1;
                }

                if (runLength > maximumRun) return false;
            }
            return true;
        }
    }
}
