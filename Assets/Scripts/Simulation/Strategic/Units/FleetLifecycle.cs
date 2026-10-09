using System;
using Rebellion.Game;
using Rebellion.Game.Units;

namespace Rebellion.Simulation
{
    /// <summary>Maintains structural fleet invariants after contained ships move or disappear.</summary>
    internal static class FleetLifecycle
    {
        /// <summary>Removes a registered fleet when it is attached and contains no capital ships.</summary>
        /// <param name="game">The active game graph.</param>
        /// <param name="fleet">The fleet or snapshot to inspect.</param>
        /// <returns>True when an empty fleet was detached.</returns>
        internal static bool RemoveEmptyFleet(GameRoot game, Fleet fleet)
        {
            if (game == null)
                throw new ArgumentNullException(nameof(game));

            Fleet liveFleet = string.IsNullOrEmpty(fleet?.InstanceID)
                ? null
                : game.GetSceneNodeByInstanceID<Fleet>(fleet.InstanceID);
            if (
                liveFleet == null
                || liveFleet.GetChildren<CapitalShip>().Count != 0
                || liveFleet.GetParent() == null
            )
                return false;

            game.DetachNode(liveFleet);
            return true;
        }
    }
}
