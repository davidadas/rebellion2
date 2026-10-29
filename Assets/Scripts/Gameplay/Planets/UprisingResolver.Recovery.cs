using System;
using System.Collections.Generic;
using System.Linq;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Captures and restores the owner's existing runtime facts at a writable recovery boundary.
    /// </summary>
    public partial class UprisingResolver
    {
        /// <summary>
        /// Captures prior garrison surplus used to distinguish near-uprising transitions.
        /// </summary>
        /// <returns>The exact planet references and their previously reconciled surplus.</returns>
        internal List<PlanetIntegerRecoveryState> CaptureRecoveryState() =>
            _garrisonSurplusByPlanet
                .Select(entry => new PlanetIntegerRecoveryState
                {
                    Planet = entry.Key,
                    Value = entry.Value,
                })
                .ToList();

        /// <summary>
        /// Restores garrison history without accepting the current garrison as the prior state.
        /// </summary>
        /// <param name="states">The captured prior garrison facts.</param>
        internal void RestoreRecoveryState(IReadOnlyList<PlanetIntegerRecoveryState> states)
        {
            if (states == null)
                throw new ArgumentNullException(nameof(states));
            _garrisonSurplusByPlanet.Clear();
            foreach (PlanetIntegerRecoveryState state in states)
                _garrisonSurplusByPlanet.Add(state.Planet, state.Value);
        }
    }
}
