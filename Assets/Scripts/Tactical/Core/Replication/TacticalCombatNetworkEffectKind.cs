namespace Rebellion.Tactical
{
    /// <summary>
    /// Identifies a one-shot presentation edge emitted by the authoritative simulation.
    /// </summary>
    public enum TacticalCombatNetworkEffectKind
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
