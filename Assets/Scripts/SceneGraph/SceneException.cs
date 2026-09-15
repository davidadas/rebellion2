namespace Rebellion.SceneGraph
{
    /// <summary>
    /// Base exception for scene graph operations.
    /// </summary>
    public abstract class SceneException : System.Exception
    {
        /// <summary>
        /// Initializes a new instance of the SceneException class.
        /// </summary>
        protected SceneException() { }

        /// <summary>
        /// Initializes a new instance of the SceneException class.
        /// </summary>
        /// <param name="message">The message.</param>
        protected SceneException(string message)
            : base(message) { }

        /// <summary>
        /// Initializes a new instance of the SceneException class.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="inner">The inner.</param>
        protected SceneException(string message, System.Exception inner)
            : base(message, inner) { }
    }
}
