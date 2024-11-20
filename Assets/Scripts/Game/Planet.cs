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
    // Distance Constants
    private const int DISTANCE_DIVISOR = 5;
    private const int DISTANCE_BASE = 100;

    // Planet Properties
    public bool IsColonized { get; set; }
    public int OrbitSlots { get; set; }
    public int GroundSlots { get; set; }
    public int NumResourceNodes { get; set; }
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

    // Manufacturing Info
    [PersistableIgnore]
    public Dictionary<ManufacturingType, List<IManufacturable>> ManufacturingQueue =
        new Dictionary<ManufacturingType, List<IManufacturable>>();

    /// <summary>
    /// Default constructor used for serialization.
    /// </summary>
    public Planet() { }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public bool IsBlockaded()
    {
        return Fleets.Any(fleet => fleet.OwnerInstanceID != this.OwnerInstanceID);
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public bool CanManufactureUnits()
    {
        return IsBlockaded() || IsDestroyed || IsUprising;
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public Dictionary<ManufacturingType, List<IManufacturable>> GetManufacturingQueue()
    {
        return ManufacturingQueue;
    }

    /// <summary>
    /// Returns the popular support for a faction on the planet.
    /// </summary>
    /// <param name="factionInstanceId"></param>
    public int GetPopularSupport(string factionInstanceId)
    {
        return PopularSupport.TryGetValue(factionInstanceId, out int support) ? support : 0;
    }

    /// <summary>
    /// Sets the popular support for a faction on the planet.
    /// </summary>
    /// <param name="factionInstanceId">The instance ID of the faction.</param>
    /// <param name="support">The level of support.</param>
    public void SetPopularSupport(string factionInstanceId, int support)
    {
        if (!PopularSupport.ContainsKey(factionInstanceId))
        {
            PopularSupport.Add(factionInstanceId, support);
        }

        PopularSupport[factionInstanceId] = support;
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
    public int GetTravelTime(Planet targetPlanet)
    {
        // Calculate the Euclidean distance between the two planets.
        int dx = this.PositionX - targetPlanet.PositionX;
        int dy = this.PositionY - targetPlanet.PositionY;

        // Approximate the Euclidean distance using a binary search.
        double rawDistance = Math.Sqrt(dx * dx + dy * dy);

        // Convert the distance to travel time in ticks.
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
        int totalMaterialCost = manufacturable.GetConstructionCost() * quantity;
        ManufacturingType requiredManufacturingType = manufacturable.GetManufacturingType();
        double combinedRate = 0;

        foreach (var slot in Buildings.Values)
        {
            foreach (var building in slot)
            {
                if (building.GetProductionType() == requiredManufacturingType)
                {
                    combinedRate += 1.0 / building.GetProcessRate();
                }
            }
        }

        if (combinedRate == 0)
            return 0;

        double combinedTime = totalMaterialCost / combinedRate;

        return (int)Math.Ceiling(combinedTime);
    }

    /// <summary>
    /// Calculates the total production progress per tick for a given manufacturing type on a planet.
    /// </summary>
    /// <param name="type">The manufacturing type.</param>
    /// <returns>The calculated progress.</returns>
    public int GetProductionRate(ManufacturingType type)
    {
        double combinedRate = GetBuildings(type).Sum(building => 1.0 / building.GetProcessRate());

        return (int)Math.Ceiling(combinedRate);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="unit"></param>
    public void AddToManufacturingQueue(IManufacturable unit)
    {
        if (unit is ISceneNode sceneNode && sceneNode.GetParent() == null)
        {
            throw new ArgumentException(
                $"Unit {sceneNode.DisplayName} must have a parent to be added to the manufacturing queue."
            );
        }

        ManufacturingType type = unit.GetManufacturingType();

        if (!ManufacturingQueue.ContainsKey(type))
        {
            ManufacturingQueue.Add(type, new List<IManufacturable>());
        }

        ManufacturingQueue[type].Add(unit);
    }

    /// <summary>
    /// Retrieves the count of idle production facilities for a specific manufacturing type.
    /// </summary>
    /// <param name="type">The manufacturing type.</param>
    /// <returns>The count of idle production facilities of the specified type.</returns>
    public int GetIdleManufacturingFacilities(ManufacturingType type)
    {
        // Check if there is any ongoing manufacturing for the specified type.
        if (
            ManufacturingQueue.TryGetValue(type, out List<IManufacturable> manufacturingQueue)
            && manufacturingQueue.Count > 0
        )
        {
            return 0; // If there's something in the queue, return 0 idle facilities
        }

        // If the queue is empty or doesn't exist, return all buildings of the specified type
        return Buildings
            .Values.SelectMany(buildingList => buildingList)
            .Count(building => building.GetProductionType() == type);
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
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
    /// <returns>An array of buildings.</returns>
    public List<Building> GetBuildings(BuildingSlot slot)
    {
        return Buildings[slot].ToList();
    }

    /// <summary>
    /// Gets the count of buildings of a specific type.
    /// </summary>
    /// <param name="buildingType"></param>
    /// <returns></returns>
    public int GetBuildingTypeCount(BuildingType buildingType)
    {
        return Buildings
            .Values.SelectMany(buildingList => buildingList)
            .Count(building => building.GetBuildingType() == buildingType);
    }

    /// <summary>
    /// Gets the buildings of a specific production type.
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
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
        if (!IsColonized)
            throw new GameException(
                $"Cannot add building {building.DisplayName} to {this.DisplayName}. Planet is not colonized."
            );

        BuildingSlot slot = building.GetBuildingSlot();

        if (
            slot == BuildingSlot.Ground && Buildings[slot].Count == GroundSlots
            || slot == BuildingSlot.Orbit && Buildings[slot].Count == OrbitSlots
        )
        {
            throw new GameException(
                $"Cannot add {building.DisplayName} to {this.DisplayName}. Planet is at capacity."
            );
        }

        Buildings[slot].Add(building);
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
        if (this.OwnerInstanceID != officer.OwnerInstanceID)
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
    /// <param name="node">The game node to add as a reference.</param>
    public override void AddChild(ISceneNode child)
    {
        if (child is Fleet fleet)
        {
            AddFleet(fleet);
        }
        else if (child is Officer officer)
        {
            AddOfficer(officer);
        }
        else if (child is Building building)
        {
            AddBuilding(building);
        }
        else if (child is Mission mission)
        {
            AddMission(mission);
        }
        else if (child is Regiment regiment)
        {
            AddRegiment(regiment);
        }
    }

    /// <summary>
    /// Removes a child node from the planet.
    /// </summary>
    /// <param name="child">The child node to remove.</param>
    public override void RemoveChild(ISceneNode child)
    {
        if (child is Fleet fleet)
        {
            RemoveFleet(fleet);
        }
        else if (child is Officer officer)
        {
            RemoveOfficer(officer);
        }
        else if (child is Building building)
        {
            RemoveBuilding(building);
        }
        else if (child is Mission mission)
        {
            RemoveMission(mission);
        }
        else if (child is Regiment regiment)
        {
            RemoveRegiment(regiment);
        }
    }

    /// <summary>
    /// Gets the child nodes of the planet.
    /// </summary>
    /// <returns>An array of child nodes.</returns>
    public override IEnumerable<ISceneNode> GetChildren()
    {
        IEnumerable<ISceneNode> buildings = Buildings
            .Values.SelectMany(buildingList => buildingList)
            .Cast<ISceneNode>();

        return Fleets
            .Cast<ISceneNode>()
            .Concat(Officers.Cast<ISceneNode>())
            .Concat(Missions.Cast<ISceneNode>())
            .Concat(Regiments.Cast<ISceneNode>())
            .Concat(buildings);
    }
}
