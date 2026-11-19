namespace Rebellion.Tactical.Synchronization
{
    /// <summary>
    /// Identifies a one-shot presentation edge emitted by the authoritative simulation.
    /// </summary>
    public enum TacticalCombatEventKind
    {
        Shot,
        Impact,
        Destruction,
        Destroyed,
        Withdrawn,
        Recovered,
        Superlaser,
        TractorSources,
        GravityWell,
        Movie,
        Notification,
    }
}
