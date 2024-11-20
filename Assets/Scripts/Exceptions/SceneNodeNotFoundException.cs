using System;

/// <summary>
/// Exception thrown when a scene node is not found.
/// </summary>
public class SceneNodeNotFoundException : SceneException
{
    public string NodeInstanceID { get; set; }

    /// <summary>
    /// Initializes a new instance of the ISceneNodeNotFoundException class with a specified error message.
    /// </summary>
    /// <param name="nodeInstanceId">The InstanceID of the node that was not found.</param>
    public SceneNodeNotFoundException(string nodeInstanceId)
        : base($"ISceneNode not found with InstanceID {nodeInstanceId}")
    {
        NodeInstanceID = nodeInstanceId;
    }
}
