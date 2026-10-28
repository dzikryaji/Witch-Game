using System;
using System.Threading;
using System.Threading.Tasks;
using Witch.Application.Ports;
using Witch.Domain;
using Witch.Domain.Progression;

namespace Witch.Application.Flow
{
    public sealed class RetryDayUseCase
    {
        private readonly IRunStateTransactionRepository saveRepository;
        private readonly ISceneLoader sceneLoader;

        public RetryDayUseCase(IRunStateTransactionRepository saveRepository, ISceneLoader sceneLoader)
        {
            this.saveRepository = saveRepository ?? throw new ArgumentNullException(nameof(saveRepository));
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

            if (session.CurrentPhase != GamePhase.GameOver)
            {
                return ExpenseResolutionResult.Failure(ExpenseResolutionError.InvalidPhase, "Same-day retry requires game over.");
            }

            RunState retryState = session.DayStartSnapshot.RunState.WithProgress(
                session.DayStartSnapshot.RunState.CurrentDayNumber,
                session.DayStartSnapshot.RunState.Money,
                session.RunState.Suspicion,
                session.RunState.RewindCount);
            var snapshot = new DayStartSnapshot(
                retryState,
                session.DayStartSnapshot.OrderSeed,
                session.DayStartSnapshot.TimerDuration,
                session.DayStartSnapshot.ExpenseId,
                session.DayStartSnapshot.ExpenseValue,
                session.DayStartSnapshot.SaveVersion,
                session.DayStartSnapshot.CapturedAtUtc);
            var metadata = new ActiveAttemptMetadata(
                retryState.RunId,
                retryState.CurrentDayNumber,
                retryState.Suspicion,
                retryState.RewindCount,
                false,
                0,
                snapshot.OrderSeed);
            SaveStateResult save = await saveRepository.SaveRunStateAsync(
                snapshot,
                metadata,
                cancellationToken);
            if (!save.Succeeded)
            {
                return ExpenseResolutionResult.Failure(ExpenseResolutionError.SaveFailed, save.ErrorMessage);
            }

            session.RetryDay(snapshot, metadata);
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
