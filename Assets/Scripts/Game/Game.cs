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

    // Child Nodes
    public List<Faction> Factions = new List<Faction>();
    public List<Officer> UnrecruitedOfficers = new List<Officer>(); // @TODO: Convert to dictionary for faster lookups.
    public SerializableDictionary<int, List<GameEvent>> GameEventDictionary =
        new SerializableDictionary<int, List<GameEvent>>();

    // Reference List
    public SerializableDictionary<string, ReferenceNode> ReferenceDictionary =
        new SerializableDictionary<string, ReferenceNode>();

    // Scene Nodes
    [XmlIgnore]
    public SerializableDictionary<string, SceneNode> SceneNodeRegistry =
        new SerializableDictionary<string, SceneNode>();

    // Galaxy Map
    private GalaxyMap _galaxy;
    public GalaxyMap Galaxy
    {
        get
        {
            return _galaxy;
        }
        set
        {
            _galaxy = value;
            // Connect the scene graph (children to parents).
            connectSceneGraph(_galaxy);
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
    /// <param name="parent">The parent node to attach the node to.</param>
    /// <param name="node">The node to attach.</param>
    /// <exception cref="SceneAccessException">Thrown when the node is not allowed to be attached.</exception>
    public void AttachNode(SceneNode parent, SceneNode node)
    {
        // If the node already has a parent, throw an exception.
        if (node.GetParent() != null)
        {
            throw new InvalidSceneOperationException(
                $"Cannot attach node \"{node.GameID}\" to parent \"{parent.GameID}\" because it already has a parent."
            );
        }
    
        parent.AddChild(node);
        node.SetParent(parent);

        // Register the node and its children.
        node.Traverse(RegisterSceneNode);
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
                $"Cannot detach node \"{node.GameID}\" because it does not have a parent."
            );
        }

        node.GetParent().RemoveChild(node);
        node.SetParent(null);

        // Deregister the node and its children.
        node.Traverse(DeregisterSceneNode);
    }

    /// <summary>
    /// Moves a node from one parent to another.
    /// </summary>
    /// <param name="node">The node to move.</param>
    /// <param name="parent">The new parent to move the node to.</param>
    public void MoveNode(SceneNode node, SceneNode parent, bool? recurse = false)
    {
        if (node.GetParent() == null)
        {
            throw new InvalidSceneOperationException(
                $"Cannot move node \"{node.GameID}\" because it does not have a parent."
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
    public void AddReferenceNode(SceneNode node)
    {
        ReferenceDictionary.Add(node.GameID, new ReferenceNode(node));
    }

    /// <summary>
    /// Removes a reference node from the game.
    /// </summary>
    /// <param name="node">The game node to remove as a reference.</param>
    public void RegisterSceneNode(SceneNode node)
    {
        SceneNodeRegistry.TryAdd(node.InstanceID, node);
    }

    /// <summary>
    /// Deregisters a scene node from the game.
    /// </summary>
    /// <param name="node">The scene node to deregister.</param>
    public void DeregisterSceneNode(SceneNode node)
    {
        SceneNodeRegistry.Remove(node.InstanceID);
    }


    /// <summary>
    /// Gets a node in the scene by its game ID.
    /// </summary>
    /// <param name="gameId"></param>
    /// <returns>The scene node with the specified game ID, or null if not found.</returns>
    public SceneNode GetSceneNodeByGameID(string gameId)
    {
        if (SceneNodeRegistry.TryGetValue(gameId, out SceneNode node))
        {
            return node;
        } else
        {
            return null;
        }
    }

    /// <summary>
    /// Gets a node in the scene by its instance ID.
    /// </summary>
    /// <param name="instanceID"></param>
    /// <returns>The scene node with the specified instance ID, or null if not found.</returns>
    public SceneNode GetSceneNodeByInstanceID(string instanceId)
    {
        if (SceneNodeRegistry.TryGetValue(instanceId, out SceneNode node))
        {
            return node;
        } else
        {
            return null;
        }
    }

    /// <summary>
    /// Adds a game event to the game event dictionary.
    /// </summary>
    /// <param name="tick">The tick at which the game event occurs.</param>
    /// <param name="gameEvent">The game event to add.</param>
    public void AddGameEvent(int tick, GameEvent gameEvent)
    {
        List<GameEvent> eventList = GameEventDictionary.GetOrAddValue(tick, new List<GameEvent>());
        eventList.Add(gameEvent);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="tick"></param>
    /// <param name="gameEvent"></param>
    public void RemoveGameEvent(int tick, GameEvent gameEvent)
    {
        if (GameEventDictionary.ContainsKey(tick))
        {
            GameEventDictionary[tick].Remove(gameEvent);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="ownerGameID"></param>
    /// <returns></returns>
    public List<Officer> GetUnrecruitedOfficers(string ownerGameID)
    {
        return UnrecruitedOfficers.Where(officer => officer.OwnerGameID == ownerGameID).ToList();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="officer"></param>
    public void RemoveUnrecruitedOfficer(Officer officer)
    {
        UnrecruitedOfficers.Remove(officer);
    }

    /// <summary>
    /// Connects the scene graph by setting the parent of each node.
    /// </summary>
    /// <returns></returns>
    private void connectSceneGraph(GalaxyMap galaxy)
    {
        galaxy.Traverse((SceneNode node) => {
            foreach (SceneNode child in node.GetChildren()) 
            {
                child.SetParent(node);
            }
            RegisterSceneNode(node);
        });
    }
}
