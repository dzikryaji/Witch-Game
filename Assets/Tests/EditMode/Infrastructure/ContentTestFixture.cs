using UnityEngine;
using Witch.Infrastructure.Content;

namespace Witch.Infrastructure.Tests
{
    internal static class ContentTestFixture
    {
        public static GameContentCatalogDefinition CreateValidCatalog()
        {
            HerbDefinition ginger = DefinitionFactory.Herb("herb.ginger", "Ginger", "category.warm");
            HerbDefinition mint = DefinitionFactory.Herb("herb.mint", "Mint", "category.warm");
            OrderDefinition direct = DefinitionFactory.Order(
                "order.direct",
                "Bring ginger and mint.",
                ginger.StableId,
                mint.StableId);
            OrderDefinition visual = DefinitionFactory.Order(
                "order.visual",
                "Bring the knotted root and cool leaves.",
                ginger.StableId,
                mint.StableId);
            CustomerDefinition customer = DefinitionFactory.Customer(
                "customer.nervous-villager",
                "Nervous Villager");
            DayDefinition day = DefinitionFactory.Day(
                1,
                240,
                new[] { "category.warm" });
            HerbCategoryDefinition category = DefinitionFactory.Category(
                "category.warm",
                "Warming",
                "Herbs that bring heat back to the body.");
            RewindDifficultyDefinition difficulty = DefinitionFactory.Difficulty("rewind.difficulty.default", 3, 3);
            WitchHunterDialogueDefinition dialogue = DefinitionFactory.Dialogue(
                new[]
                {
                    new[] { "I am a Witch Hunter, sent to investigate this village." },
                    new[] { "I am a Witch Hunter, and I have returned." }
                },
                new[]
                {
                    "Ah, never mind. I must have been mistaken.",
                    "Strange... what was I questioning you about?"
                });
            var catalog = ScriptableObject.CreateInstance<GameContentCatalogDefinition>();
            catalog.Configure(
                new[] { ginger, mint },
                new[] { direct, visual },
                new[] { customer },
                new[] { day },
                new[] { category },
                new[]
                {
                    DefinitionFactory.Card("rewind.card.moon", "moon"),
                    DefinitionFactory.Card("rewind.card.star", "star"),
                    DefinitionFactory.Card("rewind.card.eye", "eye")
                },
                difficulty,
                witchHunterDialogueDefinition: dialogue);
            return catalog;
        }
    }

    internal static class DefinitionFactory
    {
        public static HerbDefinition Herb(string id, string name, string category)
        {
            var value = ScriptableObject.CreateInstance<HerbDefinition>();
            value.Configure(id, name, category);
            value.ConfigureGuidebook(
                name + " scientific name",
                name + " treatment text",
                name + " warning text",
                name,
                1);
            return value;
        }

        public static OrderDefinition Order(
            string id,
            string text,
            string first,
            string second,
            CustomerDefinition customer = null)
        {
            var value = ScriptableObject.CreateInstance<OrderDefinition>();
            value.Configure(id, new[] { text }, first, second, customerDefinition: customer);
            return value;
        }

        public static CustomerDefinition Customer(string id, string name)
        {
            var value = ScriptableObject.CreateInstance<CustomerDefinition>();
            value.Configure(id, name);
            return value;
        }

        public static DayDefinition Day(int day, int seconds, string[] categoryUnlocks = null)
        {
            var value = ScriptableObject.CreateInstance<DayDefinition>();
            value.Configure("day." + day, day, seconds, 20, categoryUnlocks);
            return value;
        }

        public static HerbCategoryDefinition Category(
            string id,
            string displayName,
            string description)
        {
            var value = ScriptableObject.CreateInstance<HerbCategoryDefinition>();
            value.Configure(id, displayName, description);
            return value;
        }

        public static RewindCardDefinition Card(string id, string symbol)
        {
            var value = ScriptableObject.CreateInstance<RewindCardDefinition>();
            value.Configure(id, symbol);
            return value;
        }

        public static RewindDifficultyDefinition Difficulty(string id, int start, int maximum)
        {
            var value = ScriptableObject.CreateInstance<RewindDifficultyDefinition>();
            value.Configure(id, start, maximum);
            return value;
        }

        public static WitchHunterDialogueDefinition Dialogue(string[][] accusations, string[] successLines)
        {
            var stages = new WitchHunterDialogueDefinition.Stage[successLines.Length];
            for (int index = 0; index < successLines.Length; index++)
            {
                stages[index] = new WitchHunterDialogueDefinition.Stage();
                stages[index].Configure(
                    index < accusations.Length ? accusations[index] : null,
                    successLines[index]);
            }

            var value = ScriptableObject.CreateInstance<WitchHunterDialogueDefinition>();
            value.Configure(stages);
            return value;
        }

    }
}
