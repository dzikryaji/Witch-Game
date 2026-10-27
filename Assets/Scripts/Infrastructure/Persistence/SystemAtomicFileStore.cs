using System.IO;
using System.Text;

namespace Witch.Infrastructure.Persistence
{
    public sealed class SystemAtomicFileStore : IAtomicFileStore
    {
        public bool Exists(string path)
        {
            return File.Exists(path);
        }

        public string ReadAllText(string path)
        {
            return File.ReadAllText(path, Encoding.UTF8);
        }

        public void WriteAtomically(string primaryPath, string previousPath, string contents)
        {
            string directory = Path.GetDirectoryName(primaryPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string temporaryPath = primaryPath + ".tmp";
            try
            {
                using (var stream = new FileStream(
                           temporaryPath,
                           FileMode.Create,
                           FileAccess.Write,
                           FileShare.None))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.Write(contents);
                    writer.Flush();
                    stream.Flush(true);
                }

                if (File.Exists(primaryPath))
                {
                    if (File.Exists(previousPath))
                    {
                        File.Delete(previousPath);
                    }

                    File.Replace(temporaryPath, primaryPath, previousPath);
                }
                else
                {
                    File.Move(temporaryPath, primaryPath);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }
    }
}
