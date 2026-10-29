using System;
using System.Linq;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Captures and restores the owner's existing runtime facts at a writable recovery boundary.
    /// </summary>
    public sealed partial class PlanetaryControlObserver
    {
        /// <summary>
        /// Captures the current tick's already-applied support shifts.
        /// </summary>
        /// <returns>The current tick and the displaced factions already handled.</returns>
        internal PlanetaryControlRecoveryState CaptureRecoveryState() =>
            new PlanetaryControlRecoveryState
            {
                ControlShiftTick = _controlShiftTick,
                ControlShiftedOwners = _controlShiftedOwners.ToList(),
            };

        /// <summary>
        /// Restores existing reaction bookkeeping without evicting units or applying another support shift.
        /// </summary>
        /// <param name="state">The captured control history.</param>
        internal void RestoreRecoveryState(PlanetaryControlRecoveryState state)
        {
            if (state?.ControlShiftedOwners == null)
                throw new ArgumentException(
                    "Complete planetary-control recovery facts are required.",
                    nameof(state)
                );
            _controlShiftTick = state.ControlShiftTick;
            _controlShiftedOwners.Clear();
            _controlShiftedOwners.UnionWith(state.ControlShiftedOwners);
        }
    }
}
