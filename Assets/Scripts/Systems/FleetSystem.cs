using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Util.Extensions;

namespace Rebellion.Systems
{
    /// <summary>
    /// Owns fleet formation and empty-fleet removal for the active game graph.
    /// </summary>
    public sealed class FleetSystem
    {
        private readonly GameRoot _game;

        /// <summary>
        /// Creates the fleet system for one game.
        /// </summary>
        /// <param name="game">The active game graph.</param>
        public FleetSystem(GameRoot game)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
        }

        /// <summary>
        /// Creates an empty fleet for a faction at a registered planet.
        /// </summary>
        /// <param name="destination">The destination planet or its snapshot.</param>
        /// <param name="ownerInstanceId">The owning faction identifier.</param>
        /// <returns>The attached fleet, or null when the request is invalid.</returns>
        public Fleet CreateAtPlanet(Planet destination, string ownerInstanceId)
        {
            Planet liveDestination = ResolveLivePlanet(destination);
            if (liveDestination == null || string.IsNullOrEmpty(ownerInstanceId))
                return null;

            Faction faction = _game
                .GetFactions()
                .FirstOrDefault(candidate =>
                    string.Equals(candidate.InstanceID, ownerInstanceId, StringComparison.Ordinal)
                );
            if (faction == null)
                return null;

            Fleet fleet = faction.CreateFleet();
            _game.AttachNode(fleet, liveDestination);
            return fleet;
        }

        /// <summary>
        /// Forms a fleet from a validated selection of registered capital ships.
        /// </summary>
        /// <param name="capitalShips">The capital ships or their snapshots.</param>
        /// <param name="ownerInstanceId">The faction authorized to form the fleet.</param>
        /// <returns>The attached fleet, or null when the selection is invalid.</returns>
        public Fleet CreateFromCapitalShips(
            IReadOnlyList<CapitalShip> capitalShips,
            string ownerInstanceId
        )
        {
            if (
                capitalShips == null
                || capitalShips.Count == 0
                || string.IsNullOrEmpty(ownerInstanceId)
            )
                return null;

            List<CapitalShip> ships = ResolveLiveCapitalShips(capitalShips);
            if (ships.Count != capitalShips.Count || ships.Distinct().Count() != ships.Count)
                return null;

            Planet planet = ships[0].GetParentOfType<Planet>();
            if (planet == null)
                return null;

            List<Fleet> sourceFleets = new List<Fleet>(ships.Count);
            foreach (CapitalShip ship in ships)
            {
                if (
                    !string.Equals(
                        ship.GetOwnerInstanceID(),
                        ownerInstanceId,
                        StringComparison.Ordinal
                    )
                    || ship.GetParent() is not Fleet sourceFleet
                    || !ReferenceEquals(sourceFleet.GetParentOfType<Planet>(), planet)
                    || ship.ManufacturingStatus != ManufacturingStatus.Building
                        && ship.GetTransitMovement() != null
                )
                    return null;

                sourceFleets.Add(sourceFleet);
            }

            Faction faction = _game
                .GetFactions()
                .FirstOrDefault(candidate =>
                    string.Equals(candidate.InstanceID, ownerInstanceId, StringComparison.Ordinal)
                );
            if (faction == null)
                return null;

            foreach (CapitalShip ship in ships)
                _game.DetachNode(ship);

            Fleet fleet = faction.CreateFleet(ships.ToArray());
            _game.AttachNode(fleet, planet);

            foreach (Fleet sourceFleet in sourceFleets.Distinct())
                RemoveIfEmpty(sourceFleet);

            return fleet;
        }

        /// <summary>
        /// Removes a registered fleet when it is attached and contains no capital ships.
        /// </summary>
        /// <param name="fleet">The fleet or its snapshot.</param>
        /// <returns>True when the fleet was removed.</returns>
        public bool RemoveIfEmpty(Fleet fleet)
        {
            Fleet liveFleet = ResolveLiveFleet(fleet);
            if (
                liveFleet == null
                || liveFleet.CapitalShips.Count != 0
                || liveFleet.GetParent() == null
            )
                return false;

            _game.DetachNode(liveFleet);
            return true;
        }

        /// <summary>
        /// Resolves capital-ship snapshots to their registered game nodes.
        /// </summary>
        /// <param name="capitalShips">The capital ships to resolve.</param>
        /// <returns>The resolved ships, or an empty list when any ship is invalid.</returns>
        private List<CapitalShip> ResolveLiveCapitalShips(IReadOnlyList<CapitalShip> capitalShips)
        {
            List<CapitalShip> ships = new List<CapitalShip>(capitalShips.Count);
            foreach (CapitalShip capitalShip in capitalShips)
            {
                CapitalShip liveShip = string.IsNullOrEmpty(capitalShip?.InstanceID)
                    ? null
                    : _game.GetSceneNodeByInstanceID<CapitalShip>(capitalShip.InstanceID);
                if (liveShip == null)
                    return new List<CapitalShip>();

                ships.Add(liveShip);
            }

            return ships;
        }

        /// <summary>
        /// Resolves a planet snapshot to its registered game node.
        /// </summary>
        /// <param name="planet">The planet to resolve.</param>
        /// <returns>The registered planet, or null.</returns>
        private Planet ResolveLivePlanet(Planet planet)
        {
            return string.IsNullOrEmpty(planet?.InstanceID)
                ? null
                : _game.GetSceneNodeByInstanceID<Planet>(planet.InstanceID);
        }

        /// <summary>
        /// Resolves a fleet snapshot to its registered game node.
        /// </summary>
        /// <param name="fleet">The fleet to resolve.</param>
        /// <returns>The registered fleet, or null.</returns>
        private Fleet ResolveLiveFleet(Fleet fleet)
        {
            return string.IsNullOrEmpty(fleet?.InstanceID)
                ? null
                : _game.GetSceneNodeByInstanceID<Fleet>(fleet.InstanceID);
        }
    }
}
