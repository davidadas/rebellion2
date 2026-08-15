using Rebellion.Game.Messages;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Notifications
{
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

    public enum AdvisorSubjectNotification
    {
        None,
        Report,
        Captured,
        Released,
    }

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

    public static class AdvisorNotificationPresetExtensions
    {
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

    public static class AdvisorNotificationPolicy
    {
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

    [PersistableObject]
    public sealed class AdvisorAnimation
    {
        [PersistableAttribute]
        public string Animation { get; set; }

        [PersistableAttribute]
        public string AnimationPath { get; set; }

        [PersistableAttribute]
        public int? FrameCount { get; set; }

        [PersistableAttribute]
        public string Audio { get; set; }

        [PersistableAttribute]
        public string AudioPath { get; set; }

        [PersistableAttribute]
        public float? DelayBeforeSeconds { get; set; }

        [PersistableAttribute]
        public bool? RequiresAnnouncementsEnabled { get; set; }
    }

    [PersistableObject]
    public sealed class AdvisorNotification
    {
        [PersistableAttribute]
        public AdvisorNotificationPreset? Preset { get; set; }

        [PersistableAttribute]
        public int? LifetimeTicks { get; set; }

        public AdvisorAnimation Droid { get; set; }
        public AdvisorAnimation Protocol { get; set; }

        public bool HasOverrides => Droid != null || Protocol != null;
    }
}
