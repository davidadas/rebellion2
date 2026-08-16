namespace Rebellion.Game.Advisor
{
    /// <summary>
    /// Names reusable advisor notification behavior available to authored messages.
    /// </summary>
    public enum AdvisorNotificationPreset
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
        Bombardment,
        PlanetaryAssault,
        SubjectReport,
        SubjectCaptured,
        SubjectReleased,
    }

    /// <summary>
    /// Resolves authored presets to semantic notification types.
    /// </summary>
    public static class AdvisorNotificationPresetExtensions
    {
        /// <summary>
        /// Returns the semantic notification represented by a non-subject preset.
        /// </summary>
        public static AdvisorNotificationType ToNotificationType(
            this AdvisorNotificationPreset preset
        ) =>
            preset switch
            {
                AdvisorNotificationPreset.PositivePopularSupport =>
                    AdvisorNotificationType.PositivePopularSupport,
                AdvisorNotificationPreset.NegativePopularSupport =>
                    AdvisorNotificationType.NegativePopularSupport,
                AdvisorNotificationPreset.Manufacturing => AdvisorNotificationType.Manufacturing,
                AdvisorNotificationPreset.Research => AdvisorNotificationType.Research,
                AdvisorNotificationPreset.FleetArrived => AdvisorNotificationType.FleetArrived,
                AdvisorNotificationPreset.UnitsArrived => AdvisorNotificationType.UnitsArrived,
                AdvisorNotificationPreset.CapitalShipRepaired =>
                    AdvisorNotificationType.CapitalShipRepaired,
                AdvisorNotificationPreset.StarfighterRepaired =>
                    AdvisorNotificationType.StarfighterRepaired,
                AdvisorNotificationPreset.Maintenance => AdvisorNotificationType.Maintenance,
                AdvisorNotificationPreset.BlockadeInitiated =>
                    AdvisorNotificationType.BlockadeInitiated,
                AdvisorNotificationPreset.BlockadeDetected =>
                    AdvisorNotificationType.BlockadeDetected,
                AdvisorNotificationPreset.FieldPersonnel => AdvisorNotificationType.FieldPersonnel,
                AdvisorNotificationPreset.AgentReport => AdvisorNotificationType.AgentReport,
                AdvisorNotificationPreset.PlanetaryStatus =>
                    AdvisorNotificationType.PlanetaryStatus,
                AdvisorNotificationPreset.PrisonerEscaped =>
                    AdvisorNotificationType.PrisonerEscaped,
                AdvisorNotificationPreset.InterceptedCommunication =>
                    AdvisorNotificationType.InterceptedCommunication,
                AdvisorNotificationPreset.Bombardment => AdvisorNotificationType.Bombardment,
                AdvisorNotificationPreset.PlanetaryAssault =>
                    AdvisorNotificationType.PlanetaryAssault,
                _ => AdvisorNotificationType.None,
            };
    }
}
