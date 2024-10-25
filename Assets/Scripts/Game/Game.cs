using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using System;
using ICollectionExtensions;
using IDictionaryExtensions;

public class Game
{
    // Game Details
    public GameSummary Summary { get; set; }

    // Game Events
    public SerializableDictionary<int, List<ScheduledEvent>> ScheduledEvents =
        new SerializableDictionary<int, List<ScheduledEvent>>();
    public List<GameEvent> EventPool = new List<GameEvent>();
    public SerializableHashSet<string> CompletedEventIDs = new SerializableHashSet<string>();

    // Reference List
    public SerializableDictionary<string, ReferenceNode> NodesByTypeID =
        new SerializableDictionary<string, ReferenceNode>();

    // Scene Nodes
    [XmlIgnore]
    public SerializableDictionary<string, SceneNode> NodesByInstanceID =
        new SerializableDictionary<string, SceneNode>();

    
    // Game Objects
    public List<Faction> Factions = new List<Faction>();
    public List<Officer> UnrecruitedOfficers = new List<Officer>(); // @TODO: Convert to dictionary for faster lookups.
    private GalaxyMap _galaxy;
    public GalaxyMap Galaxy
    {
        get
        {
            return _galaxy;
        }
        set
        {
            _galaxy ??= value;
        }
    }

    // Game States
    public int CurrentTick = 0;

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
    /// Attaches a node to a parent node.
    /// </summary>
    /// <param name="node">The node to attach.</param>
    /// <param name="parent">The parent node to attach the node to.</param>
    /// <exception cref="SceneAccessException">Thrown when the node is not allowed to be attached.</exception>
    public void AttachNode(SceneNode node, SceneNode parent)
    {
        // If the node already has a parent, throw an exception.
        if (node.GetParent() != null)
        {
            throw new InvalidSceneOperationException(
                $"Cannot attach node \"{node.TypeID}\" to parent \"{parent.TypeID}\" because it already has a parent."
            );
        }
    
        parent.AddChild(node);
        node.SetParent(parent);

        // Register the node and its children.
        node.Traverse(AddNodeByInstanceID);
    }

    /// <summary>
    /// Detaches a node from its parent.
    /// </summary>
    /// <param name="node">The node to detach.</param>
    /// <exception cref="SceneAccessException">Thrown when the node is not allowed to be detached.</exception>
    public void DetachNode(SceneNode node)
    {
        if (node.GetParent() == null)
        {
            throw new InvalidSceneOperationException(
                $"Cannot detach node \"{node.TypeID}\" because it does not have a parent."
            );
        }

        node.GetParent().RemoveChild(node);
        node.SetParent(null);

        // Deregister the node and its children.
        node.Traverse(RemoveNodeByInstanceID);
    }

    /// <summary>
    /// Moves a node from one parent to another.
    /// </summary>
    /// <param name="node">The node to move.</param>
    /// <param name="parent">The new parent to move the node to.</param>
    /// <param name="recurse">Whether to move the node's children as well.</param>
    public void MoveNode(SceneNode node, SceneNode parent, bool? recurse = false)
    {
        if (node.GetParent() == null)
        {
            throw new InvalidSceneOperationException(
                $"Cannot move node \"{node.TypeID}\" because it does not have a parent."
            );
        }

        // Remove the node from its current parent and add it to the new parent.
        node.GetParent().RemoveChild(node);
        parent.AddChild(node);
        node.SetParent(parent);

        if (recurse == true)
        {
            // Register the node and its children.
            node.Traverse((SceneNode node) => {
                foreach (SceneNode child in node.GetChildren()) 
                {
                    MoveNode(child, node);
                }
            });
        }
    }

    /// <summary>
    /// Adds a reference node to the game.
    /// </summary>
    /// <param name="node">The game node to add as a reference.</param>
    public void AddNodeByTypeID(SceneNode node)
    {
        NodesByTypeID.Add(node.TypeID, new ReferenceNode(node));
    }

    /// <summary>
    /// Removes a reference node from the game.
    /// </summary>
    /// <param name="node">The game node to remove as a reference.</param>
    public void AddNodeByInstanceID(SceneNode node)
    {
        NodesByInstanceID.TryAdd(node.InstanceID, node);
    }

    /// <summary>
    /// Deregisters a scene node from the game.
    /// </summary>
    /// <param name="node">The scene node to deregister.</param>
    public void RemoveNodeByInstanceID(SceneNode node)
    {
        NodesByInstanceID.Remove(node.InstanceID);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="typeId"></param>
    /// <returns></returns>
    public T GetSceneNodeByTypeID<T>(string typeId) where T : SceneNode
    {
        if (NodesByTypeID.TryGetValue(typeId, out ReferenceNode referenceNode))
        {
            return referenceNode.GetReference() as T;
        } else
        {
            return null;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="instanceId"></param>
    /// <returns></returns>
    public T GetSceneNodeByInstanceID<T>(string instanceId) where T : SceneNode
    {
        if (NodesByInstanceID.TryGetValue(instanceId, out SceneNode node))
        {
            return node as T;
        } else
        {
            return null;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="instanceIDs"></param>
    /// <returns></returns>
    public List<SceneNode> GetSceneNodesByInstanceIDs(List<string> instanceIDs)
    {
        List<SceneNode> matchingNodes = new List<SceneNode>();

        foreach (var instanceId in instanceIDs)
        {
            if (NodesByInstanceID.TryGetValue(instanceId, out SceneNode node))
            {
                matchingNodes.Add(node);
            }
        }

        return matchingNodes;
    }

    /// <summary>
    /// Adds a game event to the game event dictionary.
    /// </summary>
    /// <param name="tick">The tick at which the game event occurs.</param>
    /// <param name="gameEvent">The game event to add.</param>
    public void ScheduleGameEvent(int tick, GameEvent gameEvent)
    {
        ScheduledEvent scheduledEvent = new ScheduledEvent(gameEvent, tick);
        ScheduledEvents[tick].Add(scheduledEvent);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="tick"></param>
    /// <param name="scheduledEvent"></param>
    public void RemoveScheduledEvent(int tick, ScheduledEvent scheduledEvent)
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
    public void AddCompletedEventID(GameEvent gameEvent)
    {
        CompletedEventIDs.Add(gameEvent.InstanceID);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="ownerTypeID"></param>
    /// <returns></returns>
    public List<Officer> GetUnrecruitedOfficers(string ownerTypeID)
    {
        return UnrecruitedOfficers.Where(officer => officer.OwnerTypeID == ownerTypeID).ToList();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="officer"></param>
    public void RemoveUnrecruitedOfficer(Officer officer)
    {
        UnrecruitedOfficers.Remove(officer);
    }
}
