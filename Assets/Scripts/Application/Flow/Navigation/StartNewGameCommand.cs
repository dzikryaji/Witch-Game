namespace Witch.Application.Flow
{
    public sealed class StartNewGameCommand
    {
        public StartNewGameCommand(string runId)
        {
            RunId = runId;
        }

        public string RunId { get; }
    }
}
