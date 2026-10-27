using System;
using Witch.Application.Ports;

namespace Witch.Application.Flow
{
    public sealed class StopShopTimerUseCase
    {
        private readonly IClock clock;

        public StopShopTimerUseCase(IClock clock)
        {
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public void Execute(GameSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            session.StopTimer(clock.UtcNow);
        }
    }
}
