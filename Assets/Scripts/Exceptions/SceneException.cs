public enum SceneExceptionType
{
    Access,
    Deletion,
    Insertion,
}

public class SceneException : GameException
{
    /// <summary>
    ///
    /// </summary>
    /// <param name="accessor"></param>
    /// <param name="accessee"></param>
    /// <param name="type"></param>
    /// <param name="reason"></param>
    public SceneException(
        GameNode accessor,
        GameNode accessee,
        SceneExceptionType type,
        string reason = ""
    )
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
        SceneExceptionType type,
        string reason = ""
    )
    {
        switch (type)
        {
            case SceneExceptionType.Access:
            {
                return $"Cannot add \"{accessor.DisplayName}\" to \"{accessee.DisplayName}\". Accessor does not have access.";
            }
            case SceneExceptionType.Deletion:
            {
                return $"Cannot delete \"{accessor.DisplayName} from \"{accessee.DisplayName}\".{" " + reason}";
            }
            case SceneExceptionType.Insertion:
            {
                return $"Cannot insert \"{accessor.DisplayName} as child of \"{accessee.DisplayName}\".{" " + reason}";
            }
            default:
            {
                return $"{accessor.DisplayName} performed invalid operation on {accessee.DisplayName}.";
            }
        }
    }
}
