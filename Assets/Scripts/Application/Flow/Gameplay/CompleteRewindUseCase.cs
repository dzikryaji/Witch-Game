using System;
using System.Threading;
using System.Threading.Tasks;
using Witch.Application.Ports;
using Witch.Domain;
using Witch.Domain.Progression;

namespace Witch.Application.Flow
{
    public sealed class CompleteRewindUseCase
    {
        private readonly IRunStateTransactionRepository saveRepository;
        private readonly ISceneLoader sceneLoader;
        private readonly IRandomSource randomSource;

        public CompleteRewindUseCase(
            IRunStateTransactionRepository saveRepository,
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

            if (session.RewindCompletionApplied
                && !session.AttemptMetadata.PendingRewind
                && (session.CurrentPhase == GamePhase.ShopPreparation
                    || session.CurrentPhase == GamePhase.CustomerEncounter))
            {
                return RewindTransactionResult.Success(null, alreadyApplied: true);
            }

            if (session.CurrentPhase != GamePhase.RewindMinigame || !session.AttemptMetadata.PendingRewind)
            {
                return RewindTransactionResult.Failure(RewindTransactionError.InvalidPhase, "No active rewind can complete.");
            }

            int nextSeed = randomSource.NextInt(1, int.MaxValue);
            if (nextSeed == session.DayStartSnapshot.OrderSeed)
            {
                nextSeed = nextSeed == int.MaxValue - 1 ? 1 : nextSeed + 1;
            }

            RewindOutcome outcome = RewindRules.Complete(
                session.DayStartSnapshot,
                session.RunState,
                nextSeed);
            var snapshot = new DayStartSnapshot(
                outcome.RestoredRunState,
                outcome.NextAttemptSeed,
                session.DayStartSnapshot.TimerDuration,
                session.DayStartSnapshot.ExpenseId,
                session.DayStartSnapshot.ExpenseValue,
                session.DayStartSnapshot.SaveVersion,
                session.DayStartSnapshot.CapturedAtUtc);
            var metadata = new ActiveAttemptMetadata(
                outcome.RestoredRunState.RunId,
                outcome.RestoredRunState.CurrentDayNumber,
                0,
                outcome.RestoredRunState.RewindCount,
                false,
                0,
                outcome.NextAttemptSeed);
            SaveStateResult save = await saveRepository.SaveRunStateAsync(
                snapshot,
                metadata,
                cancellationToken);
            if (!save.Succeeded)
            {
                return RewindTransactionResult.Failure(RewindTransactionError.SaveFailed, save.ErrorMessage);
            }

            if (session.CanResumeInPlace) session.CompleteRewindInPlace(snapshot, metadata);
            else session.CompleteRewind(snapshot, metadata);
            SceneLoadResult scene = await sceneLoader.LoadAsync(SceneId.Shop, cancellationToken);
            return scene.Succeeded
                ? RewindTransactionResult.Success(null)
                : RewindTransactionResult.Failure(RewindTransactionError.SceneLoadFailed, scene.ErrorMessage);
        }
    }
}
