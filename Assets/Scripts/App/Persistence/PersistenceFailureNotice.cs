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
    /// Retains a pending storage failure independently of the scene presenting it.
    /// </summary>
    public sealed class PersistenceFailureNotice
    {
        public PersistenceOperation? Operation { get; private set; }
        public bool IsPending => Operation.HasValue;
        public event Action Changed;

        /// <summary>
        /// Publishes the failed operation for acknowledgment by the current scene.
        /// </summary>
        /// <param name="operation">The operation that could not complete.</param>
        public void Show(PersistenceOperation operation)
        {
            if (operation is not (PersistenceOperation.Save or PersistenceOperation.Load))
                throw new ArgumentOutOfRangeException(nameof(operation));
            Operation = operation;
            Changed?.Invoke();
        }

        /// <summary>
        /// Dismisses the pending failure after acknowledgment.
        /// </summary>
        public void Acknowledge()
        {
            if (!IsPending)
                return;
            Operation = null;
            Changed?.Invoke();
        }
    }
}
