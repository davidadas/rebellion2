using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Rebellion.Util.Serialization;

namespace Rebellion.SceneGraph
{
    /// <summary>
    /// Base implementation of the <see cref="ISceneNode"/> interface.
    /// </summary>
    public abstract class BaseSceneNode : BaseGameEntity, ISceneNode
    {
        [ThreadStatic]
        private static Func<string, IDisposable> _performanceMeasurementFactory;

        /// <summary>
        /// Gets or sets the current thread's optional operation measurements.
        /// </summary>
        internal static Func<string, IDisposable> PerformanceMeasurementFactory
        {
            get => _performanceMeasurementFactory;
            set => _performanceMeasurementFactory = value;
        }

        // Parent Info.
        public string ParentInstanceID { get; set; }

        public string LastParentInstanceID { get; set; }

        [PersistableIgnore]
        public ISceneNode ParentNode { get; set; }

        [PersistableIgnore]
        public ISceneNode LastParentNode { get; set; }

        /// <summary>
        /// Gets or sets whether this node participates in active gameplay.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        // Owner Info.
        private string _ownerInstanceId;

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
            OnHierarchyChanged();
        }

        /// <summary>
        /// Notifies this subtree after its ancestor chain changes.
        /// </summary>
        protected virtual void OnHierarchyChanged()
        {
            foreach (ISceneNode child in EnumerateChildren())
                ((BaseSceneNode)child).OnHierarchyChanged();
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
        public bool IsActive()
        {
            ISceneNode node = this;
            ISceneNode cycleAnchor = this;
            long checkpointDistance = 1;
            long distance = 0;
            while (node != null)
            {
                if (!node.IsEnabled)
                    return false;
                node = node.GetParent();
                if (ReferenceEquals(node, cycleAnchor))
                    throw new InvalidOperationException("Cycle detected in scene graph.");
                if (++distance == checkpointDistance)
                {
                    cycleAnchor = node;
                    checkpointDistance *= 2;
                    distance = 0;
                }
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
            ISceneNode parent = ParentNode;
            ISceneNode cycleAnchor = this;
            long checkpointDistance = 1;
            long distance = 0;

            while (parent != null)
            {
                if (ReferenceEquals(parent, this) || ReferenceEquals(parent, cycleAnchor))
                    throw new InvalidOperationException("Cycle detected in scene graph.");

                if (parent is T matchingParent)
                {
                    return matchingParent;
                }

                if (++distance == checkpointDistance)
                {
                    cycleAnchor = parent;
                    checkpointDistance *= 2;
                    distance = 0;
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
        /// Collects direct children through the container's raw traversal contract.
        /// </summary>
        /// <typeparam name="T">The child type retained in the snapshot.</typeparam>
        /// <param name="collector">The current query's validation and collection state.</param>
        protected virtual void CollectDirectChildren<T>(ref DirectChildCollector<T> collector)
            where T : class, ISceneNode
        {
            foreach (ISceneNode child in EnumerateChildren())
                collector.Add(child);
        }

        /// <summary>
        /// Returns a read-only snapshot of this node's children at the requested depth.
        /// </summary>
        /// <param name="recursive">Whether to include children at every depth.</param>
        /// <param name="includeDisabled">Whether disabled children may be returned.</param>
        /// <returns>The matching children.</returns>
        public IReadOnlyList<ISceneNode> GetChildren(
            bool recursive = false,
            bool includeDisabled = false
        )
        {
            using var performanceMeasurement = MeasureOperation(nameof(GetChildren));
            if (!recursive)
                return GetDirectChildren<ISceneNode>(includeDisabled);

            return GetDescendants<ISceneNode>(includeDisabled);
        }

        /// <summary>
        /// Returns a read-only snapshot of children assignable to <typeparamref name="T"/> at the requested depth.
        /// </summary>
        /// <typeparam name="T">The child type to return.</typeparam>
        /// <param name="recursive">Whether to include children at every depth.</param>
        /// <param name="includeDisabled">Whether disabled children may be returned.</param>
        /// <returns>The matching children.</returns>
        public IReadOnlyList<T> GetChildren<T>(bool recursive = false, bool includeDisabled = false)
            where T : class, ISceneNode
        {
            using var performanceMeasurement = MeasureOperation(nameof(GetChildren));
            if (recursive)
                return GetDescendants<T>(includeDisabled);

            return GetDirectChildren<T>(includeDisabled);
        }

        /// <summary>
        /// Collects matching descendants into one ordered, read-only snapshot.
        /// </summary>
        /// <typeparam name="T">The descendant type to include.</typeparam>
        /// <param name="includeDisabled">Whether inactive children may be returned.</param>
        /// <returns>The depth-first descendant snapshot, including an empty read-only list when no descendants match.</returns>
        private IReadOnlyList<T> GetDescendants<T>(bool includeDisabled)
            where T : class, ISceneNode
        {
            using var performanceMeasurement = MeasureOperation(nameof(GetDescendants));
            List<T> descendants = new List<T>();
            HashSet<ISceneNode> currentPath = new HashSet<ISceneNode> { this };
            CollectDescendants(this, includeDisabled, currentPath, descendants);
            return descendants.AsReadOnly();
        }

        /// <summary>
        /// Visits every descendant in direct-child order while rejecting cycles before collecting matches.
        /// </summary>
        /// <typeparam name="T">The descendant type to include.</typeparam>
        /// <param name="node">The current node whose direct children are visited.</param>
        /// <param name="includeDisabled">Whether inactive children may be visited.</param>
        /// <param name="currentPath">The nodes on the active traversal path.</param>
        /// <param name="descendants">The ordered matching descendants collected so far.</param>
        private static void CollectDescendants<T>(
            ISceneNode node,
            bool includeDisabled,
            HashSet<ISceneNode> currentPath,
            List<T> descendants
        )
            where T : class, ISceneNode
        {
            foreach (
                ISceneNode child in node.GetChildren(
                    recursive: false,
                    includeDisabled: includeDisabled
                )
            )
            {
                if (!currentPath.Add(child))
                    throw new InvalidOperationException("Cycle detected in scene graph.");
                if (child is T matchingChild)
                    descendants.Add(matchingChild);
                CollectDescendants(child, includeDisabled, currentPath, descendants);
                currentPath.Remove(child);
            }
        }

        /// <summary>
        /// Begins an optional runtime-provided measurement for this operation.
        /// </summary>
        /// <param name="operation">The operation being measured.</param>
        /// <returns>The current measurement, or null when diagnostics are inactive.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static IDisposable MeasureOperation(string operation) =>
            _performanceMeasurementFactory?.Invoke(operation);

        /// <summary>
        /// Copies direct children while sharing each parent hierarchy's validation within this query only.
        /// </summary>
        /// <typeparam name="T">The child type to include.</typeparam>
        /// <param name="includeDisabled">Whether inactive children may be returned.</param>
        /// <returns>The ordered, read-only child snapshot.</returns>
        private IReadOnlyList<T> GetDirectChildren<T>(bool includeDisabled)
            where T : class, ISceneNode
        {
            using var performanceMeasurement = MeasureOperation(nameof(GetDirectChildren));
            DirectChildCollector<T> collector = new DirectChildCollector<T>(this, includeDisabled);
            CollectDirectChildren(ref collector);
            return collector.GetSnapshot();
        }

        /// <summary>
        /// Creates a detached copy of this node and, when requested, its descendant hierarchy.
        /// </summary>
        /// <param name="recursive">Whether to copy descendants at every depth.</param>
        /// <param name="includeDisabled">Whether disabled descendants may be copied.</param>
        /// <param name="configureCopy">Optional configuration applied to each detached node copy.</param>
        /// <returns>A detached copy of this node.</returns>
        public ISceneNode CreateCopy(
            bool recursive = false,
            bool includeDisabled = false,
            Action<ISceneNode> configureCopy = null
        )
        {
            using var performanceMeasurement = MeasureOperation(nameof(CreateCopy));
            BaseSceneNode copy = CreateNodeCopy();
            if (copy == null || copy.GetType() != GetType())
            {
                throw new InvalidOperationException(
                    $"{GetType().Name}.{nameof(CreateNodeCopy)} must return a non-null {GetType().Name}."
                );
            }

            CopyStateTo(copy);
            configureCopy?.Invoke(copy);
            if (recursive)
                CopyChildrenTo(copy, includeDisabled, configureCopy);
            return copy;
        }

        /// <summary>
        /// Creates an empty copy with the same concrete scene-node type.
        /// </summary>
        /// <returns>An empty node of the same concrete type.</returns>
        protected abstract BaseSceneNode CreateNodeCopy();

        /// <summary>
        /// Copies the state shared by every scene node into a detached destination.
        /// </summary>
        /// <param name="copy">The destination node.</param>
        protected virtual void CopyStateTo(BaseSceneNode copy)
        {
            copy.InstanceID = InstanceID;
            copy.TypeID = TypeID;
            copy.DisplayName = DisplayName;
            copy.DisplayStatus = DisplayStatus;
            copy.DisplayImagePath = DisplayImagePath;
            copy.SmallDisplayImagePath = SmallDisplayImagePath;
            copy.MessageImagePath = MessageImagePath;
            copy.InTransitImagePath = InTransitImagePath;
            copy.InTransitSmallImagePath = InTransitSmallImagePath;
            copy.DamagedImagePath = DamagedImagePath;
            copy.DamagedSmallImagePath = DamagedSmallImagePath;
            copy.CapturedOverlayImagePath = CapturedOverlayImagePath;
            copy.InjuredImagePath = InjuredImagePath;
            copy.Description = Description;
            copy.OwnerInstanceID = OwnerInstanceID;
            copy.IsEnabled = IsEnabled;
        }

        /// <summary>
        /// Copies eligible direct children and attaches them to their copied parent.
        /// </summary>
        /// <param name="copy">The destination parent.</param>
        /// <param name="includeDisabled">Whether disabled branches should be copied.</param>
        /// <param name="configureCopy">Optional configuration applied to each detached node copy.</param>
        private void CopyChildrenTo(
            BaseSceneNode copy,
            bool includeDisabled,
            Action<ISceneNode> configureCopy
        )
        {
            using var performanceMeasurement = MeasureOperation(nameof(CopyChildrenTo));
            foreach (ISceneNode child in EnumerateChildren())
            {
                if (!includeDisabled && !child.IsActive())
                    continue;

                ISceneNode childCopy = child.CreateCopy(
                    recursive: true,
                    includeDisabled: includeDisabled,
                    configureCopy: configureCopy
                );
                AttachCopiedChild(copy, child, childCopy);
            }
        }

        /// <summary>
        /// Attaches a copied child while preserving any relationship metadata owned by the parent.
        /// </summary>
        /// <param name="destination">The copied parent receiving the child.</param>
        /// <param name="sourceChild">The source child whose relationship is being copied.</param>
        /// <param name="copiedChild">The detached copied child.</param>
        protected virtual void AttachCopiedChild(
            BaseSceneNode destination,
            ISceneNode sourceChild,
            ISceneNode copiedChild
        )
        {
            destination.AddChild(copiedChild);
            copiedChild.SetParent(destination);
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

        /// <summary>
        /// Validates raw children and builds one independent direct-child snapshot.
        /// </summary>
        /// <typeparam name="T">The child type retained after activity validation.</typeparam>
        protected struct DirectChildCollector<T>
            where T : class, ISceneNode
        {
            private readonly BaseSceneNode _container;
            private readonly bool _includeDisabled;
            private List<T> _children;
            private bool? _containerIsActive;
            private ISceneNode _sharedParent;
            private bool? _sharedParentIsActive;

            /// <summary>
            /// Creates collection state for one container query.
            /// </summary>
            /// <param name="container">The container whose children are inspected.</param>
            /// <param name="includeDisabled">Whether inactive children may be retained.</param>
            public DirectChildCollector(BaseSceneNode container, bool includeDisabled)
            {
                _container = container;
                _includeDisabled = includeDisabled;
                _children = null;
                _containerIsActive = null;
                _sharedParent = null;
                _sharedParentIsActive = null;
            }

            /// <summary>
            /// Validates one raw child before retaining a matching type.
            /// </summary>
            /// <param name="child">The next raw child in container order.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Add(ISceneNode child)
            {
                if (!_includeDisabled)
                {
                    if (!child.IsEnabled)
                        return;
                    ISceneNode parent = child.GetParent();
                    if (ReferenceEquals(parent, _container))
                    {
                        _containerIsActive ??= _container.IsActive();
                        if (!_containerIsActive.Value)
                            return;
                    }
                    else
                    {
                        if (
                            !_sharedParentIsActive.HasValue
                            || !ReferenceEquals(_sharedParent, parent)
                        )
                        {
                            _sharedParent = parent;
                            _sharedParentIsActive = child.IsActive();
                        }
                        if (!_sharedParentIsActive.Value)
                            return;
                    }
                }

                if (child is T matchingChild)
                {
                    _children ??= new List<T>();
                    _children.Add(matchingChild);
                }
            }

            /// <summary>
            /// Returns the completed snapshot with its own backing collection.
            /// </summary>
            /// <returns>The shared empty array or the independent read-only child list.</returns>
            public IReadOnlyList<T> GetSnapshot() =>
                _children == null ? Array.Empty<T>() : _children.AsReadOnly();
        }
    }
}
