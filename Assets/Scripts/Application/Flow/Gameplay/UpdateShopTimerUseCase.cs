using System;
using Witch.Application.Ports;

namespace Witch.Application.Flow
{
    public sealed class UpdateShopTimerUseCase
    {
        private readonly IClock clock;

        public UpdateShopTimerUseCase(IClock clock)
        {
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public void Execute(GameSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            session.RefreshTimer(clock.UtcNow);
        }
    }
}
