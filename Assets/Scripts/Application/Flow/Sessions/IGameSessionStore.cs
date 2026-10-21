namespace Witch.Application.Flow
{
    public interface IGameSessionStore
    {
        GameSession ActiveSession { get; }

        void SetActive(GameSession session);

        void Clear();
    }
}
