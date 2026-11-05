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
                "customer.villager",
                "Nervous Villager");
            DayDefinition day = DefinitionFactory.Day(
                1,
                240,
                new[] { customer.StableId },
                new[] { "category.warm" });
            GuidebookEntryDefinition guidebook = DefinitionFactory.Guidebook(
                "guidebook.warm",
                "Warm Herbs",
                "category.warm");
            RewindDifficultyDefinition difficulty = DefinitionFactory.Difficulty("rewind.difficulty.default", 3, 3);
            RunConfigurationDefinition run = DefinitionFactory.RunConfiguration(0, 1, 1);
            var catalog = ScriptableObject.CreateInstance<GameContentCatalogDefinition>();
            catalog.Configure(
                new[] { ginger, mint },
                new[] { direct, visual },
                new[] { customer },
                new[] { day },
                new[] { guidebook },
                new[]
                {
                    DefinitionFactory.Card("rewind.card.moon", "moon"),
                    DefinitionFactory.Card("rewind.card.star", "star"),
                    DefinitionFactory.Card("rewind.card.eye", "eye")
                },
                difficulty,
                run);
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

        public static OrderDefinition Order(string id, string text, string first, string second)
        {
            var value = ScriptableObject.CreateInstance<OrderDefinition>();
            value.Configure(id, new[] { text }, first, second);
            return value;
        }

        public static CustomerDefinition Customer(string id, string name)
        {
            var value = ScriptableObject.CreateInstance<CustomerDefinition>();
            value.Configure(id, name);
            return value;
        }

        public static DayDefinition Day(int day, int seconds, string[] customers, string[] categoryUnlocks = null)
        {
            var value = ScriptableObject.CreateInstance<DayDefinition>();
            value.Configure("day." + day, day, seconds, customers, 20, categoryUnlocks);
            return value;
        }

        public static GuidebookEntryDefinition Guidebook(
            string id,
            string title,
            string category)
        {
            var value = ScriptableObject.CreateInstance<GuidebookEntryDefinition>();
            value.Configure(id, title, category);
            value.ConfigureContents(GuidebookContentsItemDefinition.Create(category, title, title));
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

        public static RunConfigurationDefinition RunConfiguration(int money, int saveVersion, int firstDay)
        {
            var value = ScriptableObject.CreateInstance<RunConfigurationDefinition>();
            value.Configure("run.default", money, saveVersion, firstDay, 20);
            return value;
        }
    }
}
