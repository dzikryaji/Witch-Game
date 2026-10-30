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
            RecipeDefinition recipe = DefinitionFactory.Recipe(
                "recipe.tonic",
                ginger.StableId,
                mint.StableId);
            OrderDefinition direct = DefinitionFactory.Order(
                "order.direct",
                "Bring ginger and mint.",
                new[] { "clue.ginger", "clue.mint" },
                ginger.StableId,
                mint.StableId);
            OrderDefinition visual = DefinitionFactory.Order(
                "order.visual",
                "Bring the knotted root and cool leaves.",
                new[] { "clue.ginger", "clue.mint" },
                ginger.StableId,
                mint.StableId);
            CustomerDefinition customer = DefinitionFactory.Customer(
                "customer.villager",
                "Nervous Villager");
            DayDefinition day = DefinitionFactory.Day(
                1,
                240,
                new[] { direct.StableId, visual.StableId },
                new[] { customer.StableId });
            EconomyDefinition economy = DefinitionFactory.Economy(10, 20, 3);
            GuidebookEntryDefinition guidebook = DefinitionFactory.Guidebook(
                "guidebook.warm",
                "Warm Herbs",
                "category.warm",
                new[] { "clue.ginger", "clue.mint" });
            CategoryUnlockDefinition unlock = DefinitionFactory.Unlock(
                "unlock.warm",
                "category.warm",
                1,
                new[] { ginger.StableId, mint.StableId },
                new[] { recipe.StableId });
            RewindDifficultyDefinition difficulty = DefinitionFactory.Difficulty("rewind.difficulty.default", 3, 3);
            RunConfigurationDefinition run = DefinitionFactory.RunConfiguration(0, 1, 1);
            var catalog = ScriptableObject.CreateInstance<GameContentCatalogDefinition>();
            catalog.Configure(
                new[] { ginger, mint },
                new[] { recipe },
                new[] { direct, visual },
                new[] { customer },
                new[] { day },
                economy,
                new[] { guidebook },
                new[] { unlock },
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

        public static RecipeDefinition Recipe(string id, string first, string second)
        {
            var value = ScriptableObject.CreateInstance<RecipeDefinition>();
            value.Configure(id, first, second);
            return value;
        }

        public static OrderDefinition Order(string id, string text, string[] clues, string first, string second)
        {
            var value = ScriptableObject.CreateInstance<OrderDefinition>();
            value.Configure(id, text, clues, first, second);
            return value;
        }

        public static CustomerDefinition Customer(string id, string name)
        {
            var value = ScriptableObject.CreateInstance<CustomerDefinition>();
            value.Configure(id, name);
            return value;
        }

        public static DayDefinition Day(int day, int seconds, string[] orders, string[] customers)
        {
            var value = ScriptableObject.CreateInstance<DayDefinition>();
            value.Configure("day." + day, day, seconds, orders, customers);
            return value;
        }

        public static EconomyDefinition Economy(int payment, int expense, int completionDay)
        {
            var value = ScriptableObject.CreateInstance<EconomyDefinition>();
            value.Configure("economy.default", payment, expense, completionDay, "expense.day.");
            return value;
        }

        public static GuidebookEntryDefinition Guidebook(
            string id,
            string title,
            string category,
            string[] clues)
        {
            var value = ScriptableObject.CreateInstance<GuidebookEntryDefinition>();
            value.Configure(id, title, category, clues);
            return value;
        }

        public static CategoryUnlockDefinition Unlock(
            string id,
            string category,
            int day,
            string[] herbs,
            string[] recipes)
        {
            var value = ScriptableObject.CreateInstance<CategoryUnlockDefinition>();
            value.Configure(id, category, day, herbs, recipes);
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
