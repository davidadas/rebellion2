using System.Collections;
using System.Collections.Generic;

/// <summary>
/// A simple container which acts as a reference for a SceneNode. Its primary purpose
/// is to provide common functionality for all nodes in the scene graph which have no children.
/// </summary>
public abstract class LeafNode : SceneNode
{
    /// <summary>
    /// Default constructor.
    /// </summary>
    protected LeafNode() { }

    /// <summary>
    /// Adds a child to the node. For leaf nodes, this operation does nothing.
    /// </summary>
    /// <param name="child">The child node to add.</param>
    public override void AddChild(SceneNode child)
    {
        // Do nothing (leaf nodes do not have children).
    }

    /// <summary>
    /// Removes a child from the node. For leaf nodes, this operation does nothing.
    /// </summary>
    /// <param name="child">The child node to remove.</param>
    public override void RemoveChild(SceneNode child)
    {
        // Do nothing (leaf nodes do not have children).
    }

    /// <summary>
    /// Retrieves the children of the node. For leaf nodes, this operation returns an empty collection.
    /// </summary>
    /// <returns>An empty collection of children.</returns>
    public override IEnumerable<SceneNode> GetChildren()
    {
        yield break; // No children to return
    }
}
