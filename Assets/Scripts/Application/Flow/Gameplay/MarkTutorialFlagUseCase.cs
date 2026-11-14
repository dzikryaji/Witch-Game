using System;
using System.Threading;
using System.Threading.Tasks;
using Witch.Application.Ports;
using Witch.Domain.Progression;

namespace Witch.Application.Flow
{
    public sealed class MarkTutorialFlagUseCase
    {
        private readonly IRunStateTransactionRepository saveRepository;

        public MarkTutorialFlagUseCase(IRunStateTransactionRepository saveRepository)
        {
            this.saveRepository = saveRepository ?? throw new ArgumentNullException(nameof(saveRepository));
        }

        public async Task<SaveStateResult> ExecuteAsync(
            GameSession session,
            string flagId,
            CancellationToken cancellationToken = default)
        {
            if (session == null)
            {
                return SaveStateResult.Failure(SaveStateError.InvalidState, "Session is required.");
            }

            if (string.IsNullOrWhiteSpace(flagId))
            {
                return SaveStateResult.Failure(SaveStateError.InvalidState, "Tutorial flag ID is required.");
            }

            session.ApplyTutorialFlag(flagId, true);
            return await saveRepository.SaveRunStateAsync(
                session.DayStartSnapshot,
                session.AttemptMetadata,
                cancellationToken);
        }
    }
}
