using System;
using System.Threading;
using System.Threading.Tasks;
using Witch.Application.Ports;
using Witch.Domain;

namespace Witch.Application.Flow
{
    public sealed class ContinueToNextDayUseCase
    {
        private readonly ISceneLoader sceneLoader;

        public ContinueToNextDayUseCase(ISceneLoader sceneLoader)
        {
            this.sceneLoader = sceneLoader ?? throw new ArgumentNullException(nameof(sceneLoader));
        }

        public async Task<ExpenseResolutionResult> ExecuteAsync(
            GameSession session,
            CancellationToken cancellationToken = default)
        {
            if (session == null)
            {
                return ExpenseResolutionResult.Failure(ExpenseResolutionError.MissingSession, "Session is required.");
            }

            if (session.CurrentPhase != GamePhase.DaySummary)
            {
                return ExpenseResolutionResult.Failure(ExpenseResolutionError.InvalidPhase, "Day summary is not active.");
            }

            session.ContinueFromDaySummary();
            SceneLoadResult scene = await sceneLoader.LoadAsync(SceneId.Shop, cancellationToken);
            if (!scene.Succeeded)
            {
                await sceneLoader.LoadAsync(SceneId.MainMenu, cancellationToken);
                return ExpenseResolutionResult.Failure(ExpenseResolutionError.SceneLoadFailed, scene.ErrorMessage);
            }

            return ExpenseResolutionResult.Success();
        }
    }
}
