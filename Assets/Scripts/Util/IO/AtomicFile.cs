using System;
using System.IO;
using System.Threading.Tasks;

namespace Rebellion.Util.IO
{
    /// <summary>
    /// Captures a complete file on the calling thread and publishes its owned bytes on a worker.
    /// </summary>
    internal static class AtomicFile
    {
        /// <summary>
        /// Writes a complete sibling payload before returning its ordered durable publication task.
        /// Only file handles, paths, and the supplied filesystem continuation cross to the worker.
        /// </summary>
        /// <param name="path">The final payload path.</param>
        /// <param name="write">Writes the payload synchronously on the calling thread.</param>
        /// <param name="afterPublication">Optional filesystem work included in ordered completion.</param>
        /// <returns>The durable publication, subsequent filesystem work, and owned-file cleanup.</returns>
        internal static Task WriteAsync(
            string path,
            Action<Stream> write,
            Action afterPublication = null
        )
        {
            if (write == null)
                throw new ArgumentNullException(nameof(write));
            string destination = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(destination);
            SerialFileOperations.Reservation reservation = SerialFileOperations.Reserve(directory);
            string temporaryPath = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            FileStream stream = null;
            try
            {
                reservation.Wait();
                stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 64 * 1024
                );
                write(stream);
                FileStream ownedStream = stream;
                return reservation.RunAsync(() =>
                {
                    try
                    {
                        using (ownedStream)
                            ownedStream.Flush(true);
                        if (File.Exists(destination))
                            File.Replace(temporaryPath, destination, null);
                        else
                            File.Move(temporaryPath, destination);
                        afterPublication?.Invoke();
                    }
                    finally
                    {
                        try
                        {
                            ownedStream.Dispose();
                        }
                        finally
                        {
                            if (File.Exists(temporaryPath))
                                File.Delete(temporaryPath);
                        }
                    }
                });
            }
            catch
            {
                try
                {
                    if (stream != null)
                    {
                        try
                        {
                            stream.Dispose();
                        }
                        finally
                        {
                            if (File.Exists(temporaryPath))
                                File.Delete(temporaryPath);
                        }
                    }
                }
                finally
                {
                    reservation.Dispose();
                }
                throw;
            }
        }
    }
}
