using System.Collections.Generic;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.UIState;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Game.Commands
{
    /// <summary>
    /// Answers simulation questions without exposing mutable feature implementations.
    /// </summary>
    public interface IGameQueries
    {
        /// <summary>Gets the current authoritative tick.</summary>
        int CurrentTick { get; }

        /// <summary>Gets the faction controlled by the local human participant.</summary>
        Faction PlayerFaction { get; }

        /// <summary>Gets the local participant's durable interface state.</summary>
        PlayerUIState PlayerUIState { get; }

        /// <summary>Builds the observed galaxy projection for a faction.</summary>
        /// <param name="faction">The observing faction.</param>
        /// <returns>The faction-visible galaxy projection.</returns>
        GalaxyMap BuildGalaxyView(Faction faction);

        /// <summary>Gets the combat decision currently awaiting player input.</summary>
        /// <param name="pending">The pending combat result when one exists.</param>
        /// <returns>True when combat is awaiting a decision.</returns>
        bool TryGetPendingCombat(out PendingCombatResult pending);

        /// <summary>Determines whether a new manufacturing order can start.</summary>
        /// <param name="producer">The producing planet.</param>
        /// <param name="template">The unit template.</param>
        /// <param name="destination">The completed unit destination.</param>
        /// <param name="count">The requested quantity.</param>
        /// <param name="ownerInstanceID">The requesting faction.</param>
        /// <returns>True when the order can start.</returns>
        bool CanStartManufacturing(
            Planet producer,
            IManufacturable template,
            ISceneNode destination,
            int count,
            string ownerInstanceID
        );

        /// <summary>Determines whether an existing unit can enter a manufacturing queue.</summary>
        /// <param name="producer">The producing planet.</param>
        /// <param name="unit">The unit to queue.</param>
        /// <param name="destination">The completed unit destination.</param>
        /// <param name="count">The requested quantity.</param>
        /// <param name="ownerInstanceID">The requesting faction.</param>
        /// <returns>True when the queue can accept the unit.</returns>
        bool CanAcceptManufacturingOrder(
            Planet producer,
            IManufacturable unit,
            ISceneNode destination,
            int count,
            string ownerInstanceID
        );

        /// <summary>Estimates the production duration for an order.</summary>
        /// <param name="producer">The producing planet.</param>
        /// <param name="template">The unit template.</param>
        /// <param name="count">The requested quantity.</param>
        /// <returns>The estimated ticks, or null when production is unavailable.</returns>
        int? EstimateManufacturingTicks(Planet producer, IManufacturable template, int count);

        /// <summary>Estimates post-production transit time.</summary>
        /// <param name="unit">The manufactured unit.</param>
        /// <param name="producer">The producing planet.</param>
        /// <param name="destination">The delivery destination.</param>
        /// <param name="ticks">The estimated transit ticks.</param>
        /// <returns>True when transit can be estimated.</returns>
        bool TryEstimateManufacturedTransitTicks(
            IMovable unit,
            Planet producer,
            ContainerNode destination,
            out int ticks
        );

        /// <summary>Estimates transit time for a selected group.</summary>
        /// <param name="items">The selected scene nodes.</param>
        /// <param name="destination">The requested destination.</param>
        /// <param name="ownerInstanceID">The requesting faction.</param>
        /// <param name="ticks">The estimated transit ticks.</param>
        /// <returns>True when the selection can travel to the destination.</returns>
        bool TryGetSelectionTransitTicks(
            IReadOnlyList<ISceneNode> items,
            ContainerNode destination,
            string ownerInstanceID,
            out int ticks
        );

        /// <summary>Determines whether a fleet waypoint route is valid.</summary>
        /// <param name="items">The selected scene nodes.</param>
        /// <param name="planetInstanceIDs">The ordered waypoint planets.</param>
        /// <param name="ownerInstanceID">The requesting faction.</param>
        /// <returns>True when the route can be assigned.</returns>
        bool CanSetFleetWaypoints(
            IReadOnlyList<ISceneNode> items,
            IReadOnlyList<string> planetInstanceIDs,
            string ownerInstanceID
        );

        /// <summary>Determines whether selected units can be retired.</summary>
        /// <param name="items">The selected scene nodes.</param>
        /// <param name="ownerInstanceID">The requesting faction.</param>
        /// <returns>True when every selected unit can be retired.</returns>
        bool CanRetire(IReadOnlyList<ISceneNode> items, string ownerInstanceID);

        /// <summary>Gets available mission options for a context.</summary>
        /// <param name="context">The proposed mission context.</param>
        /// <returns>The available mission options.</returns>
        List<MissionOption> GetMissionOptions(MissionContext context);

        /// <summary>Calculates mission odds for a context.</summary>
        /// <param name="context">The proposed mission context.</param>
        /// <param name="observedDetectors">The detectors visible to the observer.</param>
        /// <returns>The calculated mission odds.</returns>
        MissionOdds GetMissionOdds(
            MissionContext context,
            IReadOnlyList<ISceneNode> observedDetectors = null
        );

        /// <summary>Determines whether a mission can be created.</summary>
        /// <param name="context">The proposed mission context.</param>
        /// <returns>True when the mission is valid.</returns>
        bool CanCreateMission(MissionContext context);

        /// <summary>Determines whether fleets can bombard a planet.</summary>
        /// <param name="fleets">The attacking fleets.</param>
        /// <param name="planet">The target planet.</param>
        /// <param name="type">The bombardment order.</param>
        /// <returns>True when bombardment is available.</returns>
        bool CanBombard(IReadOnlyList<Fleet> fleets, Planet planet, BombardmentType type);

        /// <summary>Determines whether fleets can assault a planet.</summary>
        /// <param name="fleets">The attacking fleets.</param>
        /// <param name="planet">The target planet.</param>
        /// <returns>True when an assault is available.</returns>
        bool CanAssault(IReadOnlyList<Fleet> fleets, Planet planet);

        /// <summary>Calculates the bombardment strength of fleets.</summary>
        /// <param name="fleets">The fleets to evaluate.</param>
        /// <returns>The combined bombardment strength.</returns>
        int GetBombardmentStrength(IEnumerable<Fleet> fleets);

        /// <summary>Calculates the active bombardment shield strength of a planet.</summary>
        /// <param name="planet">The defending planet.</param>
        /// <returns>The active shield strength.</returns>
        int GetBombardmentShieldStrength(Planet planet);

        /// <summary>Calculates a fleet's projected bombardment strength.</summary>
        /// <param name="fleet">The fleet to evaluate.</param>
        /// <returns>The projected bombardment strength.</returns>
        int GetProjectedBombardmentStrength(Fleet fleet);

        /// <summary>Calculates one capital ship's projected bombardment contribution.</summary>
        /// <param name="fleet">The containing fleet.</param>
        /// <param name="capitalShip">The capital ship to evaluate.</param>
        /// <returns>The projected bombardment contribution.</returns>
        int GetProjectedCapitalShipBombardmentStrength(Fleet fleet, CapitalShip capitalShip);

        /// <summary>Calculates planetary shield resistance to bombardment.</summary>
        /// <param name="planet">The defending planet.</param>
        /// <returns>The active shield resistance.</returns>
        int GetBombardmentShieldResistance(Planet planet);

        /// <summary>Determines whether a planet has active military targets.</summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <param name="defenderInstanceID">The defending faction.</param>
        /// <returns>True when a military target remains.</returns>
        bool HasActiveMilitaryTargets(Planet planet, string defenderInstanceID);

        /// <summary>Calculates the assault leadership bonus.</summary>
        /// <param name="officers">The participating officers.</param>
        /// <param name="rank">The required command rank.</param>
        /// <param name="ownerInstanceID">The attacking faction.</param>
        /// <returns>The leadership bonus.</returns>
        int GetAssaultLeadershipBonus(
            IEnumerable<Officer> officers,
            OfficerRank rank,
            string ownerInstanceID
        );

        /// <summary>Determines whether planetary shields prevent an assault.</summary>
        /// <param name="planet">The defending planet.</param>
        /// <returns>True when shields block the assault.</returns>
        bool IsAssaultBlockedByShields(Planet planet);

        /// <summary>Estimates assault success for attacking fleets.</summary>
        /// <param name="fleets">The attacking fleets.</param>
        /// <param name="planet">The defending planet.</param>
        /// <returns>The estimated success percentage.</returns>
        int EstimateAssaultSuccess(IReadOnlyList<Fleet> fleets, Planet planet);

        /// <summary>Calculates a faction's garrison requirement on a planet.</summary>
        /// <param name="planet">The planet to evaluate.</param>
        /// <param name="faction">The occupying faction.</param>
        /// <returns>The required regiment count.</returns>
        int GetGarrisonRequirement(Planet planet, Faction faction);
    }
}
