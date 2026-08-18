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
        /// Enumerates the raw direct children stored by this node.
        /// </summary>
        /// <returns>The raw direct children.</returns>
        protected abstract IEnumerable<ISceneNode> EnumerateChildren();

        /// <summary>
        /// Returns a read-only snapshot of this node's direct children.
        /// </summary>
        /// <param name="includeDisabled">Whether disabled children may be returned.</param>
        /// <returns>The direct children.</returns>
        public IReadOnlyList<ISceneNode> GetChildren(bool includeDisabled = false)
        {
            IEnumerable<ISceneNode> children = EnumerateChildren();
            return (
                includeDisabled ? children : children.Where(child => child.IsEnabledInHierarchy())
            )
                .ToList()
                .AsReadOnly();
        }

        /// <summary>
        /// Returns a read-only snapshot of direct children assignable to <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The direct child type to return.</typeparam>
        /// <param name="includeDisabled">Whether disabled children may be returned.</param>
        /// <returns>The matching direct children.</returns>
        public IReadOnlyList<T> GetChildren<T>(bool includeDisabled = false)
            where T : class, ISceneNode
        {
            return GetChildren(includeDisabled).OfType<T>().ToList().AsReadOnly();
        }

        /// <summary>
        /// Returns a read-only snapshot of all descendants of this node.
        /// </summary>
        /// <param name="includeDisabled">Whether disabled branches may be traversed.</param>
        /// <returns>All descendants.</returns>
        public IReadOnlyList<ISceneNode> GetDescendants(bool includeDisabled = false)
        {
            List<ISceneNode> descendants = new List<ISceneNode>();
            HashSet<ISceneNode> currentPath = new HashSet<ISceneNode> { this };

            void Collect(ISceneNode node)
            {
                foreach (ISceneNode child in node.GetChildren(includeDisabled))
                {
                    if (!currentPath.Add(child))
                        throw new InvalidOperationException("Cycle detected in scene graph.");
                    descendants.Add(child);
                    Collect(child);
                    currentPath.Remove(child);
                }
            }

            Collect(this);
            return descendants.AsReadOnly();
        }

        /// <summary>
        /// Returns a read-only snapshot of descendants assignable to <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The descendant type to return.</typeparam>
        /// <param name="includeDisabled">Whether disabled branches may be traversed.</param>
        /// <returns>The matching descendants.</returns>
        public IReadOnlyList<T> GetDescendants<T>(bool includeDisabled = false)
            where T : class, ISceneNode
        {
            return GetDescendants(includeDisabled).OfType<T>().ToList().AsReadOnly();
        }

        /// <summary>
        /// Visits this node and all descendants, including disabled branches.
        /// </summary>
        /// <param name="action">The action to perform on each node.</param>
        internal void TraverseIncludingDisabled(Action<ISceneNode> action)
        {
            action(this);
            foreach (ISceneNode child in GetChildren(includeDisabled: true))
                ((BaseSceneNode)child).TraverseIncludingDisabled(action);
        }

        /// <summary>
        /// Called to traverse this scene node and all of its children.
        /// </summary>
        /// <param name="action">The action to perform on each scene node.</param>
        public void Traverse(Action<ISceneNode> action)
        {
            action(this);
            foreach (ISceneNode child in GetChildren())
                child.Traverse(action);
        }
    }
}
