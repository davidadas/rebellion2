using System;
using System.Collections.Generic;
using System.Linq;

namespace Rebellion.SceneGraph
{
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
        /// Retrieves the children of the current node that match the specified predicate.
        /// </summary>
        /// <typeparam name="T">The type of the children to retrieve.</typeparam>
        /// <param name="predicate">A predicate to filter the children.</param>
        /// <param name="recurse">A flag indicating whether the traversal should be recursive.</param>
        /// <returns>An enumerable collection of children that match the specified predicate.</returns>
        public override IEnumerable<T> GetChildren<T>(Func<T, bool> predicate, bool recurse = true)
        {
            List<T> matchingChildren = new List<T>();
            predicate ??= _ => true;

            if (recurse)
            {
                // Use the Traverse method for recursive traversal.
                Traverse(
                    (ISceneNode node) =>
                    {
                        if (node != this && node is T typedNode && predicate(typedNode))
                        {
                            matchingChildren.Add(typedNode);
                        }
                    }
                );
            }
            else
            {
                // For non-recursive, only check immediate children.
                foreach (ISceneNode child in GetChildren())
                {
                    if (child is T typedNode && predicate(typedNode))
                    {
                        matchingChildren.Add(typedNode);
                    }
                }
            }

            return matchingChildren;
        }

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
}
