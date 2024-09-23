using System;
using System.Collections.Generic;

/// <summary>
/// Represents an abstract scene node in the game.
/// </summary>
public abstract class SceneNode : GameEntity
{
    // Parent Info
    [CloneIgnore]
    public string ParentGameID { get; set; }
    protected SceneNode ParentNode;

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
        ParentNode = parentNode;
        ParentGameID = parentNode?.GameID;
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
    /// <param name="child"></param>
    protected internal abstract void AddChild(SceneNode child);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="child"></param>
    protected internal abstract void RemoveChild(SceneNode child);
    
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
