namespace Witch.Application.Flow
{
    public sealed class LoadRunCommand
    {
        private LoadRunCommand(bool loadLatest, string runId)
        {
            LoadLatest = loadLatest;
            RunId = runId;
        }

        public bool LoadLatest { get; }

        public string RunId { get; }

        public static LoadRunCommand Latest()
        {
            return new LoadRunCommand(true, null);
        }

        public static LoadRunCommand ByRunId(string runId)
        {
            return new LoadRunCommand(false, runId);
        }
    }
}
