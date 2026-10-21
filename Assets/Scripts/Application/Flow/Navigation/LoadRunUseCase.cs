using System;
using System.Threading;
using System.Threading.Tasks;
using Witch.Application.Ports;
using Witch.Domain;

namespace Witch.Application.Flow
{
    public sealed class LoadRunUseCase : ILoadRunUseCase
    {
        private readonly ISaveRepository saveRepository;
        private readonly IGameSessionStore sessionStore;
        private readonly ISceneLoader sceneLoader;

        public LoadRunUseCase(
            ISaveRepository saveRepository,
            IGameSessionStore sessionStore,
            ISceneLoader sceneLoader)
        {
            this.saveRepository = saveRepository ?? throw new ArgumentNullException(nameof(saveRepository));
            this.sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
            this.sceneLoader = sceneLoader ?? throw new ArgumentNullException(nameof(sceneLoader));
        }

        public async Task<LoadRunResult> ExecuteAsync(
            LoadRunCommand command,
            CancellationToken cancellationToken = default)
        {
            if (command == null)
            {
                return LoadRunResult.Failure(LoadRunError.MissingCommand, "Load command is required.");
            }

            if (!command.LoadLatest && string.IsNullOrWhiteSpace(command.RunId))
            {
                return LoadRunResult.Failure(LoadRunError.InvalidRunId, "Run ID is required.");
            }

            LoadStateResult loadResult = command.LoadLatest
                ? await saveRepository.LoadLatestAsync(cancellationToken)
                : await saveRepository.LoadAsync(command.RunId, cancellationToken);
            if (!loadResult.Succeeded)
            {
                return LoadRunResult.Failure(LoadRunError.LoadFailed, loadResult.ErrorMessage);
            }

            var session = new GameSession(loadResult.RunState, GamePhase.ShopPreparation);
            sessionStore.SetActive(session);

            SceneLoadResult sceneResult = await sceneLoader.LoadAsync(SceneId.Shop, cancellationToken);
            if (!sceneResult.Succeeded)
            {
                sessionStore.Clear();
                return LoadRunResult.Failure(LoadRunError.SceneLoadFailed, sceneResult.ErrorMessage);
            }

            return LoadRunResult.Success(session);
        }
    }
}
