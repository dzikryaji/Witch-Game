using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Witch.Application.Flow;
using Witch.Application.Ports;
using Witch.Domain;
using Witch.Domain.Orders;
using Witch.Domain.Potions;
using Witch.Domain.Progression;
using Witch.Infrastructure.Persistence;

namespace Witch.Infrastructure.Tests
{
    public sealed class PersistenceAdapterTests
    {
        private string temporaryDirectory;

        [SetUp]
        public void SetUp()
        {
            temporaryDirectory = Path.Combine(Path.GetTempPath(), "witch-persistence-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, true);
            }
        }

        [Test]
        public async Task InMemoryRepository_SnapshotAndAttemptContractsRoundTripIndependently()
        {
            GameSaveState save = CreateSave("run.memory", suspicion: 0, seed: 111);
            var repository = new InMemoryRunRepository();
            await repository.SaveRunStateAsync(save.DayStartSnapshot, save.AttemptMetadata);
            var updated = new ActiveAttemptMetadata("run.memory", 1, 2, 0, false, 0, 111);

            SaveStateResult update = await repository.SaveActiveAttemptMetadataAsync(updated);
            DayStartSnapshotLoadResult snapshot = await repository.LoadLatestDayStartSnapshotAsync();
            ActiveAttemptMetadataLoadResult attempt = await repository.LoadLatestActiveAttemptMetadataAsync();

            Assert.That(update.Succeeded, Is.True, update.ErrorMessage);
            Assert.That(snapshot.Snapshot.RunState.Suspicion, Is.Zero);
            Assert.That(attempt.Metadata.Suspicion, Is.EqualTo(2));
            Assert.That(attempt.Metadata.ActiveOrderSeed, Is.EqualTo(111));
        }

        [Test]
        public async Task JsonRepository_RoundTripsCurrentRunAndFlags()
        {
            GameSaveState save = CreateSave("run.json", suspicion: 2, seed: 222);
            var repository = new JsonRunRepository(temporaryDirectory, 1);

            SaveStateResult write = await repository.SaveRunStateAsync(save.DayStartSnapshot, save.AttemptMetadata);
            LoadStateResult load = await repository.LoadLatestAsync();

            Assert.That(write.Succeeded, Is.True, write.ErrorMessage);
            Assert.That(load.Succeeded, Is.True, load.ErrorMessage);
            Assert.That(load.SavedGame.AttemptMetadata.Suspicion, Is.EqualTo(2));
            Assert.That(load.SavedGame.AttemptMetadata.ActiveOrderSeed, Is.EqualTo(222));
            Assert.That(load.SavedGame.DayStartSnapshot.RunState.TutorialFlags["tutorial.intro"], Is.True);
        }

        [Test]
        public async Task JsonRepository_MigratesLegacyAloeHerbIdToBorage()
        {
            var repository = new JsonRunRepository(temporaryDirectory, 1);
            GameSaveState save = CreateSave("run.legacy-aloe", suspicion: 0, seed: 222);
            await repository.SaveRunStateAsync(save.DayStartSnapshot, save.AttemptMetadata);
            string path = Path.Combine(temporaryDirectory, JsonRunRepository.CurrentRunFileName);
            File.WriteAllText(path, File.ReadAllText(path).Replace("herb.mint", "herb.aloe"));

            LoadStateResult load = await repository.LoadLatestAsync();

            Assert.That(load.Succeeded, Is.True, load.ErrorMessage);
            Assert.That(load.SavedGame.DayStartSnapshot.RunState.UnlockedHerbIds, Does.Contain("herb.borage"));
            Assert.That(load.SavedGame.DayStartSnapshot.RunState.UnlockedHerbIds, Does.Not.Contain("herb.aloe"));
        }

        [Test]
        public async Task JsonRepository_RoundTripsCompletedRunFlag()
        {
            GameSaveState save = CreateSave("run.complete", suspicion: 0, seed: 223);
            DayStartSnapshot source = save.DayStartSnapshot;
            var completedSnapshot = new DayStartSnapshot(
                source.RunState.MarkCompleted(),
                source.OrderSeed,
                source.TimerDuration,
                source.ExpenseId,
                source.ExpenseValue,
                source.SaveVersion,
                source.CapturedAtUtc);
            var repository = new JsonRunRepository(temporaryDirectory, 1);

            await repository.SaveRunStateAsync(completedSnapshot, save.AttemptMetadata);
            LoadStateResult load = await repository.LoadLatestAsync();

            Assert.That(load.Succeeded, Is.True, load.ErrorMessage);
            Assert.That(load.SavedGame.DayStartSnapshot.RunState.RunCompleted, Is.True);
        }

        [Test]
        public async Task JsonRepository_MissingAndUnsupportedDataReturnTypedErrors()
        {
            var fileStore = new MemoryAtomicFileStore();
            var versionOne = new JsonRunRepository("save", 1, fileStore);
            LoadStateResult missing = await versionOne.LoadLatestAsync();
            GameSaveState save = CreateSave("run.version", suspicion: 0, seed: 333);
            await versionOne.SaveRunStateAsync(save.DayStartSnapshot, save.AttemptMetadata);
            var versionTwo = new JsonRunRepository("save", 2, fileStore);

            LoadStateResult unsupported = await versionTwo.LoadLatestAsync();

            Assert.That(missing.Error, Is.EqualTo(LoadStateError.NotFound));
            Assert.That(unsupported.Error, Is.EqualTo(LoadStateError.UnsupportedVersion));
        }

        [Test]
        public async Task JsonRepository_CorruptPrimaryRecoversPreviousCopy()
        {
            var repository = new JsonRunRepository(temporaryDirectory, 1);
            GameSaveState original = CreateSave("run.recovery", suspicion: 0, seed: 444);
            await repository.SaveRunStateAsync(original.DayStartSnapshot, original.AttemptMetadata);
            await repository.SaveActiveAttemptMetadataAsync(
                new ActiveAttemptMetadata("run.recovery", 1, 1, 0, false, 0, 444));
            File.WriteAllText(Path.Combine(temporaryDirectory, JsonRunRepository.CurrentRunFileName), "{ broken json");

            LoadStateResult recovered = await repository.LoadLatestAsync();

            Assert.That(recovered.Succeeded, Is.True, recovered.ErrorMessage);
            Assert.That(recovered.SavedGame.AttemptMetadata.Suspicion, Is.Zero);
        }

        [Test]
        public async Task JsonRepository_FailedReplaceKeepsPreviousValidPrimary()
        {
            var fileStore = new MemoryAtomicFileStore();
            var repository = new JsonRunRepository("save", 1, fileStore);
            GameSaveState original = CreateSave("run.failure", suspicion: 0, seed: 555);
            await repository.SaveRunStateAsync(original.DayStartSnapshot, original.AttemptMetadata);
            fileStore.FailWrites = true;

            SaveStateResult failed = await repository.SaveActiveAttemptMetadataAsync(
                new ActiveAttemptMetadata("run.failure", 1, 1, 0, false, 0, 555));
            fileStore.FailWrites = false;
            LoadStateResult load = await repository.LoadLatestAsync();

            Assert.That(failed.Error, Is.EqualTo(SaveStateError.WriteFailed));
            Assert.That(load.Succeeded, Is.True, load.ErrorMessage);
            Assert.That(load.SavedGame.AttemptMetadata.Suspicion, Is.Zero);
        }

        [Test]
        public async Task SettingsUseSeparateFileFromRunSave()
        {
            var runRepository = new JsonRunRepository(temporaryDirectory, 1);
            var settingsRepository = new JsonSettingsRepository(temporaryDirectory);
            GameSaveState save = CreateSave("run.settings", 0, 666);
            await runRepository.SaveRunStateAsync(save.DayStartSnapshot, save.AttemptMetadata);
            await settingsRepository.SaveSettingsAsync(new GameSettingsState(0.9f, 0.5f, 0.75f, true));

            SettingsLoadResult settings = await settingsRepository.LoadSettingsAsync();

            Assert.That(File.Exists(Path.Combine(temporaryDirectory, JsonRunRepository.CurrentRunFileName)), Is.True);
            Assert.That(File.Exists(Path.Combine(temporaryDirectory, JsonSettingsRepository.SettingsFileName)), Is.True);
            Assert.That(settings.Settings.ReducedMotion, Is.True);
            Assert.That(settings.Settings.MasterVolume, Is.EqualTo(0.9f));
            Assert.That(settings.Settings.ReducedMismatchDelay, Is.True);
        }

        [Test]
        public async Task QuitAtSuspicionTwoAndPendingRewindReloadSafelyThenRewindChangesSeed()
        {
            var repository = new InMemoryRunRepository();
            GameSession session = CreateSession();
            await repository.SaveRunStateAsync(session.DayStartSnapshot, session.AttemptMetadata);
            PotionServiceRules rules = CreatePotionRules();

            await ServeWrongAsync(session, repository, rules, 1);
            await ServeWrongAsync(session, repository, rules, 2);
            GameSession suspicionTwo = await ReloadAsync(repository);

            Assert.That(suspicionTwo.RunState.Suspicion, Is.EqualTo(2));
            Assert.That(suspicionTwo.AttemptMetadata.ActiveOrderSeed, Is.EqualTo(777));
            Assert.That(suspicionTwo.RunState.RewindCount, Is.Zero);

            await ServeWrongAsync(session, repository, rules, 3);
            await new EnterRewindUseCase(
                    repository,
                    new TestSceneLoader(),
                    new FixedRandomSource(888))
                .ExecuteAsync(session);
            GameSession pending = await ReloadAsync(repository);

            Assert.That(pending.CurrentPhase, Is.EqualTo(GamePhase.RewindMinigame));
            Assert.That(pending.AttemptMetadata.PendingRewind, Is.True);
            Assert.That(pending.AttemptMetadata.MinigameBoardSeed, Is.EqualTo(888));

            await new CompleteRewindUseCase(
                    repository,
                    new TestSceneLoader(),
                    new FixedRandomSource(999))
                .ExecuteAsync(pending);
            GameSession rewound = await ReloadAsync(repository);

            Assert.That(rewound.RunState.Suspicion, Is.Zero);
            Assert.That(rewound.RunState.RewindCount, Is.EqualTo(1));
            Assert.That(rewound.AttemptMetadata.ActiveOrderSeed, Is.EqualTo(999));
        }

        [Test]
        public void DeterministicRandomSource_ReplaysSameSequence()
        {
            var first = new Witch.Infrastructure.Platform.DeterministicRandomSource(123);
            var second = new Witch.Infrastructure.Platform.DeterministicRandomSource(123);

            Assert.That(first.NextInt(1, 1000), Is.EqualTo(second.NextInt(1, 1000)));
            Assert.That(first.NextInt(1, 1000), Is.EqualTo(second.NextInt(1, 1000)));
        }

        private static async Task<GameSession> ReloadAsync(InMemoryRunRepository repository)
        {
            var store = new GameSessionStore();
            LoadRunResult result = await new LoadRunUseCase(
                    repository,
                    repository,
                    store,
                    new TestSceneLoader())
                .ExecuteAsync(LoadRunCommand.Latest());
            Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
            return result.Session;
        }

        private static async Task ServeWrongAsync(
            GameSession session,
            InMemoryRunRepository repository,
            PotionServiceRules rules,
            int index)
        {
            if (session.CurrentPhase == GamePhase.ShopPreparation)
            {
                new OpenShopUseCase().Execute(session);
            }

            new AdmitCustomerUseCase(new TestClock()).Execute(
                session,
                new AdmitCustomerCommand(
                    "customer." + index,
                    new OrderRequest("order." + index, new HerbPair("herb.ginger", "herb.mint"))));
            var preparation = new PotionPreparationUseCase(rules);
            preparation.Grind(session, "herb.mint");
            preparation.AddToPot(session);
            preparation.Grind(session, "herb.sage");
            preparation.AddToPot(session);
            preparation.Brew(session);
            ServePotionResult result = await new ServePotionUseCase(rules, repository).ExecuteAsync(session);
            Assert.That(result.Succeeded, Is.True, result.ErrorMessage);
        }

        private static GameSession CreateSession()
        {
            return new StartNewRunUseCase().Execute(new StartNewRunCommand(
                "run.quit",
                0,
                new[] { "category.warm" },
                new[] { "herb.ginger", "herb.mint", "herb.sage" },
                new[] { "recipe.warming", "recipe.cooling" },
                new[] { "guidebook.warm" },
                777,
                TimeSpan.FromMinutes(4),
                "expense.day.1",
                20,
                1,
                DateTimeOffset.UtcNow)).Session;
        }

        private static PotionServiceRules CreatePotionRules()
        {
            return new PotionServiceRules(
                10,
                new RecipeCatalog(new[]
                {
                    new Recipe("recipe.warming", new HerbPair("herb.ginger", "herb.mint")),
                    new Recipe("recipe.cooling", new HerbPair("herb.mint", "herb.sage"))
                }));
        }

        private static GameSaveState CreateSave(string runId, int suspicion, int seed)
        {
            var state = new RunState(
                runId,
                1,
                5,
                0,
                0,
                new[] { "category.warm" },
                new[] { "herb.ginger", "herb.mint" },
                new[] { "recipe.warming" },
                new[] { "guidebook.warm" },
                new Dictionary<string, bool> { { "tutorial.intro", true } });
            var snapshot = new DayStartSnapshot(
                state,
                seed,
                TimeSpan.FromMinutes(4),
                "expense.day.1",
                20,
                1,
                DateTimeOffset.Parse("2026-10-24T00:00:00+08:00"));
            var metadata = new ActiveAttemptMetadata(runId, 1, suspicion, 0, false, 0, seed);
            return new GameSaveState(snapshot, metadata);
        }

        private sealed class MemoryAtomicFileStore : IAtomicFileStore
        {
            private readonly Dictionary<string, string> files = new Dictionary<string, string>();
            public bool FailWrites { get; set; }

            public bool Exists(string path) => files.ContainsKey(path);
            public string ReadAllText(string path) => files[path];

            public void WriteAtomically(string primaryPath, string previousPath, string contents)
            {
                if (FailWrites)
                {
                    throw new IOException("replace failed");
                }

                if (files.TryGetValue(primaryPath, out string previous))
                {
                    files[previousPath] = previous;
                }

                files[primaryPath] = contents;
            }
        }

        private sealed class TestSceneLoader : ISceneLoader
        {
            public Task<SceneLoadResult> LoadAsync(SceneId sceneId, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(SceneLoadResult.Success());
            }
        }

        private sealed class TestClock : IClock
        {
            public DateTimeOffset UtcNow => DateTimeOffset.Parse("2026-10-24T00:00:00+08:00");
        }

        private sealed class FixedRandomSource : IRandomSource
        {
            private readonly int value;
            public FixedRandomSource(int value) { this.value = value; }
            public int NextInt(int minimumInclusive, int maximumExclusive) => value;
        }
    }
}
