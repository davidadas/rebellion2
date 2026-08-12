using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Events;
using Rebellion.Game.Results;
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

        /// <summary>
        /// Creates a new GameEventSystem.
        /// </summary>
        /// <param name="game">The game instance.</param>
        /// <param name="provider">Random number provider for stochastic event actions.</param>
        public GameEventSystem(GameRoot game, IRandomNumberProvider provider)
        {
            _game = game;
            _provider = provider;
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
                EnsureExecutionMode(gameEvent);
                if (HasResultTrigger(gameEvent))
                    continue;

                if (gameEvent.Target?.MaintainsStatePerTarget == true)
                {
                    foreach (ISceneNode target in gameEvent.Target.Resolve(_game, _provider))
                    {
                        if (
                            TryProcessEvent(
                                gameEvent,
                                null,
                                null,
                                target,
                                true,
                                out List<GameResult> scopedResults
                            )
                        )
                            allResults.AddRange(scopedResults);
                    }
                }
                else if (
                    TryProcessEvent(
                        gameEvent,
                        null,
                        null,
                        null,
                        false,
                        out List<GameResult> globalResults
                    )
                )
                {
                    allResults.AddRange(globalResults);
                    if (!gameEvent.Repeats)
                        eventsToRemove.Add(gameEvent);
                }
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
                    EnsureExecutionMode(gameEvent);
                    processedEvents.Add(gameEvent);
                    if (
                        !TryProcessEvent(
                            gameEvent,
                            trigger,
                            triggerResult,
                            null,
                            false,
                            out List<GameResult> reactions
                        )
                    )
                        continue;

                    eventResults.AddRange(reactions);
                    if (!gameEvent.Repeats)
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
        /// <param name="scopeTarget">The planet whose independent schedule is being processed.</param>
        /// <param name="maintainsStatePerTarget">Whether scheduling state is isolated by target.</param>
        /// <param name="results">Receives results produced by the event.</param>
        /// <returns>True when the event executed; otherwise false.</returns>
        private bool TryProcessEvent(
            GameEvent gameEvent,
            GameEventTrigger trigger,
            GameResult triggerResult,
            ISceneNode scopeTarget,
            bool maintainsStatePerTarget,
            out List<GameResult> results
        )
        {
            GameEventState state =
                scopeTarget == null
                    ? _game.EventRuntime.GetState(gameEvent.InstanceID)
                    : _game.EventRuntime.GetState(gameEvent.InstanceID, scopeTarget.InstanceID);
            if (!gameEvent.Repeats && state.IsComplete)
            {
                results = new List<GameResult>();
                return false;
            }

            ISceneNode executionTarget = scopeTarget;
            GameEventExecutionContext context = null;
            if (maintainsStatePerTarget)
            {
                context = new GameEventExecutionContext(
                    gameEvent,
                    state,
                    executionTarget,
                    triggerResult,
                    trigger
                );
                if (!gameEvent.AreConditionsMet(_game, context))
                {
                    state.IsTargetActive = false;
                    state.IsInitialized = false;
                    results = new List<GameResult>();
                    return false;
                }
                state.IsTargetActive = true;
            }
            ScheduleAnchor scheduleAnchor = maintainsStatePerTarget
                ? ScheduleAnchor.TargetEligibility
                : ScheduleAnchor.CampaignStart;
            if (!InitializeSchedule(gameEvent, state, scheduleAnchor))
            {
                results = new List<GameResult>();
                return false;
            }
            if (_game.CurrentTick < state.NextEligibleTick)
            {
                results = new List<GameResult>();
                return false;
            }

            if (executionTarget == null && gameEvent.Target != null)
            {
                executionTarget = string.IsNullOrWhiteSpace(state.SelectedTargetInstanceID)
                    ? gameEvent.Target.Resolve(_game, _provider).SingleOrDefault()
                    : _game.GetSceneNodeByInstanceID<ISceneNode>(state.SelectedTargetInstanceID);
                if (executionTarget != null)
                    state.SelectedTargetInstanceID = executionTarget.InstanceID;
            }
            if (gameEvent.Target != null && executionTarget == null)
            {
                results = new List<GameResult>();
                return false;
            }

            context ??= new GameEventExecutionContext(
                gameEvent,
                state,
                executionTarget,
                triggerResult,
                trigger
            );
            if (!maintainsStatePerTarget && !gameEvent.AreConditionsMet(_game, context))
            {
                results = new List<GameResult>();
                return false;
            }

            state.IsTargetActive = maintainsStatePerTarget;
            GameLogger.Log($"Executing game event: {gameEvent.GetDisplayName()}");
            results = gameEvent.Execute(_game, _provider, context);
            state.ExecutionCount++;
            state.IsComplete = !gameEvent.Repeats;
            state.LastExecutionTick = _game.CurrentTick;
            if (gameEvent.Repeats)
            {
                GetRepeatRange(gameEvent, out int minimum, out int maximum);
                state.NextEligibleTick = _game.CurrentTick + RollRange(minimum, maximum);
                state.SelectedTargetInstanceID = null;
            }
            _game.EventRuntime.Complete(gameEvent.InstanceID);
            return true;
        }

        /// <summary>
        /// Returns whether an event activates from simulation results instead of tick scheduling.
        /// </summary>
        /// <param name="gameEvent">The event to inspect.</param>
        /// <returns>True when a stable or legacy trigger is configured.</returns>
        private static bool HasResultTrigger(GameEvent gameEvent) => gameEvent.Triggers.Count > 0;

        private static void EnsureExecutionMode(GameEvent gameEvent)
        {
            if (gameEvent.Triggers.Count > 0 && gameEvent.Schedule != null)
                throw new InvalidOperationException(
                    $"Event '{gameEvent.InstanceID}' cannot combine result triggers with a tick schedule."
                );
            if (gameEvent.Triggers.Count > 0 && gameEvent.Target?.MaintainsStatePerTarget == true)
                throw new InvalidOperationException(
                    $"Event '{gameEvent.InstanceID}' cannot combine result triggers with an EachPlanet target."
                );
            if (!gameEvent.RunsOnce && gameEvent.Schedule?.IsOneShot == true)
                throw new InvalidOperationException(
                    $"Event '{gameEvent.InstanceID}' must set RunsOnce when using a one-shot schedule."
                );
        }

        /// <summary>
        /// Initializes an event's absolute first eligible tick from its authored schedule.
        /// </summary>
        /// <param name="gameEvent">The event definition.</param>
        /// <param name="state">The persistent runtime state to initialize.</param>
        /// <param name="anchor">
        /// The explicit origin used for relative first-execution delays.
        /// </param>
        private bool InitializeSchedule(
            GameEvent gameEvent,
            GameEventState state,
            ScheduleAnchor anchor
        )
        {
            if (state.IsInitialized)
                return true;

            AfterEvent after = gameEvent.Schedule?.After;
            if (after != null)
            {
                if (string.IsNullOrWhiteSpace(after.EventInstanceID))
                    throw new InvalidOperationException("After.EventInstanceID is required.");
                if (after.DelayTicks < 0)
                    throw new InvalidOperationException("After.DelayTicks cannot be negative.");
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
                if (dependencies.DelayTicks < 0)
                    throw new InvalidOperationException(
                        "Dependent schedule delay cannot be negative."
                    );
                if (dependencies.Events == null || dependencies.Events.Count == 0)
                    throw new InvalidOperationException(
                        "AfterAll and AfterAny require at least one event dependency."
                    );

                List<GameEventState> predecessorStates = new List<GameEventState>();
                HashSet<string> dependencyIDs = new HashSet<string>(StringComparer.Ordinal);
                foreach (EventDependency dependency in dependencies.Events)
                {
                    if (string.IsNullOrWhiteSpace(dependency.EventInstanceID))
                        throw new InvalidOperationException(
                            "Event dependency instance ID is required."
                        );
                    if (!dependencyIDs.Add(dependency.EventInstanceID))
                        throw new InvalidOperationException(
                            $"Duplicate event dependency '{dependency.EventInstanceID}'."
                        );
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
            int anchorTick = anchor == ScheduleAnchor.TargetEligibility ? _game.CurrentTick : 0;
            state.NextEligibleTick = checked(anchorTick + RollRange(minimum, maximum));
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

        private enum ScheduleAnchor
        {
            CampaignStart,
            TargetEligibility,
        }
    }
}
