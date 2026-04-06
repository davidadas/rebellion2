using System;
using System.Collections.Generic;
using Rebellion.Util.Attributes;

namespace Rebellion.SceneGraph
{
    /// <summary>
    /// Base implementation of the <see cref="ISceneNode"/> interface.
    /// </summary>
    public abstract class BaseSceneNode : BaseGameEntity, ISceneNode
    {
        // Parent Info
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

        // Owner Info
        private string _ownerInstanceId;

        [CloneIgnore]
        public string OwnerInstanceID
        {
            get => _ownerInstanceId;
            set => SetOwnerInstanceID(value);
        }
        public List<string> AllowedOwnerInstanceIDs { get; set; }

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
        /// Sets the owner Instance ID. If the ID is not in the allowed list, throws an exception.
        /// </summary>
        /// <param name="ownerInstanceId">The owner Instance ID to set.</param>
        /// <exception cref="InvalidOperationException">Thrown when the owner Instance ID is invalid.</exception>
        public void SetOwnerInstanceID(string ownerInstanceId)
        {
            if (
                AllowedOwnerInstanceIDs == null
                || AllowedOwnerInstanceIDs.Count == 0
                || AllowedOwnerInstanceIDs.Contains(ownerInstanceId)
                || ownerInstanceId == null
            )
            {
                _ownerInstanceId = ownerInstanceId;
            }
            else
            {
                throw new InvalidOperationException(
                    $"Invalid OwnerInstanceID \"{ownerInstanceId}\" for object \"{DisplayName}\". Allowed values: {string.Join(", ", AllowedOwnerInstanceIDs)}, or null."
                );
            }
        }

        /// <summary>
        /// Returns true if this node can accept the given child.
        /// </summary>
        /// <param name="child">The candidate child node.</param>
        /// <returns>True if AddChild would succeed; false otherwise.</returns>
        public abstract bool CanAcceptChild(ISceneNode child);

        /// <summary>
        /// Called when the scene node is added to the game world.
        /// </summary>
        /// <param name="child">The scene node to add.</param>
        public abstract void AddChild(ISceneNode child);

        /// <summary>
        /// Called when the scene node is removed from the game world.
        /// </summary>
        /// <param name="child">The scene node to remove.</param>
        public abstract void RemoveChild(ISceneNode child);

        /// <summary>
        /// Called to retrieve all children of the scene node.
        /// </summary>
        /// <returns>An enumerable collection of children.</returns>
        public abstract IEnumerable<ISceneNode> GetChildren();

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
        /// Determines whether the specified owner instance ID is present in the allowed owner list.
        /// </summary>
        /// <param name="ownerInstanceId">The instance ID of the owner to validate.</param>
        /// <returns>True if the owner instance ID exists in the allowed list; otherwise, false.</returns>
        public bool HasAllowedOwnerInstanceID(string ownerInstanceId)
        {
            if (string.IsNullOrEmpty(ownerInstanceId))
                return false;

            // If null or empty, assumes universally that it is allowed.
            // This is done so that when modding you do not need to add new faction IDs
            // to every single planet that exists inside of the game.
            if (AllowedOwnerInstanceIDs == null || AllowedOwnerInstanceIDs.Count == 0)
                return true;

            return AllowedOwnerInstanceIDs.Contains(ownerInstanceId);
        }

        /// <summary>
        /// Called to traverse this scene node and all of its children.
        /// </summary>
        /// <param name="action">The action to perform on each scene node.</param>
        public abstract void Traverse(Action<ISceneNode> action);
    }
}
