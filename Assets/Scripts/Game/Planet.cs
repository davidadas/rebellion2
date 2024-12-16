using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ICollectionExtensions;

/// <summary>
/// Represents a planet in the game. A planet is a scene node that can contain fleets,
/// officers, regiments, missions, and buildings. It also has a popular support rating,
/// which is a measure of how much the planet's population supports a given faction.
/// </summary>
public class Planet : ContainerNode
{
    // PLANET Constants
    private const int DISTANCE_DIVISOR = 5;
    private const int DISTANCE_BASE = 100;
    private const int MAX_POPULAR_SUPPORT = 100;

    // Planet Properties
    public bool IsColonized { get; set; }
    public int OrbitSlots { get; set; }
    public int GroundSlots { get; set; }
    public int NumRawResourceNodes { get; set; }
    public int PositionX { get; set; }
    public int PositionY { get; set; }

    // Planet Status
    public bool IsUprising { get; set; }
    public bool IsDestroyed { get; set; }
    public bool IsHeadquarters { get; set; }

    // Popular Support
    public Dictionary<string, int> PopularSupport = new Dictionary<string, int>();

    // Child Nodes
    public List<Fleet> Fleets = new List<Fleet>();
    public List<Officer> Officers = new List<Officer>();
    public List<Regiment> Regiments = new List<Regiment>();
    public List<Mission> Missions = new List<Mission>();
    public Dictionary<BuildingSlot, List<Building>> Buildings = new Dictionary<
        BuildingSlot,
        List<Building>
    >()
    {
        { BuildingSlot.Ground, new List<Building>() },
        { BuildingSlot.Orbit, new List<Building>() },
    };

    // Manufacturing Status
    [PersistableIgnore]
    public Dictionary<ManufacturingType, List<IManufacturable>> ManufacturingQueue { get; } =
        new Dictionary<ManufacturingType, List<IManufacturable>>();

    /// <summary>
    /// Default constructor used for deserialization.
    /// </summary>
    public Planet() { }

    /// <summary>
    /// Checks if the planet is blockaded.
    /// </summary>
    /// <returns>True if the planet is blockaded, false otherwise.</returns>
    public bool IsBlockaded()
    {
        return Fleets.Any(fleet => fleet.OwnerInstanceID != this.OwnerInstanceID);
    }

    /// <summary>
    /// Gets the total number of raw resource nodes available on the planet or system.
    /// This represents the maximum number of resources that can be utilized.
    /// </summary>
    /// <returns>The total number of raw resource nodes.</returns>
    public int GetRawResourceNodes()
    {
        return NumRawResourceNodes;
    }

    /// <summary>
    /// Gets the number of available resource nodes that are not blockaded.
    /// If the location is blockaded, no resources can be accessed.
    /// </summary>
    /// <returns>The number of accessible resource nodes, or 0 if blockaded.</returns>
    public int GetAvailableResourceNodes()
    {
        return IsBlockaded() ? 0 : GetRawMinedResourceNodes();
    }

    /// <summary>
    /// Gets the total number of mined resource nodes, capped by the raw resource node count.
    /// This reflects the effective number of resource nodes that can be mined based on mining buildings.
    /// </summary>
    /// <returns>The total number of mined resource nodes, limited by the raw node count.</returns>
    public int GetRawMinedResourceNodes()
    {
        int mineCount = GetBuildingTypeCount(BuildingType.Mine);
        return Math.Min(NumRawResourceNodes, mineCount);
    }

    /// <summary>
    /// Gets the number of mined resources that are available and not under construction.
    /// If the location is blockaded, no resources are available.
    /// </summary>
    /// <returns>The number of available mined resources, or 0 if blockaded.</returns>
    public int GetAvailableMinedResources()
    {
        if (IsBlockaded())
        {
            return 0;
        }

        int mineCount = GetBuildingTypeCount(BuildingType.Mine, includeUnderConstruction: false);
        return Math.Min(NumRawResourceNodes, mineCount);
    }

    /// <summary>
    /// Gets the total refinement capacity based on available refinery buildings.
    /// This represents the maximum number of resources that can be refined.
    /// </summary>
    /// <returns>The total refinement capacity.</returns>
    public int GetRawRefinementCapacity()
    {
        return GetBuildingTypeCount(BuildingType.Refinery);
    }

    /// <summary>
    /// Gets the available refinement capacity that is not blockaded and excludes refineries under construction.
    /// If the location is blockaded, no refinement capacity is available.
    /// </summary>
    /// <returns>The available refinement capacity, or 0 if blockaded


    /// <summary>
    /// Returns the popular support for a faction on the planet.
    /// </summary>
    /// <param name="factionInstanceId">The instance ID of the faction.</param>
    /// <returns>The popular support for the faction.</returns>
    public int GetPopularSupport(string factionInstanceId)
    {
        return PopularSupport.TryGetValue(factionInstanceId, out int support) ? support : 0;
    }

    /// <summary>
    /// Sets the popular support for a faction on the planet.
    /// </summary>
    /// <param name="factionInstanceId">The instance ID of the faction.</param>
    /// <param name="support">The new level of support for the faction.</param>
    public void SetPopularSupport(string factionInstanceId, int support)
    {
        // Calculate the difference between the new support and the current support.
        int currentSupport = PopularSupport.TryGetValue(factionInstanceId, out int existingSupport)
            ? existingSupport
            : 0;
        int supportDifference = support - currentSupport;
        int totalSupport = PopularSupport.Values.Sum();

        // Check if the total support is within the maximum limit.
        if (totalSupport + supportDifference <= MAX_POPULAR_SUPPORT)
        {
            PopularSupport[factionInstanceId] = support;
        }
        // If the total support exceeds the maximum limit, shift support from other factions.
        else
        {
            int overage = totalSupport + supportDifference - MAX_POPULAR_SUPPORT;
            ShiftFactionSupport(factionInstanceId, overage);
            PopularSupport[factionInstanceId] = support;
        }
    }

    /// <summary>
    /// Shifts the support of other factions to accommodate the increase in support for the given faction.
    /// </summary>
    /// <param name="excludedFactionId">The faction ID to exclude from reduction.</param>
    /// <param name="overage">The amount of support to reduce from other factions.</param>
    private void ShiftFactionSupport(string excludedFactionId, int overage)
    {
        PopularSupport
            .Where(kvp => kvp.Key != excludedFactionId) // Filter out the excluded faction.
            .OrderByDescending(kvp => kvp.Value) // Sort factions by support in descending order.
            .ToList()
            .ForEach(kvp =>
            {
                // Decrease faction's support by the overage amount.
                int reduction = Math.Min(overage, kvp.Value);
                PopularSupport[kvp.Key] -= reduction;
                overage -= reduction;

                if (overage <= 0)
                {
                    return;
                }
            });
    }

    /// <summary>
    /// Gets the position of the planet as a Point.
    /// </summary>
    /// <returns>A Point representing the planet's position.</returns>
    public Point GetPosition()
    {
        return new Point(PositionX, PositionY);
    }

    /// <summary>
    /// Calculates the travel time to another planet using a binary search approximation of Euclidean distance.
    /// The travel time is calculated in ticks.
    /// </summary>
    /// <param name="targetPlanet">The target planet.</param>
    /// <returns>Travel time in ticks.</returns>
    public int GetDistanceTo(Planet targetPlanet)
    {
        int dx = this.PositionX - targetPlanet.PositionX;
        int dy = this.PositionY - targetPlanet.PositionY;
        double rawDistance = Math.Sqrt(dx * dx + dy * dy);

        return (int)(rawDistance / DISTANCE_DIVISOR * DISTANCE_BASE / 100);
    }

    /// <summary>
    /// Calculates the total number of days required to produce a specified number of manufacturable items.
    /// </summary>
    /// <param name="manufacturable">The item to be manufactured, which implements IManufacturable.</param>
    /// <param name="quantity">The number of items to manufacture.</param>
    /// <returns>The total days required to complete the manufacturing.</returns>
    public int GetBuildTime(IManufacturable manufacturable, int quantity)
    {
        // Calculate the total material cost and the combined production rate for the manufacturable.
        int totalMaterialCost = manufacturable.GetConstructionCost() * quantity;

        // Calculate the combined time required to produce the items.
        ManufacturingType requiredManufacturingType = manufacturable.GetManufacturingType();
        double combinedRate = GetCombinedProductionRate(requiredManufacturingType);

        if (combinedRate == 0)
        {
            return 0;
        }

        // Calculate the combined time required to produce the items.
        double combinedTime = totalMaterialCost / combinedRate;

        return (int)Math.Ceiling(combinedTime);
    }

    /// <summary>
    /// Calculates the combined production rate for a specific manufacturing type.
    /// </summary>
    /// <param name="manufacturingType">The manufacturing type to calculate the rate for.</param>
    /// <returns>The combined production rate.</returns>
    private double GetCombinedProductionRate(ManufacturingType manufacturingType)
    {
        return Buildings
            .Values.SelectMany(buildingList => buildingList)
            .Where(building => building.GetProductionType() == manufacturingType)
            .Sum(building => 1.0 / building.GetProcessRate());
    }

    /// <summary>
    /// Calculates the total production progress per tick for a given manufacturing type on a planet.
    /// </summary>
    /// <param name="type">The manufacturing type.</param>
    /// <returns>The calculated progress.</returns>
    public int GetProductionRate(ManufacturingType type)
    {
        double combinedRate = GetCombinedProductionRate(type);

        return (int)Math.Ceiling(combinedRate);
    }

    /// <summary>
    /// Checks if units can be manufactured on this planet.
    /// </summary>
    /// <returns>True if units can be manufactured, false otherwise.</returns>
    public bool CanManufactureUnits()
    {
        return !IsBlockaded() && !IsDestroyed && !IsUprising;
    }

    /// <summary>
    /// Gets the manufacturing queue for the planet.
    /// </summary>
    /// <returns>The manufacturing queue.</returns>
    public Dictionary<ManufacturingType, List<IManufacturable>> GetManufacturingQueue()
    {
        return ManufacturingQueue;
    }

    /// <summary>
    /// Adds a manufacturable unit to the manufacturing queue.
    /// </summary>
    /// <param name="manufacturable">The unit to be added to the manufacturing queue.</param>
    public void AddToManufacturingQueue(IManufacturable manufacturable)
    {
        ValidateManufacturable(manufacturable);
        ManufacturingType type = manufacturable.GetManufacturingType();

        if (!ManufacturingQueue.ContainsKey(type))
        {
            ManufacturingQueue.Add(type, new List<IManufacturable>());
        }

        manufacturable.SetPosition(GetPosition());
        ManufacturingQueue[type].Add(manufacturable);
    }

    /// <summary>
    /// Validates if a manufacturable can be added to the manufacturing queue.
    /// </summary>
    /// <param name="manufacturable">The manufacturable to validate.</param>
    ///
    private void ValidateManufacturable(IManufacturable manufacturable)
    {
        if (manufacturable is ISceneNode sceneNode && sceneNode.GetParent() == null)
        {
            throw new InvalidSceneOperationException(
                $"Unit {sceneNode.GetDisplayName()} must have a parent to be added to the manufacturing queue."
            );
        }

        if (this.OwnerInstanceID != manufacturable.OwnerInstanceID)
        {
            throw new SceneAccessException(manufacturable, this);
        }
    }

    /// <summary>
    /// Retrieves the count of idle production facilities for a specific manufacturing type.
    /// </summary>
    /// <param name="type">The manufacturing type.</param>
    /// <returns>The count of idle production facilities of the specified type.</returns>
    public int GetIdleManufacturingFacilities(ManufacturingType type)
    {
        if (
            ManufacturingQueue.TryGetValue(type, out List<IManufacturable> manufacturingQueue)
            && manufacturingQueue.Count > 0
        )
        {
            return 0;
        }

        return Buildings
            .Values.SelectMany(buildingList => buildingList)
            .Count(building => building.GetProductionType() == type);
    }

    /// <summary>
    /// Gets the fleets on the planet.
    /// </summary>
    /// <returns>A list of fleets.</returns>
    public List<Fleet> GetFleets()
    {
        return Fleets;
    }

    /// <summary>
    /// Adds a fleet to the planet.
    /// </summary>
    /// <param name="fleet">The fleet to add.</param>
    public void AddFleet(Fleet fleet)
    {
        Fleets.Add(fleet);
    }

    /// <summary>
    /// Removes a fleet from the planet.
    /// </summary>
    /// <param name="fleet">The fleet to remove.</param>
    public void RemoveFleet(Fleet fleet)
    {
        Fleets.Remove(fleet);
    }

    /// <summary>
    /// Gets the buildings in a specific slot.
    /// </summary>
    /// <param name="slot">The building slot.</param>
    /// <param name="includeUnderConstruction">Whether to include buildings under construction.</param>
    /// <returns>A list of buildings.</returns>
    public List<Building> GetBuildings(BuildingSlot slot, bool includeUnderConstruction = false)
    {
        return Buildings[slot].ToList();
    }

    /// <summary>
    /// Gets the count of buildings of a specific type.
    /// </summary>
    /// <param name="buildingType">The type of building.</param>
    /// <param name="includeUnderConstruction">Whether to include buildings under construction.</param>
    /// <returns>The count of buildings of the specified type.</returns>
    public int GetBuildingTypeCount(BuildingType buildingType, bool includeUnderConstruction = true)
    {
        return Buildings
            .Values.SelectMany(buildingList => buildingList)
            .Count(building =>
            {
                return (
                        includeUnderConstruction
                        || building.GetManufacturingStatus() == ManufacturingStatus.Complete
                    )
                    && building.GetBuildingType() == buildingType;
            });
    }

    /// <summary>
    /// Gets the buildings of a specific production type.
    /// </summary>
    /// <param name="productionType">The production type.</param>
    /// <returns>A list of buildings of the specified production type.</returns>
    public List<Building> GetBuildings(ManufacturingType productionType)
    {
        return Buildings
            .Values.SelectMany(buildingList => buildingList)
            .Where(building => building.GetProductionType() == productionType)
            .ToList();
    }

    /// <summary>
    /// Adds a building to the planet.
    /// </summary>
    /// <param name="building">The building to add.</param>
    /// <exception cref="GameException">Thrown when the planet is not colonized or at capacity.</exception>
    private void AddBuilding(Building building)
    {
        ValidateBuilding(building);
        BuildingSlot slot = building.GetBuildingSlot();
        Buildings[slot].Add(building);
    }

    /// <summary>
    /// Validates if a building can be added to the planet.
    /// </summary>
    /// <param name="building">The building to validate.</param>
    private void ValidateBuilding(Building building)
    {
        // Check if the planet is colonized.
        if (!IsColonized)
        {
            throw new GameStateException(
                $"Cannot add building {building.GetDisplayName()} to {this.GetDisplayName()}. Planet is not colonized."
            );
        }

        // Check if the building is owned by the planet's owner.
        if (building.GetOwnerInstanceID() != this.GetOwnerInstanceID())
        {
            throw new SceneAccessException(building, this);
        }

        // Check if the planet is at capacity.
        BuildingSlot slot = building.GetBuildingSlot();
        if (
            (slot == BuildingSlot.Ground && Buildings[slot].Count == GroundSlots)
            || (slot == BuildingSlot.Orbit && Buildings[slot].Count == OrbitSlots)
        )
        {
            throw new GameStateException(
                $"Cannot add {building.GetDisplayName()} to {this.GetDisplayName()}. Planet is at capacity."
            );
        }
    }

    /// <summary>
    /// Removes a building from the planet.
    /// </summary>
    /// <param name="building">The building to remove.</param>
    private void RemoveBuilding(Building building)
    {
        BuildingSlot slot = building.GetBuildingSlot();
        Buildings[slot].Remove(building);
    }

    /// <summary>
    /// Gets the available slots for a specific building slot.
    /// </summary>
    /// <param name="slot">The building slot.</param>
    /// <returns>The number of available slots.</returns>
    public int GetAvailableSlots(BuildingSlot slot)
    {
        int numUsedSlots = Buildings[slot].Count(building => building.GetBuildingSlot() == slot);
        int maxSlots = slot == BuildingSlot.Ground ? GroundSlots : OrbitSlots;
        return maxSlots - numUsedSlots;
    }

    /// <summary>
    /// Adds an officer to the planet.
    /// </summary>
    /// <param name="officer">The officer to add.</param>
    private void AddOfficer(Officer officer)
    {
        if (officer.GetOwnerInstanceID() != this.OwnerInstanceID)
        {
            throw new SceneAccessException(officer, this);
        }
        Officers.Add(officer);
    }

    /// <summary>
    /// Removes an officer from the planet.
    /// </summary>
    /// <param name="officer">The officer to remove.</param>
    private void RemoveOfficer(Officer officer)
    {
        Officers.Remove(officer);
    }

    /// <summary>
    /// Adds a mission to the planet.
    /// </summary>
    /// <param name="mission">The mission to add.</param>
    private void AddMission(Mission mission)
    {
        Missions.Add(mission);
    }

    /// <summary>
    /// Removes a mission from the planet.
    /// </summary>
    /// <param name="mission">The mission to remove.</param>
    private void RemoveMission(Mission mission)
    {
        Missions.Remove(mission);
    }

    /// <summary>
    /// Adds a regiment to the planet.
    /// </summary>
    /// <param name="regiment">The regiment to add.</param>
    private void AddRegiment(Regiment regiment)
    {
        if (regiment.GetOwnerInstanceID() != this.GetOwnerInstanceID())
        {
            throw new SceneAccessException(regiment, this);
        }
        Regiments.Add(regiment);
    }

    /// <summary>
    /// Removes a regiment from the planet.
    /// </summary>
    /// <param name="regiment">The regiment to remove.</param>
    private void RemoveRegiment(Regiment regiment)
    {
        Regiments.Remove(regiment);
    }

    /// <summary>
    /// Adds a reference node to the game.
    /// </summary>
    /// <param name="child">The game node to add as a reference.</param>
    public override void AddChild(ISceneNode child)
    {
        switch (child)
        {
            case Fleet fleet:
                AddFleet(fleet);
                break;
            case Officer officer:
                AddOfficer(officer);
                break;
            case Building building:
                AddBuilding(building);
                break;
            case Mission mission:
                AddMission(mission);
                break;
            case Regiment regiment:
                AddRegiment(regiment);
                break;
            default:
                throw new InvalidSceneOperationException(
                    $"Cannot add {child.GetDisplayName()} to {this.GetDisplayName()}. "
                        + $"Only fleets, officers, buildings, missions, and regiments are allowed."
                );
        }
    }

    /// <summary>
    /// Removes a child node from the planet.
    /// </summary>
    /// <param name="child">The child node to remove.</param>
    public override void RemoveChild(ISceneNode child)
    {
        switch (child)
        {
            case Fleet fleet:
                RemoveFleet(fleet);
                break;
            case Officer officer:
                RemoveOfficer(officer);
                break;
            case Building building:
                RemoveBuilding(building);
                break;
            case Mission mission:
                RemoveMission(mission);
                break;
            case Regiment regiment:
                RemoveRegiment(regiment);
                break;
            default:
                throw new InvalidSceneOperationException(
                    $"Cannot remove {child.GetDisplayName()} from {this.GetDisplayName()}. "
                        + $"Only fleets, officers, buildings, missions, and regiments are allowed."
                );
        }
    }

    /// <summary>
    /// Gets the child nodes of the planet.
    /// </summary>
    /// <returns>An enumerable of child nodes.</returns>
    public override IEnumerable<ISceneNode> GetChildren()
    {
        IEnumerable<ISceneNode> buildings = Buildings
            .Values.SelectMany(buildingList => buildingList)
            .Cast<ISceneNode>();

        return Fleets
            .Cast<ISceneNode>()
            .Concat(Officers)
            .Concat(Missions)
            .Concat(Regiments)
            .Concat(buildings);
    }
}
