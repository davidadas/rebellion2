namespace Rebellion.Game.Advisor
{
    /// <summary>
    /// Identifies the semantic advisor notification selected by gameplay. Numeric values are the
    /// authored notification codes consumed by faction advisor themes and must remain stable.
    /// </summary>
    public enum AdvisorNotificationType
    {
        /// <summary>No advisor interruption is requested.</summary>
        None = 0,

        /// <summary>Popular support changed in the recipient's favor.</summary>
        PositivePopularSupport = 1,

        /// <summary>Popular support changed against the recipient.</summary>
        NegativePopularSupport = 2,

        /// <summary>A manufacturing update requires attention.</summary>
        Manufacturing = 3,

        /// <summary>A research update requires attention.</summary>
        Research = 4,

        /// <summary>A fleet completed transit.</summary>
        FleetArrived = 5,

        /// <summary>One or more non-fleet units completed transit.</summary>
        UnitsArrived = 6,

        /// <summary>A capital ship completed repairs.</summary>
        CapitalShipRepaired = 8,

        /// <summary>A starfighter unit completed repairs.</summary>
        StarfighterRepaired = 9,

        /// <summary>Maintenance requirements changed.</summary>
        Maintenance = 12,

        /// <summary>The recipient established a blockade.</summary>
        BlockadeInitiated = 13,

        /// <summary>The recipient detected an opposing blockade.</summary>
        BlockadeDetected = 14,

        /// <summary>Personnel in the field submitted a report.</summary>
        FieldPersonnel = 20,

        /// <summary>An agent submitted a mission report.</summary>
        AgentReport = 21,

        /// <summary>A planet's strategic state changed.</summary>
        PlanetaryStatus = 28,

        /// <summary>A prisoner escaped custody.</summary>
        PrisonerEscaped = 36,

        /// <summary>The recipient intercepted a communication.</summary>
        InterceptedCommunication = 41,

        /// <summary>An opposing bombardment occurred.</summary>
        Bombardment = 46,

        /// <summary>An opposing planetary assault occurred.</summary>
        PlanetaryAssault = 47,
    }
}
