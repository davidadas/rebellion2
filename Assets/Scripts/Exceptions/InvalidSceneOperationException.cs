/// <summary>
/// Thrown when an invalid operation is attempted on a scene node.
/// </summary>
public class InvalidSceneOperationException : SceneException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidSceneOperationException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public InvalidSceneOperationException(string message) : base(message) { }
}
