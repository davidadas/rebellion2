using System;
using System.Collections.Generic;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;
using Rebellion.Util.Serialization;

namespace Rebellion.Game.Events
{
    /// <summary>
    /// Defines stable content-facing identifiers for simulation result types.
    /// </summary>
    public static class GameEventTriggerRegistry
    {
        private static readonly Dictionary<string, Type> TypesById = new Dictionary<string, Type>(
            StringComparer.Ordinal
        )
        {
            ["core:dagobah.completed"] = typeof(DagobahCompletedResult),
            ["core:force.discovered"] = typeof(ForceDiscoveryResult),
            ["core:mission.completed"] = typeof(MissionCompletedResult),
            ["core:officer.capture-changed"] = typeof(OfficerCaptureStateResult),
            ["core:officer.encountered"] = typeof(OfficerEncounterResult),
            ["core:story-capture.resolved"] = typeof(StoryCaptureResolvedResult),
            ["core:story-final-battle.completed"] = typeof(StoryFinalBattleCompletedResult),
            ["core:story-pickup.completed"] = typeof(StoryPickupCompletedResult),
            ["core:unit.arrived"] = typeof(UnitArrivedResult),
        };

        public static bool IsKnown(string triggerId) =>
            !string.IsNullOrWhiteSpace(triggerId) && TypesById.ContainsKey(triggerId);

        public static bool Matches(string triggerId, GameResult result) =>
            result != null
            && TypesById.TryGetValue(triggerId, out Type resultType)
            && resultType.IsInstanceOfType(result);

        public static bool MatchesLegacyTypeName(string typeName, GameResult result) =>
            result != null
            && string.Equals(typeName, result.GetType().Name, StringComparison.Ordinal);
    }

    internal static class GameEventHierarchy
    {
        public static bool Contains(ISceneNode container, ISceneNode node)
        {
            if (container == null || node == null)
                return false;

            for (ISceneNode current = node; current != null; current = current.GetParent())
            {
                if (current == container)
                    return true;
            }

            return false;
        }
    }

    public enum GameEventScope
    {
        Global,
        EachPlanet,
    }

    public enum PlanetScopeOwnership
    {
        Any,
        Owned,
        Neutral,
    }

    /// <summary>
    /// Describes one concrete execution of a data-defined event.
    /// Scoped events receive the entity whose independent schedule activated them.
    /// </summary>
    public sealed class GameEventExecutionContext
    {
        private readonly Dictionary<string, object> _bindings = new Dictionary<string, object>(
            StringComparer.Ordinal
        );

        public GameEvent Event { get; }
        public GameEventState State { get; }
        public ISceneNode ScopeTarget { get; }
        public GameResult TriggerResult { get; }

        public GameEventExecutionContext(
            GameEvent gameEvent,
            GameEventState state,
            ISceneNode scopeTarget,
            GameResult triggerResult = null
        )
        {
            Event = gameEvent;
            State = state;
            ScopeTarget = scopeTarget;
            TriggerResult = triggerResult;
            Bind("scope", scopeTarget);
            Bind("trigger", triggerResult);
            GameEventBindings.BindTriggerValues(this, triggerResult);
        }

        public T GetScopeTarget<T>()
            where T : class, ISceneNode => ScopeTarget as T;

        public void Bind(string name, object value)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A binding name is required.", nameof(name));
            if (value != null)
                _bindings[name] = value;
        }

        public bool TryGetBinding<T>(string name, out T value)
            where T : class
        {
            if (_bindings.TryGetValue(name, out object binding) && binding is T typed)
            {
                value = typed;
                return true;
            }

            value = null;
            return false;
        }

        public T GetBinding<T>(string name)
            where T : class => TryGetBinding(name, out T value) ? value : null;
    }

    internal static class GameEventBindings
    {
        public static void BindTriggerValues(
            GameEventExecutionContext context,
            GameResult triggerResult
        )
        {
            switch (triggerResult)
            {
                case UnitArrivedResult arrival:
                    context.Bind("unit", arrival.Unit);
                    context.Bind("destination", arrival.Destination);
                    context.Bind("planet", arrival.Destination);
                    break;
                case OfficerEncounterResult encounter:
                    context.Bind("officer", encounter.EncounteredOfficer);
                    context.Bind("opponent", encounter.OpposingOfficer);
                    break;
                case OfficerCaptureStateResult capture:
                    context.Bind("officer", capture.TargetOfficer ?? capture.CapturedOfficer);
                    context.Bind("linkedOfficer", capture.LinkedOfficer);
                    context.Bind("context", capture.Context);
                    break;
                case MissionCompletedResult completion:
                    context.Bind("mission", completion.Mission);
                    break;
            }
        }
    }

    /// <summary>
    /// Persists the runtime scheduling state for one data-defined game event.
    /// Event definitions remain content; this state is the save-game-owned execution history.
    /// </summary>
    [PersistableObject]
    public sealed class GameEventState
    {
        public bool IsInitialized { get; set; }
        public bool IsScopeActive { get; set; }
        public int NextEligibleTick { get; set; }
        public int ExecutionCount { get; set; }
        public int LastExecutionTick { get; set; } = -1;
    }

    /// <summary>
    /// Represents a triggered game event: a set of conditions that, when met, execute a set of actions.
    /// Execute returns the results of those actions for notification and logging.
    /// </summary>
    public class GameEvent : BaseGameEntity
    {
        public bool IsRepeatable { get; set; }
        public GameEventScope Scope { get; set; }
        public PlanetScopeOwnership PlanetScopeOwnership { get; set; }
        public PlanetSystemType PlanetScopeSystemType { get; set; }
        public bool FilterPlanetScopeSystemType { get; set; }
        public string TriggerResultType { get; set; }
        public string Trigger { get; set; }

        /// <summary>
        /// Gets or sets whether this reaction replaces its triggering result's automatic message.
        /// </summary>
        public bool SuppressTriggerMessage { get; set; }

        /// <summary>
        /// Gets or sets whether this reaction replaces automatic messages from its source event.
        /// </summary>
        public bool SuppressSourceMessages { get; set; }
        public int InitialDelayTicks { get; set; }
        public int InitialDelayRandomTicks { get; set; }
        public int RepeatDelayTicks { get; set; }
        public int RepeatDelayRandomTicks { get; set; }
        public List<GameConditional> Conditionals { get; set; } = new List<GameConditional>();
        public List<GameAction> Actions { get; set; } = new List<GameAction>();
        public List<GameEffect> Effects { get; set; } = new List<GameEffect>();

        public GameEvent() { }

        public GameEvent(List<GameConditional> conditionals, List<GameAction> actions)
        {
            Conditionals = conditionals;
            Actions = actions;
        }

        /// <summary>
        /// Returns true if all conditions are met.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <returns>True if every conditional is satisfied.</returns>
        public bool AreConditionsMet(GameRoot game)
        {
            return AreConditionsMet(game, (GameResult)null);
        }

        /// <summary>
        /// Returns true if all conditions accept the current game and triggering result.
        /// </summary>
        public bool AreConditionsMet(GameRoot game, GameResult triggerResult)
        {
            return AreConditionsMet(
                game,
                new GameEventExecutionContext(this, null, null, triggerResult)
            );
        }

        public bool AreConditionsMet(GameRoot game, GameEventExecutionContext context)
        {
            foreach (GameConditional conditional in Conditionals)
            {
                if (!conditional.IsMet(game, context))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Executes the event's actions and returns all results.
        /// </summary>
        /// <param name="game">The current game state.</param>
        /// <param name="provider">Random number provider for stochastic actions.</param>
        /// <returns>Combined results from all executed actions.</returns>
        public List<GameResult> Execute(GameRoot game, IRandomNumberProvider provider)
        {
            return Execute(game, provider, null);
        }

        /// <summary>
        /// Executes the event for one concrete global or scoped schedule.
        /// </summary>
        public List<GameResult> Execute(
            GameRoot game,
            IRandomNumberProvider provider,
            GameEventExecutionContext context
        )
        {
            List<GameResult> results = new List<GameResult>();

            foreach (GameAction action in Actions)
            {
                foreach (GameResult result in action.Execute(game, provider, context))
                {
                    if (result != null && string.IsNullOrEmpty(result.SourceEventInstanceID))
                        result.SourceEventInstanceID = InstanceID;
                    results.Add(result);
                }
            }

            return results;
        }
    }

    /// <summary>
    /// A data-defined exception to the normal hidden Force-user discovery rules.
    /// When one or more rules select a candidate, discovery is allowed when any matching
    /// rule accepts the discoverer and its inherited event conditions are met.
    /// </summary>
    [PersistableObject(Name = "ForceDiscoveryRule")]
    public sealed class ForceDiscoveryRule : GameEvent
    {
        public string CandidateOfficerInstanceID { get; set; }
        public string DiscovererOfficerInstanceID { get; set; }
        public ForceDiscoveryPresentation Presentation { get; set; }

        /// <summary>
        /// Returns whether this rule governs discovery of the supplied candidate.
        /// </summary>
        public bool AppliesTo(Officer candidate)
        {
            return candidate?.InstanceID == CandidateOfficerInstanceID;
        }

        /// <summary>
        /// Evaluates the authored discoverer restriction and normal event conditions.
        /// </summary>
        public bool Allows(GameRoot game, Officer discoverer, Officer candidate)
        {
            if (
                !string.IsNullOrWhiteSpace(DiscovererOfficerInstanceID)
                && discoverer?.InstanceID != DiscovererOfficerInstanceID
            )
                return false;

            return AreConditionsMet(
                game,
                new ForceDiscoveryResult
                {
                    EventType = ForceEventType.ForceUserDiscovered,
                    Officer = candidate,
                    Discoverer = discoverer,
                }
            );
        }
    }
}
