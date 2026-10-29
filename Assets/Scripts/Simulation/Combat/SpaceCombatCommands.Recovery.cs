namespace Rebellion.Simulation
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
        /// Restores the selected encounter without changing fleet flags or rebuilding its selection.
        /// </summary>
        /// <param name="decision">The existing selected encounter, or null.</param>
        internal void RestoreRecoveryDecision(SpaceCombatDecision decision) =>
            _pendingDecision = decision;
    }
}
