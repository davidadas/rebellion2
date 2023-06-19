public class MoveUnitEvent : GameEvent
{
    /// <summary>
    ///
    /// </summary>
    public MoveUnitEvent()
        : base() { }

    /// <summary>
    ///
    /// </summary>
    /// <param name="source"></param>
    /// <param name="target"></param>
    public MoveUnitEvent(GameNode source, GameNode target)
        : base(source, target) { }

    /// <summary>
    ///
    /// </summary>
    /// <param name="source"></param>
    /// <param name="args"></param>
    protected override void OnTrigger(GameNode source, GameEventArgs args)
    {
        GameNode target = args.Target;
        target.Attach(source);
    }
}
