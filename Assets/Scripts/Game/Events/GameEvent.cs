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
    /// Persists the activation history for one authored event definition.
    /// </summary>
    [PersistableObject]
    public sealed class GameEventState
    {
        public bool IsInitialized { get; set; }
        public int NextEligibleTick { get; set; }
        public int ActivationCount { get; set; }
        public int LastActivationTick { get; set; } = -1;
        public bool IsComplete { get; set; }
    }

    /// <summary>
    /// Assigns an explicitly selected value to an event-local binding name.
    /// </summary>
    [PersistableObject(Name = "Bind")]
    public sealed class GameEventBinding
    {
        /// <summary>Gets or sets the stable trigger argument to expose.</summary>
        [PersistableAttribute]
        public string Argument { get; set; }

        /// <summary>Gets or sets the event-local name assigned to the value.</summary>
        [PersistableAttribute]
        public string As { get; set; }

        [PersistableMember(Name = "From")]
        public List<GameEventSelector> Selectors { get; set; } = new List<GameEventSelector>();

        /// <summary>Selects exactly one scene node and stores it in the evaluation context.</summary>
        internal void Bind(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventEvaluationContext context
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
    /// Defines one scheduled or triggered event and the actions performed when it activates.
    /// </summary>
    [PersistableObject]
    public sealed class GameEvent
    {
        public string InstanceID { get; set; }

        [PersistableAttribute]
        public int? MaximumActivations { get; set; }

        public List<GameEventBinding> Bindings { get; set; } = new List<GameEventBinding>();
        public List<GameEventTrigger> Triggers { get; set; } = new List<GameEventTrigger>();
        public GameEventScheduler Schedule { get; set; }
        public List<GameConditional> Conditionals { get; set; } = new List<GameConditional>();
        public List<GameAction> Actions { get; set; } = new List<GameAction>();

        /// <summary>Returns whether the event remains below its authored activation limit.</summary>
        internal bool CanActivate(GameEventState state) =>
            !state.IsComplete
            && (!MaximumActivations.HasValue || state.ActivationCount < MaximumActivations.Value);

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
        /// Returns true if all conditions accept the supplied evaluation context.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="context">The scoped target, trigger, state, and runtime bindings.</param>
        /// <returns>True if every conditional is satisfied.</returns>
        internal bool AreConditionsMet(GameRoot game, GameEventEvaluationContext context)
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
        internal GameActionContext ExecuteActions(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventEvaluationContext context,
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
