using Rebellion.Util.Serialization;

namespace Rebellion.Game.Advisor
{
    /// <summary>
    /// Defines one advisor animation and its optional audio and timing overrides.
    /// </summary>
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

    /// <summary>
    /// Supplies an advisor preset or authored droid and protocol presentation overrides.
    /// </summary>
    [PersistableObject]
    public sealed class AdvisorNotification
    {
        [PersistableAttribute]
        public AdvisorNotificationPreset? Preset { get; set; }

        [PersistableAttribute]
        public int? LifetimeTicks { get; set; }

        public AdvisorAnimation Droid { get; set; }
        public AdvisorAnimation Protocol { get; set; }

        public bool HasOverrides => LifetimeTicks.HasValue || Droid != null || Protocol != null;
    }
}
