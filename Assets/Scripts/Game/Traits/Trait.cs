using System.Collections.Generic;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Traits
{
    /// <summary>
    /// Defines a reusable, content-authored character trait.
    /// </summary>
    [PersistableObject]
    public sealed class Trait
    {
        public string ID { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public List<Modifier> Effects { get; set; } = new List<Modifier>();
    }
}
