using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Represents a node in the scene graph that can contain other nodes. It does not
/// itself manage any children directly, leaving that responsibility to derivded classes.
/// </summary>
/// <remarks>
/// This class is inherited by classes that manage a collection of child nodes. Examples
/// include Planets, PlanetSystems, CapitalShips, etc. Units without children should use
/// the <see cref="LeafNode"/> class instead.
/// </remarks>
public abstract class ContainerNode : BaseSceneNode
{
    /// <summary>
    /// Traverses the scene graph and performs the specified action on each node.
    /// </summary>
    /// <param name="action">The action to perform on each node.</param>
    public override void Traverse(Action<ISceneNode> action)
    {
        action(this);

        List<ISceneNode> children = GetChildren().ToList();

        // Loop directly over the live collection by iterating until all nodes are processed.
        int index = 0;
        while (index < children.Count)
        {
            ISceneNode child = children[index];
            child.Traverse(action);

            // Move to the next child only if the current child was not removed.
            if (index < children.Count && children[index] == child)
            {
                index++;
            }
        }
    }
}
