using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Rebellion.Util.IO
{
    /// <summary>
    /// Publishes complete text documents using owned temporary files and a destination-scoped process lease.
    /// </summary>
    public static class AtomicTextFile
    {
        /// <summary>
        /// Reads one complete published document while allowing another process to replace its file entry.
        /// </summary>
        /// <param name="path">The published document path.</param>
        /// <returns>The complete document opened by this reader.</returns>
        public static string ReadAllText(string path)
        {
            using FileStream stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read | FileShare.Delete
            );
            using StreamReader reader = new StreamReader(stream, Encoding.UTF8, true);
            return reader.ReadToEnd();
        }

        /// <summary>
        /// Writes an owned sibling file and atomically publishes the complete document.
        /// Overlapping publishers retain the complete document from the last successful publication.
        /// </summary>
        /// <param name="path">The destination document path.</param>
        /// <param name="contents">The complete replacement document.</param>
        public static void WriteAllText(string path, string contents)
        {
            string destination = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            string temporaryPath = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            bool ownsTemporaryFile = false;
            try
            {
                using (
                    FileStream stream = new FileStream(
                        temporaryPath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None
                    )
                )
                {
                    ownsTemporaryFile = true;
                    using (
                        StreamWriter writer = new StreamWriter(
                            stream,
                            new UTF8Encoding(false),
                            1024,
                            true
                        )
                    )
                    {
                        writer.Write(contents);
                        writer.Flush();
                    }
                    stream.Flush(true);
                }
                WithPublicationLock(
                    destination,
                    () =>
                    {
                        if (File.Exists(destination))
                            File.Replace(temporaryPath, destination, null);
                        else
                            File.Move(temporaryPath, destination);
                    }
                );
                ownsTemporaryFile = false;
            }
            finally
            {
                if (ownsTemporaryFile && File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        /// <summary>
        /// Produces and publishes a document while preventing another publisher from replacing its inputs.
        /// </summary>
        /// <param name="path">The destination document path.</param>
        /// <param name="getContents">Produces the complete replacement while holding the destination lease.</param>
        public static void UpdateAllText(string path, Func<string> getContents)
        {
            if (getContents == null)
                throw new ArgumentNullException(nameof(getContents));
            string destination = Path.GetFullPath(path);
            WithPublicationLock(destination, () => WriteAllText(destination, getContents()));
        }

        /// <summary>
        /// Serializes operations sharing a destination across threads and processes.
        /// </summary>
        /// <param name="destination">The canonical destination path.</param>
        /// <param name="operation">The synchronous operation protected by the destination lease.</param>
        private static void WithPublicationLock(string destination, Action operation)
        {
            using Mutex publication = new Mutex(false, GetPublicationMutexName(destination));
            bool ownsPublication = false;
            try
            {
                try
                {
                    publication.WaitOne();
                }
                catch (AbandonedMutexException)
                {
                    // An abandoned lease is acquired by the waiter after its former process exits.
                }
                ownsPublication = true;
                operation();
            }
            finally
            {
                if (ownsPublication)
                    publication.ReleaseMutex();
            }
        }

        /// <summary>
        /// Identifies the shared publication lease without exposing a path or exceeding mutex name limits.
        /// </summary>
        /// <param name="destination">The canonical destination path.</param>
        /// <returns>The process-shared publication mutex name.</returns>
        private static string GetPublicationMutexName(string destination)
        {
            string identity =
                Path.DirectorySeparatorChar == '\\' ? destination.ToUpperInvariant() : destination;
            using SHA256 algorithm = SHA256.Create();
            string digest = BitConverter
                .ToString(algorithm.ComputeHash(Encoding.UTF8.GetBytes(identity)))
                .Replace("-", string.Empty);
            return "Rebellion.AtomicTextFile." + digest;
        }
    }
}
