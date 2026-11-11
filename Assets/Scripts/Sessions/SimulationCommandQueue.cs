using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Executes submitted commands in order only while the owning simulation is writable.
    /// The owner pumps this queue from its existing frame loop; no worker touches live state.
    /// </summary>
    internal sealed class SimulationCommandQueue : IDisposable
    {
        private readonly Func<bool> _canExecute;
        private readonly Queue<PendingCommand> _pending = new();
        private readonly int _ownerThread = Thread.CurrentThread.ManagedThreadId;
        private bool _executing;
        private bool _disposed;

        public Exception Failure { get; private set; }
        public bool IsExecuting => _executing;

        /// <summary>
        /// Connects command admission to the owner's tick and result-publication boundary.
        /// </summary>
        /// <param name="canExecute">Whether a command can currently mutate the live graph.</param>
        public SimulationCommandQueue(Func<bool> canExecute)
        {
            _canExecute = canExecute ?? throw new ArgumentNullException(nameof(canExecute));
        }

        /// <summary>
        /// Submits one synchronous mutation and completes it only after actual execution.
        /// </summary>
        /// <typeparam name="T">The command's completed result.</typeparam>
        /// <param name="command">The mutation to execute on the owning application thread.</param>
        /// <returns>The execution result, cancellation on replacement, or an execution failure.</returns>
        public Task<T> EnqueueAsync<T>(Func<T> command)
        {
            VerifyOwnerThread();
            if (command == null)
                throw new ArgumentNullException(nameof(command));
            if (_disposed)
                return Task.FromCanceled<T>(new CancellationToken(true));
            if (Failure != null)
                return Task.FromException<T>(Failure);
            PendingCommand<T> pending = new(command);
            _pending.Enqueue(pending);
            Pump();
            return pending.Completion;
        }

        /// <summary>
        /// Drains writable commands without allowing reentrant execution or skipping queued work.
        /// </summary>
        public void Pump()
        {
            VerifyOwnerThread();
            if (_disposed || Failure != null || _executing || _pending.Count == 0)
                return;
            _executing = true;
            try
            {
                while (!_disposed && Failure == null && _pending.Count > 0 && _canExecute())
                {
                    Exception failure = _pending.Dequeue().Execute();
                    if (failure == null)
                        continue;
                    Failure = failure;
                    CancelPendingCommands();
                }
            }
            finally
            {
                _executing = false;
            }
        }

        /// <summary>
        /// Invalidates commands from an outgoing graph before a replacement becomes active.
        /// </summary>
        public void Reset()
        {
            VerifyOwnerThread();
            if (_disposed)
                throw new ObjectDisposedException(nameof(SimulationCommandQueue));
            if (_executing)
                throw new InvalidOperationException(
                    "An executing command cannot replace its graph."
                );
            CancelPendingCommands();
            Failure = null;
        }

        /// <summary>
        /// Cancels unexecuted requests when the originating session ends.
        /// </summary>
        public void Dispose()
        {
            VerifyOwnerThread();
            if (_disposed)
                return;
            _disposed = true;
            CancelPendingCommands();
        }

        /// <summary>
        /// Completes every unstarted request without applying it to another graph.
        /// </summary>
        public void CancelPendingCommands()
        {
            VerifyOwnerThread();
            while (_pending.Count > 0)
                _pending.Dequeue().Cancel();
        }

        /// <summary>
        /// Rejects accidental worker access before any live state or queue is touched.
        /// </summary>
        private void VerifyOwnerThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != _ownerThread)
                throw new InvalidOperationException(
                    "Simulation commands must run on the owning thread."
                );
        }

        /// <summary>
        /// Represents one queued result independently of its concrete result type.
        /// </summary>
        private abstract class PendingCommand
        {
            /// <summary>
            /// Executes the mutation and captures any failure after execution may have begun.
            /// </summary>
            /// <returns>The execution failure, or null after successful completion.</returns>
            public abstract Exception Execute();

            /// <summary>
            /// Cancels a request that has not started execution.
            /// </summary>
            public abstract void Cancel();
        }

        /// <summary>
        /// Owns one typed command result without running continuations inside graph mutation.
        /// </summary>
        /// <typeparam name="T">The mutation's completed result.</typeparam>
        private sealed class PendingCommand<T> : PendingCommand
        {
            private readonly Func<T> _command;
            private readonly TaskCompletionSource<T> _completion = new(
                TaskCreationOptions.RunContinuationsAsynchronously
            );

            public Task<T> Completion => _completion.Task;

            /// <summary>
            /// Retains a mutation until the simulation permits its execution.
            /// </summary>
            /// <param name="command">The synchronous mutation owned by this request.</param>
            public PendingCommand(Func<T> command)
            {
                _command = command;
            }

            /// <summary>
            /// Applies the mutation exactly once and completes its typed outcome.
            /// </summary>
            /// <returns>The execution failure, or null when execution completed.</returns>
            public override Exception Execute()
            {
                try
                {
                    _completion.TrySetResult(_command());
                    return null;
                }
                catch (Exception exception)
                {
                    _completion.TrySetException(exception);
                    return exception;
                }
            }

            /// <summary>
            /// Cancels this unstarted command before its graph can be replaced or disposed.
            /// </summary>
            public override void Cancel() => _completion.TrySetCanceled();
        }
    }
}
