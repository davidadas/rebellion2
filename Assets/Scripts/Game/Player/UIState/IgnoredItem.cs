using Rebellion.Util.Serialization;

namespace Rebellion.Game.UIState
{
    /// <summary>
    /// Stores one interface target ignored by a player.
    /// </summary>
    [PersistableObject]
    public sealed class IgnoredItem
    {
        public string TargetInstanceID { get; set; }
        public string ItemTypeID { get; set; }
    }
}
