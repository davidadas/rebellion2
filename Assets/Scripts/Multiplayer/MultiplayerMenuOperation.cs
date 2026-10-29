using System;
using System.Threading;
using System.Threading.Tasks;

namespace Rebellion.Multiplayer
{
    /// <summary>
    /// Identifies the form that owns an asynchronous menu command and its error.
    /// </summary>
    internal enum MultiplayerMenuPage
    {
        Browser,
        Create,
        Password,
        Save,
        Authentication,
    }

    /// <summary>
    /// Owns cancellable form work and ignores completions belonging to dismissed operations.
    /// </summary>
    internal sealed class MultiplayerMenuOperation : IDisposable
    {
        private CancellationTokenSource _active;
        public bool IsBusy => _active != null;
        public MultiplayerMenuPage Page { get; private set; }
        public Exception Error { get; private set; }
        public event Action Changed;

        /// <summary>
        /// Runs one form command without allowing an older completion to affect a newer form.
        /// </summary>
        /// <param name="page">The form owning this command.</param>
        /// <param name="operation">The cancellable command.</param>
        /// <returns>The pending command.</returns>
        public async Task RunAsync(
            MultiplayerMenuPage page,
            Func<CancellationToken, Task> operation
        )
        {
            if (IsBusy)
                return;
            CancellationTokenSource owner = new();
            CancellationToken token = owner.Token;
            _active = owner;
            Page = page;
            Error = null;
            Changed?.Invoke();
            try
            {
                await operation(token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception exception)
            {
                if (!token.IsCancellationRequested)
                    Error = exception;
            }
            finally
            {
                if (ReferenceEquals(_active, owner))
                {
                    _active = null;
                    owner.Dispose();
                    Changed?.Invoke();
                }
            }
        }

        /// <summary>
        /// Cancels the current form while allowing a replacement command to start immediately.
        /// </summary>
        public void Cancel()
        {
            CancellationTokenSource owner = _active;
            _active = null;
            Error = null;
            owner?.Cancel();
            owner?.Dispose();
            Changed?.Invoke();
        }

        /// <summary>
        /// Releases the active command when its presentation owner is destroyed.
        /// </summary>
        public void Dispose()
        {
            Cancel();
        }
    }
}
