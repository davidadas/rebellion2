public enum SceneExceptionType
{
    Access
}

public class SceneException : GameException
{
    /// <summary>
    ///
    /// </summary>
    /// <param name="accessor"></param>
    /// <param name="accessee"></param>
    /// <param name="type"></param>
    public SceneException(GameNode accessor, GameNode accessee, SceneExceptionType type)
        : base(generateErrorText(accessor, accessee, type)) { }

    /// <summary>
    ///
    /// </summary>
    /// <param name="accessor"></param>
    /// <param name="accessee"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    private static string generateErrorText(
        GameNode accessor,
        GameNode accessee,
        SceneExceptionType type
    )
    {
        switch (type)
        {
            case SceneExceptionType.Access:
            {
                return $"Cannot add \"{accessor.DisplayName}\" to \"{accessee.DisplayName}\". Accessor does not have access.";
            }
            default:
            {
                return $"{accessor.DisplayName} performed invalid operation on {accessee.DisplayName}.";
            }
        }
    }
}
