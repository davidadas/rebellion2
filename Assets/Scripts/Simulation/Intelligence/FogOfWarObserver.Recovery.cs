using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Results;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Captures and restores the owner's existing runtime facts at a writable recovery boundary.
    /// </summary>
    public sealed partial class FogOfWarObserver
    {
        /// <summary>
        /// Captures ownership facts whose immediate knowledge change was already observed.
        /// </summary>
        /// <returns>The retained fact identities without refreshing any observation.</returns>
        internal List<PlanetOwnershipChangedResult> CaptureRecoveryOwnershipChanges() =>
            _observedOwnershipChanges.Select(entry => entry.Key).ToList();

        /// <summary>
        /// Restores knowledge deduplication without replacing remembered state from the live world.
        /// </summary>
        /// <param name="changes">The already observed ownership fact identities.</param>
        internal void RestoreRecoveryOwnershipChanges(
            IReadOnlyList<PlanetOwnershipChangedResult> changes
        )
        {
            if (changes == null)
                throw new ArgumentNullException(nameof(changes));
            foreach (
                KeyValuePair<
                    PlanetOwnershipChangedResult,
                    object
                > entry in _observedOwnershipChanges.ToArray()
            )
                _observedOwnershipChanges.Remove(entry.Key);
            foreach (PlanetOwnershipChangedResult change in changes)
                _observedOwnershipChanges.Add(change, new object());
        }
    }
}
