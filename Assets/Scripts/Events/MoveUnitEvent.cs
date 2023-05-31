public class MoveUnitEvent : GameEvent
{
    /// <summary>
    /// 
    /// </summary>
    public MoveUnitEvent() : base() { }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="source"></param>
    /// <param name="target"></param>
    public MoveUnitEvent(GameNode source, GameNode target) : base(source, target) { }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    protected override void OnTrigger(GameNode sender, GameEventArgs args)
    {
        // Do stuff.
    }
}
