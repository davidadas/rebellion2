using System.Collections.Generic;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Factions
{
    /// <summary>
    /// Defines one faction-specific pool of ship names and its fallback pool.
    /// </summary>
    [PersistableObject(Name = "NamePool")]
    public class FactionNamePool
    {
        public string NamePoolID { get; set; }
        public string FallbackNamePoolID { get; set; }

        [PersistableCollectionItem(Name = "Name")]
        public List<string> Names { get; set; } = new List<string>();
        public int NextNameIndex { get; set; }
    }
}
