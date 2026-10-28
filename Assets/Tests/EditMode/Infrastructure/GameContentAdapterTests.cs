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
        public void Load_AuthoredThreeDayPrototypeContentIsValid()
        {
            GameContentCatalogDefinition catalog = AssetDatabase.LoadAssetAtPath<GameContentCatalogDefinition>(
                "Assets/Content/GameContentCatalog.asset");

            GameContentLoadResult result = new GameContentAdapter().Load(catalog);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(result.Succeeded, Is.True, string.Join("\n", result.Errors));
            Assert.That(result.Content.OrdersById.Count, Is.EqualTo(15));
            Assert.That(result.Content.DaysByNumber.Count, Is.EqualTo(3));
            Assert.That(result.Content.DaysByNumber[1].TimerDuration, Is.EqualTo(TimeSpan.FromMinutes(4)));
            Assert.That(result.Content.DaysByNumber[2].TimerDuration, Is.EqualTo(TimeSpan.FromMinutes(4) + TimeSpan.FromSeconds(20)));
            Assert.That(result.Content.DaysByNumber[3].TimerDuration, Is.EqualTo(TimeSpan.FromMinutes(4) + TimeSpan.FromSeconds(40)));
            Assert.That(result.Content.HerbsById.Count, Is.EqualTo(25));
            Assert.That(result.Content.HerbsById.ContainsKey("herb.lavender"), Is.True);
            Assert.That(result.Content.HerbsById["herb.ginger"].ScientificName, Is.EqualTo("Zingiber officinale"));
            Assert.That(result.Content.HerbsById["herb.ginger"].WarningText, Does.Contain("blood clotting"));
            Assert.That(result.Content.HerbsById["herb.ginger"].GuidebookPage, Is.EqualTo(3));
            Assert.That(result.Content.HerbsById["herb.mint"].DisplayName, Is.EqualTo("Peppermint"));
            Assert.That(result.Content.HerbsById["herb.mint"].ClueIds, Does.Contain("clue.mint.cool-leaf"));
            Assert.That(result.Content.GuidebookEntriesById.ContainsKey("guidebook.soothe"), Is.True);
            Assert.That(result.Content.GuidebookEntriesById["guidebook.soothe"].BodyText, Does.Contain("quiet the nerves"));
            Assert.That(result.Content.GuidebookEntriesById["guidebook.soothe"].BodyText, Does.Not.Contain("Lavender"));
            Assert.That(result.Content.GuidebookEntriesById["guidebook.warm-cool"].BodyText, Does.Not.Contain("\\n"));
            Assert.That(result.Content.NewRunSetup.UnlockedHerbIds.Count, Is.EqualTo(10));
            Assert.That(result.Content.CategoryUnlocks.Single(value => value.CategoryId == "category.soothe").FirstDay, Is.EqualTo(4));
            var dayThreeState = result.Content.DayProgressionSetup.ApplyUnlocks(
                new Witch.Domain.Progression.RunState("content-test", 3, 0, 0, 0));
            Assert.That(dayThreeState.UnlockedHerbIds, Has.Count.EqualTo(10));
            Assert.That(dayThreeState.UnlockedHerbIds, Does.Not.Contain("herb.lavender"));
            var dayFourState = result.Content.DayProgressionSetup.ApplyUnlocks(
                new Witch.Domain.Progression.RunState("content-test", 4, 0, 0, 0));
            Assert.That(dayFourState.UnlockedHerbIds, Does.Contain("herb.lavender"));
            Assert.That(dayFourState.UnlockedRecipeIds, Does.Contain("recipe.soothing-cordial"));
            Assert.That(result.Content.DaysByNumber.Values.All(day => day.OrderIds.Distinct().Count() == day.OrderIds.Count), Is.True);
            Assert.That(result.Content.DaysByNumber.Values.All(day => day.OrderIds.Count >= 3), Is.True);
            Assert.That(result.Content.DaysByNumber.Values.All(day =>
                (day.OrderIds.Count - 1) * result.Content.CorrectOrderPayment >= result.Content.NewRunSetup.FirstDayExpenseValue), Is.True);
            Assert.That(result.Content.RewindCardIds.Count, Is.EqualTo(8));
            Assert.That(result.Content.RewindDifficulty.MaximumPairCount, Is.EqualTo(8));
        }

        [Test]
        public void Load_ValidFixtureMapsAllRuntimeInputs()
        {
            GameContentCatalogDefinition catalog = ContentTestFixture.CreateValidCatalog();

            GameContentLoadResult result = new GameContentAdapter().Load(catalog);

            Assert.That(result.Succeeded, Is.True, string.Join("\n", result.Errors));
            Assert.That(result.Content.Recipes.Recipes.Count, Is.EqualTo(1));
            Assert.That(result.Content.OrdersById.Count, Is.EqualTo(2));
            Assert.That(result.Content.NewRunSetup.StartingMoney, Is.Zero);
            Assert.That(result.Content.NewRunSetup.FirstDayTimerDuration, Is.EqualTo(TimeSpan.FromMinutes(4)));
            Assert.That(result.Content.NewRunSetup.FirstDayExpenseValue, Is.EqualTo(20));
            Assert.That(result.Content.ExpenseRules.CompletionDay, Is.EqualTo(3));
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
        public void Validate_MixedCategoryOrderReturnsError()
        {
            GameContentCatalogDefinition catalog = ContentTestFixture.CreateValidCatalog();
            catalog.Herbs[1].Configure("herb.mint", "Mint", "category.cool");

            ContentValidationReport report = new GameContentValidator().Validate(catalog);

            Assert.That(report.Errors.Any(error => error.Contains("must share one category")), Is.True);
        }

        [Test]
        public void Validate_LockedHerbAndUnknownClueReturnsErrors()
        {
            GameContentCatalogDefinition catalog = ContentTestFixture.CreateValidCatalog();
            catalog.Orders[0].Configure(
                "order.direct",
                "Bring ginger and sage.",
                new[] { "clue.unknown" },
                "herb.ginger",
                "herb.sage");

            ContentValidationReport report = new GameContentValidator().Validate(catalog);

            Assert.That(report.Errors.Any(error => error.Contains("locked or unknown herb 'herb.sage'")), Is.True);
            Assert.That(report.Errors.Any(error => error.Contains("unknown guidebook clue 'clue.unknown'")), Is.True);
        }

        [Test]
        public void Validate_DuplicateDayOrderAndSingleSeedPoolReturnsErrors()
        {
            GameContentCatalogDefinition catalog = ContentTestFixture.CreateValidCatalog();
            catalog.Days[0].Configure(
                "day.1",
                1,
                240,
                new[] { "order.direct", "order.direct" },
                new[] { "customer.villager" });

            ContentValidationReport report = new GameContentValidator().Validate(catalog);

            Assert.That(report.Errors.Any(error => error.Contains("repeats order 'order.direct'")), Is.True);
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
                new[] { "order.direct", "order.visual" },
                Array.Empty<string>());

            ContentValidationReport report = new GameContentValidator().Validate(catalog);

            Assert.That(report.Errors.Any(error => error.Contains("at least one customer")), Is.True);
        }

        [Test]
        public void Validate_SameHerbRecipeAndSmallCardPoolReturnsErrors()
        {
            GameContentCatalogDefinition catalog = ContentTestFixture.CreateValidCatalog();
            catalog.Recipes[0].Configure("recipe.tonic", "herb.ginger", "herb.ginger");
            catalog.RewindDifficulty.Configure("rewind.difficulty.default", 3, 4);

            ContentValidationReport report = new GameContentValidator().Validate(catalog);

            Assert.That(report.Errors.Any(error => error.Contains("two different herbs")), Is.True);
            Assert.That(report.Errors.Any(error => error.Contains("4 unique rewind cards")), Is.True);
        }
    }
}
