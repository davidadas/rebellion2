using System;
using System.Collections.Generic;

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
    /// <param name="action"></param>
    void Traverse(Action<ISceneNode> action);
}
