using System.Collections.Generic;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Traits
{
    /// <summary>
    /// Defines a reusable, content-authored effect that can be temporarily applied to an entity.
    /// </summary>
    [PersistableObject]
    public sealed class StatusEffect
    {
        public string ID { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public List<Modifier> Modifiers { get; set; } = new List<Modifier>();
    }
}
