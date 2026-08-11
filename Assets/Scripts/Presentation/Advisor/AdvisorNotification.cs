using Rebellion.Util.Serialization;

namespace Rebellion.Presentation.Advisor
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
        None = AdvisorNotificationType.None,
        PositivePopularSupport = AdvisorNotificationType.PositivePopularSupport,
        NegativePopularSupport = AdvisorNotificationType.NegativePopularSupport,
        Manufacturing = AdvisorNotificationType.Manufacturing,
        Research = AdvisorNotificationType.Research,
        FleetArrived = AdvisorNotificationType.FleetArrived,
        UnitsArrived = AdvisorNotificationType.UnitsArrived,
        CapitalShipRepaired = AdvisorNotificationType.CapitalShipRepaired,
        StarfighterRepaired = AdvisorNotificationType.StarfighterRepaired,
        Maintenance = AdvisorNotificationType.Maintenance,
        BlockadeInitiated = AdvisorNotificationType.BlockadeInitiated,
        BlockadeDetected = AdvisorNotificationType.BlockadeDetected,
        FieldPersonnel = AdvisorNotificationType.FieldPersonnel,
        AgentReport = AdvisorNotificationType.AgentReport,
        PlanetaryStatus = AdvisorNotificationType.PlanetaryStatus,
        PrisonerEscaped = AdvisorNotificationType.PrisonerEscaped,
        InterceptedCommunication = AdvisorNotificationType.InterceptedCommunication,
        Bombardment = AdvisorNotificationType.Bombardment,
        PlanetaryAssault = AdvisorNotificationType.PlanetaryAssault,
        SubjectReport,
        SubjectCaptured,
        SubjectReleased,
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
