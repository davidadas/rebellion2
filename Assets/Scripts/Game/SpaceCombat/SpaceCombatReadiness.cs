using Rebellion.Game.Units;

namespace Rebellion.Game.SpaceCombat
{
    /// <summary>
    /// Defines the capital-ship contribution to a fleet's orbital readiness.
    /// </summary>
    internal static class SpaceCombatReadiness
    {
        /// <summary>
        /// Checks a direct child's active, completion, transit, and ownership state.
        /// The fleet's own availability is evaluated by the caller.
        /// </summary>
        /// <param name="ship">The fleet's direct capital-ship child.</param>
        /// <param name="fleet">The fleet whose readiness is evaluated.</param>
        /// <returns>Whether the child contributes to this fleet's orbital presence.</returns>
        internal static bool IsCapitalReady(CapitalShip ship, Fleet fleet) =>
            ship.IsEnabled
            && ship.ManufacturingStatus == ManufacturingStatus.Complete
            && ship.Movement == null
            && ship.OwnerInstanceID == fleet.OwnerInstanceID;
    }
}
