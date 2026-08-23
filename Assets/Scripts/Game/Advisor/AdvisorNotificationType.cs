namespace Rebellion.Game.Advisor
{
    /// <summary>
    /// Identifies the semantic advisor notification selected by gameplay.
    /// </summary>
    public enum AdvisorNotificationType
    {
        /// <summary>No advisor interruption is requested.</summary>
        None,

        /// <summary>Popular support changed in the recipient's favor.</summary>
        PositivePopularSupport,

        /// <summary>Popular support changed against the recipient.</summary>
        NegativePopularSupport,

        /// <summary>A manufacturing update requires attention.</summary>
        Manufacturing,

        /// <summary>A research update requires attention.</summary>
        Research,

        /// <summary>A fleet completed transit.</summary>
        FleetArrived,

        /// <summary>One or more non-fleet units completed transit.</summary>
        UnitsArrived,

        /// <summary>A capital ship completed repairs.</summary>
        CapitalShipRepaired,

        /// <summary>A starfighter unit completed repairs.</summary>
        StarfighterRepaired,

        /// <summary>Maintenance requirements changed.</summary>
        Maintenance,

        /// <summary>The recipient established a blockade.</summary>
        BlockadeInitiated,

        /// <summary>The recipient detected an opposing blockade.</summary>
        BlockadeDetected,

        /// <summary>Personnel in the field submitted a report.</summary>
        FieldPersonnel,

        /// <summary>An agent submitted a mission report.</summary>
        AgentReport,

        /// <summary>A planet's strategic state changed.</summary>
        PlanetaryStatus,

        /// <summary>A prisoner escaped custody.</summary>
        PrisonerEscaped,

        /// <summary>The recipient intercepted a communication.</summary>
        InterceptedCommunication,

        /// <summary>An opposing fleet is approaching the recipient's headquarters.</summary>
        EnemyFleetApproachingHeadquarters,

        /// <summary>Han Solo supplied an intelligence update.</summary>
        HanSoloIntelligenceUpdate,

        /// <summary>An opposing bombardment occurred.</summary>
        Bombardment,

        /// <summary>An opposing planetary assault occurred.</summary>
        PlanetaryAssault,
    }
}
