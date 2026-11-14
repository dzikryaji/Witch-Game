using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using Witch.Infrastructure.Content;

namespace Witch.Infrastructure.Tests
{
    public sealed class GameContentAdapterTests
    {
        [Test]
        public void Load_AuthoredFourteenDayContentIsValid()
        {
            GameContentCatalogDefinition catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalogDefinition>(
                "Assets/Content/GameContentCatalog.asset");

            GameContentLoadResult result = new GameContentAdapter().Load(catalog);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(result.Succeeded, Is.True, string.Join("\n", result.Errors));
            Assert.That(result.Content.OrdersById.Count, Is.EqualTo(250));
            Assert.That(result.Content.OrdersById.Values.Select(order => order.Difficulty).Distinct(),
                Is.EquivalentTo(new[] { OrderDifficulty.Simple, OrderDifficulty.Mid, OrderDifficulty.Hard }));
            Assert.That(result.Content.OrdersById["order.simple.w01"].OrderBubbles, Is.Not.Empty);
            Assert.That(result.Content.DaysByNumber.Count, Is.EqualTo(14));
            Assert.That(result.Content.DaysByNumber.Values.All(day => day.TimerDuration == TimeSpan.FromMinutes(5)), Is.True);
            Assert.That(result.Content.DaysByNumber.Values.All(day => day.DailyExpense == 20), Is.True);
            Assert.That(result.Content.HerbsById.Count, Is.EqualTo(25));
            Assert.That(result.Content.HerbsById.ContainsKey("herb.lavender"), Is.True);
            Assert.That(result.Content.HerbsById["herb.ginger"].ScientificName, Is.EqualTo("Zingiber officinale"));
            Assert.That(result.Content.HerbsById["herb.ginger"].WarningText, Does.Contain("blood clotting"));
            Assert.That(result.Content.HerbsById["herb.ginger"].GuidebookPage, Is.EqualTo(3));
            Assert.That(result.Content.HerbsById["herb.peppermint"].DisplayName, Is.EqualTo("Peppermint"));
            Assert.That(result.Content.HerbsById["herb.borage"].DisplayName, Is.EqualTo("Borage"));
            Assert.That(result.Content.HerbsById["herb.borage"].HerbIllustration, Is.Not.Null);
            Assert.That(result.Content.HerbCategoriesById.Count, Is.EqualTo(5));
            Assert.That(result.Content.HerbCategoriesById["category.warm"].DisplayName, Is.EqualTo("Warming"));
            Assert.That(result.Content.HerbCategoriesById["category.warm"].Description,
                Does.Contain("heat back to the body"));
            Assert.That(result.Content.HerbCategoriesById["category.soothe"].DisplayName, Is.EqualTo("Soothing"));
            Assert.That(result.Content.NewRunSetup.UnlockedHerbIds.Count, Is.EqualTo(10));
            Assert.That(result.Content.DaysByNumber[3].UnlockedCategoryIds, Does.Contain("category.soothe"));
            Assert.That(result.Content.DaysByNumber[7].UnlockedCategoryIds, Does.Contain("category.cleanse"));
            var dayThreeState = result.Content.DayProgressionSetup.ApplyUnlocks(
                new Witch.Domain.Progression.RunState("content-test", 3, 0, 0, 0));
            Assert.That(dayThreeState.UnlockedCategoryIds, Does.Contain("category.soothe"));
            Assert.That(dayThreeState.UnlockedHerbIds, Does.Contain("herb.lavender"));
            int initiallyAvailableOrders = result.Content.OrdersById.Values.Count(order =>
                order.IsAvailable(result.Content.NewRunSetup.UnlockedHerbIds.ToHashSet(StringComparer.Ordinal)));
            Assert.That(initiallyAvailableOrders, Is.EqualTo(110));
            Assert.That((initiallyAvailableOrders - 1) * result.Content.CorrectOrderPayment,
                Is.GreaterThanOrEqualTo(result.Content.NewRunSetup.FirstDayExpenseValue));
            Assert.That(result.Content.RewindCardIds.Count, Is.EqualTo(12));
            Assert.That(result.Content.RewindDifficulty.MaximumPairCount, Is.EqualTo(8));
            Assert.That(result.Content.WitchHunterDialogue.StageCount, Is.EqualTo(5));
            Assert.That(result.Content.WitchHunterDialogue.GetAccusations(0)[0],
                Is.EqualTo("I am a Witch Hunter, sent to investigate this village."));
            Assert.That(result.Content.WitchHunterDialogue.GetAccusations(4).Count, Is.EqualTo(4));
            Assert.That(result.Content.WitchHunterDialogue.GetSuccessLine(4),
                Is.EqualTo("What was I doing here? I have matters elsewhere!"));
        }

        [Test]
        public void OrderAvailability_FollowsUnlockedHerbs()
        {
            GameContentCatalogDefinition catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalogDefinition>(
                "Assets/Content/GameContentCatalog.asset");
            GameRuntimeContent content = new GameContentAdapter().Load(catalog).Content;
            var dayOneHerbs = content.NewRunSetup.UnlockedHerbIds.ToHashSet(StringComparer.Ordinal);
            var dayThreeHerbs = content.DayProgressionSetup.ApplyUnlocks(
                    new Witch.Domain.Progression.RunState("content-test", 3, 0, 0, 0))
                .UnlockedHerbIds.ToHashSet(StringComparer.Ordinal);

            Assert.That(content.OrdersById["order.simple.w01"].IsAvailable(dayOneHerbs), Is.True);
            Assert.That(content.OrdersById["order.simple.s01"].IsAvailable(dayOneHerbs), Is.False);
            Assert.That(content.OrdersById["order.simple.s01"].IsAvailable(dayThreeHerbs), Is.True);
        }

        [Test]
        public void OrderDifficulty_ComesFromDayDefinition()
        {
            GameContentCatalogDefinition catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalogDefinition>(
                "Assets/Content/GameContentCatalog.asset");
            GameRuntimeContent content = new GameContentAdapter().Load(catalog).Content;

            Assert.That(content.DaysByNumber[1].OrderDifficulties, Is.EqualTo(new[] { OrderDifficulty.Simple }));
            Assert.That(content.DaysByNumber[2].OrderDifficulties, Is.EqualTo(new[] { OrderDifficulty.Simple }));
            Assert.That(content.DaysByNumber[3].OrderDifficulties, Is.EqualTo(new[] { OrderDifficulty.Simple }));
            Assert.That(content.DaysByNumber[4].OrderDifficulties,
                Is.EqualTo(new[] { OrderDifficulty.Simple, OrderDifficulty.Mid }));
            Assert.That(content.DaysByNumber[5].OrderDifficulties,
                Is.EqualTo(new[] { OrderDifficulty.Simple, OrderDifficulty.Mid }));
            Assert.That(content.DaysByNumber[6].OrderDifficulties, Is.EqualTo(new[] { OrderDifficulty.Mid }));
            Assert.That(content.DaysByNumber[7].UnlockedCategoryIds, Does.Contain("category.cleanse"));
            Assert.That(content.DaysByNumber[8].OrderDifficulties, Is.EqualTo(new[] { OrderDifficulty.Mid }));
            Assert.That(content.DaysByNumber[9].OrderDifficulties,
                Is.EqualTo(new[] { OrderDifficulty.Mid, OrderDifficulty.Hard }));
            Assert.That(content.DaysByNumber[11].UnlockedCategoryIds, Does.Contain("category.mend"));
            Assert.That(content.DaysByNumber[12].OrderDifficulties, Is.EqualTo(new[] { OrderDifficulty.Hard }));
            Assert.That(content.DaysByNumber[14].OrderDifficulties, Is.EqualTo(new[] { OrderDifficulty.Hard }));
        }

        [Test]
        public void Load_ValidFixtureMapsAllRuntimeInputs()
        {
            GameContentCatalogDefinition catalog = ContentTestFixture.CreateValidCatalog();

            GameContentLoadResult result = new GameContentAdapter().Load(catalog);

            Assert.That(result.Succeeded, Is.True, string.Join("\n", result.Errors));
            Assert.That(result.Content.OrdersById.Count, Is.EqualTo(2));
            Assert.That(result.Content.NewRunSetup.StartingMoney, Is.Zero);
            Assert.That(result.Content.NewRunSetup.FirstDayTimerDuration, Is.EqualTo(TimeSpan.FromMinutes(4)));
            Assert.That(result.Content.NewRunSetup.FirstDayExpenseValue, Is.EqualTo(20));
            Assert.That(result.Content.ExpenseRules.CompletionDay, Is.EqualTo(1));
            Assert.That(result.Content.RewindCardIds.Count, Is.EqualTo(3));
            Assert.That(result.Content.OrdersById.Values.All(order =>
                order.Customer.StableId == "customer.nervous-villager"), Is.True);
        }

        [Test]
        public void Validate_MissingAndDuplicateStableIdsReturnsClearErrors()
        {
            GameContentCatalogDefinition catalog = ContentTestFixture.CreateValidCatalog();
            catalog.Herbs[0].Configure("", "Ginger", "category.warm");
            catalog.Herbs[1].Configure("", "Mint", "category.warm");

            ContentValidationReport report = new GameContentValidator().Validate(catalog);

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Errors.Any(error => error.Contains("Herb stable ID is required")), Is.True);
            Assert.That(report.Errors.Any(error => error.Contains("Duplicate herb stable ID")), Is.True);
        }

        [Test]
        public void Validate_OrderWithoutSentencesReturnsClearError()
        {
            GameContentCatalogDefinition catalog = ContentTestFixture.CreateValidCatalog();
            catalog.Orders[0].Configure(
                "order.direct",
                Array.Empty<string>(),
                "herb.ginger",
                "herb.mint");

            ContentValidationReport report = new GameContentValidator().Validate(catalog);

            Assert.That(report.Errors.Any(error => error.Contains("needs at least one non-empty bubble")), Is.True);
        }

        [Test]
        public void Validate_MissingHerbGuidebookContentReturnsClearErrors()
        {
            GameContentCatalogDefinition catalog = ContentTestFixture.CreateValidCatalog();
            catalog.Herbs[0].ConfigureGuidebook("", "", "", "", 0);

            ContentValidationReport report = new GameContentValidator().Validate(catalog);

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Errors.Any(error => error.Contains("needs a scientific name")), Is.True);
            Assert.That(report.Errors.Any(error => error.Contains("needs treatment text")), Is.True);
            Assert.That(report.Errors.Any(error => error.Contains("needs warning text")), Is.True);
            Assert.That(report.Errors.Any(error => error.Contains("needs an accessible label")), Is.True);
            Assert.That(report.Errors.Any(error => error.Contains("needs a positive guidebook page")), Is.True);
        }

        [Test]
        public void Validate_MissingCategoryContentReturnsClearErrors()
        {
            GameContentCatalogDefinition catalog = ContentTestFixture.CreateValidCatalog();
            catalog.HerbCategories[0].Configure("category.warm", "", "");

            ContentValidationReport report = new GameContentValidator().Validate(catalog);

            Assert.That(report.Errors.Any(error => error.Contains("needs a display name")), Is.True);
            Assert.That(report.Errors.Any(error => error.Contains("needs a description")), Is.True);
        }

        [Test]
        public void Validate_MixedCategoryOrderReturnsError()
        {
            GameContentCatalogDefinition catalog = ContentTestFixture.CreateValidCatalog();
            catalog.Herbs[1].Configure("herb.mint", "Mint", "category.cool");

            ContentValidationReport report = new GameContentValidator().Validate(catalog);

            Assert.That(report.Errors.Any(error => error.Contains("must share one category")), Is.True);
        }

        [Test]
        public void Validate_LockedHerbReturnsError()
        {
            GameContentCatalogDefinition catalog = ContentTestFixture.CreateValidCatalog();
            catalog.Orders[0].Configure(
                "order.direct",
                new[] { "Bring ginger and sage." },
                "herb.ginger",
                "herb.sage");

            ContentValidationReport report = new GameContentValidator().Validate(catalog);

            Assert.That(report.Errors.Any(error => error.Contains("unknown ID 'herb.sage'")), Is.True);
        }

        [Test]
        public void Validate_DayWithTooFewAvailableOrdersReturnsError()
        {
            GameContentCatalogDefinition catalog = ContentTestFixture.CreateValidCatalog();
            catalog.Days[0].Configure(
                "day.1",
                1,
                240,
                20,
                new[] { "category.unknown" });

            ContentValidationReport report = new GameContentValidator().Validate(catalog);

            Assert.That(report.Errors.Any(error => error.Contains("alternate order seed")), Is.True);
        }

        [Test]
        public void Load_WithoutCustomerDefinitionsUsesNervousVillagerFallback()
        {
            GameContentCatalogDefinition catalog = ContentTestFixture.CreateValidCatalog();
            catalog.Configure(
                catalog.Herbs.ToArray(),
                catalog.Orders.ToArray(),
                Array.Empty<CustomerDefinition>(),
                catalog.Days.ToArray(),
                catalog.HerbCategories.ToArray(),
                catalog.RewindCards.ToArray(),
                catalog.RewindDifficulty,
                witchHunterDialogueDefinition: catalog.WitchHunterDialogue);

            GameContentLoadResult result = new GameContentAdapter().Load(catalog);

            Assert.That(result.Succeeded, Is.True, string.Join("\n", result.Errors));
            Assert.That(result.Content.OrdersById.Values.All(order =>
                order.Customer.StableId == "customer.nervous-villager"), Is.True);
        }

        [Test]
        public void Load_OrderCustomerOverridesFallback()
        {
            GameContentCatalogDefinition catalog = ContentTestFixture.CreateValidCatalog();
            CustomerDefinition customer = DefinitionFactory.Customer("customer.assigned", "Assigned Villager");
            catalog.Orders[0].Configure(
                "order.direct",
                new[] { "Bring ginger and mint." },
                "herb.ginger",
                "herb.mint",
                customerDefinition: customer);
            catalog.Configure(
                catalog.Herbs.ToArray(),
                catalog.Orders.ToArray(),
                catalog.Customers.Append(customer).ToArray(),
                catalog.Days.ToArray(),
                catalog.HerbCategories.ToArray(),
                catalog.RewindCards.ToArray(),
                catalog.RewindDifficulty,
                witchHunterDialogueDefinition: catalog.WitchHunterDialogue);

            GameContentLoadResult result = new GameContentAdapter().Load(catalog);

            Assert.That(result.Succeeded, Is.True, string.Join("\n", result.Errors));
            Assert.That(result.Content.OrdersById["order.direct"].Customer.DisplayName,
                Is.EqualTo("Assigned Villager"));
        }

        [Test]
        public void Validate_MissingWitchHunterDialogueReturnsError()
        {
            GameContentCatalogDefinition catalog = ContentTestFixture.CreateValidCatalog();
            AssignDialogue(catalog, null);

            ContentValidationReport report = new GameContentValidator().Validate(catalog);

            Assert.That(report.Errors.Any(error => error.Contains("Witch hunter dialogue definition is required")), Is.True);
        }

        [Test]
        public void Validate_WitchHunterDialogueWithoutStagesReturnsError()
        {
            GameContentCatalogDefinition catalog = ContentTestFixture.CreateValidCatalog();
            AssignDialogue(catalog, DefinitionFactory.Dialogue(
                Array.Empty<string[]>(),
                Array.Empty<string>()));

            ContentValidationReport report = new GameContentValidator().Validate(catalog);

            Assert.That(report.Errors.Any(error => error.Contains("needs at least one stage")), Is.True);
        }

        [Test]
        public void Validate_WitchHunterStageWithoutAccusationsReturnsError()
        {
            GameContentCatalogDefinition catalog = ContentTestFixture.CreateValidCatalog();
            AssignDialogue(catalog, DefinitionFactory.Dialogue(
                new[] { Array.Empty<string>() },
                new[] { "Ah, never mind." }));

            ContentValidationReport report = new GameContentValidator().Validate(catalog);

            Assert.That(report.Errors.Any(error => error.Contains("stage 1 needs at least one accusation")), Is.True);
        }

        [Test]
        public void Validate_WitchHunterBlankAccusationReturnsError()
        {
            GameContentCatalogDefinition catalog = ContentTestFixture.CreateValidCatalog();
            AssignDialogue(catalog, DefinitionFactory.Dialogue(
                new[]
                {
                    new[] { "A question." },
                    new[] { "Another question.", "   " }
                },
                new[] { "First success.", "Second success." }));

            ContentValidationReport report = new GameContentValidator().Validate(catalog);

            Assert.That(report.Errors.Any(error => error.Contains("stage 2 has a blank accusation")), Is.True);
        }

        [Test]
        public void Validate_WitchHunterBlankSuccessLineReturnsError()
        {
            GameContentCatalogDefinition catalog = ContentTestFixture.CreateValidCatalog();
            AssignDialogue(catalog, DefinitionFactory.Dialogue(
                new[] { new[] { "A question." } },
                new[] { " " }));

            ContentValidationReport report = new GameContentValidator().Validate(catalog);

            Assert.That(report.Errors.Any(error => error.Contains("stage 1 needs a success line")), Is.True);
        }

        [Test]
        public void Load_MapsWitchHunterDialogueStages()
        {
            GameContentCatalogDefinition catalog = ContentTestFixture.CreateValidCatalog();

            GameContentLoadResult result = new GameContentAdapter().Load(catalog);

            Assert.That(result.Succeeded, Is.True, string.Join("\n", result.Errors));
            Assert.That(result.Content.WitchHunterDialogue.StageCount, Is.EqualTo(2));
            Assert.That(result.Content.WitchHunterDialogue.GetAccusations(0),
                Is.EqualTo(new[] { "I am a Witch Hunter, sent to investigate this village." }));
            Assert.That(result.Content.WitchHunterDialogue.GetSuccessLine(1),
                Is.EqualTo("Strange... what was I questioning you about?"));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => result.Content.WitchHunterDialogue.GetAccusations(2));
        }

        private static void AssignDialogue(
            GameContentCatalogDefinition catalog,
            WitchHunterDialogueDefinition dialogue)
        {
            catalog.Configure(
                catalog.Herbs.ToArray(),
                catalog.Orders.ToArray(),
                catalog.Customers.ToArray(),
                catalog.Days.ToArray(),
                catalog.HerbCategories.ToArray(),
                catalog.RewindCards.ToArray(),
                catalog.RewindDifficulty,
                witchHunterDialogueDefinition: dialogue);
        }

        [Test]
        public void Validate_SmallCardPoolReturnsError()
        {
            GameContentCatalogDefinition catalog = ContentTestFixture.CreateValidCatalog();
            catalog.RewindDifficulty.Configure("rewind.difficulty.default", 3, 4);

            ContentValidationReport report = new GameContentValidator().Validate(catalog);

            Assert.That(report.Errors.Any(error => error.Contains("4 unique rewind cards")), Is.True);
        }
    }
}
