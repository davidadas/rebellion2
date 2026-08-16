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
}
