// using System;
// using System.Collections.Generic;

// /// <summary>
// /// Represents an abstract scene node in the game.
// /// </summary>
// public abstract class SceneNode : GameEntity
// {
//     // Parent Info
//     [CloneIgnore]
//     public string ParentInstanceID { get; set; }

//     [CloneIgnore]
//     public string LastParentInstanceID { get; set; }

//     [CloneIgnore]
//     protected SceneNode ParentNode;

//     [CloneIgnore]
//     protected SceneNode LastParentNode;

//     /// <summary>
//     /// Default constructor.
//     /// </summary>
//     public SceneNode() { }

//     /// <summary>
//     /// Sets the parent scene node of the current scene node.
//     /// </summary>
//     /// <param name="newParent">The parent scene node.</param>
//     public void SetParent(ISceneNode newParent)
//     {
//         if (ParentNode == newParent)
//             return;

//         SceneNode oldParent = ParentNode;

//         // Remove from old parent.
//         if (oldParent != null)
//         {
//             oldParent.RemoveChild(this);
//         }

//         // Update parent references.
//         LastParentNode = oldParent;
//         ParentNode = newParent;
//         LastParentInstanceID = ParentInstanceID;
//         ParentInstanceID = newParent?.InstanceID;
//     }

//     /// <summary>
//     /// Gets the parent scene node of the current scene node.
//     /// </summary>
//     /// <returns>The parent scene node.</returns>
//     public SceneNode GetParent()
//     {
//         return ParentNode;
//     }

//     /// <summary>
//     /// Returns the last parent scene node of the current scene node.
//     /// </summary>
//     /// <returns>The last parent scene node.</returns>
//     public SceneNode GetLastParent()
//     {
//         return LastParentNode;
//     }

//     /// <summary>
//     /// Returns the closest parent scene node of the specified type.
//     /// </summary>
//     /// <typeparam name="T">The type of the parent scene node.</typeparam>
//     /// <returns>The closest parent scene node of the specified type.</returns>
//     public T GetParentOfType<T>()
//         where T : SceneNode
//     {
//         // Check if the current scene node is the specified type.
//         if (this is T)
//         {
//             return (T)this;
//         }

//         // Check the parent scene nodes.
//         SceneNode parent = ParentNode;
//         HashSet<SceneNode> visitedNodes = new HashSet<SceneNode>();

//         while (parent != null)
//         {
//             if (!visitedNodes.Add(parent))
//             {
//                 // We've encountered this node before, which indicates a cycle.
//                 throw new InvalidOperationException("Cycle detected in scene graph.");
//             }

//             if (parent is T matchingParent)
//             {
//                 return matchingParent;
//             }
//             parent = parent.GetParent();
//         }

//         // No parent of the specified type was found.
//         return null;
//     }

//     /// <summary>
//     /// Returns all children of the current scene node that match the specified game owner id and type.
//     /// </summary>
//     /// <typeparam name="T">The type of the children to retrieve.</typeparam>
//     /// <param name="ownerInstanceId">The game owner id to match.</param>
//     /// <returns>An enumerable collection of children that match the specified game owner id and type.</returns>
//     public IEnumerable<T> GetChildrenByOwnerInstanceID<T>(string ownerInstanceId)
//         where T : SceneNode
//     {
//         List<T> matchingChildren = new List<T>();

//         Traverse(node =>
//         {
//             if (node is T && node.OwnerInstanceID == ownerInstanceId)
//             {
//                 matchingChildren.Add((T)node);
//             }
//         });

//         return matchingChildren;
//     }

//     /// <summary>
//     /// Adds a child scene node to the current scene node.
//     /// </summary>
//     /// <param name="child">The child scene node to add.</param>
//     public abstract void AddChild(ISceneNode child);

//     /// <summary>
//     /// Removes a child scene node from the current scene node.
//     /// </summary>
//     /// <param name="child">The child scene node to remove.</param>
//     public abstract void RemoveChild(ISceneNode child);

//     /// <summary>
//     /// Gets an enumerable collection of child scene nodes.
//     /// </summary>
//     /// <returns>An enumerable collection of scene nodes.</returns>
//     public abstract IEnumerable<SceneNode> GetChildren();

//     /// <summary>
//     /// Traverses the scene node and its children, applying the specified action to each node.
//     /// </summary>
//     /// <param name="action">The action to apply to each scene node.</param>
//     public void Traverse(Action<SceneNode> action)
//     {
//         action(this);

//         foreach (var child in GetChildren())
//         {
//             child.Traverse(action);
//         }
//     }
// }
