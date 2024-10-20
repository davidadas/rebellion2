using System;
using System.Collections.Generic;

/// <summary>
/// Represents an abstract scene node in the game.
/// </summary>
public abstract class SceneNode : GameEntity
{
    // Parent Info
    [CloneIgnore]
    public string ParentTypeID { get; set; }
    public string LastParentTypeID { get; set; }
    protected SceneNode ParentNode;
    protected SceneNode LastParentNode;

    /// <summary>
    /// Default constructor.
    /// </summary>
    public SceneNode() { }

    /// <summary>
    /// Sets the parent scene node of the current scene node.
    /// </summary>
    /// <param name="parentNode">The parent scene node.</param>
    public void SetParent(SceneNode parentNode)
    {
        LastParentNode = ParentNode;
        ParentNode = parentNode;
        LastParentTypeID = ParentTypeID;
        ParentTypeID = parentNode?.TypeID;
    }

    /// <summary>
    /// Gets the parent scene node of the current scene node.
    /// </summary>
    /// <returns>The parent scene node.</returns>
    public SceneNode GetParent()
    {
        return ParentNode;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public SceneNode GetLastParent()
    {
        return LastParentNode;
    }

    /// Gets the closest parent scene node of the specified type.
    /// </summary>
    /// <typeparam name="T">The type of the parent scene node.</typeparam>
    /// <returns>The closest parent scene node of the specified type.</returns>
    public T GetClosestParentOfType<T>() where T : SceneNode
    {
        SceneNode parent = ParentNode;
        while (parent != null)
        {
            if (parent is T)
            {
                return (T)parent;
            }
            parent = parent.GetParent();
        }
        return null;
    }

    /// <summary>
    /// Gets all children of the current scene node that match the specified game owner id and type.
    /// </summary>
    /// <typeparam name="T">The type of the children to retrieve.</typeparam>
    /// <param name="ownerTypeId">The game owner id to match.</param>
    /// <returns>An enumerable collection of children that match the specified game owner id and type.</returns>
    public IEnumerable<T> GetChildrenByOwnerTypeID<T>(string ownerTypeId) where T : SceneNode
    {
        List<T> matchingChildren = new List<T>();

        Traverse(node =>
        {
            if (node is T && node.OwnerTypeID == ownerTypeId)
            {
                matchingChildren.Add((T)node);
            }
        });

        return matchingChildren;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="child"></param>
    public abstract void AddChild(SceneNode child);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="child"></param>
    public abstract void RemoveChild(SceneNode child);
    
    /// <summary>
    /// Gets an enumerable collection of child scene nodes.
    /// </summary>
    /// <returns>An enumerable collection of scene nodes.</returns>
    public abstract IEnumerable<SceneNode> GetChildren();

    /// <summary>
    /// Traverses the scene node and its children, applying the specified action to each node.
    /// </summary>
    /// <param name="action">The action to apply to each scene node.</param>
    public void Traverse(Action<SceneNode> action)
    {
        action(this);

        foreach (var child in GetChildren())
        {
            child.Traverse(action);
        }
    }
}
