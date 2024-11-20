using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class Faction : BaseGameEntity
{
    public Dictionary<MessageType, List<Message>> Messages = new Dictionary<
        MessageType,
        List<Message>
    >()
    {
        // { MessageType.Conflict, new List<Message>() },
        // { MessageType.Mission, new List<Message>() },
        // { MessageType.PopularSupport, new List<Message>() },
        // { MessageType.Resource, new List<Message>() },
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
    /// Default constructor used for serializaiton.
    /// </summary>
    public Faction() { }

    /// <summary>
    /// Checks if the faction is controlled by AI.
    /// </summary>
    /// <returns>True if the faction is AI controlled, false otherwise.</returns>
    public bool IsAIControlled() => string.IsNullOrEmpty(PlayerID);

    /// <summary>
    /// Returns a list of units owned by the faction.
    /// </summary>
    /// <returns>A list of units owned by the faction.</returns>
    public List<ISceneNode> GetAllOwnedUnits()
    {
        return ownedEntities.Values.SelectMany(x => x).ToList();
    }

    /// <summary>
    /// Returns a list of units owned by the faction.
    /// </summary>
    /// <typeparam name="T">The type of unit to get.</typeparam>
    /// <returns>A list of units owned by the faction.</returns>
    public List<T> GetOwnedUnitsByType<T>()
        where T : ISceneNode
    {
        return ownedEntities[typeof(T)].Cast<T>().ToList();
    }

    /// <summary>
    /// Adds a unit to the ownedEntities dictionary.
    /// </summary>
    /// <typeparam name="T">The type of unit to add.</typeparam>
    /// <param name="unit">The unit to add.</param>
    public void AddOwnedUnit<T>(ref T unit)
        where T : ISceneNode
    {
        if (ownedEntities.ContainsKey(unit.GetType()))
        {
            ownedEntities[unit.GetType()].Add(unit);
        }
    }

    /// <summary>
    /// Removes a unit from the ownedEntities dictionary.
    /// </summary>
    /// <typeparam name="T">The type of unit to remove.</typeparam>
    /// <param name="unit">The unit to remove.</param>
    public void RemoveOwnedUnit<T>(ref T unit)
        where T : ISceneNode
    {
        if (ownedEntities.ContainsKey(unit.GetType()))
        {
            ownedEntities[unit.GetType()].Remove(unit);
        }
    }

    /// <summary>
    /// Adds a technology node to the TechnologyLevels dictionary.
    /// </summary>
    /// <typeparam name="T">The type of technology to add.</typeparam>
    /// <param name="level">The level of the technology.</param>
    /// <param name="node">The technology node to add.</param>
    public void AddTechnologyNode(int level, Technology node)
    {
        ISceneNode tech = node.GetReference();
        Type technologyType = tech.GetType();

        // Check if the technology is manufacturable.
        if (!(tech is IManufacturable manufacturableTech))
        {
            throw new GameException(
                $"Technology {tech.DisplayName} must implement IManufacturable."
            );
        }

        // Check if the technology is allowed to be owned by the faction.
        if (
            tech.AllowedOwnerInstanceIDs != null
            && !tech.AllowedOwnerInstanceIDs.Contains(InstanceID)
        )
        {
            throw new GameException(
                $"Cannot add technology {tech.DisplayName} to faction {DisplayName}. Owner IDs do not match."
            );
        }

        // Ensure the dictionary for this technology type exists.
        if (
            !TechnologyLevels.TryGetValue(
                manufacturableTech.GetManufacturingType(),
                out var techLevels
            )
        )
        {
            techLevels = new SortedDictionary<int, List<Technology>>();
            TechnologyLevels[manufacturableTech.GetManufacturingType()] = techLevels;
        }

        // Ensure the list for this level exists.
        if (!techLevels.TryGetValue(level, out var nodesAtLevel))
        {
            nodesAtLevel = new List<Technology>();
            techLevels[level] = nodesAtLevel;
        }

        // Add the new node to the list, if it's not already present.
        if (!nodesAtLevel.Contains(node))
        {
            nodesAtLevel.Add(node);
        }
    }

    /// <summary>
    /// Returns a list of available technologies for the faction based on their current research level.
    /// </summary>
    /// <typeparam name="T">The type of technology to retrieve.</typeparam>
    /// <returns>A list of available technologies of type T.</returns>
    public List<T> GetAvailableTechnologies<T>()
        where T : class, ISceneNode, IManufacturable
    {
        // Create a default instance to get the manufacturing type
        T defaultInstance = Activator.CreateInstance<T>();
        ManufacturingType manufacturingType = defaultInstance.GetManufacturingType();

        // Check if the manufacturing type exists in the technology levels
        if (!TechnologyLevels.TryGetValue(manufacturingType, out var techLevels))
        {
            return new List<T>();
        }

        int currentResearchLevel = GetManufacturingResearchLevel(manufacturingType);

        // Filter technologies based on the current research level.
        return techLevels
            .Where(kvp => kvp.Key <= currentResearchLevel) // Only consider technologies up to the current research level.
            .SelectMany(kvp => kvp.Value) // Flatten the list of Technologys.
            .Select(technology => technology.GetReference() as T) // Convert each Technology to type T.
            .Where(tech => tech != null && tech.GetRequiredResearchLevel() <= currentResearchLevel) // Ensure validity and requirements.
            .ToList();
    }

    /// <summary>
    /// Returns the research level required to manufacture the manufacturable.
    /// </summary>
    /// <param name="manufacturingType"></param>
    /// <returns></returns>
    public int GetManufacturingResearchLevel(ManufacturingType manufacturingType)
    {
        return ManufacturingResearchLevels[manufacturingType];
    }

    /// <summary>
    /// Sets the research level required to manufacture the manufacturable.
    /// </summary>
    /// <param name="manufacturingType"></param>
    /// <param name="level"></param>
    /// <returns></returns>
    public void SetManufacturingResearchLevel(ManufacturingType manufacturingType, int level)
    {
        ManufacturingResearchLevels[manufacturingType] = level;
    }

    /// <summary>
    /// Returns a list of planets with idle manufacturing facilities.
    /// </summary>
    /// <returns>A list of planets with idle manufacturing facilities.</returns>
    public List<Planet> GetIdleFacilities(ManufacturingType manufacturingType)
    {
        return GetOwnedUnitsByType<Planet>()
            .FindAll(p => p.GetIdleManufacturingFacilities(manufacturingType) > 0);
    }

    /// <summary>
    /// Adds a message to the Messages dictionary based on its type.
    /// </summary>
    /// <param name="message">The message to add.</param>
    public void AddMessage(Message message)
    {
        Messages[message.Type].Add(message);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="message"></param>
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
    public Planet GetNearestPlanet(ISceneNode fromNode)
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
