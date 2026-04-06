namespace Rebellion.SceneGraph
{
    /// <summary>
    /// Base exception for scene graph operations.
    /// </summary>
    public abstract class SceneException : System.Exception
    {
        protected SceneException() { }

        protected SceneException(string message)
            : base(message) { }

        protected SceneException(string message, System.Exception inner)
            : base(message, inner) { }
    }
}
