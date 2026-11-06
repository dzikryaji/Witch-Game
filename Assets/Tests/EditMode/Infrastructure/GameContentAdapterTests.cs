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
        public void Load_AuthoredEightDayContentIsValid()
        {
            GameContentCatalogDefinition catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalogDefinition>(
                "Assets/Content/GameContentCatalog.asset");

            GameContentLoadResult result = new GameContentAdapter().Load(catalog);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(result.Succeeded, Is.True, string.Join("\n", result.Errors));
            Assert.That(result.Content.OrdersById.Count, Is.EqualTo(150));
            Assert.That(result.Content.OrdersById.Values.Select(order => order.Difficulty).Distinct(),
                Is.EquivalentTo(new[] { OrderDifficulty.Simple, OrderDifficulty.Mid }));
            Assert.That(result.Content.OrdersById["order.simple.w01"].OrderBubbles, Is.Not.Empty);
            Assert.That(result.Content.DaysByNumber.Count, Is.EqualTo(8));
            Assert.That(result.Content.DaysByNumber.Values.All(day => day.TimerDuration == TimeSpan.FromMinutes(4)), Is.True);
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
            var dayThreeState = result.Content.DayProgressionSetup.ApplyUnlocks(
                new Witch.Domain.Progression.RunState("content-test", 3, 0, 0, 0));
            Assert.That(dayThreeState.UnlockedCategoryIds, Does.Contain("category.soothe"));
            Assert.That(dayThreeState.UnlockedHerbIds, Does.Contain("herb.lavender"));
            int initiallyAvailableOrders = result.Content.OrdersById.Values.Count(order =>
                order.IsAvailable(result.Content.NewRunSetup.UnlockedHerbIds.ToHashSet(StringComparer.Ordinal)));
            Assert.That(initiallyAvailableOrders, Is.EqualTo(70));
            Assert.That((initiallyAvailableOrders - 1) * result.Content.CorrectOrderPayment,
                Is.GreaterThanOrEqualTo(result.Content.NewRunSetup.FirstDayExpenseValue));
            Assert.That(result.Content.RewindCardIds.Count, Is.EqualTo(8));
            Assert.That(result.Content.RewindDifficulty.MaximumPairCount, Is.EqualTo(8));
        }

        [Test]
        public void OrderAvailability_FollowsUnlockedHerbs()
        {
            GameContentCatalogDefinition catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalogDefinition>(
                "Assets/Content/GameContentCatalog.asset");
            GameRuntimeContent content = new GameContentAdapter().Load(catalog).Content;
            var dayOneHerbs = content.NewRunSetup.UnlockedHerbIds.ToHashSet(StringComparer.Ordinal);
            var dayFourHerbs = content.DayProgressionSetup.ApplyUnlocks(
                    new Witch.Domain.Progression.RunState("content-test", 4, 0, 0, 0))
                .UnlockedHerbIds.ToHashSet(StringComparer.Ordinal);

            Assert.That(content.OrdersById["order.simple.w01"].IsAvailable(dayOneHerbs), Is.True);
            Assert.That(content.OrdersById["order.simple.s01"].IsAvailable(dayOneHerbs), Is.False);
            Assert.That(content.OrdersById["order.simple.s01"].IsAvailable(dayFourHerbs), Is.True);
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
                new[] { "customer.villager" },
                20,
                new[] { "category.unknown" });

            ContentValidationReport report = new GameContentValidator().Validate(catalog);

            Assert.That(report.Errors.Any(error => error.Contains("alternate order seed")), Is.True);
        }

        [Test]
        public void Validate_DayWithoutCustomersReturnsError()
        {
            GameContentCatalogDefinition catalog = ContentTestFixture.CreateValidCatalog();
            catalog.Days[0].Configure(
                "day.1",
                1,
                240,
                Array.Empty<string>());

            ContentValidationReport report = new GameContentValidator().Validate(catalog);

            Assert.That(report.Errors.Any(error => error.Contains("at least one customer")), Is.True);
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
