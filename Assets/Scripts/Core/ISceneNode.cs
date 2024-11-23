using System;
using System.Collections.Generic;

/// <summary>
/// The ISceneNode interface serves as the foundational contract for all scene nodes in the game.
/// It defines essential properties and methods that enable objects to interact within the game's
/// hierarchical scene graph structure. Implementing this interface allows entities to define
/// parent-child relationships, manage ownership, and facilitate traversal of the scene graph.
/// </summary>
/// <remarks>
/// This interface, along with the <see cref="IGameEntity"/> interface, was designed to allow other
/// interfaces to declare themselves as objects within the game. While classes implementing interfaces
/// that extend this will naturally inherit the associated properties and methods, this explicit structure
/// eliminates the need for cumbersome type casts or checks when interacting with game entities. This
/// approach is particularly beneficial when working with collections of entities, as it allows seamless
/// iteration and method calls without verifying types.
/// </remarks>
public interface ISceneNode : IGameEntity
{
    // Parent Info
    [CloneIgnore]
    public string ParentInstanceID { get; set; }

    [CloneIgnore]
    public string LastParentInstanceID { get; set; }

    [CloneIgnore]
    [PersistableIgnore]
    public ISceneNode ParentNode { get; set; }

    [CloneIgnore]
    [PersistableIgnore]
    public ISceneNode LastParentNode { get; set; }

    // Owner Info
    [CloneIgnore]
    public string OwnerInstanceID { get; set; }
    public List<string> AllowedOwnerInstanceIDs { get; set; }

    /// <summary>
    ///
    /// </summary>
    /// <param name="newParent"></param>
    void SetParent(ISceneNode newParent);

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    ISceneNode GetParent();

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    ISceneNode GetLastParent();

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    string GetOwnerInstanceID();

    /// <summary>
    ///
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    T GetParentOfType<T>()
        where T : class, ISceneNode;

    /// <summary>
    ///
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="ownerInstanceId"></param>
    /// <returns></returns>
    IEnumerable<T> GetChildrenByOwnerInstanceID<T>(string ownerInstanceId)
        where T : class, ISceneNode;

    /// <summary>
    ///
    /// </summary>
    /// <param name="child"></param>
    void AddChild(ISceneNode child);

    /// <summary>
    ///
    /// </summary>
    /// <param name="child"></param>
    void RemoveChild(ISceneNode child);

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    IEnumerable<ISceneNode> GetChildren();

    /// <summary>
    ///
    /// </summary>
    /// <param name="ownerInstanceId"></param>
    /// <returns></returns>
    IEnumerable<ISceneNode> GetChildrenByOwnerInstanceID(string ownerInstanceId);

    /// <summary>
    ///
    /// </summary>
    /// <param name="action"></param>
    void Traverse(Action<ISceneNode> action);
}
