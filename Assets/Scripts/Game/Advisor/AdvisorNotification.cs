using Rebellion.Util.Serialization;

namespace Rebellion.Game.Advisor
{
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
