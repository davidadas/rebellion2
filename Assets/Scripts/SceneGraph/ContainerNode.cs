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
        /// Adds each supplied child through the container's canonical mutation path.
        /// </summary>
        /// <param name="children">The children to add.</param>
        internal void AddChildren(IEnumerable<ISceneNode> children)
        {
            foreach (ISceneNode child in children ?? Enumerable.Empty<ISceneNode>())
                AddChild(child);
        }

        /// <summary>
        /// Removes matching children through the container's canonical mutation path.
        /// </summary>
        /// <typeparam name="T">The child type to inspect.</typeparam>
        /// <param name="predicate">The condition identifying children to remove.</param>
        internal void RemoveChildren<T>(Func<T, bool> predicate)
            where T : class, ISceneNode
        {
            foreach (
                T child in GetChildren(includeDisabled: true).OfType<T>().Where(predicate).ToList()
            )
                RemoveChild(child);
        }

        /// <summary>
        /// Removes every direct child through the container's canonical mutation path.
        /// </summary>
        internal void RemoveAllChildren()
        {
            foreach (ISceneNode child in GetChildren(includeDisabled: true).ToList())
                RemoveChild(child);
        }

        internal virtual bool CanAcceptChild(
            ISceneNode child,
            IReadOnlyCollection<ISceneNode> plannedChildren
        )
        {
            return CanAcceptChild(child);
        }
    }
}
