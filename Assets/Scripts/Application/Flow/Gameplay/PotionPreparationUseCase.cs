using System;
using Witch.Domain;
using Witch.Domain.Potions;

namespace Witch.Application.Flow
{
    public sealed class PotionPreparationUseCase
    {
        private readonly PotionServiceRules rules;

        public PotionPreparationUseCase(PotionServiceRules rules)
        {
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        public PreparationResult Grind(GameSession session, string herbId)
        {
            if (!CanPrepare(session, out PreparationResult failure))
            {
                return failure;
            }

            if (string.IsNullOrWhiteSpace(herbId))
            {
                return PreparationResult.Failure("Herb ID is required.");
            }

            session.SetGroundHerb(herbId);
            return PreparationResult.Success(PreparationResultKind.Ground);
        }

        public PreparationResult AddToPot(GameSession session)
        {
            if (!CanPrepare(session, out PreparationResult failure))
            {
                return failure;
            }

            try
            {
                session.AddGroundHerbToPot();
                return PreparationResult.Success(PreparationResultKind.AddedToPot);
            }
            catch (InvalidOperationException exception)
            {
                return PreparationResult.Failure(exception.Message);
            }
            catch (ArgumentException exception)
            {
                return PreparationResult.Failure(exception.Message);
            }
        }

        public PreparationResult DiscardGroundHerb(GameSession session)
        {
            if (!CanPrepare(session, out PreparationResult failure))
            {
                return failure;
            }

            if (string.IsNullOrWhiteSpace(session.GroundHerbId))
            {
                return PreparationResult.Failure("There is no ground herb to discard.");
            }

            session.ClearGroundHerb();
            return PreparationResult.Success(PreparationResultKind.GroundDiscarded);
        }

        public PreparationResult UndoLastPotIngredient(GameSession session)
        {
            if (!CanPrepare(session, out PreparationResult failure))
            {
                return failure;
            }

            try
            {
                session.RemoveLastPotIngredient();
                return PreparationResult.Success(PreparationResultKind.RemovedFromPot);
            }
            catch (InvalidOperationException exception)
            {
                return PreparationResult.Failure(exception.Message);
            }
        }

        public PreparationResult Brew(GameSession session)
        {
            if (!CanPrepare(session, out PreparationResult failure))
            {
                return failure;
            }

            if (!session.PotionDraft.IsReadyToBrew)
            {
                return PreparationResult.Failure("Two different ground herbs are required.");
            }

            var pair = new HerbPair(
                session.PotionDraft.Ingredients[0].HerbId,
                session.PotionDraft.Ingredients[1].HerbId);
            BrewOutcome outcome = rules.Brew(pair);
            if (outcome.Kind == BrewOutcomeKind.InvalidRecipe)
            {
                session.ClearWorkbench();
                return PreparationResult.Success(PreparationResultKind.InvalidRecipeDiscarded);
            }

            session.SetPreparedRecipe(outcome.Recipe);
            return PreparationResult.Success(PreparationResultKind.Prepared);
        }

        public PreparationResult Discard(GameSession session)
        {
            if (session == null)
            {
                return PreparationResult.Failure("Session is required.");
            }

            session.ClearWorkbench();
            return PreparationResult.Success(PreparationResultKind.Discarded);
        }

        private static bool CanPrepare(GameSession session, out PreparationResult failure)
        {
            if (session == null)
            {
                failure = PreparationResult.Failure("Session is required.");
                return false;
            }

            if (session.CurrentPhase != GamePhase.CustomerEncounter || session.ServiceCommitted)
            {
                failure = PreparationResult.Failure("Potion preparation requires an active customer encounter.");
                return false;
            }

            failure = null;
            return true;
        }
    }
}
