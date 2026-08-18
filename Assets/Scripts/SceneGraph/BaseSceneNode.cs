using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Util.Extensions;
using Rebellion.Util.Serialization;

namespace Rebellion.SceneGraph
{
    /// <summary>
    /// Base implementation of the <see cref="ISceneNode"/> interface.
    /// </summary>
    public abstract class BaseSceneNode : BaseGameEntity, ISceneNode
    {
        // Parent Info.
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

        /// <summary>
        /// Gets or sets whether this node participates in active gameplay.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        // Owner Info.
        private string _ownerInstanceId;

        [CloneIgnore]
        public string OwnerInstanceID
        {
            get => _ownerInstanceId;
            set => SetOwnerInstanceID(value);
        }

        /// <summary>
        /// Default constructor.
        /// </summary>
        public BaseSceneNode() { }

        /// <summary>
        /// Sets the parent scene node of the current scene node.
        /// </summary>
        /// <param name="newParent">The parent scene node.</param>
        public void SetParent(ISceneNode newParent)
        {
            if (ParentNode == newParent)
            {
                return;
            }

            ISceneNode oldParent = ParentNode;

            // Remove from old parent.
            oldParent?.RemoveChild(this);

            // Update parent references.
            LastParentNode = oldParent;
            ParentNode = newParent;
            LastParentInstanceID = ParentInstanceID;
            ParentInstanceID = newParent?.InstanceID;
        }

        /// <summary>
        /// Gets the parent scene node of the current scene node.
        /// </summary>
        /// <returns>The parent scene node.</returns>
        public ISceneNode GetParent()
        {
            return ParentNode;
        }

        /// <summary>
        /// Returns the last parent scene node of the current scene node.
        /// </summary>
        /// <returns>The last parent scene node.</returns>
        public ISceneNode GetLastParent()
        {
            return LastParentNode;
        }

        /// <summary>
        /// Returns whether this node and every ancestor are enabled.
        /// </summary>
        /// <returns>True when the node is active in the scene hierarchy.</returns>
        public bool IsEnabledInHierarchy()
        {
            ISceneNode node = this;
            HashSet<ISceneNode> visitedNodes = new HashSet<ISceneNode>();
            while (node != null)
            {
                if (!visitedNodes.Add(node))
                    throw new InvalidOperationException("Cycle detected in scene graph.");
                if (!node.IsEnabled)
                    return false;
                node = node.GetParent();
            }

            return true;
        }

        /// <summary>
        /// Returns the instance id of the parent scene node.
        /// </summary>
        /// <returns>The instance id of the parent scene node.</returns>
        public string GetOwnerInstanceID()
        {
            return OwnerInstanceID;
        }

        /// <summary>
        /// Returns the closest parent scene node of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of the parent scene node.</typeparam>
        /// <returns>The closest parent scene node of the specified type.</returns>
        public T GetParentOfType<T>()
            where T : class, ISceneNode
        {
            // Check the parent scene nodes.
            ISceneNode parent = ParentNode;
            HashSet<ISceneNode> visitedNodes = new HashSet<ISceneNode> { this };

            while (parent != null)
            {
                if (!visitedNodes.Add(parent))
                {
                    // Node has already been visited, indicating a cycle in the scene graph.
                    throw new InvalidOperationException("Cycle detected in scene graph.");
                }

                if (parent is T matchingParent)
                {
                    return matchingParent;
                }

                parent = parent.GetParent();
            }

            // No parent of the specified type was found.
            return null;
        }

        /// <summary>
        /// Sets the current owner's stable faction instance ID.
        /// </summary>
        /// <param name="ownerInstanceId">The owner Instance ID to set.</param>
        public void SetOwnerInstanceID(string ownerInstanceId)
        {
            _ownerInstanceId = ownerInstanceId;
        }

        /// <summary>
        /// Returns true if this node can accept the given child.
        /// </summary>
        /// <param name="child">The candidate child node.</param>
        /// <returns>True if AddChild would succeed; false otherwise.</returns>
        public abstract bool CanAcceptChild(ISceneNode child);

        /// <summary>
        /// Adds a child to this node's child collection.
        /// </summary>
        /// <param name="child">The child node to add.</param>
        public abstract void AddChild(ISceneNode child);

        /// <summary>
        /// Removes a child from this node's child collection.
        /// </summary>
        /// <param name="child">The child node to remove.</param>
        public abstract void RemoveChild(ISceneNode child);

        /// <summary>
        /// Called to retrieve all children of the scene node.
        /// </summary>
        /// <returns>An enumerable collection of children.</returns>
        public abstract IEnumerable<ISceneNode> GetChildren(bool includeDisabled = false);

        /// <summary>
        /// Visits this node and all descendants, including disabled branches.
        /// </summary>
        /// <param name="action">The action to perform on each node.</param>
        internal void TraverseIncludingDisabled(Action<ISceneNode> action)
        {
            action(this);
            foreach (ISceneNode child in GetChildren(includeDisabled: true).ToList())
                ((BaseSceneNode)child).TraverseIncludingDisabled(action);
        }

        /// <summary>
        /// Called to retrieve all children of the scene node that match the specified type.
        /// </summary>
        /// <typeparam name="T">The type of scene node to retrieve.</typeparam>
        /// <param name="predicate">The predicate to filter the children.</param>
        /// <param name="recurse">Whether to recursively search for children.</param>
        /// <returns>An enumerable collection of children.</returns>
        public abstract IEnumerable<T> GetChildren<T>(Func<T, bool> predicate, bool recurse = true)
            where T : class, ISceneNode;

        /// <summary>
        /// Called to traverse this scene node and all of its children.
        /// </summary>
        /// <param name="action">The action to perform on each scene node.</param>
        public abstract void Traverse(Action<ISceneNode> action);
    }
}
