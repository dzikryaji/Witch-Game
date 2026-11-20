
using System;
using System.Threading;
using System.Threading.Tasks;
using Witch.Application.Ports;
using Witch.Domain;
using Witch.Domain.Progression;

namespace Witch.Application.Flow
{
    public sealed class AbandonMinigameUseCase
    {
        private readonly IRunStateTransactionRepository saveRepository;

        public AbandonMinigameUseCase(IRunStateTransactionRepository saveRepository)
        {
            this.saveRepository = saveRepository
                ?? throw new ArgumentNullException(nameof(saveRepository));
        }

        public async Task<MinigameTransactionResult> ExecuteAsync(
            GameSession session,
            CancellationToken cancellationToken = default)
        {
            if (session == null)
            {
                return MinigameTransactionResult.Failure(
                    MinigameTransactionError.MissingSession,
                    "Session is required.");
            }

            if (session.CurrentPhase != GamePhase.MinigameActive
                || !session.AttemptMetadata.PendingMinigame)
            {
                return MinigameTransactionResult.Failure(
                    MinigameTransactionError.InvalidPhase,
                    "No active minigame to abandon.");
            }

            // Restore the exact state captured at the beginning of the day.
            RunState checkpoint = session.DayStartSnapshot.RunState;

            if (checkpoint.CurrentDayNumber == 1)
            {
                checkpoint = checkpoint.WithTutorialFlag(
                    "shop.tutorial.day1.guided", true);
            }

            var snapshot = new DayStartSnapshot(
                checkpoint,
                session.DayStartSnapshot.OrderSeed,
                session.DayStartSnapshot.TimerDuration,
                session.DayStartSnapshot.ExpenseId,
                session.DayStartSnapshot.ExpenseValue,
                session.DayStartSnapshot.SaveVersion,
                session.DayStartSnapshot.CapturedAtUtc);

            // Clear the pending minigame and reset daily counters.
            var metadata = new ActiveAttemptMetadata(
                checkpoint.RunId,
                checkpoint.CurrentDayNumber,
                checkpoint.Suspicion,
                checkpoint.MinigameWinCount,
                false,
                0,
                snapshot.OrderSeed,
                default,
                default,
                default,
                0,
                0,
                0);

            SaveStateResult save = await saveRepository.SaveRunStateAsync(
                snapshot,
                metadata,
                cancellationToken);

            if (!save.Succeeded)
            {
                return MinigameTransactionResult.Failure(
                    MinigameTransactionError.SaveFailed,
                    save.ErrorMessage);
            }

            return MinigameTransactionResult.Success(null);
        }
    }
}
