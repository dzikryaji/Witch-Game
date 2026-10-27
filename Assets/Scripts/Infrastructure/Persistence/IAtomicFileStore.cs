namespace Witch.Infrastructure.Persistence
{
    public interface IAtomicFileStore
    {
        bool Exists(string path);
        string ReadAllText(string path);
        void WriteAtomically(string primaryPath, string previousPath, string contents);
    }
}
