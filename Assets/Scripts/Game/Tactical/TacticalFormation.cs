namespace Rebellion.Game.Tactical
{
    /// <summary>
    /// Defines how a capital-ship group distributes itself around its target.
    /// </summary>
    public enum TacticalFormation
    {
        /// <summary>The group maintains a common firing side at range.</summary>
        StandOff = 1,

        /// <summary>The group spreads around the target.</summary>
        Surround = 2,
    }
}
