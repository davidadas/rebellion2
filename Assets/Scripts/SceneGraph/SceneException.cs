namespace Rebellion.SceneGraph
{
    /// <summary>
    /// Base exception for scene graph operations.
    /// </summary>
    public abstract class SceneException : System.Exception
    {
        public SceneException(string text)
            : base(text) { }
    }
}
