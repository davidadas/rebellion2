using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Events;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

namespace Rebellion.Systems
{
    /// <summary>
    /// Processes game events each tick and returns results for notification/logging.
    /// </summary>
    public class GameEventSystem : IGameResultHandler<GameResult>
    {
        private readonly GameRoot _game;
        private readonly IRandomNumberProvider _provider;
        private readonly UnitFactory _unitFactory;
        private readonly GameRequestDispatcher _requestDispatcher;

        /// <summary>
        /// Creates a new GameEventSystem.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="provider">Random number provider for stochastic event actions.</param>
        /// <param name="unitFactory">Factory for actions that create runtime units.</param>
        /// <param name="requestDispatcher">Routes action requests to authoritative systems.</param>
        public GameEventSystem(
            GameRoot game,
            IRandomNumberProvider provider,
            UnitFactory unitFactory = null,
            GameRequestDispatcher requestDispatcher = null
        )
        {
            _game = game;
            _provider = provider;
            _unitFactory = unitFactory;
            _requestDispatcher = requestDispatcher;
        }

        /// <summary>
        /// Validates the authored trigger, schedule, binding, and dependency contracts for the
        /// complete event pool before gameplay begins.
        /// </summary>
        /// <param name="events">The event definitions installed in the game.</param>
        /// <exception cref="InvalidOperationException">Thrown when an event contract is ambiguous or references an unknown event.</exception>
        public void ValidateEvents(IReadOnlyCollection<GameEvent> events)
        {
            GameEvent[] definitions =
                events?.Where(gameEvent => gameEvent != null).ToArray() ?? Array.Empty<GameEvent>();
            HashSet<string> eventIDs = new HashSet<string>(StringComparer.Ordinal);
            foreach (GameEvent gameEvent in definitions)
            {
                if (
                    string.IsNullOrWhiteSpace(gameEvent.InstanceID)
                    || !eventIDs.Add(gameEvent.InstanceID)
                )
                    throw new InvalidOperationException(
                        $"Game event instance ID '{gameEvent.InstanceID}' is missing or duplicated."
                    );
                if (gameEvent.TriggerCount is <= 0)
                    throw new InvalidOperationException(
                        $"Event '{gameEvent.InstanceID}' TriggerCount must be positive."
                    );
                if (gameEvent.Triggers.Count > 0 && gameEvent.Schedule != null)
                    throw new InvalidOperationException(
                        $"Event '{gameEvent.InstanceID}' cannot combine triggers with a schedule."
                    );
                ValidateSchedule(gameEvent);
                if (gameEvent.TriggerCount != 1 && gameEvent.Schedule is { IsRecurring: false })
                    throw new InvalidOperationException(
                        $"Event '{gameEvent.InstanceID}' cannot repeat with a one-shot schedule."
                    );

                Dictionary<string, Type> aliases = new Dictionary<string, Type>(
                    StringComparer.Ordinal
                );
                HashSet<string> sharedAliases = null;
                foreach (GameEventTrigger trigger in gameEvent.Triggers)
                {
                    _ = trigger.ResultType;
                    HashSet<string> triggerAliases = new HashSet<string>(StringComparer.Ordinal);
                    foreach (GameEventTriggerBinding binding in trigger.Bindings)
                    {
                        if (
                            string.IsNullOrWhiteSpace(binding.As)
                            || string.Equals(binding.As, "target", StringComparison.Ordinal)
                            || !triggerAliases.Add(binding.As)
                        )
                            throw new InvalidOperationException(
                                $"Event '{gameEvent.InstanceID}' trigger '{trigger.Event}' has a missing, duplicate, or reserved binding alias '{binding.As}'."
                            );
                        Type argumentType = trigger.GetArgumentType(binding.Argument);
                        if (
                            aliases.TryGetValue(binding.As, out Type existingType)
                            && existingType != argumentType
                        )
                            throw new InvalidOperationException(
                                $"Event '{gameEvent.InstanceID}' binds '{binding.As}' with incompatible types."
                            );
                        aliases[binding.As] = argumentType;
                    }

                    if (sharedAliases == null)
                        sharedAliases = triggerAliases;
                    else if (!sharedAliases.SetEquals(triggerAliases))
                        throw new InvalidOperationException(
                            $"Event '{gameEvent.InstanceID}' must expose the same binding aliases on every trigger path."
                        );
                }

                for (int first = 0; first < gameEvent.Triggers.Count; first++)
                {
                    for (int second = first + 1; second < gameEvent.Triggers.Count; second++)
                    {
                        Type firstType = gameEvent.Triggers[first].ResultType;
                        Type secondType = gameEvent.Triggers[second].ResultType;
                        if (
                            firstType.IsAssignableFrom(secondType)
                            || secondType.IsAssignableFrom(firstType)
                        )
                            throw new InvalidOperationException(
                                $"Event '{gameEvent.InstanceID}' has overlapping result triggers."
                            );
                    }
                }
            }

            foreach (GameEvent gameEvent in definitions)
            {
                IEnumerable<string> dependencies =
                    gameEvent.Schedule?.After != null
                        ? new[] { gameEvent.Schedule.After.EventInstanceID }
                        : (
                            gameEvent.Schedule?.AfterAll ?? gameEvent.Schedule?.AfterAny
                        )?.Events.Select(dependency => dependency.EventInstanceID)
                            ?? Enumerable.Empty<string>();
                foreach (string dependencyID in dependencies)
                {
                    if (!eventIDs.Contains(dependencyID))
                        throw new InvalidOperationException(
                            $"Event '{gameEvent.InstanceID}' references unknown event '{dependencyID}'."
                        );
                }
            }
        }

        /// <summary>
        /// Validates the immutable scheduling contract for one event before gameplay begins.
        /// </summary>
        /// <param name="gameEvent">The event whose authored schedule is validated.</param>
        private static void ValidateSchedule(GameEvent gameEvent)
        {
            GameEventScheduler schedule = gameEvent.Schedule;
            if (schedule == null)
                return;

            int configuredModes =
                (schedule.At == null ? 0 : 1)
                + (schedule.Every == null ? 0 : 1)
                + (schedule.Random == null ? 0 : 1)
                + (schedule.After == null ? 0 : 1)
                + (schedule.AfterAll == null ? 0 : 1)
                + (schedule.AfterAny == null ? 0 : 1);
            if (configuredModes != 1)
                throw new InvalidOperationException(
                    $"Event '{gameEvent.InstanceID}' schedule requires exactly one mode."
                );
            if (schedule.At is { Tick: < 0 })
                throw new InvalidOperationException(
                    $"Event '{gameEvent.InstanceID}' At.Tick cannot be negative."
                );
            if (schedule.Every is { Ticks: < 1 })
                throw new InvalidOperationException(
                    $"Event '{gameEvent.InstanceID}' Every.Ticks must be positive."
                );
            if (schedule.Every is { InitialDelayTicks: < 0 })
                throw new InvalidOperationException(
                    $"Event '{gameEvent.InstanceID}' Every.InitialDelayTicks cannot be negative."
                );
            if (
                schedule.Random != null
                && (
                    schedule.Random.MinimumTicks < 1
                    || schedule.Random.MaximumTicks < schedule.Random.MinimumTicks
                )
            )
                throw new InvalidOperationException(
                    $"Event '{gameEvent.InstanceID}' Random requires a positive ordered tick range."
                );
            if (schedule.After is { DelayTicks: < 0 })
                throw new InvalidOperationException(
                    $"Event '{gameEvent.InstanceID}' After.DelayTicks cannot be negative."
                );

            AfterEvents dependencies = schedule.AfterAll ?? schedule.AfterAny;
            if (dependencies == null)
                return;
            if (dependencies.DelayTicks < 0 || dependencies.Events.Count == 0)
                throw new InvalidOperationException(
                    $"Event '{gameEvent.InstanceID}' dependent schedule requires events and a nonnegative delay."
                );
            if (
                dependencies.Events.Any(dependency =>
                    string.IsNullOrWhiteSpace(dependency.EventInstanceID)
                )
                || dependencies
                    .Events.Select(dependency => dependency.EventInstanceID)
                    .Distinct(StringComparer.Ordinal)
                    .Count() != dependencies.Events.Count
            )
                throw new InvalidOperationException(
                    $"Event '{gameEvent.InstanceID}' dependent schedule contains a missing or duplicate event ID."
                );
        }

        /// <summary>
        /// Processes all eligible events and returns the aggregate results.
        /// </summary>
        /// <param name="gameEvents">The events to evaluate.</param>
        /// <returns>Results produced by events that executed.</returns>
        public List<GameResult> ProcessEvents(List<GameEvent> gameEvents)
        {
            List<GameResult> allResults = new List<GameResult>();
            List<GameEvent> eventsToRemove = new List<GameEvent>();

            foreach (GameEvent gameEvent in gameEvents.ToArray())
            {
                if (HasResultTrigger(gameEvent))
                    continue;

                if (TryProcessEvent(gameEvent, null, null, out List<GameResult> globalResults))
                    allResults.AddRange(globalResults);
                if (_game.EventRuntime.GetState(gameEvent.InstanceID).IsExhausted)
                    eventsToRemove.Add(gameEvent);
            }

            foreach (GameEvent eventToRemove in eventsToRemove)
                _game.RemoveEvent(eventToRemove);

            return allResults;
        }

        /// <summary>
        /// Executes events whose authored trigger type matches a newly produced simulation result.
        /// </summary>
        public List<GameResult> HandleResults(IReadOnlyList<GameResult> results)
        {
            List<GameResult> eventResults = new List<GameResult>();
            if (results == null)
                return eventResults;

            Dictionary<Type, List<(GameEvent Event, GameEventTrigger Trigger)>> triggerIndex =
                BuildTriggerIndex();

            foreach (GameResult triggerResult in results)
            {
                List<(GameEvent Event, GameEventTrigger Trigger)> candidates = triggerIndex
                    .Where(entry => entry.Key.IsInstanceOfType(triggerResult))
                    .SelectMany(entry => entry.Value)
                    .ToList();
                HashSet<GameEvent> processedEvents = new HashSet<GameEvent>();
                foreach ((GameEvent gameEvent, GameEventTrigger trigger) in candidates)
                {
                    if (
                        processedEvents.Contains(gameEvent)
                        || !_game.GetEventPool().Contains(gameEvent)
                        || !trigger.Matches(triggerResult)
                    )
                        continue;
                    processedEvents.Add(gameEvent);
                    if (
                        !TryProcessEvent(
                            gameEvent,
                            trigger,
                            triggerResult,
                            out List<GameResult> reactions
                        )
                    )
                        continue;

                    eventResults.AddRange(reactions);
                    if (_game.EventRuntime.GetState(gameEvent.InstanceID).IsExhausted)
                        _game.RemoveEvent(gameEvent);
                }
            }

            return eventResults;
        }

        private Dictionary<
            Type,
            List<(GameEvent Event, GameEventTrigger Trigger)>
        > BuildTriggerIndex()
        {
            Dictionary<Type, List<(GameEvent Event, GameEventTrigger Trigger)>> index = new();
            foreach (GameEvent gameEvent in _game.GetEventPool())
            {
                foreach (GameEventTrigger trigger in gameEvent.Triggers)
                {
                    if (
                        !index.TryGetValue(
                            trigger.ResultType,
                            out List<(GameEvent, GameEventTrigger)> entries
                        )
                    )
                    {
                        entries = new List<(GameEvent, GameEventTrigger)>();
                        index.Add(trigger.ResultType, entries);
                    }
                    entries.Add((gameEvent, trigger));
                }
            }
            return index;
        }

        /// <summary>
        /// Executes a single game event if its conditions are met.
        /// </summary>
        /// <param name="gameEvent">The event to process.</param>
        /// <param name="trigger">The trigger definition that matched the result, if any.</param>
        /// <param name="triggerResult">The simulation result that activated the event, if any.</param>
        /// <param name="results">Receives results produced by the event.</param>
        /// <returns>True when the event executed; otherwise false.</returns>
        private bool TryProcessEvent(
            GameEvent gameEvent,
            GameEventTrigger trigger,
            GameResult triggerResult,
            out List<GameResult> results
        )
        {
            GameEventState state = _game.EventRuntime.GetState(gameEvent.InstanceID);
            if (ShouldExhaust(gameEvent, state) || !gameEvent.CanExecute(state))
            {
                results = new List<GameResult>();
                return false;
            }

            if (!InitializeSchedule(gameEvent, state))
            {
                results = new List<GameResult>();
                return false;
            }
            if (_game.CurrentTick < state.NextEligibleTick)
            {
                results = new List<GameResult>();
                return false;
            }

            GameEventExecutionContext selectorContext = new GameEventExecutionContext(
                gameEvent,
                state,
                null,
                triggerResult,
                trigger
            );
            ISceneNode target = gameEvent.Target?.Select(_game, _provider, selectorContext);
            GameEventExecutionContext context = new GameEventExecutionContext(
                gameEvent,
                state,
                target,
                triggerResult,
                trigger
            );
            if (!gameEvent.AreConditionsMet(_game, context))
            {
                results = new List<GameResult>();
                return false;
            }

            GameLogger.Log($"Executing game event: {gameEvent.InstanceID}");
            GameActionContext actionContext = gameEvent.Execute(
                _game,
                _provider,
                context,
                _unitFactory
            );
            results = new List<GameResult>(actionContext.Results);
            if (actionContext.Requests.Count > 0)
            {
                if (_requestDispatcher == null)
                    throw new InvalidOperationException(
                        $"Event '{gameEvent.InstanceID}' produced requests without a configured dispatcher."
                    );
                results.AddRange(_requestDispatcher.Process(actionContext.Requests));
            }

            state.ExecutionCount++;
            state.LastExecutionTick = _game.CurrentTick;
            if (gameEvent.Schedule is { IsRecurring: false })
                state.IsExhausted = true;
            bool isExhausted = ShouldExhaust(gameEvent, state);
            if (!isExhausted)
            {
                GetRepeatRange(gameEvent, out int minimum, out int maximum);
                state.NextEligibleTick = _game.CurrentTick + RollRange(minimum, maximum);
            }
            return true;
        }

        /// <summary>
        /// Returns whether an event reached its trigger count or authored stop conditions.
        /// </summary>
        private bool ShouldExhaust(GameEvent gameEvent, GameEventState state)
        {
            if (state.IsExhausted)
                return true;

            int? triggerCount = gameEvent.TriggerCount;
            bool reachedCount = triggerCount.HasValue && state.ExecutionCount >= triggerCount.Value;
            bool reachedUntil =
                gameEvent.Until.Count > 0
                && gameEvent.Until.All(condition =>
                    condition.IsMet(
                        new GameConditionContext(
                            _game,
                            new GameEventExecutionContext(gameEvent, state, null, null)
                        )
                    )
                );
            state.IsExhausted = reachedCount || reachedUntil;
            return state.IsExhausted;
        }

        /// <summary>
        /// Returns whether an event activates from simulation results instead of tick scheduling.
        /// </summary>
        /// <param name="gameEvent">The event to inspect.</param>
        /// <returns>True when a typed result trigger is configured.</returns>
        private static bool HasResultTrigger(GameEvent gameEvent) => gameEvent.Triggers.Count > 0;

        /// <summary>
        /// Initializes an event's absolute first eligible tick from its authored schedule.
        /// </summary>
        /// <param name="gameEvent">The event definition.</param>
        /// <param name="state">The persistent runtime state to initialize.</param>
        private bool InitializeSchedule(GameEvent gameEvent, GameEventState state)
        {
            if (state.IsInitialized)
                return true;

            AfterEvent after = gameEvent.Schedule?.After;
            if (after != null)
            {
                GameEventState predecessor = _game.EventRuntime.GetState(after.EventInstanceID);
                if (predecessor.ExecutionCount == 0)
                    return false;

                state.NextEligibleTick = checked(predecessor.LastExecutionTick + after.DelayTicks);
                state.IsInitialized = true;
                return true;
            }

            AfterEvents dependencies = gameEvent.Schedule?.AfterAll ?? gameEvent.Schedule?.AfterAny;
            if (dependencies != null)
            {
                List<GameEventState> predecessorStates = new List<GameEventState>();
                foreach (EventDependency dependency in dependencies.Events)
                {
                    predecessorStates.Add(_game.EventRuntime.GetState(dependency.EventInstanceID));
                }
                bool isAfterAll = gameEvent.Schedule.AfterAll != null;
                if (
                    isAfterAll
                    && predecessorStates.Any(predecessor => predecessor.ExecutionCount == 0)
                )
                    return false;
                List<GameEventState> completed = predecessorStates
                    .Where(predecessor => predecessor.ExecutionCount > 0)
                    .ToList();
                if (completed.Count == 0)
                    return false;

                int dependencyTick = isAfterAll
                    ? completed.Max(predecessor => predecessor.LastExecutionTick)
                    : completed.Min(predecessor => predecessor.LastExecutionTick);
                state.NextEligibleTick = checked(dependencyTick + dependencies.DelayTicks);
                state.IsInitialized = true;
                return true;
            }

            GetInitialRange(gameEvent, out int minimum, out int maximum);
            state.NextEligibleTick = RollRange(minimum, maximum);
            state.IsInitialized = true;
            return true;
        }

        /// <summary>
        /// Gets the inclusive tick range for an event's first execution.
        /// </summary>
        /// <param name="gameEvent">The event whose schedule is evaluated.</param>
        /// <param name="minimum">Receives the minimum delay in ticks.</param>
        /// <param name="maximum">Receives the maximum delay in ticks.</param>
        private static void GetInitialRange(GameEvent gameEvent, out int minimum, out int maximum)
        {
            if (gameEvent.Schedule == null)
            {
                minimum = maximum = 0;
                return;
            }
            gameEvent.Schedule.GetInitialRange(out minimum, out maximum);
        }

        /// <summary>
        /// Gets the inclusive delay range for an event's next repeat.
        /// </summary>
        /// <param name="gameEvent">The event whose schedule is evaluated.</param>
        /// <param name="minimum">Receives the minimum delay in ticks.</param>
        /// <param name="maximum">Receives the maximum delay in ticks.</param>
        private static void GetRepeatRange(GameEvent gameEvent, out int minimum, out int maximum)
        {
            if (gameEvent.Schedule == null)
            {
                minimum = maximum = 0;
                return;
            }
            gameEvent.Schedule.GetRepeatRange(out minimum, out maximum);
        }

        /// <summary>
        /// Selects one value from an inclusive deterministic range.
        /// </summary>
        /// <param name="minimum">The inclusive lower bound.</param>
        /// <param name="maximum">The inclusive upper bound.</param>
        /// <returns>The selected value.</returns>
        private int RollRange(int minimum, int maximum)
        {
            return minimum == maximum ? minimum : _provider.NextInt(minimum, maximum + 1);
        }
    }
}
