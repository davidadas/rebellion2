namespace Rebellion.Game.Advisor
{
    /// <summary>
    /// Identifies the semantic advisor notification selected by gameplay.
    /// </summary>
    public enum AdvisorNotificationType
    {
        None,
        PositivePopularSupport,
        NegativePopularSupport,
        Manufacturing,
        Research,
        FleetArrived,
        UnitsArrived,
        CapitalShipRepaired,
        StarfighterRepaired,
        Maintenance,
        BlockadeInitiated,
        BlockadeDetected,
        FieldPersonnel,
        AgentReport,
        PlanetaryStatus,
        PrisonerEscaped,
        InterceptedCommunication,
        EnemyFleetApproachingHeadquarters,
        HanSoloIntelligenceUpdate,
        Bombardment,
        PlanetaryAssault,
    }
}
