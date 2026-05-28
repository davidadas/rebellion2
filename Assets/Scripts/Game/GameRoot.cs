using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Events;
using Rebellion.Game.Factions;
using Rebellion.Game.Units;
using Rebellion.Game.World;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
using Rebellion.Util.Serialization;

namespace Rebellion.Game
{
    /// <summary>
    /// Specifies different speeds for the game tick processing.
    /// </summary>
    public enum TickSpeed
    {
        Fast,
        Medium,
        Slow,
        Paused,
    }

    /// <summary>
    /// Represents the root node of the game scene graph. The game root owns global state
    /// such as factions, events, configuration, random state, and the node registry used
    /// by systems to process the simulation.
    /// </summary>
    [PersistableObject(Name = "Game")]
    public class GameRoot
    {
        // Scene graph.
        private GalaxyMap _galaxy;

        // Game details.
        public GameSummary Summary { get; set; } = new GameSummary();
        public GameMetadata Metadata { get; set; } = new GameMetadata();

        // Configuration.
        [PersistableIgnore]
        public GameConfig Config { get; private set; }

        // Random generators.
        [PersistableIgnore]
        private IRandomNumberProvider _random;

        [PersistableIgnore]
        public IRandomNumberProvider Random
        {
            get => _random ??= new SystemRandomProvider(Summary.Seed, _restoredIndex);
            set => _random = value;
        }

        [PersistableIgnore]
        private long _restoredIndex;
        public long RandomIndex
        {
            get => (_random as SystemRandomProvider)?.CallCount ?? _restoredIndex;
            set
            {
                _restoredIndex = value;
                if (_random is SystemRandomProvider srp && srp.CallCount != value)
                    _random = new SystemRandomProvider(Summary.Seed, value);
            }
        }

        // Game state.
        public int CurrentTick;
        public TickSpeed GameSpeed = TickSpeed.Medium;

        // Game events.
        public List<GameEvent> EventPool = new List<GameEvent>();
        public HashSet<string> CompletedEventIDs = new HashSet<string>();

        // Scene nodes.
        [PersistableIgnore]
        public Dictionary<string, ISceneNode> NodesByInstanceID =
            new Dictionary<string, ISceneNode>();

        // Game objects.
        public List<Faction> Factions = new List<Faction>();
        public List<Officer> UnrecruitedOfficers = new List<Officer>();

        // Root scene node.
        public GalaxyMap Galaxy
        {
            get { return _galaxy; }
            set
            {
                if (value != null)
                {
                    // Register the galaxy and its children, then set the parents.
                    _galaxy = InitializeGalaxy(value);
                }
            }
        }

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public GameRoot()
        {
            Galaxy = new GalaxyMap();
        }

        /// <summary>
        /// Constructor that requires config at construction time.
        /// Prevents partially initialized Game instances.
        /// </summary>
        /// <param name="config">The runtime configuration.</param>
        public GameRoot(GameConfig config)
        {
            SetConfig(config);
            Galaxy = new GalaxyMap();
        }

        /// <summary>
        /// Constructor that initializes the game with a summary and config.
        /// Used for loading saved games.
        /// </summary>
        /// <param name="summary">The game summary from save file.</param>
        /// <param name="config">The runtime configuration.</param>
        public GameRoot(GameSummary summary, GameConfig config)
        {
            Summary = summary;
            SetConfig(config);
            Galaxy = new GalaxyMap();
        }

        /// <summary>
        /// Injects runtime configuration.
        /// </summary>
        /// <param name="config">The configuration to inject.</param>
        public void SetConfig(GameConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            Config = config;
        }

        /// <summary>
        /// Gets the game configuration.
        /// Throws if config not set (caller forgot to inject).
        /// </summary>
        /// <returns>The game configuration.</returns>
        public GameConfig GetConfig()
        {
            if (Config == null)
            {
                throw new InvalidOperationException(
                    "GameConfig not set. Call Game.SetConfig() during initialization."
                );
            }

            return Config;
        }

        /// <summary>
        /// Sets the game speed for tick processing.
        /// Affects how quickly the game state updates and events are processed.
        /// </summary>
        /// <param name="gameSpeed">The desired game speed.</param>
        public void SetGameSpeed(TickSpeed gameSpeed)
        {
            GameSpeed = gameSpeed;
        }

        /// <summary>
        /// Gets the current game speed.
        /// Used by GameManager to determine how quickly to process game ticks and events.
        /// Affects the pacing of the game and how quickly the game state updates.
        /// For example, Fast speed may process multiple ticks per second, while Slow may process one tick every few seconds.
        /// Paused means no ticks are processed until the speed is changed.
        /// The actual tick processing logic in GameManager should reference this value to adjust its timing accordingly.
        /// </summary>
        /// <returns>The current game speed.</returns>
        public TickSpeed GetGameSpeed()
        {
            return GameSpeed;
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
        /// Returns the faction controlled by the local player, resolved from
        /// <see cref="GameSummary.PlayerFactionID"/>.
        /// </summary>
        /// <returns>The player's <see cref="Faction"/>.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when no summary has been set or the summary has no player faction ID.
        /// </exception>
        public Faction GetPlayerFaction()
        {
            if (Summary == null)
            {
                throw new InvalidOperationException(
                    "GameSummary is null. Cannot determine player faction."
                );
            }

            if (string.IsNullOrEmpty(Summary.PlayerFactionID))
            {
                throw new InvalidOperationException("PlayerFactionID was not set in GameSummary.");
            }

            Faction faction = Factions.FirstOrDefault(f => f.InstanceID == Summary.PlayerFactionID);

            if (faction == null)
            {
                throw new InvalidOperationException(
                    $"Player faction with InstanceID '{Summary.PlayerFactionID}' does not exist in this game."
                );
            }

            return faction;
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
                throw new InvalidOperationException(
                    $"Cannot attach node \"{node.GetDisplayName()}\" to parent \"{parent.GetDisplayName()}\" because it already has a parent."
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
                throw new InvalidOperationException(
                    $"Cannot detach node \"{node.GetDisplayName()}\" because it does not have a parent."
                );
            }

            ISceneNode parent = node.GetParent();

            parent.RemoveChild(node);
            node.SetParent(null);

            // Deregister the node from the faction's list of owned units.
            DeregsiterOwnedUnit(node);

            // Deregister the node and its children.
            node.Traverse(RemoveSceneNodeByInstanceID);
        }

        /// <summary>
        /// Moves a node from its current parent to a new parent.
        /// </summary>
        /// <param name="node">The node to move.</param>
        /// <param name="newParent">The destination parent.</param>
        /// <exception cref="ArgumentNullException">Thrown when either argument is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the destination rejects the node.</exception>
        public void MoveNode(ISceneNode node, ISceneNode newParent)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node));
            if (newParent == null)
                throw new ArgumentNullException(nameof(newParent));

            ISceneNode oldParent = node.GetParent();

            if (oldParent == null)
            {
                throw new InvalidOperationException(
                    $"Cannot move node \"{node.GetDisplayName()}\" because it has no parent."
                );
            }

            if (oldParent == newParent)
            {
                return;
            }

            DetachNode(node);

            try
            {
                AttachNode(node, newParent);
            }
            catch
            {
                AttachNode(node, oldParent);
                throw;
            }
        }

        /// <summary>
        /// Retrieves a list of scene nodes by their Instance IDs.
        /// </summary>
        /// <typeparam name="T">The type of nodes to retrieve.</typeparam>
        /// <param name="instanceId">The Instance ID of the nodes to retrieve.</param>
        /// <returns>A list of scene nodes with the specified Instance IDs.</returns>
        public T GetSceneNodeByInstanceID<T>(string instanceId)
            where T : class
        {
            if (string.IsNullOrEmpty(instanceId))
                return null;
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

            foreach (string instanceId in instanceIDs)
            {
                if (NodesByInstanceID.TryGetValue(instanceId, out ISceneNode node))
                {
                    matchingNodes.Add(node);
                }
            }

            return matchingNodes;
        }

        /// <summary>
        /// Retrieves units by the specified owner's InstanceID and type T.
        /// </summary>
        /// <typeparam name="T">The type of units to retrieve.</typeparam>
        /// <param name="ownerInstanceId">The owner's InstanceID of the units.</param>
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
        /// Retrieves all nodes of a specified type T, stopping further traversal of a branch when
        /// type T is found. An optional predicate filters which matching nodes are included.
        /// </summary>
        public List<T> GetSceneNodesByType<T>(Func<T, bool> predicate = null)
            where T : class
        {
            List<T> result = new List<T>();

            void Traverse(ISceneNode node)
            {
                if (node is T typedNode)
                {
                    if (predicate == null || predicate(typedNode))
                        result.Add(typedNode);
                    return;
                }

                foreach (ISceneNode child in node.GetChildren())
                    Traverse(child);
            }

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
                GetFactionByOwnerInstanceID(node.OwnerInstanceID).AddOwnedUnit(node);
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
                GetFactionByOwnerInstanceID(node.OwnerInstanceID).RemoveOwnedUnit(node);
            }
        }

        /// <summary>
        /// Transfers ownership of a node to a different faction. Deregisters the node from
        /// its current owner's index (if any), updates the owner ID on the node, and adds
        /// it to the new owner's index.
        /// </summary>
        /// <param name="node">The node to transfer.</param>
        /// <param name="ownerInstanceId">The new owner's faction instance ID.</param>
        public void ChangeUnitOwnership(ISceneNode node, string ownerInstanceId)
        {
            Faction faction = GetFactionByOwnerInstanceID(ownerInstanceId);

            DeregsiterOwnedUnit(node);

            node.SetOwnerInstanceID(ownerInstanceId);
            faction.AddOwnedUnit(node);
        }

        /// <summary>
        /// Returns the full pool of active game events.
        /// </summary>
        /// <returns>The list backing <see cref="EventPool"/>.</returns>
        public List<GameEvent> GetEventPool()
        {
            return EventPool;
        }

        /// <summary>
        /// Removes the given event from the active event pool.
        /// </summary>
        /// <param name="gameEvent">The event to remove.</param>
        public void RemoveEvent(GameEvent gameEvent)
        {
            EventPool.Remove(gameEvent);
        }

        /// <summary>
        /// Finds an active event by its instance ID.
        /// </summary>
        /// <param name="instanceId">The event's instance ID.</param>
        /// <returns>The matching event, or null if none is present in the pool.</returns>
        public GameEvent GetEventByInstanceID(string instanceId)
        {
            return EventPool.Find(gameEvent => gameEvent.InstanceID == instanceId);
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
        /// <param name="eventInstanceId">The ID of the game event to check.</param>
        /// <returns>True if the game event has been completed; false otherwise.</returns>
        public bool IsEventComplete(string eventInstanceId)
        {
            return CompletedEventIDs.Contains(eventInstanceId);
        }

        /// <summary>
        /// Returns a list of unrecruited officers that can be recruited by the specified owner Instance ID.
        /// </summary>
        /// <param name="ownerInstanceId">The owner Instance ID of the faction that can recruit the officers.</param>
        /// <returns>A list of unrecruited officers that can be recruited by the specified owner Instance ID.</returns>
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
        /// Removes a reference node from the game.
        /// </summary>
        /// <param name="node">The game node to remove as a reference.</param>
        public void AddSceneNodeByInstanceID(ISceneNode node)
        {
            try
            {
                NodesByInstanceID.Add(node.InstanceID, node);
            }
            // If the node already exists in the game, throw an exception.
            catch (ArgumentException)
            {
                throw new InvalidOperationException(
                    $"Cannot add duplicate node \"{node.GetInstanceID()}\" and Display Name \"{node.GetDisplayName()}\" to scene."
                );
            }
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
        /// Initializes the galaxy map by registering nodes and setting parents.
        /// </summary>
        /// <param name="galaxy">The galaxy map to initialize.</param>
        /// <returns>The initialized GalaxyMap.</returns>
        private GalaxyMap InitializeGalaxy(GalaxyMap galaxy)
        {
            galaxy.Traverse(
                (ISceneNode node) =>
                {
                    // Register the node by its instance ID.
                    AddSceneNodeByInstanceID(node);

                    // Set the parent of each child node.
                    foreach (ISceneNode child in node.GetChildren())
                    {
                        child.SetParent(node);
                    }

                    // Register the node to the faction's list of owned units.
                    if (node.OwnerInstanceID != null)
                    {
                        RegisterOwnedUnit(node);
                    }
                }
            );

            return galaxy;
        }
    }
}
