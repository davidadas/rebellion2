using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Rebellion.Util.IO
{
    /// <summary>
    /// Orders directory operations independently of scene and store-instance lifetimes.
    /// Worker operations must contain filesystem work without returning to a waiting caller's context.
    /// </summary>
    internal static class SerialFileOperations
    {
        private static readonly object _gate = new object();
        private static readonly Dictionary<string, DirectoryQueue> _queues = new(
            Path.DirectorySeparatorChar == '\\'
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal
        );

        /// <summary>
        /// Reserves the next directory operation, retaining only unfinished operations.
        /// </summary>
        /// <param name="directory">The directory owning the operation's files.</param>
        /// <returns>The reservation whose disposal releases subsequent operations.</returns>
        internal static Reservation Reserve(string directory)
        {
            string key = GetDirectoryKey(directory);
            lock (_gate)
            {
                if (!_queues.TryGetValue(key, out DirectoryQueue queue))
                {
                    queue = new DirectoryQueue();
                    _queues.Add(key, queue);
                }
                Reservation reservation = new Reservation(key, queue.Tail);
                queue.Pending++;
                queue.Tail = reservation.Completion;
                return reservation;
            }
        }

        /// <summary>
        /// Executes a store operation on its caller after earlier directory work finishes.
        /// </summary>
        /// <typeparam name="T">The operation's result type.</typeparam>
        /// <param name="directory">The directory owning the operation's files.</param>
        /// <param name="operation">The operation performed on the calling thread.</param>
        /// <returns>The operation's result.</returns>
        internal static T Run<T>(string directory, Func<T> operation)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));
            using Reservation reservation = Reserve(directory);
            reservation.Wait();
            return operation();
        }

        /// <summary>
        /// Canonicalizes directory aliases while retaining filesystem roots.
        /// </summary>
        /// <param name="directory">The directory to canonicalize.</param>
        /// <returns>The absolute directory key without a redundant trailing separator.</returns>
        private static string GetDirectoryKey(string directory)
        {
            string path = Path.GetFullPath(directory);
            return path.Length == Path.GetPathRoot(path).Length
                ? path
                : path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        /// <summary>
        /// Retains only a directory's unfinished operation barriers.
        /// </summary>
        private sealed class DirectoryQueue
        {
            internal Task Tail = Task.CompletedTask;
            internal int Pending;

            /// <summary>
            /// Starts an empty directory queue.
            /// </summary>
            internal DirectoryQueue() { }
        }

        /// <summary>
        /// Transfers directory ownership from synchronous preparation to optional worker publication.
        /// Its completion is an ordering barrier; the operation's caller separately owns any failure.
        /// </summary>
        internal sealed class Reservation : IDisposable
        {
            private readonly string _key;
            private readonly Task _preceding;
            private readonly TaskCompletionSource<bool> _completion = new(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
            private int _disposed;

            internal Task Completion => _completion.Task;

            /// <summary>
            /// Retains one directory position after its preceding operation.
            /// </summary>
            /// <param name="key">The canonical directory key.</param>
            /// <param name="preceding">The preceding operation's release barrier.</param>
            internal Reservation(string key, Task preceding)
            {
                _key = key;
                _preceding = preceding;
            }

            /// <summary>
            /// Waits without depending on the caller's synchronization context.
            /// </summary>
            internal void Wait()
            {
                _preceding.GetAwaiter().GetResult();
            }

            /// <summary>
            /// Runs filesystem work on a worker, releasing ownership after cleanup.
            /// </summary>
            /// <param name="operation">The filesystem work, which must not call its waiting thread.</param>
            /// <returns>The operation's completion or original failure.</returns>
            internal Task RunAsync(Action operation)
            {
                if (operation == null)
                    throw new ArgumentNullException(nameof(operation));
                return Task.Run(async () =>
                {
                    try
                    {
                        await _preceding.ConfigureAwait(false);
                        operation();
                    }
                    finally
                    {
                        Dispose();
                    }
                });
            }

            /// <summary>
            /// Releases this position once, including after a failed operation.
            /// </summary>
            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0)
                    return;
                if (_preceding.IsCompleted)
                    Release();
                else
                    _ = _preceding.ContinueWith(
                        _ => Release(),
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default
                    );
            }

            /// <summary>
            /// Releases the barrier only after the preceding directory position has finished.
            /// </summary>
            private void Release()
            {
                lock (_gate)
                {
                    DirectoryQueue queue = _queues[_key];
                    queue.Pending--;
                    if (queue.Pending == 0)
                        _queues.Remove(_key);
                }
                _completion.TrySetResult(true);
            }
        }
    }
}
