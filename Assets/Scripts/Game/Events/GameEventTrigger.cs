using System.Collections.Generic;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Binds selected arguments from one typed simulation result to an event activation.
    /// </summary>
    [PersistableObject(Name = "Trigger")]
    public sealed class GameEventTrigger
    {
        [PersistableAttribute]
        public string Event { get; set; }

        public List<GameEventTriggerBinding> Bindings { get; set; } =
            new List<GameEventTriggerBinding>();
    }

    /// <summary>
    /// Gives one public trigger argument a local name within an event activation.
    /// </summary>
    [PersistableObject(Name = "Bind")]
    public sealed class GameEventTriggerBinding
    {
        [PersistableAttribute]
        public string Argument { get; set; }

        [PersistableAttribute]
        public string As { get; set; }
    }
}
