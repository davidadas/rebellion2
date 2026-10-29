using System.Linq;
using Rebellion.Game.Units;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Captures and restores the owner's existing runtime facts at a writable recovery boundary.
    /// </summary>
    public partial class SpaceCombatCommands
    {
        /// <summary>
        /// Captures the exact selected encounter without finding or starting another encounter.
        /// </summary>
        /// <returns>The selected encounter, or null when no decision is pending.</returns>
        internal SpaceCombatDecision CaptureRecoveryDecision() => _pendingDecision;

        /// <summary>
        /// Restores the selected encounter and its derived combat flags without clearing fleet orders.
        /// </summary>
        /// <param name="decision">The existing selected encounter, or null.</param>
        internal void RestoreRecoveryDecision(SpaceCombatDecision decision)
        {
            _pendingDecision = decision;
            if (decision == null)
                return;
            foreach (
                string identity in decision.AttackerFleetInstanceIDs.Concat(
                    decision.DefenderFleetInstanceIDs
                )
            )
                _game.GetSceneNodeByInstanceID<Fleet>(identity)?.RestoreRecoveryCombatState(true);
        }
    }
}
