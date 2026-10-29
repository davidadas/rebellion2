using System;
using System.Collections.Generic;
using Rebellion.Game.Results;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Captures and restores the owner's existing runtime facts at a writable recovery boundary.
    /// </summary>
    public partial class ManufacturingCommands
    {
        /// <summary>
        /// Captures manufacturing facts awaiting the next manufacturing phase.
        /// </summary>
        /// <returns>The pending facts without consuming or publishing them.</returns>
        internal List<GameResult> CaptureRecoveryResults() => new List<GameResult>(_pendingResults);

        /// <summary>
        /// Restores manufacturing facts at their existing release boundary.
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
