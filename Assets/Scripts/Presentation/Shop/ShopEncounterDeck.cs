using System;
using System.Collections.Generic;
using Witch.Domain.Orders;
using Witch.Domain.Potions;

namespace Witch.Presentation.Shop
{
    public sealed class ShopEncounterDeck
    {
        private readonly IReadOnlyList<ShopEncounterViewData> templates;
        private readonly int seed;
        private readonly bool randomize;

        public ShopEncounterDeck(
            IReadOnlyList<ShopEncounterViewData> encounterTemplates,
            int encounterSeed,
            bool randomizeEncounters)
        {
            templates = encounterTemplates ?? throw new ArgumentNullException(nameof(encounterTemplates));
            if (templates.Count == 0)
            {
                throw new ArgumentException("At least one encounter template is required.", nameof(encounterTemplates));
            }

            seed = encounterSeed;
            randomize = randomizeEncounters;
        }

        public ShopEncounterViewData Draw(int admissionIndex)
        {
            if (admissionIndex < 0) throw new ArgumentOutOfRangeException(nameof(admissionIndex));

            int templateIndex = randomize
                ? DrawRandomizedTemplateIndex(admissionIndex)
                : admissionIndex % templates.Count;
            ShopEncounterViewData template = templates[templateIndex];
            int cycle = admissionIndex / templates.Count;
            string orderId = cycle == 0
                ? template.Order.OrderId
                : template.Order.OrderId + ".repeat." + cycle;
            return new ShopEncounterViewData(
                template.CustomerId,
                template.CustomerName,
                new OrderRequest(orderId, template.Order.RequestedPair),
                template.ClueText);
        }

        private int DrawRandomizedTemplateIndex(int admissionIndex)
        {
            var random = new Random(seed);
            var sequence = new List<int>();
            HerbPair previousPair = null;
            int previousRunLength = 0;

            while (sequence.Count <= admissionIndex)
            {
                List<int> bag = CreateValidShuffledBag(random, previousPair, previousRunLength);
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
            }

            return sequence[admissionIndex];
        }

        private List<int> CreateValidShuffledBag(Random random, HerbPair previousPair, int previousRunLength)
        {
            const int AttemptLimit = 1000;
            for (int attempt = 0; attempt < AttemptLimit; attempt++)
            {
                var bag = new List<int>(templates.Count);
                for (int index = 0; index < templates.Count; index++) bag.Add(index);
                for (int index = bag.Count - 1; index > 0; index--)
                {
                    int swapIndex = random.Next(index + 1);
                    (bag[index], bag[swapIndex]) = (bag[swapIndex], bag[index]);
                }

                if (HasMaximumPairRun(bag, previousPair, previousRunLength, 2)) return bag;
            }

            throw new InvalidOperationException(
                "Encounter templates cannot be shuffled without exceeding two consecutive orders for one recipe.");
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
