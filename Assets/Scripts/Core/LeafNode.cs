using System.Collections;
using System.Collections.Generic;

/// <summary>
/// A simple container which acts as a reference for a SceneNode. Its primary purpose
/// is to provide common functionality for all nodes in the scene graph which have no children.
/// </summary>
public class LeafNode : BaseSceneNode
{
    /// <summary>
    /// Default constructor.
    /// </summary>
    protected LeafNode() { }

    /// <summary>
    /// Adds a child to the node. For leaf nodes, this operation does nothing.
    /// </summary>
    /// <param name="child">The child node to add.</param>
    public override void AddChild(ISceneNode child)
    {
        // Do nothing (leaf nodes do not have children).
    }

    /// <summary>
    /// Removes a child from the node. For leaf nodes, this operation does nothing.
    /// </summary>
    /// <param name="child">The child node to remove.</param>
    public override void RemoveChild(ISceneNode child)
    {
        // Do nothing (leaf nodes do not have children).
    }

    /// <summary>
    /// Retrieves the children of the node. For leaf nodes, this operation returns an empty collection.
    /// </summary>
    /// <returns>An empty collection of children.</returns>
    public override IEnumerable<ISceneNode> GetChildren()
    {
        yield break; // No children to return
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="action"></param>
    public override void Traverse(System.Action<ISceneNode> action)
    {
        action(this);
    }
}
