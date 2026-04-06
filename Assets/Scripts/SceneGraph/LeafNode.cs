using System;
using System.Collections;
using System.Collections.Generic;

namespace Rebellion.SceneGraph
{
    /// <summary>
    /// A simple container which acts as a reference for a SceneNode. Its primary purpose
    /// is to provide common functionality for all nodes in the scene graph which have no children.
    /// </summary>
    /// <remarks>
    /// This class is inherited by classes that have no child nodes. Examples include
    /// officers, starfighters, and other entities that do not have children. For nodes
    /// that have children, see the <see cref="ContainerNode"/> class.
    /// </remarks>
    public class LeafNode : BaseSceneNode
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        protected LeafNode() { }

        /// <summary>
        /// Leaf nodes cannot have children.
        /// </summary>
        /// <param name="child">The candidate child node.</param>
        /// <returns>Always false.</returns>
        public override bool CanAcceptChild(ISceneNode child) => false;

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
        ///
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="predicate"></param>
        /// <param name="recurse"></param>
        /// <returns></returns>
        public override IEnumerable<T> GetChildren<T>(Func<T, bool> predicate, bool recurse = true)
        {
            yield break;
        }

        /// <summary>
        /// Retrieves the children of the node. For leaf nodes, this operation returns an empty collection.
        /// </summary>
        /// <returns>An empty collection of children.</returns>
        public override IEnumerable<ISceneNode> GetChildren()
        {
            yield break;
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
}
