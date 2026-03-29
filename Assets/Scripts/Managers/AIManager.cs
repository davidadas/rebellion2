using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Core.Simulation;
using Rebellion.Game;
using Rebellion.SceneGraph;
using Rebellion.Systems;
using Rebellion.Util.Common;

/// <summary>
/// Manages AI behavior for factions in the game.
/// </summary>
public class AIManager
{
    private readonly GameRoot game;
    private readonly MissionSystem missionManager;
    private readonly MovementSystem movementManager;
    private readonly ManufacturingSystem manufacturingManager;

    public AIManager(
        GameRoot game,
        MissionSystem missionManager,
        MovementSystem movementManager,
        ManufacturingSystem manufacturingManager
    )
    {
        this.game = game;
        this.missionManager = missionManager;
        this.movementManager = movementManager;
        this.manufacturingManager = manufacturingManager;
    }

    /// <summary>
    /// Updates the AI for all AI-controlled factions.
    /// </summary>
    public void Update(IRandomNumberProvider provider)
    {
        foreach (Faction faction in game.Factions.Where(f => f.IsAIControlled()))
        {
            UpdateFaction(faction, provider);
        }
    }

    /// <summary>
    /// Runs the AI decision cycle for one faction.
    /// Order is intentional: crises first, economy before military, missions last.
    /// </summary>
    private void UpdateFaction(Faction faction, IRandomNumberProvider provider)
    {
        HandleUprisings(faction, provider);
        HandleBlockades(faction);
        DeployPatrolFleetsToSystems(faction);
        UpdateEconomy(faction);
        UpdateCapitalShipProduction(faction);
        UpdateStarfighterProduction(faction);
        UpdateTroopTraining(faction);
        UpdateFleetMovement(faction);
        UpdateOfficerMissions(faction, provider);
    }

    // -------------------------------------------------------------------------
    // Phase 1: Uprising suppression
    // -------------------------------------------------------------------------

    /// <summary>
    /// Sends the best available leader to suppress each owned planet in uprising.
    /// </summary>
    private void HandleUprisings(Faction faction, IRandomNumberProvider provider)
    {
        List<Planet> risingPlanets = faction
            .GetOwnedUnitsByType<Planet>()
            .Where(p => p.IsInUprising)
            .ToList();

        foreach (Planet planet in risingPlanets)
        {
            // Ownership may have changed since the list was built (e.g. a diplomacy mission
            // succeeded mid-tick). Skip rather than throw.
            if (planet.GetOwnerInstanceID() != faction.InstanceID)
                continue;

            Officer leader = faction
                .GetAvailableOfficers(faction)
                .Where(o => o.IsMovable())
                .OrderByDescending(o => o.GetSkillValue(MissionParticipantSkill.Leadership))
                .FirstOrDefault();

            if (leader == null)
                break;

            missionManager.InitiateMission(MissionType.SubdueUprising, leader, planet, provider);
        }
    }

    // -------------------------------------------------------------------------
    // Phase 2: Blockade relief
    // -------------------------------------------------------------------------

    /// <summary>
    /// Routes the nearest idle fleet toward each blockaded owned planet.
    /// Tracks dispatched fleets so the same fleet isn't sent twice.
    /// </summary>
    private void HandleBlockades(Faction faction)
    {
        List<Planet> blockaded = faction
            .GetOwnedUnitsByType<Planet>()
            .Where(p => p.IsBlockaded())
            .OrderByDescending(p => p.GetRawResourceNodes())
            .ToList();

        HashSet<string> dispatched = new HashSet<string>();

        foreach (Planet planet in blockaded)
        {
            Fleet relief = faction
                .GetOwnedUnitsByType<Fleet>()
                .Where(f =>
                    f.IsMovable()
                    && f.CapitalShips.Count > 0
                    && !dispatched.Contains(f.GetInstanceID())
                )
                .OrderBy(f => f.GetParentOfType<Planet>()?.GetRawDistanceTo(planet) ?? int.MaxValue)
                .FirstOrDefault();

            if (relief == null)
                break;

            movementManager.RequestMove(relief, planet);
            dispatched.Add(relief.GetInstanceID());
        }
    }

    // -------------------------------------------------------------------------
    // Phase 3: Economy (buildings)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds buildings in priority order: mines to match raw nodes, then
    /// refineries to match mine output, then production facilities.
    /// </summary>
    private void UpdateEconomy(Faction faction)
    {
        if (faction.GetTotalRawMinedResources() < faction.GetTotalRawResourceNodes())
        {
            BuildOneOf(faction, BuildingType.Mine);
        }
        else if (faction.GetTotalRawRefinementCapacity() < faction.GetTotalRawMinedResources())
        {
            BuildOneOf(faction, BuildingType.Refinery);
        }
        else
        {
            foreach (
                BuildingType facilityType in new[]
                {
                    BuildingType.ConstructionFacility,
                    BuildingType.Shipyard,
                    BuildingType.TrainingFacility,
                }
            )
            {
                if (BuildOneOf(faction, facilityType))
                    break;
            }
        }
    }

    /// <summary>
    /// Enqueues one building of the given type on the best available construction yard.
    /// Returns true if a building was successfully enqueued.
    /// </summary>
    private bool BuildOneOf(Faction faction, BuildingType buildingType)
    {
        Technology tech = GetHighestTierTechnology(
            faction,
            ManufacturingType.Building,
            buildingType
        );
        if (tech == null)
            return false;

        IManufacturable item = tech.GetReferenceCopy();
        item.SetOwnerInstanceID(faction.GetInstanceID());

        if (game.GetRefinedMaterials(faction) < item.GetConstructionCost())
            return false;

        List<Planet> idleYards = faction.GetIdleFacilities(ManufacturingType.Building);
        if (!idleYards.Any())
            return false;

        if (item is not Building building)
            return false;

        Planet target = GetBestPlanetForBuilding(idleYards[0], faction, building);
        if (target == null)
            return false;

        item.SetOwnerInstanceID(faction.GetInstanceID());
        if (item is IMovable movable)
        {
            movable.Movement ??= new MovementState();
            movable.Movement.DestinationInstanceID = target.GetInstanceID();
        }

        manufacturingManager.Enqueue(idleYards[0], item, ignoreCost: false);
        return true;
    }

    // -------------------------------------------------------------------------
    // Phase 4: Capital ship production
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds capital ships at idle shipyards until the faction has one per owned planet.
    /// ManufacturingSystem creates the fleet container automatically on completion.
    /// </summary>
    private void UpdateCapitalShipProduction(Faction faction)
    {
        Technology tech = GetHighestTierTechnology(
            faction,
            ManufacturingType.Ship,
            typeof(CapitalShip)
        );
        if (tech == null)
            return;

        int ownedShips = faction.GetOwnedUnitsByType<Fleet>().Sum(f => f.CapitalShips.Count);
        int targetShips = faction.GetOwnedUnitsByType<Planet>().Count;

        foreach (Planet shipyard in faction.GetIdleFacilities(ManufacturingType.Ship))
        {
            if (ownedShips >= targetShips)
                break;

            IManufacturable item = tech.GetReferenceCopy();
            item.SetOwnerInstanceID(faction.GetInstanceID());

            if (game.GetRefinedMaterials(faction) < item.GetConstructionCost())
                continue;

            manufacturingManager.Enqueue(shipyard, item, ignoreCost: false);
            ownedShips++;
        }
    }

    // -------------------------------------------------------------------------
    // Phase 5: Starfighter production
    // -------------------------------------------------------------------------

    /// <summary>
    /// Fills fleets with starfighters from idle shipyards.
    /// </summary>
    private void UpdateStarfighterProduction(Faction faction)
    {
        List<Planet> idleShipyards = faction.GetIdleFacilities(ManufacturingType.Ship);
        if (!idleShipyards.Any())
            return;

        Technology starfighterTech = GetHighestTierTechnology(
            faction,
            ManufacturingType.Ship,
            typeof(Starfighter)
        );
        if (starfighterTech == null)
            return;

        IManufacturable prototype = starfighterTech.GetReference();
        if (game.GetRefinedMaterials(faction) <= prototype.GetConstructionCost())
            return;

        foreach (Planet shipyard in idleShipyards)
        {
            AssignStarfightersToFleets(faction, shipyard, starfighterTech);
        }
    }

    /// <summary>
    /// Assigns starfighters to fleets with available capacity from a given shipyard.
    /// </summary>
    private void AssignStarfightersToFleets(
        Faction faction,
        Planet shipyard,
        Technology starfighterTech
    )
    {
        IEnumerable<Fleet> fleetsWithCapacity = faction
            .GetOwnedUnitsByType<Fleet>()
            .Where(fleet => fleet.GetExcessStarfighterCapacity() > 0)
            .OrderBy(fleet => fleet.GetExcessStarfighterCapacity());

        foreach (Fleet fleet in fleetsWithCapacity)
        {
            if (
                game.GetRefinedMaterials(faction)
                > starfighterTech.GetReference().GetConstructionCost()
            )
            {
                IManufacturable item = starfighterTech.GetReferenceCopy();
                item.SetOwnerInstanceID(faction.GetInstanceID());

                if (item is IMovable movable)
                {
                    movable.Movement ??= new MovementState();
                    movable.Movement.DestinationInstanceID = fleet.GetInstanceID();
                }

                manufacturingManager.Enqueue(shipyard, item, ignoreCost: false);
            }
            else
            {
                break;
            }
        }
    }

    // -------------------------------------------------------------------------
    // Phase 6: Troop training
    // -------------------------------------------------------------------------

    /// <summary>
    /// Fills fleets with regiments from idle training facilities.
    /// </summary>
    private void UpdateTroopTraining(Faction faction)
    {
        List<Planet> idleTrainingFacilities = faction.GetIdleFacilities(ManufacturingType.Troop);
        if (!idleTrainingFacilities.Any())
            return;

        Technology regimentTech = GetHighestTierTechnology(
            faction,
            ManufacturingType.Troop,
            typeof(Regiment)
        );
        if (regimentTech == null)
            return;

        IManufacturable prototype = regimentTech.GetReference();
        if (game.GetRefinedMaterials(faction) <= prototype.GetConstructionCost())
            return;

        foreach (Planet trainingFacility in idleTrainingFacilities)
        {
            AssignRegimentsToFleets(faction, trainingFacility, regimentTech);
        }
    }

    /// <summary>
    /// Assigns regiments to fleets with available capacity from a given training facility.
    /// </summary>
    private void AssignRegimentsToFleets(
        Faction faction,
        Planet trainingFacility,
        Technology regimentTech
    )
    {
        IEnumerable<Fleet> fleetsWithCapacity = faction
            .GetOwnedUnitsByType<Fleet>()
            .Where(fleet => fleet.GetExcessRegimentCapacity() > 0)
            .OrderBy(fleet => fleet.GetExcessRegimentCapacity());

        foreach (Fleet fleet in fleetsWithCapacity)
        {
            if (
                game.GetRefinedMaterials(faction)
                > regimentTech.GetReference().GetConstructionCost()
            )
            {
                IManufacturable item = regimentTech.GetReferenceCopy();
                item.SetOwnerInstanceID(faction.GetInstanceID());

                if (item is IMovable movable)
                {
                    movable.Movement ??= new MovementState();
                    movable.Movement.DestinationInstanceID = fleet.GetInstanceID();
                }

                manufacturingManager.Enqueue(trainingFacility, item, ignoreCost: false);
            }
            else
            {
                break;
            }
        }
    }

    // -------------------------------------------------------------------------
    // Phase 3: Patrol fleet deployment
    // -------------------------------------------------------------------------

    /// <summary>
    /// Mirrors FUN_0050add0_adjust_for_fleets: ensures every colonized planet
    /// has a patrol fleet present or en route for this faction.
    /// Prefers idle battle fleets with no capital ships (lightweight scouts).
    /// </summary>
    private void DeployPatrolFleetsToSystems(Faction faction)
    {
        string factionId = faction.GetInstanceID();

        List<Fleet> idlePatrols = faction
            .GetOwnedUnitsByType<Fleet>()
            .Where(f => f.RoleType == FleetRoleType.Patrol && f.IsMovable())
            .ToList();

        List<Fleet> availableForPatrol = faction
            .GetOwnedUnitsByType<Fleet>()
            .Where(f =>
                f.RoleType == FleetRoleType.Battle && f.IsMovable() && f.CapitalShips.Count == 0
            )
            .ToList();

        List<Planet> needsPatrol = game.GetSceneNodesByType<Planet>(p =>
            p.IsColonized
            && !faction
                .GetOwnedUnitsByType<Fleet>()
                .Any(f =>
                    f.RoleType == FleetRoleType.Patrol
                    && (
                        f.GetParentOfType<Planet>() == p
                        || (
                            f.Movement != null
                            && f.Movement.DestinationInstanceID == p.GetInstanceID()
                        )
                    )
                )
        );

        foreach (Planet planet in needsPatrol)
        {
            Fleet patrol = idlePatrols.FirstOrDefault();
            if (patrol == null)
            {
                patrol = availableForPatrol.FirstOrDefault();
                if (patrol == null)
                    break;
                patrol.RoleType = FleetRoleType.Patrol;
                availableForPatrol.Remove(patrol);
            }
            else
            {
                idlePatrols.Remove(patrol);
            }

            movementManager.RequestMove(patrol, planet);
        }
    }

    // -------------------------------------------------------------------------
    // Phase 8: Fleet movement
    // -------------------------------------------------------------------------

    /// <summary>
    /// Moves idle battle fleets: defends a contested HQ if undefended.
    /// Patrol fleets are handled by DeployPatrolFleetsToSystems and excluded here.
    /// </summary>
    private void UpdateFleetMovement(Faction faction)
    {
        List<Fleet> idle = faction
            .GetOwnedUnitsByType<Fleet>()
            .Where(f =>
                f.RoleType == FleetRoleType.Battle && f.IsMovable() && f.CapitalShips.Count > 0
            )
            .ToList();

        if (!idle.Any())
            return;

        Planet hq = game.GetSceneNodeByInstanceID<Planet>(faction.GetHQInstanceID());
        if (hq != null && hq.IsContested())
        {
            bool alreadyDefended = faction
                .GetOwnedUnitsByType<Fleet>()
                .Any(f => !f.IsMovable() && f.GetParentOfType<Planet>() == hq);

            if (!alreadyDefended)
            {
                Fleet nearest = idle.OrderBy(f =>
                        f.GetParentOfType<Planet>()?.GetRawDistanceTo(hq) ?? double.MaxValue
                    )
                    .First();
                movementManager.RequestMove(nearest, hq);
            }
        }
    }

    // -------------------------------------------------------------------------
    // Phase 9: Officer missions
    // -------------------------------------------------------------------------

    /// <summary>
    /// Dispatches available officers to missions.
    /// Mirrors the original: place character at random system, then evaluate via table lookup.
    /// </summary>
    private void UpdateOfficerMissions(Faction faction, IRandomNumberProvider provider)
    {
        List<Officer> available = faction
            .GetAvailableOfficers(faction)
            .Where(o => o.IsMovable())
            .ToList();

        GameLogger.Debug(
            $"[AI] {faction.GetDisplayName()}: {available.Count} officers available for missions."
        );

        foreach (Officer officer in available)
        {
            Planet target = FindMissionTarget(faction, provider);
            if (target == null)
                continue;

            MissionType? missionType = SelectMissionType(faction, officer, target);
            if (missionType == null)
                continue;

            GameLogger.Log(
                $"Sending {officer.GetDisplayName()} on {missionType} mission to {target.GetDisplayName()}."
            );
            missionManager.InitiateMission(missionType.Value, officer, target, provider);
        }
    }

    /// <summary>
    /// Picks a random colonized planet owned by this faction or neutral.
    /// Mirrors original's randomly_place_character target selection.
    /// </summary>
    private Planet FindMissionTarget(Faction faction, IRandomNumberProvider provider)
    {
        string factionId = faction.GetInstanceID();
        List<Planet> candidates = game.GetSceneNodesByType<Planet>(p =>
            p.IsColonized && (p.GetOwnerInstanceID() == factionId || p.GetOwnerInstanceID() == null)
        );

        if (candidates.Count == 0)
            return null;

        return candidates[provider.NextInt(0, candidates.Count - 1)];
    }

    /// <summary>
    /// Determines the best mission type for an officer at a given planet.
    /// Uses table lookups from AI.MissionTables (original MSTB .DAT files).
    /// Score formula: (officer_skill - popular_support) + leadership_rank
    /// Returns null if no viable mission exists at this planet.
    /// Priority: SubdueUprising > Diplomacy > Recruitment
    /// </summary>
    private MissionType? SelectMissionType(Faction faction, Officer officer, Planet target)
    {
        string factionId = faction.GetInstanceID();
        string owner = target.GetOwnerInstanceID();
        double popularSupport = target.GetPopularSupport(factionId);
        int rank = officer.GetSkillValue(MissionParticipantSkill.Leadership);

        // SubdueUprising: owned planet in uprising
        // Score = (combat_skill - popular_support) + rank
        if (target.IsInUprising && owner == factionId)
        {
            int score =
                officer.GetSkillValue(MissionParticipantSkill.Combat) - (int)popularSupport + rank;
            ProbabilityTable table = new ProbabilityTable(
                game.Config.AI.MissionTables.SubdueUprising
            );
            if (table.Lookup(score) > 0)
                return MissionType.SubdueUprising;
        }

        // Diplomacy: owned or neutral planet with room for support growth
        if (
            (owner == null || owner == factionId)
            && popularSupport < game.Config.Planet.MaxPopularSupport
        )
        {
            int score =
                officer.GetSkillValue(MissionParticipantSkill.Diplomacy)
                - (int)popularSupport
                + rank;
            ProbabilityTable table = new ProbabilityTable(game.Config.AI.MissionTables.Diplomacy);
            if (table.Lookup(score) > 0)
                return MissionType.Diplomacy;
        }

        // Recruitment: owned planet with unrecruited officers available
        if (owner == factionId && game.GetUnrecruitedOfficers(factionId).Any())
            return MissionType.Recruitment;

        return null;
    }

    // -------------------------------------------------------------------------
    // Building placement helpers (unchanged from original)
    // -------------------------------------------------------------------------

    private Planet GetBestPlanetForBuilding(Planet source, Faction faction, Building building)
    {
        BuildingType buildingType = building.GetBuildingType();
        BuildingSlot buildingSlot = building.GetBuildingSlot();

        return faction
            .GetOwnedUnitsByType<Planet>()
            .Where(planet => planet.GetBuildingSlotCapacity(buildingSlot) > 0)
            .OrderBy(planet => CalculateBuildingPriority(planet, buildingType))
            .ThenByDescending(planet => planet.GetBuildingSlotCapacity(buildingSlot))
            .ThenBy(planet => source.GetRawDistanceTo(planet))
            .FirstOrDefault();
    }

    private int CalculateBuildingPriority(Planet planet, BuildingType buildingType)
    {
        return buildingType switch
        {
            BuildingType.Mine => CalculateMinePriority(planet),
            BuildingType.Defense => planet.GetBuildingTypeCount(BuildingType.Defense),
            BuildingType.Refinery => CalculateRefineryPriority(planet),
            BuildingType.Shipyard
            or BuildingType.TrainingFacility
            or BuildingType.ConstructionFacility => CalculateFacilityPriority(planet, buildingType),
            _ => 0,
        };
    }

    private int CalculateMinePriority(Planet planet)
    {
        return planet.GetBuildingTypeCount(BuildingType.TrainingFacility)
            + planet.GetBuildingTypeCount(BuildingType.ConstructionFacility)
            + planet.GetBuildingTypeCount(BuildingType.Shipyard);
    }

    private int CalculateRefineryPriority(Planet planet)
    {
        int rawResourceNodeCount = planet.GetRawResourceNodes();
        int manufacturingFacilityScore = CalculateMinePriority(planet);
        return rawResourceNodeCount * 1000 + manufacturingFacilityScore;
    }

    private int CalculateFacilityPriority(Planet planet, BuildingType facilityType)
    {
        int sameFacilityCount = planet.GetBuildingTypeCount(facilityType);
        int otherFacilityCount = new[]
        {
            BuildingType.Shipyard,
            BuildingType.TrainingFacility,
            BuildingType.ConstructionFacility,
        }
            .Where(type => type != facilityType)
            .Sum(type => planet.GetBuildingTypeCount(type));

        return otherFacilityCount * 1000 - sameFacilityCount;
    }

    // -------------------------------------------------------------------------
    // Tech lookup helpers (unchanged from original)
    // -------------------------------------------------------------------------

    private Technology GetHighestTierTechnology(
        Faction faction,
        ManufacturingType manufacturingType,
        Type referenceType
    )
    {
        return faction
            .GetResearchedTechnologies(manufacturingType)
            .LastOrDefault(tech => tech.GetReference().GetType() == referenceType);
    }

    private Technology GetHighestTierTechnology(
        Faction faction,
        ManufacturingType manufacturingType,
        BuildingType buildingType
    )
    {
        return faction
            .GetResearchedTechnologies(manufacturingType)
            .LastOrDefault(tech =>
                (tech.GetReference() as Building)?.GetBuildingType() == buildingType
            );
    }
}
