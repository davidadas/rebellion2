using System.Collections.Generic;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.SpaceCombat
{
    /// <summary>
    /// Addresses the ordered playback variants for one tactical notification and optional faction.
    /// An empty variant path records a resource that is unavailable in that faction's sound bank.
    /// </summary>
    [PersistableObject]
    public sealed class TacticalCombatNotificationDefinition
    {
        public int MessageID { get; set; }
        public string FactionInstanceID { get; set; }
        public List<TacticalCombatSoundClip> Variants { get; set; } = new();
    }
}
