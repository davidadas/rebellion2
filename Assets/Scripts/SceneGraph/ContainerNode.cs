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
    /// include Planets, PlanetSectors, CapitalShips, etc. Units without children should use
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
            IReadOnlyList<T> children = GetChildren<T>(includeDisabled: true);
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            List<T> matches = null;
            for (int index = 0; index < children.Count; index++)
            {
                T child = children[index];
                if (!predicate(child))
                    continue;
                matches ??= new List<T>();
                matches.Add(child);
            }

            if (matches != null)
            {
                foreach (T child in matches)
                    RemoveChild(child);
            }
        }

        /// <summary>
        /// Removes every direct child through the container's canonical mutation path.
        /// </summary>
        internal void RemoveAllChildren()
        {
            foreach (ISceneNode child in GetChildren(includeDisabled: true).ToList())
                RemoveChild(child);
        }

        /// <summary>
        /// Checks whether the child can be accepted alongside the planned children.
        /// </summary>
        /// <param name="child">The child.</param>
        /// <param name="plannedChildren">The planned children.</param>
        /// <returns>True when this container can accept the child; otherwise false.</returns>
        internal virtual bool CanAcceptChild(
            ISceneNode child,
            IReadOnlyCollection<ISceneNode> plannedChildren
        )
        {
            return CanAcceptChild(child);
        }
    }
}
