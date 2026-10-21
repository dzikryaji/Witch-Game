using Witch.Domain;

namespace Witch.Application.Flow
{
    public sealed class OpenShopUseCase
    {
        public GameFlowResult Execute(GameSession session)
        {
            if (session == null)
            {
                return GameFlowResult.Failure(GameFlowError.MissingSession);
            }

            return session.TryTransition(GamePhase.CustomerEncounter);
        }
    }
}
