using System.Collections.Generic;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.UIState
{
    /// <summary>
    /// Stores durable interface state belonging to one game mode or interface area.
    /// </summary>
    [PersistableObject]
    public sealed class UIStateSection
    {
        public string SectionID { get; set; }
        public Dictionary<string, string> Values { get; set; } = new Dictionary<string, string>();
        public List<BookmarkedItem> BookmarkedItems { get; set; } = new List<BookmarkedItem>();
        public List<IgnoredItem> IgnoredItems { get; set; } = new List<IgnoredItem>();
        public List<WindowState> Windows { get; set; } = new List<WindowState>();
    }
}
