using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Persists the scheduling history for one authored event definition.
    /// </summary>
    [PersistableObject]
    public sealed class GameEventState
    {
        public bool IsInitialized { get; set; }
        public int NextEligibleTick { get; set; }
        public int ExecutionCount { get; set; }
        public int LastExecutionTick { get; set; } = -1;
        public bool IsExhausted { get; set; }
    }

    /// <summary>
    /// Selects one scene node and exposes it under an authored activation-scoped binding name.
    /// </summary>
    [PersistableObject(Name = "Bind")]
    public sealed class GameEventSelectionBinding
    {
        [PersistableAttribute]
        public string As { get; set; }

        [PersistableMember(Name = "From")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();

        /// <summary>Selects exactly one scene node and stores it in the activation context.</summary>
        internal void Bind(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        )
        {
            if (Selectors.Count != 1)
                throw new InvalidOperationException(
                    $"Selection binding '{As}' requires exactly one selector."
                );

            ISceneNode[] values = Selectors[0].Select(game, provider, context).Distinct().ToArray();
            if (values.Length != 1)
                throw new InvalidOperationException(
                    $"Selection binding '{As}' must resolve exactly one object but resolved {values.Length}."
                );
            context.Bind(As, values[0]);
        }
    }

    /// <summary>
    /// Represents a triggered game event: a set of conditions that, when met, execute a set of actions.
    /// Execute returns the results of those actions for notification and logging.
    /// </summary>
    [PersistableObject]
    public sealed class GameEvent
    {
        public string InstanceID { get; set; }

        [PersistableAttribute]
        public int? MaximumActivations { get; set; }

        public List<GameEventTrigger> Triggers { get; set; } = new List<GameEventTrigger>();
        public GameEventScheduler Schedule { get; set; }
        public List<GameEventSelectionBinding> Bindings { get; set; } =
            new List<GameEventSelectionBinding>();
        public List<GameConditional> Conditionals { get; set; } = new List<GameConditional>();
        public List<GameConditional> StopWhen { get; set; } = new List<GameConditional>();
        public List<GameAction> Actions { get; set; } = new List<GameAction>();

        /// <summary>Returns whether the event remains below its authored activation limit.</summary>
        internal bool CanActivate(GameEventState state) =>
            !state.IsExhausted
            && (!MaximumActivations.HasValue || state.ExecutionCount < MaximumActivations.Value);

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

        /// <summary>
        /// Returns true if all conditions accept the supplied execution context.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="context">The scoped target, trigger, state, and runtime bindings.</param>
        /// <returns>True if every conditional is satisfied.</returns>
        internal bool AreConditionsMet(GameRoot game, GameEventExecutionContext context)
        {
            foreach (GameConditional conditional in Conditionals)
            {
                if (!conditional.IsMet(game, context))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Executes the event's actions and returns their shared context.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">Random number provider for stochastic actions.</param>
        /// <param name="context">The scoped target, trigger, state, and runtime bindings.</param>
        /// <param name="unitFactory">Factory for actions that create runtime units.</param>
        /// <returns>The context containing requests and results produced by the actions.</returns>
        internal GameActionContext Execute(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context,
            UnitFactory unitFactory = null
        )
        {
            GameActionContext actionContext = new GameActionContext(
                game,
                provider,
                context,
                unitFactory
            );
            GameAction.ExecuteAll(Actions, actionContext);
            return actionContext;
        }
    }
}
