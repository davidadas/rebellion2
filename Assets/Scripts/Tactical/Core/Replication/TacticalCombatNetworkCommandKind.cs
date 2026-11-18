namespace Rebellion.Tactical
{
    /// <summary>
    /// Identifies a faction-owned semantic input at the shared combat boundary.
    /// </summary>
    public enum TacticalCombatNetworkCommandKind
    {
        Object,
        Waypoint,
        Clear,
        DisplayMode,
        ObjectCategory,
        FighterCategory,
        NearestTarget,
        Order,
        AttackMode,
        Formation,
        ToggleAutomation,
        Withdrawal,
        StationTarget,
        Pause,
        ImmediateResult,
    }
}
