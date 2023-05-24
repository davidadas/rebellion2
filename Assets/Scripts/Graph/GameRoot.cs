/// <summary>
///
/// </summary>
public abstract class GameRoot : GameNode
{
    /// <summary>
    ///
    /// </summary>
    public GameRoot()
        : base() { }

    /// <summary>
    ///
    /// </summary>
    /// <param name="node"></param>
    public abstract void AddReferenceNode(GameNode node);

    /// <summary>
    ///
    /// </summary>
    /// <param name="gameId"></param>
    /// <returns></returns>
    public abstract GameNode GetReferenceNode(string gameId);
}
