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
///
/// @NOTE: This is not meant to be the game's final AI system. This is just a simple AI system
/// that is meant to be replaced with a more sophisticated one in the future.
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
    /// <param name="provider">Random number provider for AI decisions.</param>
    public void Update(IRandomNumberProvider provider)
    {
        foreach (Faction faction in game.Factions.Where(f => f.IsAIControlled()))
        {
            UpdateFaction(faction, provider);
        }
    }

    /// <summary>
    /// Updates various aspects of the AI for a specific faction.
    /// </summary>
    /// <param name="provider">Random number provider for AI decisions.</param>
    private void UpdateFaction(Faction faction, IRandomNumberProvider provider)
    {
        GameLogger.Log(
            $"Faction {faction.GetDisplayName()} Balance: {game.GetRefinedMaterials(faction)}"
        );

        UpdateOfficers(faction, provider);
        UpdateBuildings(faction);
        UpdateShipProduction(faction);
        UpdateTroopTraining(faction);
    }

    /// <summary>
    /// Manages officer assignments for missions.
    /// </summary>
    /// <param name="provider">Random number provider for mission initiation.</param>
    private void UpdateOfficers(Faction faction, IRandomNumberProvider provider)
    {
        List<Officer> availableOfficers = faction.GetAvailableOfficers(faction);

        foreach (Officer officer in availableOfficers)
        {
            if (!officer.IsMovable())
                continue;

            if (officer.IsMain && game.GetUnrecruitedOfficers(faction.InstanceID).Any())
            {
                InitiateRecruitmentMission(officer, faction, provider);
            }
            else if (
                officer.IsMain
                || officer.GetSkillValue(MissionParticipantSkill.Diplomacy) > 60
            )
            {
                InitiateDiplomacyMission(officer, provider);
            }
        }
    }

    /// <summary>
    /// Initiates a recruitment mission for the given officer.
    /// </summary>
    /// <param name="provider">Random number provider for mission duration.</param>
    private void InitiateRecruitmentMission(
        Officer officer,
        Faction faction,
        IRandomNumberProvider provider
    )
    {
        Planet targetPlanet = faction.GetNearestPlanetTo(officer);
        GameLogger.Log(
            $"Sending {officer.GetDisplayName()} on a recruitment mission to {targetPlanet.GetDisplayName()}."
        );
        missionManager.InitiateMission(MissionType.Recruitment, officer, targetPlanet, provider);
    }

    /// <summary>
    /// Initiates a diplomacy mission for the given officer.
    /// </summary>
    /// <param name="provider">Random number provider for mission duration.</param>
    private void InitiateDiplomacyMission(Officer officer, IRandomNumberProvider provider)
    {
        Planet targetPlanet = FindNearestUnownedPlanet(officer);
        if (targetPlanet != null)
        {
            GameLogger.Log(
                $"Sending {officer.GetDisplayName()} on a diplomacy mission to {targetPlanet.GetDisplayName()}."
            );
            missionManager.InitiateMission(MissionType.Diplomacy, officer, targetPlanet, provider);
        }
    }

    /// <summary>
    /// Finds the nearest unowned planet for diplomacy missions.
    /// </summary>
    private Planet FindNearestUnownedPlanet(Officer officer)
    {
        return officer
                .GetParentOfType<PlanetSystem>()
                .GetChildren<Planet>(node =>
                    node is Planet planet
                    && planet.GetOwnerInstanceID() == null
                    && planet.IsColonized
                )
                .FirstOrDefault() as Planet;
    }

    /// <summary>
    /// Manages construction of buildings.
    /// </summary>
    private void UpdateBuildings(Faction faction)
    {
        Queue<Planet> idleConstructionYards = new Queue<Planet>(
            faction.GetIdleFacilities(ManufacturingType.Building)
        );

        if (!idleConstructionYards.Any())
            return;

        BuildStructures(idleConstructionYards, faction, BuildingType.Mine);
        BuildStructures(idleConstructionYards, faction, BuildingType.Refinery);
        BuildStructures(idleConstructionYards, faction, BuildingType.ConstructionFacility);
        BuildStructures(idleConstructionYards, faction, BuildingType.Shipyard);
        BuildStructures(idleConstructionYards, faction, BuildingType.TrainingFacility);
    }

    /// <summary>
    /// Builds structures of a specific type for the faction.
    /// </summary>
    private void BuildStructures(
        Queue<Planet> idleConstructionYards,
        Faction faction,
        BuildingType buildingType
    )
    {
        Technology buildingTech = GetHighestTierTechnology(
            faction,
            ManufacturingType.Building,
            buildingType
        );
        IManufacturable manufacturable = buildingTech.GetReference();

        if (
            game.GetRefinedMaterials(faction) < manufacturable.GetConstructionCost()
            || !idleConstructionYards.Any()
        )
            return;

        if (manufacturable is Building building)
        {
            Planet targetPlanet = GetBestPlanetForBuilding(
                idleConstructionYards.Peek(),
                faction,
                building
            );
            if (targetPlanet != null)
            {
                Planet sourcePlanet = idleConstructionYards.Dequeue();

                // Create building from technology template
                IManufacturable item = buildingTech.GetReferenceCopy();
                item.SetOwnerInstanceID(faction.GetInstanceID());

                // Set destination for movement after completion
                if (item is IMovable movable)
                {
                    movable.Movement.DestinationInstanceID = targetPlanet.GetInstanceID();
                }

                // Enqueue on source planet (AI bypasses cost check)
                manufacturingManager.Enqueue(sourcePlanet, item, ignoreCost: true);
            }
        }
    }

    /// <summary>
    /// Manages production of ships.
    /// </summary>
    private void UpdateShipProduction(Faction faction)
    {
        List<Planet> idleShipyards = faction.GetIdleFacilities(ManufacturingType.Ship);

        if (!idleShipyards.Any())
            return;

        Technology starfighterTech = GetHighestTierTechnology(
            faction,
            ManufacturingType.Ship,
            typeof(Starfighter)
        );
        IManufacturable manufacturable = starfighterTech.GetReference();

        if (game.GetRefinedMaterials(faction) <= manufacturable.GetConstructionCost())
            return;

        foreach (Planet shipyard in idleShipyards)
        {
            AssignStarfightersToFleets(faction, shipyard, starfighterTech);
        }
    }

    /// <summary>
    /// Assigns starfighters to fleets with available capacity.
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
                // Create starfighter from technology template
                IManufacturable item = starfighterTech.GetReferenceCopy();
                item.SetOwnerInstanceID(faction.GetInstanceID());

                // Set destination for movement after completion
                if (item is IMovable movable)
                {
                    movable.Movement.DestinationInstanceID = fleet.GetInstanceID();
                }

                // Enqueue on shipyard planet (AI bypasses cost check)
                manufacturingManager.Enqueue(shipyard, item, ignoreCost: true);
            }
            else
            {
                break; // Stop if we can't afford more starfighters
            }
        }
    }

    /// <summary>
    /// Manages training of troops.
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
        IManufacturable manufacturable = regimentTech.GetReference();

        if (game.GetRefinedMaterials(faction) <= manufacturable.GetConstructionCost())
            return;

        foreach (Planet trainingFacility in idleTrainingFacilities)
        {
            AssignRegimentsToFleets(faction, trainingFacility, regimentTech);
        }
    }

    /// <summary>
    /// Assigns regiments to fleets with available capacity.
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
                // Create regiment from technology template
                IManufacturable item = regimentTech.GetReferenceCopy();
                item.SetOwnerInstanceID(faction.GetInstanceID());

                // Set destination for movement after completion
                if (item is IMovable movable)
                {
                    movable.Movement.DestinationInstanceID = fleet.GetInstanceID();
                }

                // Enqueue on training facility planet (AI bypasses cost check)
                manufacturingManager.Enqueue(trainingFacility, item, ignoreCost: true);
            }
            else
            {
                break; // Stop if we can't afford more regiments
            }
        }
    }

    /// <summary>
    /// Finds the best planet for constructing the given building.
    /// </summary>
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

    /// <summary>
    /// Calculates the priority score for building placement.
    /// </summary>
    private int CalculateBuildingPriority(Planet planet, BuildingType buildingType)
    {
        switch (buildingType)
        {
            case BuildingType.Mine:
                return CalculateMinePriority(planet);
            case BuildingType.Defense:
                return planet.GetBuildingTypeCount(BuildingType.Defense);
            case BuildingType.Refinery:
                return CalculateRefineryPriority(planet);
            case BuildingType.Shipyard:
            case BuildingType.TrainingFacility:
            case BuildingType.ConstructionFacility:
                return CalculateFacilityPriority(planet, buildingType);
            default:
                return 0;
        }
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

    /// <summary>
    /// Retrieves the highest-tier technology for the given manufacturing type and reference type.
    /// </summary>
    private Technology GetHighestTierTechnology(
        Faction faction,
        ManufacturingType manufacturingType,
        Type referenceType
    )
    {
        return faction
            .GetResearchedTechnologies(manufacturingType)
            .Where(tech => tech.GetReference().GetType() == referenceType)
            .LastOrDefault();
    }

    /// <summary>
    /// Retrieves the highest-tier technology for the given manufacturing type and building type.
    /// </summary>
    private Technology GetHighestTierTechnology(
        Faction faction,
        ManufacturingType manufacturingType,
        BuildingType buildingType
    )
    {
        return faction
            .GetResearchedTechnologies(manufacturingType)
            .Where(tech => (tech.GetReference() as Building)?.GetBuildingType() == buildingType)
            .LastOrDefault();
    }
}
