using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Witch.Application.Flow;
using Witch.Application.Ports;
using Witch.Domain;
using Witch.Domain.Orders;
using Witch.Domain.Potions;
using Witch.Domain.Progression;

namespace Witch.Application.Tests
{
    /// <summary>
    /// Game Center achievement reporting rules from the Game Center
    /// Implementation Plan: report only after the authoritative gameplay
    /// transaction and save succeed, never let reporting affect the result,
    /// and keep duplicate reporting harmless.
    /// </summary>
    public sealed class GameCenterReportingTests
    {
        [Test]
        public void AchievementIds_AreStableDistinctAndNamespaced()
        {
            Assert.That(GameCenterAchievementIds.FirstShift, Does.EndWith(GameCenterAchievementIds.FirstShiftSuffix));
            Assert.That(GameCenterAchievementIds.SecretKept, Does.EndWith(GameCenterAchievementIds.SecretKeptSuffix));
            Assert.That(GameCenterAchievementIds.FirstShift, Does.StartWith("com."));
            Assert.That(GameCenterAchievementIds.BundleNamespace, Is.EqualTo("com.dzikryaji.witch"));
            Assert.That(GameCenterAchievementIds.FirstShift, Is.Not.EqualTo(GameCenterAchievementIds.SecretKept));
            Assert.That(GameCenterAchievementIds.FirstShiftSuffix, Is.EqualTo("achievement.first_shift"));
            Assert.That(GameCenterAchievementIds.SecretKeptSuffix, Is.EqualTo("achievement.secret_kept"));
            Assert.That(GameCenterAchievementIds.FirstRemedy, Is.EqualTo("com.dzikryaji.witch.achievement.first_remedy"));
            Assert.That(GameCenterAchievementIds.FiveArts, Is.EqualTo("com.dzikryaji.witch.achievement.five_arts"));
            Assert.That(GameCenterAchievementIds.MemoryMender, Is.EqualTo("com.dzikryaji.witch.achievement.memory_mender"));
            Assert.That(GameCenterAchievementIds.CleanLedger, Is.EqualTo("com.dzikryaji.witch.achievement.clean_ledger"));
        }

        [Test]
        public async Task DayOneExpenseSuccess_ReportsFirstShiftButNotCleanLedger()
        {
            var clock = new FakeClock();
            GameSession session = CreateSession();
            CompletePaidClosingCustomer(session, clock, 1);
            var gameCenter = new RecordingGameCenterService();
            var resolve = CreateResolve(new ExpenseRules(3), gameCenter);

            ExpenseResolutionResult result = await resolve.ExecuteAsync(session);

            Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
            Assert.That(gameCenter.Reports.Count, Is.EqualTo(1));
            Assert.That(gameCenter.Reports.Any(r => r.Id == GameCenterAchievementIds.FirstShift && r.Percent == 100d), Is.True);
            Assert.That(gameCenter.Reports.Any(r => r.Id == GameCenterAchievementIds.CleanLedger), Is.False);
        }

        [Test]
        public async Task DayTwoCleanExpense_ReportsCleanLedgerOnce()
        {
            var clock = new FakeClock();
            GameSession session = CreateSession();
            CompletePaidClosingCustomer(session, clock, 1);
            var gameCenter = new RecordingGameCenterService();
            var resolve = CreateResolve(new ExpenseRules(3), gameCenter);
            await resolve.ExecuteAsync(session);
            await new ContinueToNextDayUseCase(new FakeSceneLoader()).ExecuteAsync(session);
            gameCenter.Reports.Clear();
            CompletePaidClosingCustomer(session, clock, 2);

            ExpenseResolutionResult result = await resolve.ExecuteAsync(session);

            Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
            Assert.That(gameCenter.Reports.Count(r => r.Id == GameCenterAchievementIds.CleanLedger), Is.EqualTo(1));
        }

        [Test]
        public async Task FullRun_ReportsFirstShiftCleanLedgerAndSecretKeptAtVictory()
        {
            var clock = new FakeClock();
            GameSession session = CreateSession();
            var gameCenter = new RecordingGameCenterService();
            var resolve = CreateResolve(new ExpenseRules(3), gameCenter);

            for (int day = 1; day <= 3; day++)
            {
                CompletePaidClosingCustomer(session, clock, day);
                ExpenseResolutionResult result = await resolve.ExecuteAsync(session);
                Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
                if (day < 3)
                {
                    await new ContinueToNextDayUseCase(new FakeSceneLoader()).ExecuteAsync(session);
                }
            }

            // FirstShift + CleanLedger(once) + SecretKept(victory)
            Assert.That(gameCenter.Reports.Count(r => r.Id == GameCenterAchievementIds.FirstShift), Is.EqualTo(1));
            Assert.That(gameCenter.Reports.Count(r => r.Id == GameCenterAchievementIds.CleanLedger), Is.EqualTo(1));
            Assert.That(gameCenter.Reports.Count(r => r.Id == GameCenterAchievementIds.SecretKept), Is.EqualTo(1));
            Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.Victory));
        }

        [Test]
        public async Task CleanVictoryDay_ReportsCleanLedgerAndSecretKept()
        {
            var clock = new FakeClock();
            GameSession session = CreateSession(100);  // Day 1 with money
            var gameCenter = new RecordingGameCenterService();
            var resolve = CreateResolve(new ExpenseRules(1), gameCenter);

            CompletePaidClosingCustomer(session, clock, 1);

            ExpenseResolutionResult result = await resolve.ExecuteAsync(session);

            Assert.That(result.Outcome.Kind, Is.EqualTo(ExpenseOutcomeKind.Victory));
            Assert.That(gameCenter.Reports.Count(r => r.Id == GameCenterAchievementIds.SecretKept), Is.EqualTo(1));
            Assert.That(gameCenter.Reports.Count(r => r.Id == GameCenterAchievementIds.CleanLedger), Is.Zero);
        }

        [Test]
        public async Task VictoryAfterWrongService_ReportsSecretKeptOnly()
        {
            var clock = new FakeClock();
            GameSession session = CreateSession(100);
            var gameCenter = new RecordingGameCenterService();
            var resolve = CreateResolve(new ExpenseRules(1), gameCenter);

            CompleteUnpaidClosingCustomer(session, clock);
            ExpenseResolutionResult result = await resolve.ExecuteAsync(session);

            Assert.That(result.Outcome.Kind, Is.EqualTo(ExpenseOutcomeKind.Victory));
            Assert.That(gameCenter.Reports.Count(r => r.Id == GameCenterAchievementIds.SecretKept), Is.EqualTo(1));
            Assert.That(gameCenter.Reports.Count(r => r.Id == GameCenterAchievementIds.CleanLedger), Is.Zero);
        }

        [Test]
        public async Task FailedExpense_ReportsNoAchievement()
        {
            var clock = new FakeClock();
            GameSession session = CreateSession();
            CompleteUnpaidClosingCustomer(session, clock);
            var gameCenter = new RecordingGameCenterService();
            var resolve = CreateResolve(new ExpenseRules(3), gameCenter);

            ExpenseResolutionResult result = await resolve.ExecuteAsync(session);

            Assert.That(result.Outcome.Kind, Is.EqualTo(ExpenseOutcomeKind.GameOver));
            Assert.That(gameCenter.Reports, Is.Empty);
        }

        [Test]
        public async Task DayOneSaveFailure_ReportsNoAchievement()
        {
            var clock = new FakeClock();
            GameSession session = CreateSession();
            CompletePaidClosingCustomer(session, clock, 1);
            var gameCenter = new RecordingGameCenterService();
            var resolve = new ResolveExpenseUseCase(
                new ExpenseRules(3),
                CreateSetup(),
                new FakeSaveRepository
                {
                    SaveResult = SaveStateResult.Failure(SaveStateError.WriteFailed, "disk full")
                },
                new FakeRandomSource(),
                clock,
                gameCenter);

            ExpenseResolutionResult result = await resolve.ExecuteAsync(session);

            Assert.That(result.Error, Is.EqualTo(ExpenseResolutionError.SaveFailed));
            Assert.That(gameCenter.Reports, Is.Empty);
        }

        [Test]
        public async Task ReportingFailure_KeepsGameplayResultUnchanged()
        {
            var clock = new FakeClock();
            GameSession session = CreateSession();
            CompletePaidClosingCustomer(session, clock, 1);
            var gameCenter = new RecordingGameCenterService { ShouldSucceed = false };
            var resolve = CreateResolve(new ExpenseRules(3), gameCenter);

            ExpenseResolutionResult result = await resolve.ExecuteAsync(session);

            Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
            Assert.That(result.Outcome.Kind, Is.EqualTo(ExpenseOutcomeKind.NextDay));
            Assert.That(session.RunState.CurrentDayNumber, Is.EqualTo(2));
            Assert.That(gameCenter.Reports.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task ReportingException_KeepsGameplayResultUnchanged()
        {
            var clock = new FakeClock();
            GameSession session = CreateSession();
            CompletePaidClosingCustomer(session, clock, 1);
            var gameCenter = new RecordingGameCenterService { ShouldThrow = true };
            var resolve = CreateResolve(new ExpenseRules(3), gameCenter);

            ExpenseResolutionResult result = await resolve.ExecuteAsync(session);

            Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
            Assert.That(result.Outcome.Kind, Is.EqualTo(ExpenseOutcomeKind.NextDay));
        }

        [Test]
        public async Task DuplicateVictoryReporting_IsHarmlessToGameplay()
        {
            var clock = new FakeClock();
            GameSession session = CreateCompletedSession();
            var gameCenter = new RecordingGameCenterService();
            var resolve = CreateResolve(new ExpenseRules(1), gameCenter);

            ExpenseResolutionResult first = await resolve.ExecuteAsync(session);
            GameSession secondSession = CreateCompletedSession();
            ExpenseResolutionResult second = await resolve.ExecuteAsync(secondSession);

            Assert.That(first.Succeeded && second.Succeeded, Is.True);
            Assert.That(gameCenter.Reports.Count(r => r.Id == GameCenterAchievementIds.SecretKept), Is.EqualTo(2));
        }

        [Test]
        public async Task WithoutGameCenterService_ExpenseFlowIsUnchanged()
        {
            var clock = new FakeClock();
            GameSession session = CreateSession();
            CompletePaidClosingCustomer(session, clock, 1);
            var resolve = CreateResolve(new ExpenseRules(3), gameCenter: null);

            ExpenseResolutionResult result = await resolve.ExecuteAsync(session);

            Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
            Assert.That(result.Outcome.Kind, Is.EqualTo(ExpenseOutcomeKind.NextDay));
        }

        // ---- Phase 2 achievements ----

        [Test]
        public async Task FirstCorrectServe_ReportsFirstRemedyOnce()
        {
            var clock = new FakeClock();
            GameSession session = CreateSession();
            var gameCenter = new RecordingGameCenterService();
            var herbCategories = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "herb.ginger", "category.warm" },
                { "herb.mint", "category.cool" },
            };
            var serve = new ServePotionUseCase(
                new PotionServiceRules(20),
                new FakeSaveRepository(),
                gameCenter,
                herbCategories);

            ServeCorrectPotion(session, clock, serve, "herb.ginger", "herb.mint");

            Assert.That(gameCenter.Reports, Has.Count.EqualTo(1));
            Assert.That(gameCenter.Reports[0].Id, Is.EqualTo(GameCenterAchievementIds.FirstRemedy));
            Assert.That(gameCenter.Reports[0].Percent, Is.EqualTo(100d));
        }

        [Test]
        public async Task RepeatedCorrectServes_ReportsFirstRemedyOnlyOnce()
        {
            var clock = new FakeClock();
            GameSession session = CreateSession();
            var gameCenter = new RecordingGameCenterService();
            var herbCategories = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "herb.ginger", "category.warm" },
                { "herb.mint", "category.cool" },
            };
            var serve = new ServePotionUseCase(
                new PotionServiceRules(20),
                new FakeSaveRepository(),
                gameCenter,
                herbCategories);

            ServeCorrectPotion(session, clock, serve, "herb.ginger", "herb.mint");
            ServeCorrectPotion(session, clock, serve, "herb.ginger", "herb.mint");

            Assert.That(gameCenter.Reports.Count(r => r.Id == GameCenterAchievementIds.FirstRemedy), Is.EqualTo(1));
        }

        [Test]
        public async Task AllCategoriesServed_ReportsFiveArts()
        {
            var clock = new FakeClock();
            var gameCenter = new RecordingGameCenterService();
            var herbCategories = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "herb.ginger", "category.warm" },
                { "herb.mint", "category.cool" },
                { "herb.sage", "category.soothe" },
                { "herb.borage", "category.cleanse" },
                { "herb.thyme", "category.mend" },
                { "herb.wild-garlic", "category.mend" },
            };
            // One session across all serves, but a fresh ServePotionUseCase per
            // serve: progress must live in the run, not the use case.
            // HerbPair normalizes alphabetically: FirstHerbId is always the
            // alphabetically-first herb, so each pair's FirstHerbId must map to a
            // distinct category.
            GameSession session = CreateSession(100);
            var resolve = CreateResolve(new ExpenseRules(10), gameCenter);
            var serves = new[]
            {
                ("herb.ginger", "herb.mint"),      // ginger -> warm
                ("herb.mint", "herb.sage"),        // mint -> cool
                ("herb.sage", "herb.thyme"),       // sage -> soothe
                ("herb.borage", "herb.thyme"),     // borage -> cleanse
                ("herb.thyme", "herb.wild-garlic") // thyme -> mend
            };
            for (int index = 0; index < serves.Length; index++)
            {
                var servePerScene = new ServePotionUseCase(
                    new PotionServiceRules(20),
                    new FakeSaveRepository(),
                    gameCenter,
                    herbCategories);
                ServeCorrectPotion(session, clock, servePerScene, serves[index].Item1, serves[index].Item2);
                ExpenseResolutionResult result = await resolve.ExecuteAsync(session);
                Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
                if (index < serves.Length - 1)
                {
                    await new ContinueToNextDayUseCase(new FakeSceneLoader()).ExecuteAsync(session);
                }
            }

            Assert.That(gameCenter.Reports.Count(r => r.Id == GameCenterAchievementIds.FiveArts), Is.EqualTo(1));
            Assert.That(gameCenter.Reports.First(r => r.Id == GameCenterAchievementIds.FiveArts).Percent, Is.EqualTo(100d));
        }

        [Test]
        public async Task NotAllCategoriesServed_ReportsNoFiveArts()
        {
            var clock = new FakeClock();
            GameSession session = CreateSession();
            var gameCenter = new RecordingGameCenterService();
            var herbCategories = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "herb.ginger", "category.warm" },
                { "herb.mint", "category.cool" },
                { "herb.sage", "category.soothe" },
            };
            var serve = new ServePotionUseCase(
                new PotionServiceRules(20),
                new FakeSaveRepository(),
                gameCenter,
                herbCategories);

            ServeCorrectPotion(session, clock, serve, "herb.ginger", "herb.mint");

            // FirstRemedy IS reported on first correct serve; FiveArts is not
            Assert.That(gameCenter.Reports.Count(r => r.Id == GameCenterAchievementIds.FirstRemedy), Is.EqualTo(1));
            Assert.That(gameCenter.Reports.Count(r => r.Id == GameCenterAchievementIds.FiveArts), Is.EqualTo(0));
        }

        [Test]
        public async Task FirstCompletedRewind_ReportsMemoryMenderOnce()
        {
            var clock = new FakeClock();
            GameSession session = CreatePendingRewindSession(clock);
            var gameCenter = new RecordingGameCenterService();
            var complete = new CompleteRewindUseCase(
                new FakeSaveRepository(),
                new FakeSceneLoader(),
                new FakeRandomSource(),
                gameCenter);

            // EnterRewind moves RewindPending -> RewindMinigame, then CompleteRewind completes it
            await new EnterRewindUseCase(new FakeSaveRepository(), new FakeSceneLoader(), new FakeRandomSource())
                .ExecuteAsync(session);
            RewindTransactionResult result = await complete.ExecuteAsync(session);

            Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
            Assert.That(gameCenter.Reports, Has.Count.EqualTo(1));
            Assert.That(gameCenter.Reports[0].Id, Is.EqualTo(GameCenterAchievementIds.MemoryMender));
            Assert.That(gameCenter.Reports[0].Percent, Is.EqualTo(100d));
        }

        [Test]
        public async Task RepeatedRewinds_ReportsMemoryMenderOnlyOnce()
        {
            var clock = new FakeClock();
            var gameCenter = new RecordingGameCenterService();
            var complete = new CompleteRewindUseCase(
                new FakeSaveRepository(),
                new FakeSceneLoader(),
                new FakeRandomSource(),
                gameCenter);

            GameSession session = CreatePendingRewindSession(clock);
            await new EnterRewindUseCase(new FakeSaveRepository(), new FakeSceneLoader(), new FakeRandomSource())
                .ExecuteAsync(session);
            await complete.ExecuteAsync(session);

            session = CreatePendingRewindSession(clock);
            await new EnterRewindUseCase(new FakeSaveRepository(), new FakeSceneLoader(), new FakeRandomSource())
                .ExecuteAsync(session);
            await complete.ExecuteAsync(session);

            Assert.That(gameCenter.Reports.Count(r => r.Id == GameCenterAchievementIds.MemoryMender), Is.EqualTo(1));
        }

        [Test]
        public void ServeReportingException_KeepsServeResultUnchanged()
        {
            var clock = new FakeClock();
            GameSession session = CreateSession();
            var gameCenter = new RecordingGameCenterService { ShouldThrow = true };
            var herbCategories = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "herb.ginger", "category.warm" },
            };
            var serve = new ServePotionUseCase(
                new PotionServiceRules(20),
                new FakeSaveRepository(),
                gameCenter,
                herbCategories);

            new OpenShopUseCase().Execute(session);
            new AdmitCustomerUseCase(clock).Execute(
                session,
                new AdmitCustomerCommand(
                    "customer.test",
                    new OrderRequest("order.test", new HerbPair("herb.ginger", "herb.mint"))));
            clock.UtcNow = clock.UtcNow.Add(session.TimerState.RemainingTime).Add(TimeSpan.FromSeconds(1));
            new UpdateShopTimerUseCase(clock).Execute(session);
            var prepare = new PotionPreparationUseCase(new PotionServiceRules(20));
            prepare.Grind(session, "herb.ginger", "herb.mint");
            prepare.AddToPot(session);
            prepare.Brew(session);

            ServePotionResult result = serve.Execute(session);

            Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
            Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.ClosingEncounter));
        }

        [Test]
        public async Task RewindReportingException_KeepsRewindResultUnchanged()
        {
            var clock = new FakeClock();
            GameSession session = CreatePendingRewindSession(clock);
            var gameCenter = new RecordingGameCenterService { ShouldThrow = true };
            var complete = new CompleteRewindUseCase(
                new FakeSaveRepository(),
                new FakeSceneLoader(),
                new FakeRandomSource(),
                gameCenter);

            await new EnterRewindUseCase(new FakeSaveRepository(), new FakeSceneLoader(), new FakeRandomSource())
                .ExecuteAsync(session);
            RewindTransactionResult result = await complete.ExecuteAsync(session);

            Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
        }

        [Test]
        public async Task ExpensePaidWithNoWrongService_ReportsCleanLedger()
        {
            var clock = new FakeClock();
            GameSession session = CreateSession();
            var gameCenter = new RecordingGameCenterService();
            var resolve = CreateResolve(new ExpenseRules(3), gameCenter);

            CompletePaidClosingCustomer(session, clock, 1);
            Assert.That((await resolve.ExecuteAsync(session)).Succeeded, Is.True);
            await new ContinueToNextDayUseCase(new FakeSceneLoader()).ExecuteAsync(session);
            CompletePaidClosingCustomer(session, clock, 2);

            ExpenseResolutionResult result = await resolve.ExecuteAsync(session);

            Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
            Assert.That(gameCenter.Reports.Count(r => r.Id == GameCenterAchievementIds.CleanLedger), Is.EqualTo(1));
            Assert.That(gameCenter.Reports.First(r => r.Id == GameCenterAchievementIds.CleanLedger).Percent, Is.EqualTo(100d));
        }

        [Test]
        public async Task ExpensePaidAfterWrongService_ReportsNoCleanLedger()
        {
            var clock = new FakeClock();
            GameSession session = CreateSession(100);
            var gameCenter = new RecordingGameCenterService();
            var resolve = CreateResolve(new ExpenseRules(3), gameCenter);

            CompleteUnpaidClosingCustomer(session, clock);
            Assert.That(session.AttemptMetadata.WrongServiceCountToday, Is.EqualTo(1));

            ExpenseResolutionResult result = await resolve.ExecuteAsync(session);

            Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
            Assert.That(result.Outcome.Kind, Is.EqualTo(ExpenseOutcomeKind.NextDay));
            Assert.That(gameCenter.Reports.Count(r => r.Id == GameCenterAchievementIds.CleanLedger), Is.Zero);
        }

        private void ServeCorrectPotion(
            GameSession session,
            FakeClock clock,
            ServePotionUseCase serve,
            string herb1,
            string herb2)
        {
            new OpenShopUseCase().Execute(session);
            new AdmitCustomerUseCase(clock).Execute(
                session,
                new AdmitCustomerCommand(
                    "customer.test",
                    new OrderRequest("order.test", new HerbPair(herb1, herb2))));
            clock.UtcNow = clock.UtcNow.Add(session.TimerState.RemainingTime).Add(TimeSpan.FromSeconds(1));
            new UpdateShopTimerUseCase(clock).Execute(session);
            var prepare = new PotionPreparationUseCase(new PotionServiceRules(20));
            prepare.Grind(session, herb1, herb2);
            prepare.AddToPot(session);
            prepare.Brew(session);
            serve.Execute(session);
        }

        private static GameSession CreatePendingRewindSession(FakeClock clock)
        {
            GameSession session = new StartNewRunUseCase().Execute(new StartNewRunCommand(
                "run.rewind",
                101,
                new[] { "category.warm" },
                new[] { "herb.ginger", "herb.mint", "herb.sage" },
                new[] { "guidebook.warm" },
                101,
                TimeSpan.FromMinutes(4),
                "expense.day.1",
                20,
                1,
                DateTimeOffset.UtcNow)).Session;
            new OpenShopUseCase().Execute(session);
            var rules = new PotionServiceRules(20);
            var prepare = new PotionPreparationUseCase(rules);
            var serve = new ServePotionUseCase(rules);

            // Serve 3 wrong potions to reach suspicion threshold (3) and trigger RewindPending
            for (int index = 0; index < 3; index++)
            {
                new AdmitCustomerUseCase(clock).Execute(
                    session,
                    new AdmitCustomerCommand(
                        "customer." + index,
                        new OrderRequest("order." + index, new HerbPair("herb.ginger", "herb.mint"))));
                prepare.Grind(session, "herb.mint", "herb.sage");
                prepare.AddToPot(session);
                prepare.Brew(session);
                serve.Execute(session);
            }

            Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.RewindPending));
            return session;
        }

        private static ResolveExpenseUseCase CreateResolve(
            ExpenseRules rules,
            RecordingGameCenterService gameCenter)
        {
            return new ResolveExpenseUseCase(
                rules,
                CreateSetup(),
                new FakeSaveRepository(),
                new FakeRandomSource(),
                new FakeClock(),
                gameCenter);
        }

        private sealed class FakeSceneLoader : ISceneLoader
        {
            public Task<SceneLoadResult> LoadAsync(SceneId sceneId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(SceneLoadResult.Success());
            }
        }

        private static DayProgressionSetup CreateSetup()
        {
            return new DayProgressionSetup(
                new Dictionary<int, TimeSpan>
                {
                    [1] = TimeSpan.FromSeconds(1),
                    [2] = TimeSpan.FromSeconds(2),
                    [3] = TimeSpan.FromSeconds(3),
                    [4] = TimeSpan.FromSeconds(4),
                    [5] = TimeSpan.FromSeconds(5),
                    [6] = TimeSpan.FromSeconds(6)
                },
                new Dictionary<int, int> { [1] = 20, [2] = 20, [3] = 20, [4] = 20, [5] = 20, [6] = 20 },
                1);
        }

        private static GameSession CreateSession(int startingMoney = 0)
        {
            return new StartNewRunUseCase().Execute(new StartNewRunCommand(
                "run.game-center",
                startingMoney,
                new[] { "category.warm" },
                new[] { "herb.ginger", "herb.mint", "herb.sage" },
                new[] { "guidebook.warm" },
                100,
                TimeSpan.FromSeconds(1),
                "expense.day.1",
                20,
                1,
                DateTimeOffset.UtcNow)).Session;
        }

        private static GameSession CreateCompletedSession()
        {
            GameSession session = CreateSession();
            CompletePaidClosingCustomer(session, new FakeClock(), 1);
            return session;
        }

        private static void CompletePaidClosingCustomer(GameSession session, FakeClock clock, int day)
        {
            CompleteClosingCustomer(session, clock, day, correct: true);
        }

        private static void CompleteUnpaidClosingCustomer(GameSession session, FakeClock clock)
        {
            CompleteClosingCustomer(session, clock, 1, correct: false);
        }

        private static void CompleteClosingCustomer(
            GameSession session,
            FakeClock clock,
            int day,
            bool correct)
        {
            var rules = new PotionServiceRules(20);
            new OpenShopUseCase().Execute(session);
            new AdmitCustomerUseCase(clock).Execute(
                session,
                new AdmitCustomerCommand(
                    "customer.day." + day,
                    new OrderRequest(
                        "order.day." + day,
                        new HerbPair("herb.ginger", "herb.mint"))));
            clock.UtcNow = clock.UtcNow.Add(session.TimerState.RemainingTime).Add(TimeSpan.FromSeconds(1));
            new UpdateShopTimerUseCase(clock).Execute(session);
            var prepare = new PotionPreparationUseCase(rules);
            prepare.Grind(session, correct ? "herb.ginger" : "herb.mint", correct ? "herb.mint" : "herb.sage");
            prepare.AddToPot(session);
            prepare.Brew(session);
            new ServePotionUseCase(rules).Execute(session);
            Assert.That(session.CurrentPhase, Is.EqualTo(GamePhase.ClosingEncounter));
        }

        private sealed class RecordingGameCenterService : IGameCenterService
        {
            public List<(string Id, double Percent)> Reports { get; } = new List<(string, double)>();
            public bool ShouldSucceed { get; set; } = true;
            public bool ShouldThrow { get; set; }
            public bool Authenticated { get; set; } = true;

            public bool IsAuthenticated => Authenticated;

            public Task<ServiceResult> InitializeAsync()
            {
                return Task.FromResult(ServiceResult.Success());
            }

            public Task<ServiceResult> ReportAchievementAsync(string achievementId, double percentComplete)
            {
                if (ShouldThrow)
                {
                    throw new InvalidOperationException("Game Center is offline.");
                }

                Reports.Add((achievementId, percentComplete));
                return Task.FromResult(ShouldSucceed
                    ? ServiceResult.Success()
                    : ServiceResult.Failure(ServiceError.OperationFailed, "offline"));
            }

            public Task<ServiceResult> ShowDashboardAsync()
            {
                return Task.FromResult(ServiceResult.Success());
            }
        }
    }
}
