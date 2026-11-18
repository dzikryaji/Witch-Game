using System;
using System.Threading;
using System.Threading.Tasks;
using Witch.Application.Ports;
using Witch.Domain;
using Witch.Domain.Progression;

namespace Witch.Application.Flow
{
    public sealed class RestartDayUseCase
    {
        private readonly IRunStateTransactionRepository saveRepository;
        private readonly ISceneLoader sceneLoader;

        public RestartDayUseCase(IRunStateTransactionRepository saveRepository, ISceneLoader sceneLoader)
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

            if (session.CurrentPhase != GamePhase.CustomerArrival
                && session.CurrentPhase != GamePhase.CustomerEncounter
                && session.CurrentPhase != GamePhase.ClosingEncounter)
            {
                return ExpenseResolutionResult.Failure(ExpenseResolutionError.InvalidPhase, "The day can only be restarted while the shop is open.");
            }

            // Exact day-start values: money, suspicion and minigame win count as captured at the start of the day.
            RunState restartState = session.DayStartSnapshot.RunState;
            if (restartState.CurrentDayNumber == 1)
            {
                restartState = restartState.WithTutorialFlag("shop.tutorial.day1.guided", true);
            }

            var snapshot = new DayStartSnapshot(
                restartState,
                session.DayStartSnapshot.OrderSeed,
                session.DayStartSnapshot.TimerDuration,
                session.DayStartSnapshot.ExpenseId,
                session.DayStartSnapshot.ExpenseValue,
                session.DayStartSnapshot.SaveVersion,
                session.DayStartSnapshot.CapturedAtUtc);

            // Daily counters start from zero again. 'default' fits whatever type each counter uses.
            var metadata = new ActiveAttemptMetadata(
                restartState.RunId,
                restartState.CurrentDayNumber,
                restartState.Suspicion,
                restartState.MinigameWinCount,
                false,
                0,
                snapshot.OrderSeed,
                default,
                default,
                default,
                0,
                0,
                0);

            SaveStateResult save = await saveRepository.SaveRunStateAsync(snapshot, metadata, cancellationToken);
            if (!save.Succeeded)
            {
                return ExpenseResolutionResult.Failure(ExpenseResolutionError.SaveFailed, save.ErrorMessage);
            }

            session.RestartDay(snapshot, metadata);
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