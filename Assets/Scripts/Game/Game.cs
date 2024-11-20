using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ICollectionExtensions;
using IDictionaryExtensions;

[PersistableObject]
public class Game
{
    // Game Details
    public GameSummary Summary { get; set; }

    // Game States
    public int CurrentTick = 0;

    // Game Events
    public Dictionary<int, List<ScheduledEvent>> ScheduledEvents =
        new Dictionary<int, List<ScheduledEvent>>();
    public List<GameEvent> EventPool = new List<GameEvent>();
    public HashSet<string> CompletedEventIDs = new HashSet<string>();

    // Scene Nodes
    [PersistableIgnore]
    public Dictionary<string, ISceneNode> NodesByInstanceID = new Dictionary<string, ISceneNode>();

    // Game Objects
    public List<Faction> Factions = new List<Faction>();
    public List<Officer> UnrecruitedOfficers = new List<Officer>();

    // Root Scene Node
    private GalaxyMap _galaxy;
    public GalaxyMap Galaxy
    {
        get { return _galaxy; }
        set
        {
            if (value != null)
            {
                // Register the galaxy and its children, then set the parents.
                _galaxy = initializeGalaxy(value);
            }
        }
    }

    /// <summary>
    /// Default constructor.
    /// </summary>
    public Game()
    {
        Galaxy = new GalaxyMap();
    }

    /// <summary>
    /// Constructor that initializes the game with a summary.
    /// </summary>
    /// <param name="summary"></param>
    public Game(GameSummary summary)
    {
        Summary = summary;
        Galaxy = new GalaxyMap();
    }

    /// <summary>
    /// Returns the factions in the game.
    /// </summary>
    /// <returns>A list of factions in the game.</returns>
    public List<Faction> GetFactions()
    {
        return Factions;
    }

    /// <summary>
    /// Returns the faction with the specified ID.
    /// </summary>
    /// <param name="ownerInstanceId">The ID of the faction to retrieve.</param>
    /// <returns>The faction with the specified ID.</returns>
    public Faction GetFactionByOwnerInstanceID(string ownerInstanceId)
    {
        Faction faction = GetFactions().Find(faction => faction.InstanceID == ownerInstanceId);

        if (faction == null)
        {
            throw new SceneNodeNotFoundException(ownerInstanceId);
        }

        return faction;
    }

    /// <summary>
    /// Returns the galaxy map, the root node of the game.
    /// </summary>
    /// <returns>The galaxy map.</returns>
    public GalaxyMap GetGalaxyMap()
    {
        return Galaxy;
    }

    /// <summary>
    /// Attaches a node to a parent node.
    /// </summary>
    /// <param name="node">The node to attach.</param>
    /// <param name="parent">The parent node to attach the node to.</param>
    /// <exception cref="SceneAccessException">Thrown when the node is not allowed to be attached.</exception>
    public void AttachNode(ISceneNode node, ISceneNode parent)
    {
        // If the node already has a parent, throw an exception.
        if (node.GetParent() != null)
        {
            throw new InvalidSceneOperationException(
                $"Cannot attach node \"{node.DisplayName}\" to parent \"{parent.DisplayName}\" because it already has a parent."
            );
        }

        parent.AddChild(node);
        node.SetParent(parent);

        // Register the node to the faction's list of owned units.
        RegisterOwnedUnit(node);

        // Register the node and its children.
        node.Traverse(AddSceneNodeByInstanceID);
    }

    /// <summary>
    /// Detaches a node from its parent.
    /// </summary>
    /// <param name="node">The node to detach.</param>
    /// <exception cref="SceneAccessException">Thrown when the node is not allowed to be detached.</exception>
    public void DetachNode(ISceneNode node)
    {
        if (node.GetParent() == null)
        {
            throw new InvalidSceneOperationException(
                $"Cannot detach node \"{node.DisplayName}\" because it does not have a parent."
            );
        }

        node.GetParent().RemoveChild(node);
        node.SetParent(null);

        // Deregister the node from the faction's list of owned units.
        DeregsiterOwnedUnit(node);

        // Deregister the node and its children.
        node.Traverse(RemoveSceneNodeByInstanceID);
    }

    /// <summary>
    /// Removes a reference node from the game.
    /// </summary>
    /// <param name="node">The game node to remove as a reference.</param>
    public void AddSceneNodeByInstanceID(ISceneNode node)
    {
        NodesByInstanceID.TryAdd(node.InstanceID, node);
    }

    /// <summary>
    /// Deregisters a scene node from the game.
    /// </summary>
    /// <param name="node">The scene node to deregister.</param>
    public void RemoveSceneNodeByInstanceID(ISceneNode node)
    {
        NodesByInstanceID.Remove(node.InstanceID);
    }

    /// <summary>
    /// Retrieves a list of scene nodes by their type IDs.
    /// </summary>
    /// <typeparam name="T">The type of nodes to retrieve.</typeparam>
    /// <param name="instanceId">The instance ID of the nodes to retrieve.</param>
    /// <returns>A list of scene nodes with the specified type IDs.</returns>
    public T GetSceneNodeByInstanceID<T>(string instanceId)
        where T : class
    {
        if (NodesByInstanceID.TryGetValue(instanceId, out ISceneNode node))
        {
            return node as T;
        }
        else
        {
            return null;
        }
    }

    /// <summary>
    /// Retrieves a list of scene nodes by their instance IDs.
    /// </summary>
    /// <param name="instanceIDs">The list of instance IDs to retrieve.</param>
    /// <returns>A list of scene nodes with the specified instance IDs.</returns>
    public List<ISceneNode> GetSceneNodesByInstanceIDs(List<string> instanceIDs)
    {
        List<ISceneNode> matchingNodes = new List<ISceneNode>();

        foreach (var instanceId in instanceIDs)
        {
            if (NodesByInstanceID.TryGetValue(instanceId, out ISceneNode node))
            {
                matchingNodes.Add(node);
            }
        }

        return matchingNodes;
    }

    /// <summary>
    /// Retrieves units by the specified OwnerInstanceID and type T.
    /// </summary>
    /// <typeparam name="T">The type of units to retrieve.</typeparam>
    /// <param name="ownerInstanceId">The OwnerInstanceID of the units.</param>
    /// <returns>A list of units of type T with the specified OwnerInstanceID.</returns>
    public List<T> GetSceneNodesByOwnerInstanceID<T>(string ownerInstanceId)
        where T : ISceneNode
    {
        return NodesByInstanceID
            .Values.OfType<T>() // Filter nodes by the specified type T
            .Where(node => node.OwnerInstanceID == ownerInstanceId) // Match by OwnerInstanceID
            .ToList();
    }

    /// <summary>
    /// Retrieves all nodes of a specified type T, stopping further traversal if the type is found.
    /// </summary>
    /// <typeparam name="T">The type of nodes to retrieve.</typeparam>
    /// <returns>A list of nodes of type T.</returns>
    public List<T> GetSceneNodesByType<T>()
    {
        var result = new List<T>();

        // Recursive function to traverse nodes.
        void Traverse(ISceneNode node)
        {
            // If the current node is of type T, add it to the result list and stop traversing this branch.
            if (node is T typedNode)
            {
                result.Add(typedNode);
                return;
            }

            // Otherwise, continue traversing the children.
            foreach (var child in node.GetChildren())
            {
                Traverse(child);
            }
        }

        // Start traversal from the Galaxy node (root node).
        Traverse(Galaxy);

        return result;
    }

    /// <summary>
    /// Registers a unit to the faction that owns the unit.
    /// </summary>
    /// <param name="node">The unit to register.</param>
    public void RegisterOwnedUnit(ISceneNode node)
    {
        if (node.OwnerInstanceID != null)
        {
            GetFactionByOwnerInstanceID(node.OwnerInstanceID).AddOwnedUnit(ref node);
        }
    }

    /// <summary>
    /// Deregisters a unit from the faction that owns the unit.
    /// </summary>
    /// <param name="node">The unit to deregister.</param>
    public void DeregsiterOwnedUnit(ISceneNode node)
    {
        if (node.OwnerInstanceID != null)
        {
            GetFactionByOwnerInstanceID(node.OwnerInstanceID).RemoveOwnedUnit(ref node);
        }
    }

    /// <summary>
    /// Retrieves all nodes of a specified type T, stopping further traversal if the type is found.
    /// </summary>
    /// <param name="eventID">The ID of the event to retrieve.</param>
    /// <returns>The game event with the specified ID.</returns>
    public GameEvent GetPoolEventByID(string eventID)
    {
        return EventPool.FirstOrDefault(gameEvent => gameEvent.InstanceID == eventID);
    }

    /// <summary>
    /// Retrieves a list of scheduled events for the specified tick.
    /// </summary>
    /// <returns>A list of scheduled events for the specified tick.</returns>
    public List<ScheduledEvent> GetScheduledEvents(int tick)
    {
        if (ScheduledEvents.TryGetValue(tick, out List<ScheduledEvent> scheduledEvents))
        {
            return scheduledEvents;
        }
        else
        {
            return new List<ScheduledEvent>();
        }
    }

    /// <summary>
    /// Adds a game event to the game event dictionary.
    /// </summary>
    /// <param name="gameEvent">The game event to add.</param>
    /// <param name="tick">The tick at which the game event occurs.</param>
    public void ScheduleGameEvent(GameEvent gameEvent, int tick)
    {
        if (ScheduledEvents.ContainsKey(tick))
        {
            ScheduledEvents[tick].Add(new ScheduledEvent(gameEvent, tick));
        }
        else
        {
            ScheduledEvents[tick] = new List<ScheduledEvent>
            {
                new ScheduledEvent(gameEvent, tick),
            };
        }
    }

    /// <summary>
    /// Removes a scheduled event from the game event dictionary.
    /// </summary>
    /// <param name="scheduledEvent">The scheduled event to remove.</param>
    /// <param name="tick">The tick at which the scheduled event occurs.</param>
    public void RemoveScheduledEvent(ScheduledEvent scheduledEvent, int tick)
    {
        if (ScheduledEvents.ContainsKey(tick))
        {
            ScheduledEvents[tick].Remove(scheduledEvent);
        }
    }

    /// <summary>
    /// Adds a game event to the list of completed event IDs.
    /// </summary>
    /// <param name="gameEvent"></param>
    public void AddCompletedEvent(GameEvent gameEvent)
    {
        CompletedEventIDs.Add(gameEvent.InstanceID);
    }

    /// <summary>
    /// Checks if a game event has been completed.
    /// </summary>
    /// <param name="eventID">The ID of the game event to check.</param>
    /// <returns>True if the game event has been completed; false otherwise.</returns>
    public bool IsEventComplete(string eventInstanceId)
    {
        return CompletedEventIDs.Contains(eventInstanceId);
    }

    /// <summary>
    /// Returns a list of unrecruited officers that can be recruited by the specified owner type ID.
    /// </summary>
    /// <param name="ownerInstanceId">The owner type ID of the faction that can recruit the officers.</param>
    /// <returns>A list of unrecruited officers that can be recruited by the specified owner type ID.</returns>
    public List<Officer> GetUnrecruitedOfficers(string ownerInstanceId)
    {
        return UnrecruitedOfficers
            .Where(officer => officer.AllowedOwnerInstanceIDs.Contains(ownerInstanceId))
            .ToList();
    }

    /// <summary>
    /// Removes an unrecruited officer from the game.
    /// </summary>
    /// <param name="officer">The officer to remove.</param>
    public void RemoveUnrecruitedOfficer(Officer officer)
    {
        UnrecruitedOfficers.Remove(officer);
    }

    /// <summary>
    /// Initializes the galaxy map by registering nodes and setting parents.
    /// </summary>
    /// <param name="galaxy">The galaxy map to initialize.</param>
    private GalaxyMap initializeGalaxy(GalaxyMap galaxy)
    {
        galaxy.Traverse(
            (ISceneNode node) =>
            {
                // Register the node by its instance ID.
                AddSceneNodeByInstanceID(node);

                if (node.OwnerInstanceID != null)
                {
                    // Register the node to the faction's list of owned units.
                    GetFactionByOwnerInstanceID(node.OwnerInstanceID).AddOwnedUnit(ref node);
                }

                // Set the parent of each child node.
                foreach (ISceneNode child in node.GetChildren())
                {
                    child.SetParent(node);
                }
            }
        );

        return galaxy;
    }
}
