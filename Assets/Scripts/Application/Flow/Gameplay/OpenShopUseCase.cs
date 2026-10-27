using Witch.Domain;

namespace Witch.Application.Flow
{
    public sealed class OpenShopUseCase
    {
        public PhaseTransitionResult Execute(GameSession session)
        {
            if (session == null)
            {
                return PhaseTransitionResult.Failure(PhaseTransitionError.MissingSession);
            }

            return session.TryTransition(GamePhase.CustomerArrival);
        }
    }
}
