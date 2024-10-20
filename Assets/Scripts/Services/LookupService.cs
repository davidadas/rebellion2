using System;
using System.Collections.Generic;

public interface ILookupService
{
    public T GetSceneNodeByTypeID<T>(string typeId) where T : SceneNode;

    public T GetSceneNodeByInstanceID<T>(string instanceId) where T : SceneNode;

    public List<SceneNode> GetSceneNodesByInstanceIDs(List<string> instanceIDs);
}

/// <summary>
/// 
/// </summary>
public class LookupService : ILookupService
{
    private Game game;

    public LookupService(Game game)
    {
        this.game = game;
    }

    /// <summary>
    /// Returns the scene node with the given type ID.
    /// </summary>
    /// <param name="typeId">The type ID of the scene node.</param>
    /// <returns>The scene node with the given type ID.</returns>
    public T GetSceneNodeByTypeID<T>(string typeId) where T : SceneNode
    {
        return game.GetSceneNodeByTypeID<T>(typeId);
    }

    /// <summary>
    /// Returns the scene node with the given instance ID.
    /// </summary>
    /// <param name="instanceId">The instance ID of the scene node.</param>
    /// <returns>The scene node with the given instance ID.</returns>
    public T GetSceneNodeByInstanceID<T>(string instanceId) where T : SceneNode
    {
        return game.GetSceneNodeByInstanceID<T>(instanceId);
    }

    /// <summary>
    /// Returns the scene nodes with the given instance IDs.
    /// </summary>
    /// <param name="instanceIDs"></param>
    /// <returns></returns>
    public List<SceneNode> GetSceneNodesByInstanceIDs(List<string> instanceIDs)
    {
        return game.GetSceneNodesByInstanceIDs(instanceIDs);
    }
}
