using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Represents a faction in the game, managing its resources, technologies, and owned entities.
/// </summary>
public class Faction : BaseGameEntity
{
    public Dictionary<MessageType, List<Message>> Messages = new Dictionary<
        MessageType,
        List<Message>
    >()
    {
        { MessageType.Conflict, new List<Message>() },
        { MessageType.Mission, new List<Message>() },
        { MessageType.PopularSupport, new List<Message>() },
        { MessageType.Resource, new List<Message>() },
    };

    public Dictionary<
        ManufacturingType,
        SortedDictionary<int, List<Technology>>
    > TechnologyLevels { get; set; } =
        new Dictionary<ManufacturingType, SortedDictionary<int, List<Technology>>>();

    public Dictionary<ManufacturingType, int> ManufacturingResearchLevels { get; set; } =
        new Dictionary<ManufacturingType, int>()
        {
            { ManufacturingType.Building, 0 },
            { ManufacturingType.Ship, 0 },
            { ManufacturingType.Troop, 0 },
        };

    public string HQInstanceID { get; set; }
    public string PlayerID { get; set; }

    private Dictionary<Type, List<ISceneNode>> ownedEntities = new Dictionary<
        Type,
        List<ISceneNode>
    >()
    {
        { typeof(CapitalShip), new List<ISceneNode>() },
        { typeof(Fleet), new List<ISceneNode>() },
        { typeof(Officer), new List<ISceneNode>() },
        { typeof(Planet), new List<ISceneNode>() },
        { typeof(Regiment), new List<ISceneNode>() },
        { typeof(Starfighter), new List<ISceneNode>() },
    };

    /// <summary>
    /// Default constructor used for serialization.
    /// </summary>
    public Faction() { }

    /// <summary>
    /// Returns the instance ID of the faction's headquarters.
    /// </summary>
    /// <returns>The HQ instance ID.</returns>
    public string GetHQInstanceID() => HQInstanceID;

    /// <summary>
    /// Checks if the faction is controlled by AI.
    /// </summary>
    /// <returns>True if the faction is AI controlled, false otherwise.</returns>
    public bool IsAIControlled() => string.IsNullOrEmpty(PlayerID);

    /// <summary>
    /// Returns a list of all units owned by the faction.
    /// </summary>
    /// <returns>A list of all owned units.</returns>
    public List<ISceneNode> GetAllOwnedUnits()
    {
        return ownedEntities.Values.SelectMany(x => x).ToList();
    }

    /// <summary>
    /// Returns a list of units of a specific type owned by the faction.
    /// </summary>
    /// <typeparam name="T">The type of unit to get.</typeparam>
    /// <returns>A list of owned units of the specified type.</returns>
    public List<T> GetOwnedUnitsByType<T>()
        where T : ISceneNode
    {
        return ownedEntities[typeof(T)].Cast<T>().ToList();
    }

    /// <summary>
    /// Adds a unit to the faction's owned entities.
    /// </summary>
    /// <typeparam name="T">The type of unit to add.</typeparam>
    /// <param name="unit">The unit to add.</param>
    public void AddOwnedUnit<T>(T unit)
        where T : ISceneNode
    {
        if (ownedEntities.ContainsKey(unit.GetType()))
        {
            ownedEntities[unit.GetType()].Add(unit);
        }
    }

    /// <summary>
    /// Removes a unit from the faction's owned entities.
    /// </summary>
    /// <typeparam name="T">The type of unit to remove.</typeparam>
    /// <param name="unit">The unit to remove.</param>
    public void RemoveOwnedUnit<T>(T unit)
        where T : ISceneNode
    {
        if (ownedEntities.ContainsKey(unit.GetType()))
        {
            ownedEntities[unit.GetType()].Remove(unit);
        }
    }

    /// <summary>
    /// Adds a technology node to the faction's technology levels.
    /// </summary>
    /// <param name="level">The level of the technology.</param>
    /// <param name="node">The technology node to add.</param>
    public void AddTechnologyNode(int level, Technology node)
    {
        IManufacturable tech = node.GetReference();

        if (
            tech.AllowedOwnerInstanceIDs != null
            && !tech.AllowedOwnerInstanceIDs.Contains(InstanceID)
        )
        {
            throw new GameException(
                $"Cannot add technology {tech.GetDisplayName()} to faction {DisplayName}. Owner IDs do not match."
            );
        }

        if (!TechnologyLevels.TryGetValue(tech.GetManufacturingType(), out var techLevels))
        {
            techLevels = new SortedDictionary<int, List<Technology>>();
            TechnologyLevels[tech.GetManufacturingType()] = techLevels;
        }

        if (!techLevels.TryGetValue(level, out var nodesAtLevel))
        {
            nodesAtLevel = new List<Technology>();
            techLevels[level] = nodesAtLevel;
        }

        if (!nodesAtLevel.Contains(node))
        {
            nodesAtLevel.Add(node);
        }
    }

    /// <summary>
    /// Returns a list of technologies that have been researched for a specific manufacturing type.
    /// </summary>
    /// <param name="manufacturingType">The manufacturing type to check.</param>
    /// <returns>A list of researched technologies.</returns>
    public List<Technology> GetResearchedTechnologies(ManufacturingType manufacturingType)
    {
        if (!TechnologyLevels.TryGetValue(manufacturingType, out var techLevels))
        {
            return new List<Technology>();
        }

        int currentResearchLevel = GetResearchLevel(manufacturingType);

        return techLevels
            .Where(kvp => kvp.Key <= currentResearchLevel)
            .SelectMany(kvp => kvp.Value)
            .Where(tech => tech.GetRequiredResearchLevel() <= currentResearchLevel)
            .ToList();
    }

    /// <summary>
    /// Returns the research level for a specific manufacturing type.
    /// </summary>
    /// <param name="manufacturingType">The manufacturing type to check.</param>
    /// <returns>The research level for the specified manufacturing type.</returns>
    public int GetResearchLevel(ManufacturingType manufacturingType)
    {
        return ManufacturingResearchLevels[manufacturingType];
    }

    /// <summary>
    /// Sets the research level for a specific manufacturing type.
    /// </summary>
    /// <param name="manufacturingType">The manufacturing type to set.</param>
    /// <param name="level">The new research level.</param>
    public void SetResearchLevel(ManufacturingType manufacturingType, int level)
    {
        ManufacturingResearchLevels[manufacturingType] = level;
    }

    /// <summary>
    /// Returns a list of planets with idle manufacturing facilities for a specific manufacturing type.
    /// </summary>
    /// <param name="manufacturingType">The manufacturing type to check.</param>
    /// <returns>A list of planets with idle facilities.</returns>
    public List<Planet> GetIdleFacilities(ManufacturingType manufacturingType)
    {
        return GetOwnedUnitsByType<Planet>()
            .FindAll(p => p.GetIdleManufacturingFacilities(manufacturingType) > 0);
    }

    /// <summary>
    /// Adds a message to the faction's message list.
    /// </summary>
    /// <param name="message">The message to add.</param>
    public void AddMessage(Message message)
    {
        Messages[message.Type].Add(message);
    }

    /// <summary>
    /// Removes a message from the faction's message list.
    /// </summary>
    /// <param name="message">The message to remove.</param>
    public void RemoveMessage(Message message)
    {
        Messages[message.Type].Remove(message);
    }

    /// <summary>
    /// Returns a list of available officers for the faction.
    /// </summary>
    /// <param name="faction"></param>
    /// <returns></returns>
    public List<Officer> GetAvailableOfficers(Faction faction)
    {
        return GetOwnedUnitsByType<Officer>().FindAll(o => o.IsMovable());
    }

    /// <summary>
    /// Returns the closest friendly planet to the given scene node.
    /// </summary>
    /// <param name="fromNode">The scene node to measure distance from.</param>
    /// <returns>The closest friendly planet, or null if no friendly planets are found.</returns>
    public Planet GetNearestPlanetTo(ISceneNode fromNode)
    {
        Planet sourcePlanet = fromNode.GetParentOfType<Planet>();
        if (sourcePlanet == null)
        {
            throw new ArgumentException(
                "The provided ISceneNode is not on a planet.",
                nameof(fromNode)
            );
        }

        return GetOwnedUnitsByType<Planet>()
            .OrderBy(p => sourcePlanet.GetTravelTime(p))
            .FirstOrDefault();
    }
}
