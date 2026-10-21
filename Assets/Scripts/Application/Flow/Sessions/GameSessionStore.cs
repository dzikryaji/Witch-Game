using System;

namespace Witch.Application.Flow
{
    public sealed class GameSessionStore : IGameSessionStore
    {
        public GameSession ActiveSession { get; private set; }

        public void SetActive(GameSession session)
        {
            ActiveSession = session ?? throw new ArgumentNullException(nameof(session));
        }

        public void Clear()
        {
            ActiveSession = null;
        }
    }
}
