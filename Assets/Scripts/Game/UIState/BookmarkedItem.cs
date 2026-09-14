using Rebellion.Util.Serialization;

namespace Rebellion.Game.UIState
{
    /// <summary>
    /// Stores one bookmarked interface target in a stable authored slot.
    /// </summary>
    [PersistableObject]
    public sealed class BookmarkedItem
    {
        public int SlotIndex { get; set; }
        public string TargetInstanceID { get; set; }
        public string ItemTypeID { get; set; }
    }
}
