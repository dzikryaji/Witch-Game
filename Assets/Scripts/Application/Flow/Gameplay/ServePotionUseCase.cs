using System;
using System.Threading;
using System.Threading.Tasks;
using Witch.Application.Ports;
using Witch.Domain;
using Witch.Domain.Progression;
using Witch.Domain.Potions;

namespace Witch.Application.Flow
{
    public sealed class ServePotionUseCase
    {
        private readonly PotionServiceRules rules;
        private readonly IActiveAttemptMetadataRepository attemptRepository;

        public ServePotionUseCase(PotionServiceRules rules)
            : this(rules, null)
        {
        }

        public ServePotionUseCase(
            PotionServiceRules rules,
            IActiveAttemptMetadataRepository attemptRepository)
        {
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
            this.attemptRepository = attemptRepository;
        }

        public ServePotionResult Execute(GameSession session)
        {
            if (session == null)
            {
                return ServePotionResult.Failure("Session is required.");
            }

            if (session.CurrentPhase != GamePhase.CustomerEncounter || session.ServiceCommitted)
            {
                return ServePotionResult.Failure("No uncommitted customer encounter is active.");
            }

            if (session.PreparedRecipe == null)
            {
                return ServePotionResult.Failure("A prepared recipe is required.");
            }

            ServiceOutcome outcome = rules.Serve(
                session.RunState,
                session.AttemptState.ActiveOrder,
                session.PreparedRecipe);
            session.CommitService(outcome);
            return ServePotionResult.Success(outcome);
        }

        public async Task<ServePotionResult> ExecuteAsync(
            GameSession session,
            CancellationToken cancellationToken = default)
        {
            ServePotionResult validation = Validate(session);
            if (validation != null)
            {
                return validation;
            }

            ServiceOutcome outcome = rules.Serve(
                session.RunState,
                session.AttemptState.ActiveOrder,
                session.PreparedRecipe);
            if (outcome.Kind == ServiceOutcomeKind.WrongValid)
            {
                if (attemptRepository == null)
                {
                    return ServePotionResult.Failure("Persistent attempt metadata repository is required for wrong service.");
                }

                var metadata = new ActiveAttemptMetadata(
                    outcome.UpdatedRunState.RunId,
                    outcome.UpdatedRunState.CurrentDayNumber,
                    outcome.UpdatedRunState.Suspicion,
                    outcome.UpdatedRunState.RewindCount,
                    false,
                    0,
                    session.AttemptMetadata.ActiveOrderSeed);
                SaveStateResult save = await attemptRepository.SaveActiveAttemptMetadataAsync(
                    metadata,
                    cancellationToken);
                if (!save.Succeeded)
                {
                    return ServePotionResult.Failure(save.ErrorMessage);
                }
            }

            session.CommitService(outcome);
            return ServePotionResult.Success(outcome);
        }

        private static ServePotionResult Validate(GameSession session)
        {
            if (session == null)
            {
                return ServePotionResult.Failure("Session is required.");
            }

            if (session.CurrentPhase != GamePhase.CustomerEncounter || session.ServiceCommitted)
            {
                return ServePotionResult.Failure("No uncommitted customer encounter is active.");
            }

            return session.PreparedRecipe == null
                ? ServePotionResult.Failure("A prepared recipe is required.")
                : null;
        }
    }
}
