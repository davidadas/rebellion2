namespace Rebellion.SceneGraph
{
    /// <summary>
    /// Exception thrown when a scene node access violation occurs between nodes whose owners do not match.
    /// </summary>
    public class SceneAccessException : SceneException
    {
        public ISceneNode Accessor { get; }
        public ISceneNode Accessee { get; }

        /// <summary>
        /// Initializes a new instance of the SceneAccessException class.
        /// </summary>
        /// <param name="accessor">The accessor.</param>
        /// <param name="accessee">The accessee.</param>
        public SceneAccessException(ISceneNode accessor, ISceneNode accessee)
            : base(
                $"Cannot add \"{accessor.GetDisplayName()}\" to \"{accessee.GetDisplayName()}\". Owners do not match."
            )
        {
            Accessor = accessor;
            Accessee = accessee;
        }

        /// <summary>
        /// Initializes a new instance of the SceneAccessException class.
        /// </summary>
        public SceneAccessException()
            : base() { }

        /// <summary>
        /// Initializes a new instance of the SceneAccessException class.
        /// </summary>
        /// <param name="message">The message.</param>
        public SceneAccessException(string message)
            : base(message) { }

        /// <summary>
        /// Initializes a new instance of the SceneAccessException class.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="inner">The inner.</param>
        public SceneAccessException(string message, System.Exception inner)
            : base(message, inner) { }
    }
}
