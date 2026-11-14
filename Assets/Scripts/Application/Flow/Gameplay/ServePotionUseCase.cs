using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly IGameCenterService gameCenter;
        private readonly IReadOnlyDictionary<string, string> herbCategoryIds;
        private bool firstRemedyReported;
        private bool fiveArtsReported;

        public ServePotionUseCase(PotionServiceRules rules)
            : this(rules, null, null, null)
        {
        }

        public ServePotionUseCase(
            PotionServiceRules rules,
            IActiveAttemptMetadataRepository attemptRepository = null,
            IGameCenterService gameCenter = null,
            IReadOnlyDictionary<string, string> herbCategoryIds = null)
        {
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
            this.attemptRepository = attemptRepository;
            this.gameCenter = gameCenter;
            this.herbCategoryIds = herbCategoryIds ?? new Dictionary<string, string>(0);
            this.firstRemedyReported = false;
            this.fiveArtsReported = false;
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

            if (session.PreparedPotion == null)
            {
                return ServePotionResult.Failure("A prepared potion is required.");
            }

            ServiceOutcome outcome = rules.Serve(
                session.RunState,
                session.AttemptState.ActiveOrder,
                session.PreparedPotion);

            string category = CategoryForPotion(session);
            session.CommitService(outcome);

            ReportAfterServe(session, outcome, category);

            return ServePotionResult.Success(outcome);
        }

        public async Task<ServePotionResult> ExecuteAsync(
            GameSession session,
            CancellationToken cancellationToken = default)
        {
            if (session == null)
            {
                return ServePotionResult.Failure("Session is required.");
            }

            if (session.CurrentPhase != GamePhase.CustomerEncounter || session.ServiceCommitted)
            {
                return ServePotionResult.Failure("No uncommitted customer encounter is active.");
            }

            if (session.PreparedPotion == null)
            {
                return ServePotionResult.Failure("A prepared potion is required.");
            }

            ServiceOutcome outcome = rules.Serve(
                session.RunState,
                session.AttemptState.ActiveOrder,
                session.PreparedPotion);

            // Always persist ServiceCountToday after any service,
            // so Clean Ledger tracks served customers regardless of outcome kind
            if (attemptRepository != null)
            {
                var metadata = new ActiveAttemptMetadata(
                    outcome.UpdatedRunState.RunId,
                    outcome.UpdatedRunState.CurrentDayNumber,
                    outcome.UpdatedRunState.Suspicion,
                    outcome.UpdatedRunState.RewindCount,
                    false,
                    0,
                    session.AttemptMetadata.ActiveOrderSeed,
                    session.AttemptMetadata.WrongServiceCountToday + (outcome.Kind == ServiceOutcomeKind.WrongValid ? 1 : 0),
                    session.AttemptMetadata.ServiceCountToday + 1,
                    session.AttemptMetadata.MinigamePlayedToday);
                SaveStateResult save = await attemptRepository.SaveActiveAttemptMetadataAsync(
                    metadata,
                    cancellationToken);
                if (!save.Succeeded)
                {
                    // Save failure doesn't fail gameplay
                }
            }

            string category = CategoryForPotion(session);
            session.CommitService(outcome);

            ReportAfterServe(session, outcome, category);

            return ServePotionResult.Success(outcome);
        }

        private void ReportAfterServe(GameSession session, ServiceOutcome outcome, string category)
        {
            if (outcome.Kind != ServiceOutcomeKind.Correct || category == null)
            {
                return;
            }

            session.MarkCategoryServed(category);
            if (gameCenter == null)
            {
                return;
            }

            ReportFirstRemedy();
            ReportFiveArts(session);
        }

        private void ReportFirstRemedy()
        {
            if (firstRemedyReported)
            {
                return;
            }

            firstRemedyReported = true;
            _ = GameCenterReporting.ReportQuietlyAsync(gameCenter, GameCenterAchievementIds.FirstRemedy, 100d);
        }

        private void ReportFiveArts(GameSession session)
        {
            if (fiveArtsReported)
            {
                return;
            }

            int totalCategories = herbCategoryIds.Values.Distinct(StringComparer.Ordinal).Count();
            int servedCategories = session.RunState.ServedCategoryIds
                .Distinct(StringComparer.Ordinal)
                .Count();
            if (totalCategories == 0 || servedCategories < totalCategories)
            {
                return;
            }

            fiveArtsReported = true;
            _ = GameCenterReporting.ReportQuietlyAsync(gameCenter, GameCenterAchievementIds.FiveArts, 100d);
        }

        private string CategoryForPotion(GameSession session)
        {
            PreparedPotion potion = session.PreparedPotion;
            if (potion == null)
            {
                return null;
            }

            HerbPair pair = potion.HerbPair;
            if (!herbCategoryIds.TryGetValue(pair.FirstHerbId, out string category))
            {
                return null;
            }

            return category;
        }
    }
}
