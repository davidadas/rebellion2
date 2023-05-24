/// <summary>
///
/// </summary>
public abstract class GameEvent : GameLeaf
{
    public string SourceInstanceID { get; set; }
    public string TargetInstanceID { get; set; }

    /// <summary>
    /// Default constructor.
    /// </summary>
    public GameEvent() { }

    /// <summary>
    ///
    /// </summary>
    /// <param name="rootNode"></param>
    public void Trigger(GameRoot rootNode)
    {
        GameNode sender = rootNode.FindNodeByInstanceID<GameNode>(SourceInstanceID);
        GameNode target = rootNode.FindNodeByInstanceID<GameNode>(TargetInstanceID);

        GameEventArgs eventArgs = new GameEventArgs(target);

        // Invoke the child event.
        OnTrigger(sender, eventArgs);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    protected abstract void OnTrigger(GameNode sender, GameEventArgs args);
}
