using System;
using System.Collections.Generic;
using System.Linq;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Captures and restores the owner's existing runtime facts at a writable recovery boundary.
    /// </summary>
    public partial class MaintenanceCommands
    {
        /// <summary>
        /// Captures maintenance notification and pulse history independently of current capacity.
        /// </summary>
        /// <returns>The existing shortfall and next-pulse facts.</returns>
        internal MaintenanceRecoveryState CaptureRecoveryState() =>
            new MaintenanceRecoveryState
            {
                ShortfallFactionIDs = _shortfallFactions.ToList(),
                NextAutoscrapTickByFaction = new Dictionary<string, int>(
                    _nextAutoscrapTickByFaction
                ),
            };

        /// <summary>
        /// Restores maintenance history without starting another notification or resetting a pulse.
        /// </summary>
        /// <param name="state">The captured maintenance facts.</param>
        internal void RestoreRecoveryState(MaintenanceRecoveryState state)
        {
            if (state?.ShortfallFactionIDs == null || state.NextAutoscrapTickByFaction == null)
                throw new ArgumentException(
                    "Complete maintenance recovery facts are required.",
                    nameof(state)
                );
            _shortfallFactions.Clear();
            _shortfallFactions.UnionWith(state.ShortfallFactionIDs);
            _nextAutoscrapTickByFaction.Clear();
            foreach (KeyValuePair<string, int> entry in state.NextAutoscrapTickByFaction)
                _nextAutoscrapTickByFaction.Add(entry.Key, entry.Value);
        }
    }
}
