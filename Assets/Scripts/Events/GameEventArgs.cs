using System;

public class GameEventArgs : EventArgs
{
    protected GameNode Target;

    /// <summary>
    ///
    /// </summary>
    /// <param name="target"></param>
    public GameEventArgs(GameNode target)
        : base()
    {
        Target = target;
    }
}
