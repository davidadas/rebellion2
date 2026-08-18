using System;
using System.Collections.Generic;
using Rebellion.Util.Extensions;
using Rebellion.Util.Serialization;

namespace Rebellion.SceneGraph
{
    /// <summary>
    /// The ISceneNode interface serves as the foundational contract for all scene nodes in the game.
    /// It defines essential properties and methods that enable objects to interact within the game's
    /// hierarchical scene graph structure. Implementing this interface allows entities to define
    /// parent-child relationships, manage ownership, and facilitate traversal of the scene graph.
    /// </summary>
    /// <remarks>
    /// This interface, along with the <see cref="IGameEntity"/> interface, was designed to allow other
    /// interfaces to declare themselves as objects within the game. While classes implementing interfaces
    /// that extend this will naturally inherit the associated properties and methods, this explicit structure
    /// eliminates the need for cumbersome type casts or checks when interacting with game entities. This
    /// approach is particularly beneficial when working with collections of entities, as it allows seamless
    /// iteration and method calls without verifying types.
    /// </remarks>
    public interface ISceneNode : IGameEntity
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

        // Owner Info.
        [CloneIgnore]
        public string OwnerInstanceID { get; set; }

        /// <summary>
        /// Gets or sets whether this node participates in active gameplay.
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Returns whether this node and every ancestor are enabled.
        /// </summary>
        /// <returns>True when the node is active in the scene hierarchy.</returns>
        bool IsEnabledInHierarchy();

        /// <summary>
        /// Sets the parent node of this scene node.
        /// </summary>
        /// <param name="newParent"></param>
        void SetParent(ISceneNode newParent);

        /// <summary>
        /// Returns the current parent node of this scene node.
        /// </summary>
        /// <returns>The parent node, or null if this node has no parent.</returns>
        ISceneNode GetParent();

        /// <summary>
        /// Returns the previous parent node before the most recent reparenting.
        /// </summary>
        /// <returns>The last parent node, or null if the node has not been reparented.</returns>
        ISceneNode GetLastParent();

        /// <summary>
        /// Returns the instance ID of the faction that owns this scene node.
        /// </summary>
        /// <returns>The owner instance ID, or null if this node has no owner.</returns>
        string GetOwnerInstanceID();

        /// <summary>
        /// Sets the instance ID of the faction that owns this scene node.
        /// </summary>
        /// <param name="ownerInstanceID"></param>
        void SetOwnerInstanceID(string ownerInstanceID);

        /// <summary>
        /// Walks up the scene graph and returns the nearest ancestor of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>The nearest ancestor of type <typeparamref name="T"/>, or null if none is found.</returns>
        T GetParentOfType<T>()
            where T : class, ISceneNode;

        /// <summary>
        /// Returns whether this node can accept the specified child node.
        /// </summary>
        /// <param name="child"></param>
        /// <returns>True if the child can be added; otherwise, false.</returns>
        bool CanAcceptChild(ISceneNode child);

        /// <summary>
        /// Adds the specified node as a child of this node.
        /// </summary>
        /// <param name="child"></param>
        void AddChild(ISceneNode child);

        /// <summary>
        /// Removes the specified child node from this node.
        /// </summary>
        /// <param name="child"></param>
        void RemoveChild(ISceneNode child);

        /// <summary>
        /// Returns all direct children of this node.
        /// </summary>
        /// <returns>The children of this node.</returns>
        IReadOnlyList<ISceneNode> GetChildren(bool includeDisabled = false);

        /// <summary>
        /// Returns direct children assignable to <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The direct child type to return.</typeparam>
        /// <param name="includeDisabled">Whether disabled children may be returned.</param>
        /// <returns>The matching direct children.</returns>
        IReadOnlyList<T> GetChildren<T>(bool includeDisabled = false)
            where T : class, ISceneNode;

        /// <summary>
        /// Returns all descendants of this node.
        /// </summary>
        /// <param name="includeDisabled">Whether disabled branches may be traversed.</param>
        /// <returns>The descendants of this node.</returns>
        IReadOnlyList<ISceneNode> GetDescendants(bool includeDisabled = false);

        /// <summary>
        /// Returns descendants assignable to <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The descendant type to return.</typeparam>
        /// <param name="includeDisabled">Whether disabled branches may be traversed.</param>
        /// <returns>The matching descendants.</returns>
        IReadOnlyList<T> GetDescendants<T>(bool includeDisabled = false)
            where T : class, ISceneNode;

        /// <summary>
        /// Visits this node and all descendants, invoking the given action on each.
        /// </summary>
        /// <param name="action"></param>
        void Traverse(Action<ISceneNode> action);
    }
}
