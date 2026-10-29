using System;
using System.Collections.Generic;
using System.Linq;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Captures and restores the owner's existing runtime facts at a writable recovery boundary.
    /// </summary>
    public sealed partial class SmugglingCommands
    {
        /// <summary>
        /// Captures the previously reconciled relationships used for subsequent smuggling transitions.
        /// </summary>
        /// <returns>The prior relationships and diversion percentages.</returns>
        internal List<SmugglingRecoveryState> CaptureRecoveryState() =>
            _states
                .Select(entry => new SmugglingRecoveryState
                {
                    PlanetID = entry.Key,
                    DiversionPercent = entry.Value.DiversionPercent,
                    ControllerInstanceID = entry.Value.ControllerInstanceID,
                    BeneficiaryInstanceID = entry.Value.BeneficiaryInstanceID,
                })
                .ToList();

        /// <summary>
        /// Restores previous relationships without recalculating or emitting transition facts.
        /// </summary>
        /// <param name="states">The captured prior relationships.</param>
        internal void RestoreRecoveryState(IReadOnlyList<SmugglingRecoveryState> states)
        {
            if (states == null)
                throw new ArgumentNullException(nameof(states));
            _states.Clear();
            foreach (SmugglingRecoveryState state in states)
                _states.Add(
                    state.PlanetID,
                    new PlanetSmugglingState
                    {
                        DiversionPercent = state.DiversionPercent,
                        ControllerInstanceID = state.ControllerInstanceID,
                        BeneficiaryInstanceID = state.BeneficiaryInstanceID,
                    }
                );
        }
    }
}
