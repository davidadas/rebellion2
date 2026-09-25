using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Answers encounter and withdrawal questions without starting or resolving combat.
    /// </summary>
    public sealed class SpaceCombatQueries
    {
        private readonly GameRoot _game;
        private readonly MovementQueries _movement;

        /// <summary>
        /// Creates space-combat queries for the current game.
        /// </summary>
        /// <param name="game">The game containing fleets, factions, and planets.</param>
        /// <param name="movement">The movement queries supplying evacuation eligibility.</param>
        public SpaceCombatQueries(GameRoot game, MovementQueries movement)
        {
            _game = game;
            _movement = movement;
        }

        /// <summary>
        /// Returns whether both sides belong to AI-controlled factions.
        /// </summary>
        /// <param name="decision">The combat decision to evaluate.</param>
        /// <returns>True when both sides are AI-controlled.</returns>
        internal bool BothSidesAIControlled(SpaceCombatDecision decision)
        {
            Faction attacker = _game.GetFactionByOwnerInstanceID(decision.AttackerOwnerInstanceID);
            Faction defender = _game.GetFactionByOwnerInstanceID(decision.DefenderOwnerInstanceID);
            return attacker != null
                && defender != null
                && _game.IsFactionAIControlled(attacker)
                && _game.IsFactionAIControlled(defender);
        }

        /// <summary>
        /// Builds the result that pauses a player-involved encounter.
        /// </summary>
        /// <param name="decision">The pending combat decision.</param>
        /// <returns>The pending-combat result.</returns>
        internal PendingCombatResult BuildPendingCombatResult(SpaceCombatDecision decision)
        {
            List<Fleet> attackerFleets = GetFleets(decision.AttackerFleetInstanceIDs);
            List<Fleet> defenderFleets = GetFleets(decision.DefenderFleetInstanceIDs);
            Fleet attacker = GetRepresentativeFleet(attackerFleets);
            Fleet defender = GetRepresentativeFleet(defenderFleets);
            Planet planet = ResolveCombatPlanet(decision);

            return new PendingCombatResult
            {
                AttackerFleet = attacker,
                DefenderFleet = defender,
                AttackerOwnerInstanceID = decision.AttackerOwnerInstanceID,
                DefenderOwnerInstanceID = decision.DefenderOwnerInstanceID,
                Planet = planet,
                AttackerCanRetreat = CanRetreatForces(
                    attackerFleets,
                    defenderFleets,
                    planet,
                    decision.AttackerOwnerInstanceID
                ),
                DefenderCanRetreat = CanRetreatForces(
                    defenderFleets,
                    attackerFleets,
                    planet,
                    decision.DefenderOwnerInstanceID
                ),
                Tick = _game.CurrentTick,
            };
        }

        /// <summary>
        /// Finds the first pair of hostile space forces occupying the same planet.
        /// </summary>
        /// <param name="excludedFleetIds">Fleet instance IDs to skip.</param>
        /// <param name="contestedPlanet">The planet occupied by both sides.</param>
        /// <param name="attackerOwnerInstanceId">The attacking owner identifier.</param>
        /// <param name="defenderOwnerInstanceId">The defending owner identifier.</param>
        /// <param name="attackerFleets">The attacking fleets.</param>
        /// <param name="defenderFleets">The defending fleets.</param>
        /// <returns>True if hostile space forces were found.</returns>
        internal bool TryFindContestedForces(
            HashSet<string> excludedFleetIds,
            out Planet contestedPlanet,
            out string attackerOwnerInstanceId,
            out string defenderOwnerInstanceId,
            out List<Fleet> attackerFleets,
            out List<Fleet> defenderFleets
        )
        {
            contestedPlanet = null;
            attackerOwnerInstanceId = null;
            defenderOwnerInstanceId = null;
            attackerFleets = null;
            defenderFleets = null;

            foreach (Planet planet in _game.GetSceneNodesByType<Planet>())
            {
                List<Fleet> fleets = planet
                    .GetChildren<Fleet>()
                    .Where(fleet =>
                        !fleet.IsInCombat
                        && !excludedFleetIds.Contains(fleet.GetInstanceID())
                        && fleet.Movement == null
                        && HasActiveSpaceUnits(fleet)
                    )
                    .ToList();

                List<string> ownerInstanceIds = fleets
                    .Select(fleet => fleet.GetOwnerInstanceID())
                    .Concat(
                        GetActivePlanetStarfighters(planet, null)
                            .Select(fighter => fighter.GetOwnerInstanceID())
                    )
                    .Where(ownerInstanceId => !string.IsNullOrEmpty(ownerInstanceId))
                    .Distinct()
                    .OrderBy(ownerInstanceId => ownerInstanceId)
                    .ToList();

                if (ownerInstanceIds.Count < 2)
                    continue;

                string firstOwnerInstanceId = ownerInstanceIds[0];
                string secondOwnerInstanceId = ownerInstanceIds[1];
                List<Fleet> firstFleets = fleets
                    .Where(fleet => fleet.GetOwnerInstanceID() == firstOwnerInstanceId)
                    .ToList();
                List<Fleet> secondFleets = fleets
                    .Where(fleet => fleet.GetOwnerInstanceID() == secondOwnerInstanceId)
                    .ToList();

                attackerOwnerInstanceId = firstOwnerInstanceId;
                defenderOwnerInstanceId = secondOwnerInstanceId;
                attackerFleets = firstFleets;
                defenderFleets = secondFleets;

                if (attackerFleets.Count == 0 && defenderFleets.Count > 0)
                {
                    (attackerOwnerInstanceId, defenderOwnerInstanceId) = (
                        defenderOwnerInstanceId,
                        attackerOwnerInstanceId
                    );
                    (attackerFleets, defenderFleets) = (defenderFleets, attackerFleets);
                }

                contestedPlanet = planet;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Reports whether every force on one side can withdraw from its opponent.
        /// </summary>
        /// <param name="fleets">The fleets requesting withdrawal.</param>
        /// <param name="opponents">The opposing fleets.</param>
        /// <param name="planet">The combat planet.</param>
        /// <param name="ownerInstanceId">The withdrawing faction identifier.</param>
        /// <returns>True when every fleet and directly deployed fighter can evacuate.</returns>
        public bool CanRetreatForces(
            IReadOnlyList<Fleet> fleets,
            IReadOnlyList<Fleet> opponents,
            Planet planet,
            string ownerInstanceId
        )
        {
            IReadOnlyList<Fleet> retreatingFleets = fleets ?? Array.Empty<Fleet>();
            List<Starfighter> retreatingFighters = GetActivePlanetStarfighters(
                    planet,
                    ownerInstanceId
                )
                .ToList();
            return (retreatingFleets.Count > 0 || retreatingFighters.Count > 0)
                && !IsRetreatBlockedByGravityWell(planet, opponents)
                && retreatingFleets.All(fleet =>
                    HasHyperdriveCapableShip(fleet)
                    && _movement.CanEvacuateToNearestFriendlyPlanet(fleet)
                )
                && retreatingFighters.All(fighter =>
                    fighter.Hyperdrive > 0 && _movement.CanEvacuateToNearestFriendlyPlanet(fighter)
                );
        }

        /// <summary>
        /// Returns the tactical unit groups capable of leaving an automatically resolved battle.
        /// </summary>
        /// <param name="fleets">The fleets on the withdrawing side.</param>
        /// <param name="opponents">The opposing fleets.</param>
        /// <param name="planet">The combat planet.</param>
        /// <param name="ownerInstanceId">The withdrawing owner identifier.</param>
        /// <returns>The fleets and independent fighter squadrons that can withdraw.</returns>
        internal List<IReadOnlyCollection<ISceneNode>> GetAutomaticWithdrawalGroups(
            IReadOnlyList<Fleet> fleets,
            IReadOnlyList<Fleet> opponents,
            Planet planet,
            string ownerInstanceId
        )
        {
            List<IReadOnlyCollection<ISceneNode>> groups =
                new List<IReadOnlyCollection<ISceneNode>>();
            if (IsRetreatBlockedByGravityWell(planet, opponents))
                return groups;

            foreach (
                Fleet fleet in (fleets ?? Array.Empty<Fleet>()).Where(fleet =>
                    HasHyperdriveCapableShip(fleet)
                    && _movement.CanEvacuateToNearestFriendlyPlanet(fleet)
                )
            )
            {
                List<ISceneNode> fleetUnits = GetActiveCapitalShips(fleet)
                    .Cast<ISceneNode>()
                    .Concat(GetActiveStarfighters(fleet))
                    .Distinct()
                    .ToList();
                if (fleetUnits.Count > 0)
                    groups.Add(fleetUnits);
            }

            foreach (Starfighter fighter in GetActivePlanetStarfighters(planet, ownerInstanceId))
            {
                if (fighter.Hyperdrive > 0 && _movement.CanEvacuateToNearestFriendlyPlanet(fighter))
                {
                    groups.Add(new ISceneNode[] { fighter });
                }
            }

            return groups;
        }

        /// <summary>
        /// Returns whether a fleet has a surviving capital ship capable of entering hyperspace.
        /// </summary>
        /// <param name="fleet">The fleet whose withdrawal capability is being checked.</param>
        /// <returns>True when at least one active capital ship has a hyperdrive.</returns>
        internal static bool HasHyperdriveCapableShip(Fleet fleet)
        {
            return fleet != null && GetActiveCapitalShips(fleet).Any(ship => ship.Hyperdrive > 0);
        }

        /// <summary>
        /// Determines whether any opposing fleet projects a gravity well at the combat planet.
        /// </summary>
        /// <param name="fleets">The fleets attempting to retreat.</param>
        /// <param name="opponents">The opposing fleets.</param>
        /// <returns>True when an active opposing ship blocks withdrawal.</returns>
        internal static bool IsRetreatBlockedByGravityWell(
            IReadOnlyList<Fleet> fleets,
            IReadOnlyList<Fleet> opponents
        )
        {
            Planet fleetPlanet = fleets
                ?.Select(fleet => fleet?.GetParentOfType<Planet>())
                .FirstOrDefault(planet => planet != null);
            return IsRetreatBlockedByGravityWell(fleetPlanet, opponents);
        }

        /// <summary>
        /// Determines whether an opposing fleet projects a gravity well at a specified planet.
        /// </summary>
        /// <param name="planet">The planet where withdrawal would begin.</param>
        /// <param name="opponents">The opposing fleets.</param>
        /// <returns>True when an active opposing ship blocks withdrawal.</returns>
        internal static bool IsRetreatBlockedByGravityWell(
            Planet planet,
            IReadOnlyList<Fleet> opponents
        )
        {
            return planet != null
                && opponents?.Any(opponent =>
                    opponent?.GetParentOfType<Planet>() == planet
                    && GetActiveCapitalShips(opponent).Any(ship => ship.HasGravityWell)
                ) == true;
        }

        /// <summary>
        /// Resolves the planet associated with a pending combat decision.
        /// </summary>
        /// <param name="decision">The pending combat decision.</param>
        /// <returns>The recorded or fleet-hosting planet, or null.</returns>
        internal Planet ResolveCombatPlanet(SpaceCombatDecision decision)
        {
            Planet planet = _game.GetSceneNodeByInstanceID<Planet>(decision.PlanetInstanceID);
            if (planet != null)
                return planet;

            Fleet attacker = GetRepresentativeFleet(GetFleets(decision.AttackerFleetInstanceIDs));
            planet = attacker?.GetParentOfType<Planet>();
            if (planet != null)
                return planet;

            Fleet defender = GetRepresentativeFleet(GetFleets(decision.DefenderFleetInstanceIDs));
            return defender?.GetParentOfType<Planet>();
        }

        /// <summary>
        /// Resolves the participating fleets that still exist in the scene graph.
        /// </summary>
        /// <param name="fleetInstanceIds">The participating fleet identifiers.</param>
        /// <returns>The live fleets in encounter order.</returns>
        internal List<Fleet> GetFleets(IEnumerable<string> fleetInstanceIds)
        {
            return (fleetInstanceIds ?? Enumerable.Empty<string>())
                .Select(fleetInstanceId => _game.GetSceneNodeByInstanceID<Fleet>(fleetInstanceId))
                .Where(fleet => fleet != null)
                .ToList();
        }

        /// <summary>
        /// Returns the fleet used to identify a multi-fleet combat side.
        /// </summary>
        /// <param name="fleets">The participating fleets.</param>
        /// <returns>The first fleet in encounter order, or null.</returns>
        internal static Fleet GetRepresentativeFleet(IReadOnlyList<Fleet> fleets)
        {
            return fleets == null || fleets.Count == 0 ? null : fleets[0];
        }

        /// <summary>
        /// Determines whether two hostile active fleets still contest the same planet.
        /// </summary>
        /// <param name="attacker">Attacking fleet.</param>
        /// <param name="defender">Defending fleet.</param>
        /// <returns>True when both fleets remain stationary, active, hostile, and colocated.</returns>
        internal static bool AreFleetsContestingPlanet(Fleet attacker, Fleet defender)
        {
            if (attacker == null || defender == null)
                return false;

            Planet attackerPlanet = attacker.GetParentOfType<Planet>();
            Planet defenderPlanet = defender.GetParentOfType<Planet>();

            return attackerPlanet != null
                && attackerPlanet == defenderPlanet
                && attacker.Movement == null
                && defender.Movement == null
                && HasActiveSpaceUnits(attacker)
                && HasActiveSpaceUnits(defender)
                && attacker.GetOwnerInstanceID() != defender.GetOwnerInstanceID();
        }

        /// <summary>
        /// Returns whether both recorded sides retain active space forces at the encounter planet.
        /// </summary>
        /// <param name="decision">The encounter to evaluate.</param>
        /// <returns>True when hostile active forces still contest the planet.</returns>
        internal bool AreForcesContestingPlanet(SpaceCombatDecision decision)
        {
            Planet planet = ResolveCombatPlanet(decision);
            if (planet == null)
                return false;

            List<Fleet> attackerFleets = GetFleets(decision.AttackerFleetInstanceIDs);
            List<Fleet> defenderFleets = GetFleets(decision.DefenderFleetInstanceIDs);

            return decision.AttackerOwnerInstanceID != decision.DefenderOwnerInstanceID
                && HasActiveSpaceUnits(attackerFleets, planet, decision.AttackerOwnerInstanceID)
                && HasActiveSpaceUnits(defenderFleets, planet, decision.DefenderOwnerInstanceID);
        }

        /// <summary>
        /// Determines whether a fleet has any active capital ships or starfighters.
        /// </summary>
        /// <param name="fleet">Fleet to inspect.</param>
        /// <returns>True when at least one active space unit remains.</returns>
        internal static bool HasActiveSpaceUnits(Fleet fleet)
        {
            if (fleet == null)
                return false;

            return GetActiveCapitalShips(fleet).Any() || GetActiveStarfighters(fleet).Any();
        }

        /// <summary>
        /// Returns whether an owner has active units across any participating fleet or the planet.
        /// </summary>
        /// <param name="fleets">The owner's participating fleets.</param>
        /// <param name="planet">The encounter planet.</param>
        /// <param name="ownerInstanceId">The owner whose forces are being inspected.</param>
        /// <returns>True when at least one active space unit remains.</returns>
        internal static bool HasActiveSpaceUnits(
            IReadOnlyList<Fleet> fleets,
            Planet planet,
            string ownerInstanceId
        )
        {
            return fleets?.Any(fleet =>
                    fleet != null
                    && fleet.Movement == null
                    && fleet.GetParentOfType<Planet>() == planet
                    && HasActiveSpaceUnits(fleet)
                ) == true
                || GetActivePlanetStarfighters(planet, ownerInstanceId).Any();
        }

        /// <summary>
        /// Returns active capital ships in a fleet.
        /// </summary>
        /// <param name="fleet">Fleet to inspect.</param>
        /// <returns>The active capital ships.</returns>
        internal static IEnumerable<CapitalShip> GetActiveCapitalShips(Fleet fleet)
        {
            if (fleet == null)
                return Enumerable.Empty<CapitalShip>();

            return fleet.GetChildren<CapitalShip>().Where(IsActiveCapitalShip);
        }

        /// <summary>
        /// Returns active starfighters carried by active capital ships.
        /// </summary>
        /// <param name="fleet">Fleet to inspect.</param>
        /// <returns>The active starfighter groups.</returns>
        private static IEnumerable<Starfighter> GetActiveStarfighters(Fleet fleet)
        {
            if (fleet == null)
                return Enumerable.Empty<Starfighter>();

            return GetActiveCapitalShips(fleet)
                .SelectMany(ship => ship.GetChildren<Starfighter>())
                .Where(IsActiveStarfighter);
        }

        /// <summary>
        /// Returns active starfighters deployed directly to a planet for one owner.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <param name="ownerInstanceId">The owner to filter by, or null for every owner.</param>
        /// <returns>The matching active planetary starfighters.</returns>
        internal static IEnumerable<Starfighter> GetActivePlanetStarfighters(
            Planet planet,
            string ownerInstanceId
        )
        {
            if (planet == null)
                return Enumerable.Empty<Starfighter>();

            return planet
                .GetChildren<Starfighter>()
                .Where(fighter =>
                    (
                        string.IsNullOrEmpty(ownerInstanceId)
                        || fighter.GetOwnerInstanceID() == ownerInstanceId
                    ) && IsActiveStarfighter(fighter)
                );
        }

        /// <summary>
        /// Determines whether a capital ship can participate in space combat.
        /// </summary>
        /// <param name="ship">Capital ship to inspect.</param>
        /// <returns>True when the ship is complete, stationary, and has remaining hull.</returns>
        private static bool IsActiveCapitalShip(CapitalShip ship)
        {
            return ship.ManufacturingStatus == ManufacturingStatus.Complete
                && ship.Movement == null
                && ship.CurrentHullStrength > 0;
        }

        /// <summary>
        /// Determines whether a starfighter group can participate in space combat.
        /// </summary>
        /// <param name="starfighter">Starfighter group to inspect.</param>
        /// <returns>True when the group is complete, stationary, and has remaining fighters.</returns>
        internal static bool IsActiveStarfighter(Starfighter starfighter)
        {
            return starfighter.ManufacturingStatus == ManufacturingStatus.Complete
                && starfighter.Movement == null
                && starfighter.CurrentSquadronSize > 0;
        }
    }
}
