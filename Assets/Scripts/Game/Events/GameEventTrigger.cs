using System;
using System.Collections.Generic;
using Rebellion.Game.Results;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Selects one stable simulation event and declares the result arguments exposed locally.
    /// </summary>
    [PersistableObject(Name = "Trigger")]
    public sealed class GameEventTrigger
    {
        [PersistableAttribute]
        public string Event { get; set; }

        public List<GameEventTriggerBinding> Bindings { get; set; } =
            new List<GameEventTriggerBinding>();

        public GameEventTrigger() { }

        public GameEventTrigger(string eventID, params (string Argument, string As)[] bindings)
        {
            Event = eventID;
            foreach ((string argument, string localName) in bindings)
                Bindings.Add(new GameEventTriggerBinding { Argument = argument, As = localName });
        }

        internal Type ResultType => GameEventTriggerRegistry.GetResultType(Event);

        /// <summary>
        /// Gets the statically typed arguments exposed by this trigger contract.
        /// </summary>
        [PersistableIgnore]
        public IReadOnlyDictionary<string, Type> AvailableArguments =>
            GameEventTriggerRegistry.GetArguments(Event);

        internal bool Matches(GameResult result) => GameEventTriggerRegistry.Matches(Event, result);

        internal Type GetArgumentType(string argument) =>
            GameEventTriggerRegistry.GetArgumentType(Event, argument);

        internal void Bind(GameEventExecutionContext context, GameResult result) =>
            GameEventTriggerRegistry.Bind(context, this, result);
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
