using System;
using System.Collections.Generic;
using Rebellion.Game.Results;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Captures and restores the owner's existing runtime facts at a writable recovery boundary.
    /// </summary>
    public partial class MovementCommands
    {
        /// <summary>
        /// Captures movement facts awaiting the next movement phase without consuming them.
        /// </summary>
        /// <returns>The pending facts in their release order.</returns>
        internal List<GameResult> CaptureRecoveryResults() => new List<GameResult>(_pendingResults);

        /// <summary>
        /// Restores movement facts without publishing their reactions or messages.
        /// </summary>
        /// <param name="results">The captured ordered facts.</param>
        internal void RestoreRecoveryResults(IReadOnlyList<GameResult> results)
        {
            if (results == null)
                throw new ArgumentNullException(nameof(results));
            _pendingResults.Clear();
            _pendingResults.AddRange(results);
        }
    }
}
