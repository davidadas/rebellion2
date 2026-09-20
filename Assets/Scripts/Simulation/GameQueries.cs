using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Commands;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.UIState;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Exposes read-only simulation projections without exposing mutable feature implementations.
    /// </summary>
    public sealed class GameQueries : IGameQueries
    {
        private readonly GameRoot _game;
        private readonly SimulationFeatures _features;

        /// <summary>
        /// Gets the current tick number.
        /// </summary>
        public int CurrentTick => _game.CurrentTick;

        /// <summary>
        /// Gets the human-controlled faction.
        /// </summary>
        public Faction PlayerFaction => _game.GetPlayerFaction();

        /// <summary>
        /// Gets the human player's durable interface state.
        /// </summary>
        public PlayerUIState PlayerUIState =>
            _game.GetFactionPlayer(PlayerFaction.InstanceID).UIState;

        /// <summary>
        /// Creates the query facade.
        /// </summary>
        /// <param name="game">The authoritative game state.</param>
        /// <param name="features">The private session feature graph.</param>
        internal GameQueries(GameRoot game, SimulationFeatures features)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _features = features ?? throw new ArgumentNullException(nameof(features));
        }

        /// <summary>
        /// Builds the galaxy projection visible to one faction.
        /// </summary>
        /// <param name="faction">The observing faction.</param>
        /// <returns>The faction-visible galaxy projection.</returns>
        public GalaxyMap BuildGalaxyView(Faction faction) =>
            _features.FogOfWar.BuildFactionView(faction);

        /// <summary>
        /// Gets the unresolved player combat decision, when one exists.
        /// </summary>
        /// <param name="pending">The pending combat description.</param>
        /// <returns>True when combat input is required.</returns>
        public bool TryGetPendingCombat(out PendingCombatResult pending) =>
            _features.SpaceCombat.TryGetPendingCombat(out pending);

        /// <summary>
        /// Gets whether selected manufacturing work can begin.
        /// </summary>
        /// <param name="producer">The producing planet.</param>
        /// <param name="template">The unit template.</param>
        /// <param name="destination">The delivery destination.</param>
        /// <param name="count">The requested quantity.</param>
        /// <param name="ownerInstanceID">The ordering faction.</param>
        /// <returns>True when the order is currently valid.</returns>
        public bool CanStartManufacturing(
            Planet producer,
            IManufacturable template,
            ISceneNode destination,
            int count,
            string ownerInstanceID
        ) =>
            _features.Manufacturing.CanStartManufacturing(
                producer,
                template,
                destination,
                count,
                ownerInstanceID
            );

        /// <summary>
        /// Gets whether one created unit can be accepted as a manufacturing order.
        /// </summary>
        /// <param name="producer">The producing planet.</param>
        /// <param name="unit">The proposed unit.</param>
        /// <param name="destination">The delivery destination.</param>
        /// <param name="count">The requested quantity.</param>
        /// <param name="ownerInstanceID">The ordering faction.</param>
        /// <returns>True when the order is currently valid.</returns>
        public bool CanAcceptManufacturingOrder(
            Planet producer,
            IManufacturable unit,
            ISceneNode destination,
            int count,
            string ownerInstanceID
        ) =>
            _features.Manufacturing.CanAcceptManufacturingOrder(
                producer,
                unit,
                destination,
                count,
                ownerInstanceID
            );

        /// <summary>
        /// Estimates ticks required to manufacture a quantity.
        /// </summary>
        /// <param name="producer">The producing planet.</param>
        /// <param name="template">The unit template.</param>
        /// <param name="count">The requested quantity.</param>
        /// <returns>The estimated duration, or null when unavailable.</returns>
        public int? EstimateManufacturingTicks(
            Planet producer,
            IManufacturable template,
            int count
        ) => Manufacturing.EstimateManufacturingTicks(producer, template, count);

        /// <summary>
        /// Estimates deployment travel for a newly manufactured movable unit.
        /// </summary>
        /// <param name="unit">The manufactured unit template.</param>
        /// <param name="producer">The producing planet.</param>
        /// <param name="destination">The delivery destination.</param>
        /// <param name="ticks">The estimated transit duration.</param>
        /// <returns>True when transit can be estimated.</returns>
        public bool TryEstimateManufacturedTransitTicks(
            IMovable unit,
            Planet producer,
            ContainerNode destination,
            out int ticks
        ) =>
            _features.Movement.TryEstimateManufacturedTransitTicks(
                unit,
                producer,
                destination,
                out ticks
            );

        /// <summary>
        /// Estimates transit for a selected group.
        /// </summary>
        /// <param name="items">The selected units.</param>
        /// <param name="destination">The destination.</param>
        /// <param name="ownerInstanceID">The ordering faction.</param>
        /// <param name="ticks">The estimated duration.</param>
        /// <returns>True when transit can be estimated.</returns>
        public bool TryGetSelectionTransitTicks(
            IReadOnlyList<ISceneNode> items,
            ContainerNode destination,
            string ownerInstanceID,
            out int ticks
        ) =>
            _features.Movement.TryGetSelectionTransitTicks(
                items,
                destination,
                ownerInstanceID,
                out ticks
            );

        /// <summary>
        /// Checks whether selected fleets can accept a waypoint route.
        /// </summary>
        /// <param name="items">The selected fleets.</param>
        /// <param name="planetInstanceIDs">The ordered waypoint planets.</param>
        /// <param name="ownerInstanceID">The ordering faction.</param>
        /// <returns>True when the route is valid.</returns>
        public bool CanSetFleetWaypoints(
            IReadOnlyList<ISceneNode> items,
            IReadOnlyList<string> planetInstanceIDs,
            string ownerInstanceID
        ) => _features.Movement.CanSetFleetWaypointRoute(items, planetInstanceIDs, ownerInstanceID);

        /// <summary>
        /// Gets whether all selected personnel may be retired.
        /// </summary>
        /// <param name="items">The selected personnel.</param>
        /// <param name="ownerInstanceID">The owning faction.</param>
        /// <returns>True when retirement is allowed.</returns>
        public bool CanRetire(IReadOnlyList<ISceneNode> items, string ownerInstanceID) =>
            _features.Personnel.CanRetire(items, ownerInstanceID);

        /// <summary>
        /// Gets available mission options for a context.
        /// </summary>
        /// <param name="context">The mission context.</param>
        /// <returns>The available mission options.</returns>
        public List<MissionOption> GetMissionOptions(MissionContext context) =>
            _features.Missions.GetAvailableMissionOptions(context);

        /// <summary>
        /// Gets mission odds for a proposed assignment.
        /// </summary>
        /// <param name="context">The proposed mission context.</param>
        /// <param name="observedDetectors">The detectors visible to the querying faction.</param>
        /// <returns>The calculated mission odds.</returns>
        public MissionOdds GetMissionOdds(
            MissionContext context,
            IReadOnlyList<ISceneNode> observedDetectors = null
        ) => _features.Missions.GetMissionOdds(context, observedDetectors);

        /// <summary>
        /// Gets whether a mission can be created.
        /// </summary>
        /// <param name="context">The proposed mission context.</param>
        /// <returns>True when the mission is valid.</returns>
        public bool CanCreateMission(MissionContext context) =>
            _features.Missions.CanCreateMission(context);

        /// <summary>
        /// Gets whether fleets can bombard a planet.
        /// </summary>
        /// <param name="fleets">The attacking fleets.</param>
        /// <param name="planet">The target planet.</param>
        /// <param name="type">The bombardment profile.</param>
        /// <returns>True when bombardment is valid.</returns>
        public bool CanBombard(IReadOnlyList<Fleet> fleets, Planet planet, BombardmentType type) =>
            _features.Bombardment.CanExecute(fleets, planet, type);

        /// <summary>
        /// Gets whether fleets can assault a planet.
        /// </summary>
        /// <param name="fleets">The attacking fleets.</param>
        /// <param name="planet">The target planet.</param>
        /// <returns>True when an assault is valid.</returns>
        public bool CanAssault(IReadOnlyList<Fleet> fleets, Planet planet) =>
            _features.PlanetaryAssault.CanExecute(fleets, planet);

        /// <summary>
        /// Calculates bombardment strength for a fleet group.
        /// </summary>
        /// <param name="fleets">The fleets to evaluate.</param>
        /// <returns>The combined bombardment strength.</returns>
        public int GetBombardmentStrength(IEnumerable<Fleet> fleets) =>
            Bombardment.GetBombardmentStrength(fleets, _game.Config.Combat.Bombardment);

        /// <summary>
        /// Calculates active planetary shield strength on the bombardment scale.
        /// </summary>
        /// <param name="planet">The defending planet.</param>
        /// <returns>The active shield strength.</returns>
        public int GetBombardmentShieldStrength(Planet planet) =>
            Bombardment.GetBombardmentShieldStrength(planet);

        /// <summary>
        /// Calculates projected bombardment strength for a fleet.
        /// </summary>
        /// <param name="fleet">The fleet to evaluate.</param>
        /// <returns>The projected bombardment strength.</returns>
        public int GetProjectedBombardmentStrength(Fleet fleet) =>
            Bombardment.GetProjectedBombardmentStrength(fleet, _game.Config.Combat.Bombardment);

        /// <summary>
        /// Calculates projected bombardment strength for one ship in a fleet.
        /// </summary>
        /// <param name="fleet">The containing fleet.</param>
        /// <param name="capitalShip">The capital ship to evaluate.</param>
        /// <returns>The projected bombardment contribution.</returns>
        public int GetProjectedCapitalShipBombardmentStrength(
            Fleet fleet,
            CapitalShip capitalShip
        ) =>
            Bombardment.GetProjectedCapitalShipBombardmentStrength(
                fleet,
                capitalShip,
                _game.Config.Combat.Bombardment
            );

        /// <summary>
        /// Calculates active planetary shield resistance on the bombardment scale.
        /// </summary>
        /// <param name="planet">The defending planet.</param>
        /// <returns>The active shield resistance.</returns>
        public int GetBombardmentShieldResistance(Planet planet) =>
            Bombardment.GetBombardmentShieldResistance(
                Bombardment.GetBombardmentShieldStrength(planet),
                _game.Config.Combat.Bombardment
            );

        /// <summary>
        /// Gets whether a planet has active military bombardment targets.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <param name="defenderInstanceID">The defending faction.</param>
        /// <returns>True when a military target remains.</returns>
        public bool HasActiveMilitaryTargets(Planet planet, string defenderInstanceID) =>
            Bombardment.HasActiveMilitaryTargets(planet, defenderInstanceID);

        /// <summary>
        /// Calculates the ground-combat leadership bonus for a unit group.
        /// </summary>
        /// <param name="officers">The participating officers.</param>
        /// <param name="rank">The required command rank.</param>
        /// <param name="ownerInstanceID">The faction receiving the bonus.</param>
        /// <returns>The leadership bonus.</returns>
        public int GetAssaultLeadershipBonus(
            IEnumerable<Officer> officers,
            OfficerRank rank,
            string ownerInstanceID
        ) =>
            PlanetaryAssaultResolver.GetLeadershipBonus(
                officers,
                rank,
                ownerInstanceID,
                _game.Config.Combat.PlanetaryAssault
            );

        /// <summary>
        /// Gets whether active planetary shields block a ground assault.
        /// </summary>
        /// <param name="planet">The defending planet.</param>
        /// <returns>True when shields block the assault.</returns>
        public bool IsAssaultBlockedByShields(Planet planet) =>
            PlanetaryAssaultResolver.IsBlockedByShields(
                planet,
                _game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit
            );

        /// <summary>
        /// Estimates immediate ground-assault success.
        /// </summary>
        /// <param name="fleets">The attacking fleets.</param>
        /// <param name="planet">The defending planet.</param>
        /// <returns>The estimated success percentage.</returns>
        public int EstimateAssaultSuccess(IReadOnlyList<Fleet> fleets, Planet planet) =>
            PlanetaryAssaultResolver.EstimateSuccessPercent(
                fleets,
                planet,
                _game.Config.Combat.PlanetaryAssault
            );

        /// <summary>
        /// Calculates the configured garrison requirement for a planet.
        /// </summary>
        /// <param name="planet">The planet to evaluate.</param>
        /// <param name="faction">The occupying faction.</param>
        /// <returns>The required regiment count.</returns>
        public int GetGarrisonRequirement(Planet planet, Faction faction) =>
            Uprisings.CalculateGarrisonRequirement(planet, faction, _game.Config.AI.Garrison);
    }
}
