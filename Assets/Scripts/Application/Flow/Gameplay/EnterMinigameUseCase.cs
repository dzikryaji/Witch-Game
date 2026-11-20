using System;
using System.Threading;
using System.Threading.Tasks;
using Witch.Application.Ports;
using Witch.Domain;
using Witch.Domain.Progression;

namespace Witch.Application.Flow
{
    public sealed class EnterMinigameUseCase
    {
        private readonly IActiveAttemptMetadataRepository saveRepository;
        private readonly ISceneLoader sceneLoader;
        private readonly IRandomSource randomSource;

        public EnterMinigameUseCase(
            IActiveAttemptMetadataRepository saveRepository,
            ISceneLoader sceneLoader,
            IRandomSource randomSource)
        {
            this.saveRepository = saveRepository ?? throw new ArgumentNullException(nameof(saveRepository));
            this.sceneLoader = sceneLoader ?? throw new ArgumentNullException(nameof(sceneLoader));
            this.randomSource = randomSource ?? throw new ArgumentNullException(nameof(randomSource));
        }

        public async Task<MinigameTransactionResult> ExecuteAsync(
            GameSession session,
            CancellationToken cancellationToken = default)
        {
            if (session == null)
            {
                return MinigameTransactionResult.Failure(MinigameTransactionError.MissingSession, "Session is required.");
            }

            if (session.CurrentPhase == GamePhase.MinigameActive && session.AttemptMetadata.PendingMinigame)
            {
                SceneLoadResult retryScene = await sceneLoader.LoadAsync(SceneId.MatchCardMinigame, cancellationToken);
                if (retryScene.Succeeded)
                {
                    return MinigameTransactionResult.Success(
                        MatchCardBoard.Create(
                            session.AttemptMetadata.MinigameBoardSeed,
                            session.AttemptMetadata.MinigameWinCount),
                        alreadyApplied: true);
                }

                await sceneLoader.LoadAsync(SceneId.MainMenu, cancellationToken);
                return MinigameTransactionResult.Failure(
                    MinigameTransactionError.SceneLoadFailed,
                    retryScene.ErrorMessage);
            }

            if (session.CurrentPhase != GamePhase.MinigamePending)
            {
                return MinigameTransactionResult.Failure(MinigameTransactionError.InvalidPhase, "Minigame is not pending.");
            }

            int boardSeed = randomSource.NextInt(1, int.MaxValue);
            var metadata = new ActiveAttemptMetadata(
                session.RunState.RunId,
                session.RunState.CurrentDayNumber,
                session.RunState.Suspicion,
                session.RunState.MinigameWinCount,
                true,
                boardSeed,
                session.AttemptMetadata.ActiveOrderSeed,
                session.AttemptMetadata.WrongServiceCountToday,
                session.AttemptMetadata.ServiceCountToday,
                true,
                session.AttemptMetadata.CorrectServiceCountToday,
                session.AttemptMetadata.CoinsEarnedToday,
                session.AttemptMetadata.WrongOrdersForSummary,
                session.AttemptMetadata.Selection);
            SaveStateResult save = await saveRepository.SaveActiveAttemptMetadataAsync(
                metadata,
                cancellationToken);
            if (!save.Succeeded)
            {
                return MinigameTransactionResult.Failure(MinigameTransactionError.SaveFailed, save.ErrorMessage);
            }

            session.EnterMinigame(metadata);
            MatchCardBoard board = MatchCardBoard.Create(boardSeed, metadata.MinigameWinCount);
            SceneLoadResult scene = await sceneLoader.LoadAsync(SceneId.MatchCardMinigame, cancellationToken);
            if (scene.Succeeded)
            {
                return MinigameTransactionResult.Success(board);
            }

            await sceneLoader.LoadAsync(SceneId.MainMenu, cancellationToken);
            return MinigameTransactionResult.Failure(MinigameTransactionError.SceneLoadFailed, scene.ErrorMessage);
        }
    }
}
