using System;

namespace Rebellion.App
{
    /// <summary>
    /// Identifies the persistence operation whose storage failure needs acknowledgment.
    /// </summary>
    public enum PersistenceOperation
    {
        Save,
        Load,
    }

    /// <summary>
    /// Retains a concise storage-failure notice independently of the scene presenting it.
    /// </summary>
    public sealed class PersistenceFailureNotice
    {
        public string Message { get; private set; }
        public bool IsPending => Message != null;
        public event Action Changed;

        /// <summary>
        /// Publishes a user-facing failure without exposing exception details or local file paths.
        /// </summary>
        /// <param name="operation">The operation that could not complete.</param>
        public void Show(PersistenceOperation operation)
        {
            Message = operation switch
            {
                PersistenceOperation.Save => "The game could not be saved. Please try again.",
                PersistenceOperation.Load =>
                    "The saved game could not be loaded. Please try again.",
                _ => throw new ArgumentOutOfRangeException(nameof(operation)),
            };
            Changed?.Invoke();
        }

        /// <summary>
        /// Dismisses the pending failure after acknowledgment.
        /// </summary>
        public void Acknowledge()
        {
            if (!IsPending)
                return;
            Message = null;
            Changed?.Invoke();
        }
    }
}
