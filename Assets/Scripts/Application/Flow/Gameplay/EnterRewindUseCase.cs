using System;
using System.Threading;
using System.Threading.Tasks;
using Witch.Application.Ports;
using Witch.Domain;
using Witch.Domain.Progression;

namespace Witch.Application.Flow
{
    public sealed class EnterRewindUseCase
    {
        private readonly IActiveAttemptMetadataRepository saveRepository;
        private readonly ISceneLoader sceneLoader;
        private readonly IRandomSource randomSource;

        public EnterRewindUseCase(
            IActiveAttemptMetadataRepository saveRepository,
            ISceneLoader sceneLoader,
            IRandomSource randomSource)
        {
            this.saveRepository = saveRepository ?? throw new ArgumentNullException(nameof(saveRepository));
            this.sceneLoader = sceneLoader ?? throw new ArgumentNullException(nameof(sceneLoader));
            this.randomSource = randomSource ?? throw new ArgumentNullException(nameof(randomSource));
        }

        public async Task<RewindTransactionResult> ExecuteAsync(
            GameSession session,
            CancellationToken cancellationToken = default)
        {
            if (session == null)
            {
                return RewindTransactionResult.Failure(RewindTransactionError.MissingSession, "Session is required.");
            }

            if (session.CurrentPhase == GamePhase.RewindMinigame && session.AttemptMetadata.PendingRewind)
            {
                SceneLoadResult retryScene = await sceneLoader.LoadAsync(SceneId.RewindMinigame, cancellationToken);
                return retryScene.Succeeded
                    ? RewindTransactionResult.Success(
                        RewindBoard.Create(
                            session.AttemptMetadata.MinigameBoardSeed,
                            session.AttemptMetadata.RewindCount),
                        alreadyApplied: true)
                    : RewindTransactionResult.Failure(
                        RewindTransactionError.SceneLoadFailed,
                        retryScene.ErrorMessage);
            }

            if (session.CurrentPhase != GamePhase.RewindPending)
            {
                return RewindTransactionResult.Failure(RewindTransactionError.InvalidPhase, "Rewind is not pending.");
            }

            int boardSeed = randomSource.NextInt(1, int.MaxValue);
            var metadata = new ActiveAttemptMetadata(
                session.RunState.RunId,
                session.RunState.CurrentDayNumber,
                session.RunState.Suspicion,
                session.RunState.RewindCount,
                true,
                boardSeed,
                session.AttemptMetadata.ActiveOrderSeed);
            SaveStateResult save = await saveRepository.SaveActiveAttemptMetadataAsync(
                metadata,
                cancellationToken);
            if (!save.Succeeded)
            {
                return RewindTransactionResult.Failure(RewindTransactionError.SaveFailed, save.ErrorMessage);
            }

            session.EnterRewind(metadata);
            RewindBoard board = RewindBoard.Create(boardSeed, metadata.RewindCount);
            SceneLoadResult scene = await sceneLoader.LoadAsync(SceneId.RewindMinigame, cancellationToken);
            return scene.Succeeded
                ? RewindTransactionResult.Success(board)
                : RewindTransactionResult.Failure(RewindTransactionError.SceneLoadFailed, scene.ErrorMessage);
        }
    }
}
