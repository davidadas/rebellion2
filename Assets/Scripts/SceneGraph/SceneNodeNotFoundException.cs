namespace Rebellion.SceneGraph
{
    /// <summary>
    /// Exception thrown when a scene node cannot be found by instance ID.
    /// </summary>
    public class SceneNodeNotFoundException : SceneException
    {
        public string NodeInstanceID { get; set; }

        /// <summary>
        /// Initializes a new instance of the SceneNodeNotFoundException class.
        /// </summary>
        public SceneNodeNotFoundException()
            : base() { }

        /// <summary>
        /// Initializes a new instance of the SceneNodeNotFoundException class.
        /// </summary>
        /// <param name="nodeInstanceId">The node instance id.</param>
        public SceneNodeNotFoundException(string nodeInstanceId)
            : base($"ISceneNode not found with InstanceID {nodeInstanceId}")
        {
            NodeInstanceID = nodeInstanceId;
        }

        /// <summary>
        /// Initializes a new instance of the SceneNodeNotFoundException class.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="inner">The inner.</param>
        public SceneNodeNotFoundException(string message, System.Exception inner)
            : base(message, inner) { }
    }
}
