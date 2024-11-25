/// <summary>
///
/// </summary>
public class SceneAccessException : SceneException
{
    public ISceneNode Accessor { get; }
    public ISceneNode Accessee { get; }

    /// <summary>
    ///
    /// </summary>
    /// <param name="accessor"></param>
    /// <param name="accessee"></param>
    public SceneAccessException(ISceneNode accessor, ISceneNode accessee)
        : base(
            $"Cannot add \"{accessor.GetDisplayName()}\" to \"{accessee.GetDisplayName()}\". Owners do not match."
        )
    {
        Accessor = accessor;
        Accessee = accessee;
    }
}
