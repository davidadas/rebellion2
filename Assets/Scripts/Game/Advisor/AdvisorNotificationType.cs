namespace Rebellion.Game.Advisor
{
    /// <summary>
    /// Identifies the semantic advisor notification selected by gameplay.
    /// </summary>
    public enum AdvisorNotificationType
    {
        None = 0,
        PositivePopularSupport = 1,
        NegativePopularSupport = 2,
        Manufacturing = 3,
        Research = 4,
        FleetArrived = 5,
        UnitsArrived = 6,
        CapitalShipRepaired = 8,
        StarfighterRepaired = 9,
        Maintenance = 12,
        BlockadeInitiated = 13,
        BlockadeDetected = 14,
        FieldPersonnel = 20,
        AgentReport = 21,
        PlanetaryStatus = 28,
        PrisonerEscaped = 36,
        InterceptedCommunication = 41,
        Bombardment = 46,
        PlanetaryAssault = 47,
    }
}
