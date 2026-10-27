using System;
using Witch.Application.Ports;

namespace Witch.Application.Flow
{
    public sealed class SetShopTimerPausedUseCase
    {
        private readonly IClock clock;

        public SetShopTimerPausedUseCase(IClock clock)
        {
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public void Execute(GameSession session, bool paused)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            session.SetTimerPaused(paused, clock.UtcNow);
        }
    }
}
