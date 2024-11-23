using System;
using System.Collections.Generic;
using System.Linq;

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
            return;

        ISceneNode oldParent = ParentNode;

        // Remove from old parent.
        if (oldParent != null)
        {
            oldParent.RemoveChild(this);
        }

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
        // Check if the current scene node is the specified type.
        if (this is T matchingSelf)
        {
            return matchingSelf;
        }

        // Check the parent scene nodes.
        ISceneNode parent = ParentNode;
        HashSet<ISceneNode> visitedNodes = new HashSet<ISceneNode>();

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
    /// Sets the owner type id. If the ID is not in the allowed list, throws an exception.
    /// </summary>
    /// <param name="value">The owner type id to set.</param>
    /// <exception cref="GameStateException">Thrown when the owner type id is invalid.</exception>
    public void SetOwnerInstanceID(string value)
    {
        if (
            AllowedOwnerInstanceIDs == null
            || AllowedOwnerInstanceIDs.Count == 0
            || AllowedOwnerInstanceIDs.Contains(value)
        )
        {
            _ownerInstanceId = value;
        }
        else
        {
            throw new GameStateException(
                $"Invalid owner type id \"{value}\" for object \"{DisplayName}\"."
            );
        }
    }

    /// <summary>
    /// Returns all children of the current scene node that match the specified game owner id and type.
    /// </summary>
    /// <typeparam name="T">The type of the children to retrieve.</typeparam>
    /// <param name="ownerInstanceId">The game owner id to match.</param>
    /// <returns>An enumerable collection of children that match the specified game owner id and type.</returns>
    public IEnumerable<T> GetChildrenByOwnerInstanceID<T>(string ownerInstanceId)
        where T : class, ISceneNode
    {
        List<T> matchingChildren = new List<T>();

        Traverse(node =>
        {
            if (node is T && node.OwnerInstanceID == ownerInstanceId)
            {
                matchingChildren.Add((T)node);
            }
        });

        return matchingChildren;
    }

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
    /// Returns all children of the current scene node that match the specified game owner id.
    /// </summary>
    /// <param name="ownerInstanceId">The game owner id to match.</param>
    /// <returns>An enumerable collection of children that match the specified game owner id.</returns>
    public IEnumerable<ISceneNode> GetChildrenByOwnerInstanceID(string ownerInstanceId)
    {
        return GetChildren().Where(child => child.OwnerInstanceID == ownerInstanceId);
    }

    /// <summary>
    /// Called to traverse this scene node and all of its children.
    /// </summary>
    /// <param name="action">The action to perform on each scene node.</param>
    public abstract void Traverse(Action<ISceneNode> action);
}
