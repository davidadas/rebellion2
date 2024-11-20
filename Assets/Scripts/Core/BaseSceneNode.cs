using System;
using System.Collections.Generic;

/// <summary>
/// Represents an abstract scene node in the game.
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
                // We've encountered this node before, which indicates a cycle.
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
    /// <exception cref="ArgumentException">Thrown when the owner type id is invalid.</exception>
    private void SetOwnerInstanceID(string value)
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
            throw new ArgumentException(
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

    public abstract void AddChild(ISceneNode child);

    public abstract void RemoveChild(ISceneNode child);

    public abstract IEnumerable<ISceneNode> GetChildren();

    public abstract void Traverse(Action<ISceneNode> action);
}
