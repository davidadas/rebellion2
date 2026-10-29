namespace Rebellion.Simulation
{
    /// <summary>
    /// Captures and restores the owner's existing runtime facts at a writable recovery boundary.
    /// </summary>
    public partial class VictoryCommands
    {
        /// <summary>
        /// Restores whether the session has already recorded its terminal outcome.
        /// </summary>
        /// <param name="declared">The existing terminal-outcome flag.</param>
        internal void RestoreRecoveryState(bool declared) => _victoryDeclared = declared;
    }
}
