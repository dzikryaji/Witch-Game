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
        private readonly ISaveRepository saveRepository;
        private readonly IGameSessionStore sessionStore;
        private readonly ISceneLoader sceneLoader;

        public StartNewGameUseCase(
            IStartNewRunUseCase startNewRun,
            NewRunSetup setup,
            ISaveRepository saveRepository,
            IGameSessionStore sessionStore,
            ISceneLoader sceneLoader)
        {
            this.startNewRun = startNewRun ?? throw new ArgumentNullException(nameof(startNewRun));
            this.setup = setup ?? throw new ArgumentNullException(nameof(setup));
            this.saveRepository = saveRepository ?? throw new ArgumentNullException(nameof(saveRepository));
            this.sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
            this.sceneLoader = sceneLoader ?? throw new ArgumentNullException(nameof(sceneLoader));
        }

        public async Task<StartNewGameResult> ExecuteAsync(
            StartNewGameCommand command,
            CancellationToken cancellationToken = default)
        {
            if (command == null)
            {
                return StartNewGameResult.Failure(StartNewGameError.MissingCommand, "New-game command is required.");
            }

            StartNewRunResult startResult = startNewRun.Execute(setup.CreateCommand(command.RunId));
            if (!startResult.Succeeded)
            {
                return StartNewGameResult.Failure(
                    StartNewGameError.InvalidInitialState,
                    startResult.ErrorMessage);
            }

            SaveStateResult saveResult = await saveRepository
                .SaveAsync(startResult.Session.RunState, cancellationToken);
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
