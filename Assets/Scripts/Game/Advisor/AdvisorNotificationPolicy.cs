using Rebellion.Game.Messages;

namespace Rebellion.Game.Advisor
{
    /// <summary>
    /// Selects the default semantic advisor notification for automatic message results.
    /// </summary>
    public static class AdvisorNotificationPolicy
    {
        /// <summary>
        /// Returns the default notification type for a message-result category.
        /// </summary>
        public static AdvisorNotificationType GetDefault(MessageResultType? resultType) =>
            resultType switch
            {
                MessageResultType.FleetArrived => AdvisorNotificationType.FleetArrived,
                MessageResultType.ShipsArrived => AdvisorNotificationType.UnitsArrived,
                MessageResultType.ManufacturingIdle => AdvisorNotificationType.Manufacturing,
                MessageResultType.CapitalShipRepaired =>
                    AdvisorNotificationType.CapitalShipRepaired,
                MessageResultType.StarfighterRepaired =>
                    AdvisorNotificationType.StarfighterRepaired,
                MessageResultType.SabotageStrike => AdvisorNotificationType.Maintenance,
                MessageResultType.FacilityLost => AdvisorNotificationType.Maintenance,
                MessageResultType.ResearchComplete => AdvisorNotificationType.Research,
                MessageResultType.ResearchExhausted => AdvisorNotificationType.Research,
                MessageResultType.BlockadeInitiated => AdvisorNotificationType.BlockadeInitiated,
                MessageResultType.BlockadeDetected => AdvisorNotificationType.BlockadeDetected,
                MessageResultType.MaintenanceAutoscrap => AdvisorNotificationType.Maintenance,
                MessageResultType.Bombardment => AdvisorNotificationType.Bombardment,
                MessageResultType.PlanetaryAssault => AdvisorNotificationType.PlanetaryAssault,
                _ => AdvisorNotificationType.None,
            };
    }
}
