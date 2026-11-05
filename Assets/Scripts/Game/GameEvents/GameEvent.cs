using System.Collections.Generic;
using Rebellion.Game.Scripting;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.GameEvents
{
    /// <summary>
    /// Classifies an authored event without changing its execution behavior.
    /// </summary>
    public enum GameEventCategory
    {
        None,
        Story,
        Disaster,
    }

    /// <summary>
    /// Persists aggregate event history or one target's independent schedule history.
    /// </summary>
    [PersistableObject]
    public sealed class GameEventState
    {
        public bool IsInitialized { get; set; }
        public int NextEligibleTick { get; set; }
        public int ActivationCount { get; set; }
        public int LastActivationTick { get; set; } = -1;
        public bool IsComplete { get; set; }
        public Dictionary<string, GameEventState> ScopedStates { get; set; } =
            new Dictionary<string, GameEventState>();
    }

    /// <summary>
    /// Gives each deterministically selected scene node an independent recurring schedule.
    /// </summary>
    /// <remarks>
    /// The target alias is bound before ordinary event bindings. Target schedules share the
    /// event's activation limit and aggregate history; target completion remains permanent.
    /// </remarks>
    [PersistableObject(Name = "ForEach")]
    public sealed class GameEventScope
    {
        [PersistableAttribute]
        public string As { get; set; }

        [PersistableMember(Name = "From")]
        public List<GameSelector> Selectors { get; set; } = new List<GameSelector>();
    }

    /// <summary>
    /// Defines one scheduled or triggered event and the actions performed when it activates.
    /// </summary>
    [PersistableObject]
    public sealed class GameEvent
    {
        public string InstanceID { get; set; }

        /// <summary>
        /// Gets or sets the optional classification; omitted categories default to None.
        /// </summary>
        [PersistableAttribute]
        public GameEventCategory Category { get; set; }

        [PersistableAttribute]
        public int? MaximumActivations { get; set; }

        public List<GameBinding> Bindings { get; set; } = new List<GameBinding>();
        public List<GameTrigger> Triggers { get; set; } = new List<GameTrigger>();
        public GameEventScope ForEach { get; set; }
        public GameEventSchedule Schedule { get; set; }
        public List<GameConditional> Conditionals { get; set; } = new List<GameConditional>();
        public List<GameAction> Actions { get; set; } = new List<GameAction>();

        /// <summary>
        /// Creates an empty event definition for deserialization.
        /// </summary>
        public GameEvent() { }

        /// <summary>
        /// Creates an event from an in-memory condition and action pipeline.
        /// </summary>
        /// <param name="conditionals">The conditions that must all pass.</param>
        /// <param name="actions">The actions executed in authored order.</param>
        public GameEvent(List<GameConditional> conditionals, List<GameAction> actions)
        {
            Conditionals = conditionals;
            Actions = actions;
        }
    }
}
