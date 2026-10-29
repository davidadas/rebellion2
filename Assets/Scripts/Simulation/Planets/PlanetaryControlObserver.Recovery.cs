using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Results;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Captures and restores the owner's existing runtime facts at a writable recovery boundary.
    /// </summary>
    public sealed partial class PlanetaryControlObserver
    {
        /// <summary>
        /// Captures support-shift suppression and every retained ownership-reaction association.
        /// </summary>
        /// <returns>The current tick's control history and identity-keyed transition facts.</returns>
        internal PlanetaryControlRecoveryState CaptureRecoveryState() =>
            new PlanetaryControlRecoveryState
            {
                ControlShiftTick = _controlShiftTick,
                ControlShiftedOwners = _controlShiftedOwners.ToList(),
                Transitions = _transitionResults
                    .Select(entry => new OwnershipTransitionRecoveryState
                    {
                        Change = entry.Key,
                        Results = new List<GameResult>(entry.Value),
                    })
                    .ToList(),
            };

        /// <summary>
        /// Restores existing reaction bookkeeping without evicting units or applying another support shift.
        /// </summary>
        /// <param name="state">The captured control history.</param>
        internal void RestoreRecoveryState(PlanetaryControlRecoveryState state)
        {
            if (state?.ControlShiftedOwners == null || state.Transitions == null)
                throw new ArgumentException(
                    "Complete planetary-control recovery facts are required.",
                    nameof(state)
                );
            _controlShiftTick = state.ControlShiftTick;
            _controlShiftedOwners.Clear();
            _controlShiftedOwners.UnionWith(state.ControlShiftedOwners);
            foreach (
                KeyValuePair<
                    PlanetOwnershipChangedResult,
                    List<GameResult>
                > entry in _transitionResults.ToArray()
            )
                _transitionResults.Remove(entry.Key);
            foreach (OwnershipTransitionRecoveryState transition in state.Transitions)
                _transitionResults.Add(transition.Change, new List<GameResult>(transition.Results));
        }
    }
}
