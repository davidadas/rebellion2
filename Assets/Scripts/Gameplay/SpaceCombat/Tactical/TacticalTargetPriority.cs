namespace Rebellion.Gameplay
{
    /// <summary>
    /// Which class of enemy a unit engages first when it picks its own targets. The other class
    /// is engaged only when no preferred target can be reached.
    /// </summary>
    public enum TacticalTargetPriority
    {
        CapitalShips,
        Starfighters,
    }
}
