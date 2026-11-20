using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Witch.Domain.Orders;
using Witch.Domain.Potions;

namespace Witch.Presentation.Shop
{
    public sealed class ShopEncounterDeck
    {
        private const string DayOneWarmOrderId = "order.tutorial.w01";
        private const string DefaultCoolingCategoryId = "category.cool";

        private readonly IReadOnlyList<ShopEncounterViewData> templates;
        private readonly int seed;
        private readonly bool randomize;
        private readonly IReadOnlyList<string> seenOrderIds;
        private readonly IReadOnlyList<int> dayDifficultyRanks;
        private readonly bool forceDayOneTutorial;
        private readonly int dayNumber;
        private readonly string dayOneCoolingCategoryId;
        private readonly Action<string> logWarning;
        private readonly Dictionary<string, ShopEncounterViewData> templatesById;
        private readonly Dictionary<string, string> pairByOrderId;
        private readonly Dictionary<string, string> categoryByOrderId;
        private readonly List<string> relaxations = new List<string>();
        private OrderSelectionState state;

        public ShopEncounterDeck(
            IReadOnlyList<ShopEncounterViewData> encounterTemplates,
            int encounterSeed,
            bool randomizeEncounters,
            IReadOnlyList<string> seenOrderIds = null,
            string priorityIntroductionOrderId = null,
            bool removeIntroductionAfterFirstBag = false,
            IReadOnlyList<string> priorityCategoryOrderIds = null,
            OrderSelectionState selection = null,
            IReadOnlyList<int> dayDifficultyRanks = null,
            bool forceDayOneTutorial = false,
            int dayNumber = 1,
            string dayOneCoolingCategoryId = DefaultCoolingCategoryId,
            Action<string> logWarning = null)
        {
            templates = encounterTemplates ?? throw new ArgumentNullException(nameof(encounterTemplates));
            if (templates.Count == 0) throw new ArgumentException("At least one encounter template is required.", nameof(encounterTemplates));

            seed = encounterSeed;
            randomize = randomizeEncounters;
            this.seenOrderIds = Copy(seenOrderIds);
            this.dayDifficultyRanks = dayDifficultyRanks ?? new int[0];
            this.forceDayOneTutorial = forceDayOneTutorial;
            this.dayNumber = dayNumber;
            this.dayOneCoolingCategoryId = dayOneCoolingCategoryId;
            this.logWarning = logWarning ?? (message => Debug.LogWarning(message));

            state = Clone(selection ?? new OrderSelectionState());
            if (state.ScheduledIntroOrderIds.Count == 0
                && string.IsNullOrWhiteSpace(state.PendingIntroOrderId)
                && !string.IsNullOrWhiteSpace(priorityIntroductionOrderId))
            {
                state = state.WithSchedule(new[] { priorityIntroductionOrderId }, null, 2);
            }

            templatesById = new Dictionary<string, ShopEncounterViewData>(StringComparer.Ordinal);
            pairByOrderId = new Dictionary<string, string>(StringComparer.Ordinal);
            categoryByOrderId = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (ShopEncounterViewData template in templates.OrderBy(e => e.Order.OrderId, StringComparer.Ordinal))
            {
                string id = template.Order.OrderId;
                templatesById[id] = template;
                pairByOrderId[id] = OrderPairKey.From(template.Order.RequestedPair);
                categoryByOrderId[id] = template.CategoryId;
            }
        }

        public bool LastPairCycleCleared { get; private set; }

        public IReadOnlyList<string> LastRelaxations => relaxations;

        public int DrawCount { get; private set; }

        public OrderSelectionState CurrentState => Clone(state);

        public ShopEncounterViewData Draw(int admissionIndex)
        {
            return Draw(admissionIndex, state);
        }

        public ShopEncounterViewData Draw(int admissionIndex, OrderSelectionState currentState)
        {
            if (admissionIndex < 0) throw new ArgumentOutOfRangeException(nameof(admissionIndex));
            LastPairCycleCleared = false;
            relaxations.Clear();
            DrawCount++;
            state = Clone(currentState ?? new OrderSelectionState());

            ShopEncounterViewData forced = FindForcedEncounter(admissionIndex);
            if (forced != null)
            {
                string forcedCategory = RepeatedCategory(admissionIndex);
                if (forcedCategory != null
                    && string.Equals(forcedCategory, forced.CategoryId, StringComparison.Ordinal))
                {
                    Warn("rule6: three-in-a-row category ban relaxed for forced order "
                        + forced.Order.OrderId + ".");
                }
                NotePairCycleForForcedOrder(forced);
                state = state.WithAdmittedOrder(forced.Order.OrderId, forced.Order.RequestedPair);
                return CopyOf(forced);
            }

            List<ShopEncounterViewData> candidates = BuildCandidates(admissionIndex);
            if (candidates.Count == 0)
            {
                Warn("rule1: eligible orders exhausted on day " + dayNumber + " at draw " + admissionIndex + ". Reusing eligible orders.");
                candidates = templates.ToList();
            }
            if (candidates.Count == 0)
            {
                Warn("pool: no encounter template remained on day " + dayNumber + " at draw " + admissionIndex + ".");
                throw new InvalidOperationException("No order candidate is available.");
            }

            int chosen = randomize
                ? new System.Random(unchecked(seed ^ (admissionIndex * 7919) ^ 104729)).Next(candidates.Count)
                : admissionIndex % candidates.Count;
            ShopEncounterViewData result = candidates[chosen];
            state = state.WithAdmittedOrder(result.Order.OrderId, result.Order.RequestedPair);
            return CopyOf(result);
        }

        private ShopEncounterViewData FindForcedEncounter(int admissionIndex)
        {
            bool firstTimeDayOne = forceDayOneTutorial && dayNumber == 1;
            if (firstTimeDayOne && admissionIndex == 0)
            {
                if (templatesById.TryGetValue(DayOneWarmOrderId, out ShopEncounterViewData warm)) return warm;
                Warn("day1: forced tutorial order " + DayOneWarmOrderId + " is missing from the encounter pool.");
            }

            if (firstTimeDayOne && admissionIndex == 1)
            {
                List<ShopEncounterViewData> cooling = FindDayOneCoolingCandidates();
                if (cooling.Count > 0)
                {
                    int pick = new System.Random(unchecked(seed ^ 40503)).Next(cooling.Count);
                    return cooling[pick];
                }
                Warn("day1: no Cooling Tutorial candidate was available for the second customer.");
            }

            if (admissionIndex == 0 && !string.IsNullOrWhiteSpace(state.PendingIntroOrderId)
                && !WasGiven(state.PendingIntroOrderId))
            {
                if (templatesById.TryGetValue(state.PendingIntroOrderId, out ShopEncounterViewData pending)) return pending;
                Warn("intro: pending order " + state.PendingIntroOrderId + " is missing from the encounter pool.");
            }

            foreach (string introId in state.ScheduledIntroOrderIds)
            {
                if (WasGiven(introId)) continue;
                int slot = Math.Max(1, Math.Min(3, state.IntroSlot <= 0 ? 2 : state.IntroSlot));
                if (admissionIndex == slot - 1 && templatesById.TryGetValue(introId, out ShopEncounterViewData intro)) return intro;
            }
            return null;
        }

        private List<ShopEncounterViewData> FindDayOneCoolingCandidates()
        {
            string warmPair = OrderPairKey.From(new HerbPair("herb.ginger", "herb.mustardSeed"));
            var seen = new HashSet<string>(seenOrderIds, StringComparer.Ordinal);
            seen.UnionWith(state.UsedOrderIds);
            var givenPairs = new HashSet<string>(state.GivenPairKeys, StringComparer.Ordinal);
            List<ShopEncounterViewData> cooling = templates.Where(e =>
                    string.Equals(e.DifficultyId, "Tutorial", StringComparison.Ordinal)
                    && string.Equals(e.CategoryId, dayOneCoolingCategoryId, StringComparison.Ordinal)
                    && !string.Equals(pairByOrderId[e.Order.OrderId], warmPair, StringComparison.Ordinal)
                    && !seen.Contains(e.Order.OrderId)
                    && !givenPairs.Contains(pairByOrderId[e.Order.OrderId]))
                .ToList();
            if (cooling.Count == 0)
            {
                Warn("rule8: Cooling Tutorial pair cycle cleared for day 1 second customer.");
                state = state.WithPairCycleCleared();
                givenPairs.Clear();
                LastPairCycleCleared = true;
                cooling = templates.Where(e =>
                        string.Equals(e.DifficultyId, "Tutorial", StringComparison.Ordinal)
                        && string.Equals(e.CategoryId, dayOneCoolingCategoryId, StringComparison.Ordinal)
                        && !string.Equals(pairByOrderId[e.Order.OrderId], warmPair, StringComparison.Ordinal)
                        && !seen.Contains(e.Order.OrderId))
                    .ToList();
            }
            return cooling;
        }

        private void NotePairCycleForForcedOrder(ShopEncounterViewData forced)
        {
            string pair = pairByOrderId[forced.Order.OrderId];
            if (!state.GivenPairKeys.Contains(pair, StringComparer.Ordinal)) return;
            if (state.GivenPairKeys.Count < DistinctPairCount()) return;
            Warn("rule8: pair cycle cleared before forced order " + forced.Order.OrderId + ".");
            state = state.WithPairCycleCleared();
            LastPairCycleCleared = true;
        }

        private List<ShopEncounterViewData> BuildCandidates(int admissionIndex)
        {
            var seen = new HashSet<string>(seenOrderIds, StringComparer.Ordinal);
            seen.UnionWith(state.UsedOrderIds);
            List<ShopEncounterViewData> pool = templates.Where(e => !seen.Contains(e.Order.OrderId)).ToList();
            if (pool.Count == 0)
            {
                Warn("rule1: seen orders cover the eligible pool on day " + dayNumber + ". Reusing eligible orders.");
                pool = new List<ShopEncounterViewData>(templates);
            }

            var withoutReservedIntroOrders = pool
                .Where(e => !IntroIsReservedForLater(e.Order.OrderId, admissionIndex))
                .ToList();
            if (withoutReservedIntroOrders.Count > 0 || pool.Count == 0) pool = withoutReservedIntroOrders;
            else Warn("rule4: introduction order reservation relaxed on day " + dayNumber + " at draw " + admissionIndex + ".");

            var givenPairs = new HashSet<string>(state.GivenPairKeys, StringComparer.Ordinal);
            if (DistinctPairCount() > 0 && DistinctPairKeys().All(givenPairs.Contains))
            {
                Warn("rule8: pair cycle cleared on day " + dayNumber + " at draw " + admissionIndex + ".");
                state = state.WithPairCycleCleared();
                givenPairs.Clear();
                LastPairCycleCleared = true;
            }
            pool = pool.Where(e => !givenPairs.Contains(pairByOrderId[e.Order.OrderId])).ToList();

            var reservedPairs = new HashSet<string>(StringComparer.Ordinal);
            foreach (string introId in state.ScheduledIntroOrderIds.Concat(
                         string.IsNullOrWhiteSpace(state.PendingIntroOrderId)
                             ? Enumerable.Empty<string>()
                             : new[] { state.PendingIntroOrderId }))
            {
                if (WasGiven(introId) || !pairByOrderId.TryGetValue(introId, out string pair)) continue;
                reservedPairs.Add(pair);
            }
            var reservedPool = pool.Where(e => !reservedPairs.Contains(pairByOrderId[e.Order.OrderId])).ToList();
            if (reservedPool.Count > 0 || pool.Count == 0) pool = reservedPool;
            else Warn("rule3: intro pair reservation relaxed on day " + dayNumber + " at draw " + admissionIndex + ".");

            string previousPair = PreviousPair();
            if (previousPair != null)
            {
                var afterPair = pool.Where(e => !string.Equals(pairByOrderId[e.Order.OrderId], previousPair, StringComparison.Ordinal)).ToList();
                if (afterPair.Count > 0) pool = afterPair;
                else Warn("rule5: back-to-back pair ban relaxed on day " + dayNumber + " at draw " + admissionIndex + ".");
            }

            string repeatedCategory = RepeatedCategory(admissionIndex);
            if (repeatedCategory != null)
            {
                var afterCategory = pool.Where(e => !string.Equals(e.CategoryId, repeatedCategory, StringComparison.Ordinal)).ToList();
                if (afterCategory.Count > 0) pool = afterCategory;
                else Warn("rule6: three-in-a-row category ban relaxed on day " + dayNumber + " at draw " + admissionIndex + ".");
            }

            if (admissionIndex == FirstNonIntroDrawIndex() && dayDifficultyRanks.Count > 0)
            {
                int easiest = dayDifficultyRanks.Min();
                var afterTier = pool.Where(e => e.DifficultyRank == easiest).ToList();
                if (afterTier.Count > 0) pool = afterTier;
                else Warn("rule7: easier first tier relaxed on day " + dayNumber + " at draw " + admissionIndex + ".");
            }

            return pool.OrderBy(e => e.Order.OrderId, StringComparer.Ordinal).ToList();
        }

        private int FirstNonIntroDrawIndex()
        {
            if (state.UsedOrderIds.Count == 1 && state.IntroSlot == 1) return 1;
            if (!string.IsNullOrWhiteSpace(state.PendingIntroOrderId) && !WasGiven(state.PendingIntroOrderId)) return 1;
            if (state.ScheduledIntroOrderIds.Any(id => ! WasGiven(id)))
            {
                int slot = Math.Max(1, Math.Min(3, state.IntroSlot <= 0 ? 2 : state.IntroSlot));
                return slot == 1 ? 1 : 0;
            }
            return 0;
        }

        private bool WasGiven(string orderId)
        {
            return state.UsedOrderIds.Any(id => string.Equals(id, orderId, StringComparison.Ordinal));
        }

        private bool IntroIsReservedForLater(string orderId, int admissionIndex)
        {
            if (WasGiven(orderId)) return false;
            if (string.Equals(orderId, state.PendingIntroOrderId, StringComparison.Ordinal))
            {
                return admissionIndex != 0;
            }
            if (!state.ScheduledIntroOrderIds.Contains(orderId, StringComparer.Ordinal)) return false;
            int slot = Math.Max(1, Math.Min(3, state.IntroSlot <= 0 ? 2 : state.IntroSlot));
            return admissionIndex != slot - 1;
        }

        private string PreviousPair()
        {
            if (state.UsedOrderIds.Count > 0)
            {
                string last = state.UsedOrderIds[state.UsedOrderIds.Count - 1];
                return pairByOrderId.TryGetValue(last, out string pair) ? pair : null;
            }
            return state.LastPairKey;
        }

        private string RepeatedCategory(int admissionIndex)
        {
            if (state.UsedOrderIds.Count < 1) return null;
            string last = state.UsedOrderIds[state.UsedOrderIds.Count - 1];
            if (!categoryByOrderId.TryGetValue(last, out string lastCategory)) return null;
            if (state.UsedOrderIds.Count >= 2)
            {
                string previous = state.UsedOrderIds[state.UsedOrderIds.Count - 2];
                if (categoryByOrderId.TryGetValue(previous, out string previousCategory)
                    && string.Equals(lastCategory, previousCategory, StringComparison.Ordinal))
                {
                    return lastCategory;
                }
            }

            foreach (string introId in state.ScheduledIntroOrderIds)
            {
                if (WasGiven(introId)) continue;
                int slot = Math.Max(1, Math.Min(3, state.IntroSlot <= 0 ? 2 : state.IntroSlot));
                if (slot - 1 != admissionIndex + 1) continue;
                if (categoryByOrderId.TryGetValue(introId, out string introCategory)
                    && string.Equals(lastCategory, introCategory, StringComparison.Ordinal))
                {
                    return introCategory;
                }
            }
            return null;
        }

        private int DistinctPairCount()
        {
            return DistinctPairKeys().Count;
        }

        private HashSet<string> DistinctPairKeys()
        {
            return new HashSet<string>(templates.Select(e => pairByOrderId[e.Order.OrderId]), StringComparer.Ordinal);
        }

        private void Warn(string message)
        {
            relaxations.Add(message);
            logWarning(message);
        }

        private static ShopEncounterViewData CopyOf(ShopEncounterViewData source)
        {
            return new ShopEncounterViewData(
                source.CustomerId,
                source.CustomerName,
                source.CustomerIllustration,
                source.Order,
                source.OrderBubbles,
                source.DifficultyId,
                source.DifficultyRank,
                source.CategoryId);
        }

        private static OrderSelectionState Clone(OrderSelectionState source)
        {
            return new OrderSelectionState(
                source.UsedOrderIds,
                source.GivenPairKeys,
                source.ScheduledIntroOrderIds,
                source.PendingIntroOrderId,
                source.IntroSlot,
                source.LastPairKey);
        }

        private static string[] Copy(IReadOnlyList<string> source)
        {
            if (source == null) return new string[0];
            var copy = new string[source.Count];
            for (int index = 0; index < source.Count; index++) copy[index] = source[index];
            return copy;
        }
    }
}
