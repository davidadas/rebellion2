using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ICollectionExtensions;

/// <summary>
/// Represents a planet in the game. A planet is a scene node that can contain fleets, 
/// officers, regiments, missions, and buildings. It also has a popular support rating,
/// which is a measure of how much the planet's population supports a given faction.
/// </summary>
public class Planet : SceneNode
{
    // Properties
    public bool IsColonized;
    public int OrbitSlots;
    public int GroundSlots;
    public int NumResourceNodes;

    // Status
    public bool IsDestroyed;
    public bool IsHeadquarters;

    // Popular Support
    public SerializableDictionary<string, int> PopularSupport =
        new SerializableDictionary<string, int>();

    // Child Nodes
    public List<Fleet> Fleets = new List<Fleet>();
    public List<Officer> Officers = new List<Officer>();
    public List<Regiment> Regiments = new List<Regiment>();
    public List<Mission> Missions = new List<Mission>();
    public SerializableDictionary<BuildingSlot, List<Building>> Buildings =
        new SerializableDictionary<BuildingSlot, List<Building>>()
        {
            { BuildingSlot.Ground, new List<Building>() },
            { BuildingSlot.Orbit, new List<Building>() },
        };

    /// <summary>
    /// Default constructor used for serialization.
    /// </summary>
    public Planet() { }

    /// <summary>
    /// Returns the popular support for a faction on the planet.
    /// </summary>
    /// <param name="factionTypeID"></param>
    public int GetPopularSupport(string factionTypeID)
    {
        return PopularSupport.TryGetValue(factionTypeID, out int support) ? support : 0;
    }

    /// <summary>
    /// Sets the popular support for a faction on the planet.
    /// </summary>
    /// <param name="factionTypeID">The type id of the faction.</param>
    /// <param name="support">The level of support.</param>
    public void SetPopularSupport(string factionTypeID, int support)
    {
        if (!PopularSupport.ContainsKey(factionTypeID))
        {
            PopularSupport.Add(factionTypeID, support);
        }

        PopularSupport[factionTypeID] = support;
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
    /// 
    /// </summary>
    /// <param name="fleet"></param>
    public void RemoveFleet(Fleet fleet)
    {
        Fleets.Remove(fleet);
    }

    /// <summary>
    /// Adds a building to the planet.
    /// </summary>
    /// <param name="building">The building to add.</param>
    /// <exception cref="GameException">Thrown when the planet is not colonized or at capacity.</exception>
    private void AddBuilding(Building building)
    {
        // Check if the planet is colonized
        if (!IsColonized)
            throw new GameException(
                $"Cannot add building {building.DisplayName} to {this.DisplayName}. Planet is not colonized."
            );

        BuildingSlot slot = building.Slot;

        // Check if the planet has reached its capacity for the specified slot
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
        BuildingSlot slot = building.Slot;
        Buildings[slot].Remove(building);
    }

    /// <summary>
    /// Gets the buildings in a specific slot.
    /// </summary>
    /// <param name="slot">The building slot.</param>
    /// <returns>An array of buildings.</returns>
    public Building[] GetBuildings(BuildingSlot slot)
    {
        return Buildings[slot].ToArray();
    }

    /// <summary>
    /// Gets the available slots for a specific building slot.
    /// </summary>
    /// <param name="slot">The building slot.</param>
    /// <returns>The number of available slots.</returns>
    public int GetAvailableSlots(BuildingSlot slot)
    {
        int numUsedSlots = Buildings[slot].Count(building => building.Slot == slot);
        int maxSlots = slot == BuildingSlot.Ground ? GroundSlots : OrbitSlots;

        return maxSlots - numUsedSlots;
    }

    /// <summary>
    /// Adds an officer to the planet.
    /// </summary>
    /// <param name="officer">The officer to add.</param>
    private void AddOfficer(Officer officer)
    {
        if (this.OwnerTypeID != officer.OwnerTypeID)
        {
            throw new SceneAccessException(officer, this);
        }
        Officers.Add(officer);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="officer"></param>
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
    /// Remoes a regiment to the planet.
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
    public override void AddChild(SceneNode child)
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
    public override void RemoveChild(SceneNode child)
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
    public override IEnumerable<SceneNode> GetChildren()
    {
        IEnumerable<SceneNode> buildings = Buildings.Values.SelectMany(buildingList => buildingList).Cast<SceneNode>();

        return Fleets.Cast<SceneNode>()
            .Concat(Officers.Cast<SceneNode>())
            .Concat(Missions.Cast<SceneNode>())
            .Concat(Regiments.Cast<SceneNode>())
            .Concat(buildings);
    }
}
