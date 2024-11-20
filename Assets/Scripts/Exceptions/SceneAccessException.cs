/// <summary>
///
/// </summary>
public class SceneAccessException : SceneException
{
    /// <summary>
    ///
    /// </summary>
    /// <param name="accessor"></param>
    /// <param name="accessee"></param>
    public SceneAccessException(ISceneNode accessor, ISceneNode accessee)
        : base(
            $"Cannot add \"{accessor.DisplayName}\" to \"{accessee.DisplayName}\". Accessor does not have access."
        )
    {
        Accessor = accessor;
        Accessee = accessee;
    }

    public ISceneNode Accessor { get; }

    public ISceneNode Accessee { get; }
}
