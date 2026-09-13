using System.Collections.Generic;
using Rebellion.Game.Units;
using Rebellion.Util.Serialization;

namespace Rebellion.Game
{
    /// <summary>
    /// Identifies the planet feature represented by a saved bookmark.
    /// </summary>
    public enum PlanetBookmarkType
    {
        Facility,
        Defense,
        Fleet,
        Mission,
    }

    /// <summary>
    /// Stores one planet bookmark in a stable authored slot.
    /// </summary>
    [PersistableObject]
    public sealed class PlanetBookmark
    {
        public int SlotIndex { get; set; }
        public string PlanetInstanceID { get; set; }
        public PlanetBookmarkType Type { get; set; }
    }

    /// <summary>
    /// Identifies one idle-bar item hidden by a player.
    /// </summary>
    [PersistableObject]
    public sealed class IdleBarUntrackedItem
    {
        public string EntityInstanceID { get; set; }
        public ManufacturingType ManufacturingType { get; set; }
    }

    /// <summary>
    /// Stores the common state required to restore one strategy window.
    /// </summary>
    [PersistableObject]
    public sealed class StrategyWindowState
    {
        public string WindowTypeID { get; set; }
        public string TargetInstanceID { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int ZOrder { get; set; }
    }

    /// <summary>
    /// Stores durable, save-specific interface choices for one player.
    /// </summary>
    [PersistableObject]
    public sealed class PlayerUIState
    {
        public List<PlanetBookmark> Bookmarks { get; set; } = new List<PlanetBookmark>();
        public List<IdleBarUntrackedItem> UntrackedIdleBarItems { get; set; } =
            new List<IdleBarUntrackedItem>();
        public List<StrategyWindowState> StrategyWindows { get; set; } =
            new List<StrategyWindowState>();
    }
}
