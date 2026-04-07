namespace Rebellion.SceneGraph
{
    /// <summary>
    /// Exception thrown when a scene node cannot be found by instance ID.
    /// </summary>
    public class SceneNodeNotFoundException : SceneException
    {
        public string NodeInstanceID { get; set; }

        public SceneNodeNotFoundException()
            : base() { }

        public SceneNodeNotFoundException(string nodeInstanceId)
            : base($"ISceneNode not found with InstanceID {nodeInstanceId}")
        {
            NodeInstanceID = nodeInstanceId;
        }

        public SceneNodeNotFoundException(string message, System.Exception inner)
            : base(message, inner) { }
    }
}
