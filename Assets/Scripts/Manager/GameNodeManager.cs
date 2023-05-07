using System.Collections.Generic;
using ObjectExtensions;

/// <summary>
/// The UnitManager class is responsible for managing all game units in the scene.
/// It provides methods for adding, removing, and retrieving units by their instance ID
/// and game ID.
/// </summary>
public class GameNodeManager
{
    // Singleton instance.
    private static GameNodeManager instance;

    // Dictionary of all units in the scene, indexed by their instance ID.
    private Dictionary<string, GameNode> _instanceDictionary = new Dictionary<string, GameNode>();

    // Dictionary of all units in the scene, indexed by their game ID.
    private Dictionary<string, GameNode> _referenceDictionary = new Dictionary<string, GameNode>();

    // Initialize singleton
    public static GameNodeManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new GameNodeManager();
            }
            return instance;
        }
    }

    /// <summary>
    /// Adds a GameNode to the instance dictionary.
    /// </summary>
    /// <param name="node"></param>
    public void AddInstance(GameNode node)
    {
        _instanceDictionary[node.InstanceID] = node;
    }

    /// <summary>
    /// Adds a GameNode to the reference dictionary.
    /// </summary>
    /// <param name="node">The GameNode to add.</param>
    public void AddReference(GameNode node)
    {
        string gameID = node.GameID;

        if (!_referenceDictionary.ContainsKey(gameID))
        {
            _referenceDictionary[gameID] = node;
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="gameId"></param>
    /// <returns></returns>
    public TNode CreateInstance<TNode>(string gameId)
        where TNode : GameNode, new()
    {
        TNode node = (TNode)_referenceDictionary[gameId];
        TNode newNode = node.CloneWithoutAttribute<TNode>();

        return newNode;
    }
}
