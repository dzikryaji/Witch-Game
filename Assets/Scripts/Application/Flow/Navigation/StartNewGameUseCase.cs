using System;
using System.Threading;
using System.Threading.Tasks;
using Witch.Application.Ports;

namespace Witch.Application.Flow
{
    public sealed class StartNewGameUseCase : IStartNewGameUseCase
    {
        private readonly IStartNewRunUseCase startNewRun;
        private readonly NewRunSetup setup;
        private readonly IRunStateTransactionRepository saveRepository;
        private readonly IGameSessionStore sessionStore;
        private readonly ISceneLoader sceneLoader;
        private readonly IClock clock;
        private readonly IRandomSource randomSource;

        public StartNewGameUseCase(
            IStartNewRunUseCase startNewRun,
            NewRunSetup setup,
            IRunStateTransactionRepository saveRepository,
            IGameSessionStore sessionStore,
            ISceneLoader sceneLoader,
            IClock clock,
            IRandomSource randomSource)
        {
            this.startNewRun = startNewRun ?? throw new ArgumentNullException(nameof(startNewRun));
            this.setup = setup ?? throw new ArgumentNullException(nameof(setup));
            this.saveRepository = saveRepository ?? throw new ArgumentNullException(nameof(saveRepository));
            this.sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
            this.sceneLoader = sceneLoader ?? throw new ArgumentNullException(nameof(sceneLoader));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.randomSource = randomSource ?? throw new ArgumentNullException(nameof(randomSource));
        }

        public async Task<StartNewGameResult> ExecuteAsync(
            StartNewGameCommand command,
            CancellationToken cancellationToken = default)
        {
            if (command == null)
            {
                return StartNewGameResult.Failure(StartNewGameError.MissingCommand, "New-game command is required.");
            }

            int orderSeed = randomSource.NextInt(1, int.MaxValue);
            StartNewRunResult startResult = startNewRun.Execute(
                setup.CreateCommand(command.RunId, orderSeed, clock.UtcNow));
            if (!startResult.Succeeded)
            {
                return StartNewGameResult.Failure(
                    StartNewGameError.InvalidInitialState,
                    startResult.ErrorMessage);
            }

            SaveStateResult saveResult = await saveRepository
                .SaveRunStateAsync(
                    startResult.Session.DayStartSnapshot,
                    startResult.Session.AttemptMetadata,
                    cancellationToken);
            if (!saveResult.Succeeded)
            {
                return StartNewGameResult.Failure(StartNewGameError.SaveFailed, saveResult.ErrorMessage);
            }

            sessionStore.SetActive(startResult.Session);
            SceneLoadResult sceneResult = await sceneLoader.LoadAsync(SceneId.Shop, cancellationToken);
            if (!sceneResult.Succeeded)
            {
                sessionStore.Clear();
                return StartNewGameResult.Failure(StartNewGameError.SceneLoadFailed, sceneResult.ErrorMessage);
            }

            return StartNewGameResult.Success(startResult.Session);
        }
    }
}
