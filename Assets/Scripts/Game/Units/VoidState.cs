using Rebellion.Util.Serialization;

namespace Rebellion.Game.Units
{
    public enum VoidStatus
    {
        OnMission,
        Captured,
        Destroyed,
        Retired,
        Unavailable,
        Training,
    }

    [PersistableObject]
    public sealed class VoidState
    {
        public VoidStatus? Status { get; set; }
        public string DisplayText { get; set; }
    }
}
