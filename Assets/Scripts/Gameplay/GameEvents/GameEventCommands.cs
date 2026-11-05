using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.GameEvents;
using Rebellion.Game.Results;
using Rebellion.Game.Scripting;
using Rebellion.Gameplay.Scripting;
using Rebellion.SceneGraph;
using Rebellion.Util.Random;

namespace Rebellion.Gameplay.GameEvents
{
    /// <summary>
    /// Validates authored events and applies scheduled or result-triggered activations.
    /// </summary>
    public partial class GameEventCommands
    {
        private readonly GameRoot _game;
        private readonly IRandomNumberProvider _provider;
        private readonly GameActionCommands _actions;

        /// <summary>
        /// Creates event activation processing with a shared action executor.
        /// </summary>
        /// <param name="game">The active game.</param>
        /// <param name="provider">The random provider used for event schedules and bindings.</param>
        /// <param name="actions">The executor for authored game actions.</param>
        public GameEventCommands(
            GameRoot game,
            IRandomNumberProvider provider,
            GameActionCommands actions
        )
        {
            _game = game;
            _provider = provider;
            _actions = actions ?? throw new ArgumentNullException(nameof(actions));
        }

        /// <summary>
        /// Validates the authored trigger, schedule, binding, and dependency contracts for the
        /// active event pool before gameplay begins.
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
                ValidateDefinition(gameEvent);
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
                    bool isCompletedDependency =
                        _game.EventRuntime.States.TryGetValue(
                            dependencyID,
                            out GameEventState dependencyState
                        ) && dependencyState.IsComplete;
                    if (!eventIDs.Contains(dependencyID) && !isCompletedDependency)
                        throw new InvalidOperationException(
                            $"Event '{gameEvent.InstanceID}' references unknown event '{dependencyID}'."
                        );
                }
            }
        }

        /// <summary>
        /// Validates one event's schedule, scope, trigger, and binding contracts without runtime state.
        /// </summary>
        /// <param name="gameEvent">The authored definition being loaded or edited.</param>
        /// <exception cref="InvalidOperationException">Thrown when the definition has an invalid activation contract.</exception>
        public static void ValidateDefinition(GameEvent gameEvent)
        {
            if (string.IsNullOrWhiteSpace(gameEvent.InstanceID))
                throw new InvalidOperationException("Game event instance ID is required.");
            if (gameEvent.MaximumActivations is <= 0)
                throw new InvalidOperationException(
                    $"Event '{gameEvent.InstanceID}' MaximumActivations must be positive."
                );
            if ((gameEvent.Triggers.Count > 0 ? 1 : 0) + (gameEvent.Schedule != null ? 1 : 0) != 1)
                throw new InvalidOperationException(
                    $"Event '{gameEvent.InstanceID}' requires exactly one schedule or trigger collection."
                );
            ValidateSchedule(gameEvent);
            ValidateScope(gameEvent);
            if (
                gameEvent.MaximumActivations.HasValue
                && gameEvent.MaximumActivations != 1
                && gameEvent.Schedule is GameEventSchedule configuredSchedule
                && !IsRecurring(configuredSchedule)
            )
                throw new InvalidOperationException(
                    $"Event '{gameEvent.InstanceID}' cannot repeat with a one-shot schedule."
                );

            Dictionary<string, Type> aliases = new(StringComparer.Ordinal);
            if (gameEvent.ForEach != null)
                aliases.Add(gameEvent.ForEach.As, typeof(ISceneNode));
            Dictionary<string, Type> sharedTriggerBindings = null;
            foreach (GameTrigger trigger in gameEvent.Triggers)
            {
                Dictionary<string, Type> triggerBindings =
                    GameBindingResolver.ValidateTriggerBindings(
                        trigger,
                        $"Event '{gameEvent.InstanceID}'"
                    );
                if (sharedTriggerBindings == null)
                    sharedTriggerBindings = triggerBindings;
                else if (!HaveSameBindings(sharedTriggerBindings, triggerBindings))
                    throw new InvalidOperationException(
                        $"Event '{gameEvent.InstanceID}' must expose the same trigger bindings and value types on every trigger path."
                    );
            }
            foreach (
                KeyValuePair<string, Type> triggerBinding in sharedTriggerBindings
                    ?? new Dictionary<string, Type>()
            )
            {
                if (!aliases.TryAdd(triggerBinding.Key, triggerBinding.Value))
                {
                    throw new InvalidOperationException(
                        $"Event '{gameEvent.InstanceID}' has duplicate binding alias '{triggerBinding.Key}'."
                    );
                }
            }

            GameBindingResolver.ValidateBindings(
                gameEvent.Bindings,
                aliases,
                $"Event '{gameEvent.InstanceID}'"
            );
        }

        /// <summary>
        /// Validates an optional per-target schedule scope.
        /// </summary>
        /// <param name="gameEvent">The event whose scope is validated.</param>
        private static void ValidateScope(GameEvent gameEvent)
        {
            GameEventScope scope = gameEvent.ForEach;
            if (scope == null)
                return;
            if (string.IsNullOrWhiteSpace(scope.As) || scope.Selectors.Count != 1)
                throw new InvalidOperationException(
                    $"Event '{gameEvent.InstanceID}' ForEach requires one selector and a binding alias."
                );
            if (gameEvent.Triggers.Count > 0)
                throw new InvalidOperationException(
                    $"Event '{gameEvent.InstanceID}' cannot combine ForEach with result triggers."
                );
            if (gameEvent.Schedule?.Every == null && gameEvent.Schedule?.RandomInterval == null)
                throw new InvalidOperationException(
                    $"Event '{gameEvent.InstanceID}' ForEach requires a recurring schedule."
                );
            bool supportsScope = scope.Selectors[0] switch
            {
                SelectPlanets => true,
                SelectOfficers value => string.IsNullOrWhiteSpace(value.PlanetBinding),
                SelectSpecialForces value => string.IsNullOrWhiteSpace(value.PlanetBinding),
                SelectFleets value => string.IsNullOrWhiteSpace(value.PlanetBinding),
                SelectMissions value => string.IsNullOrWhiteSpace(value.PlanetBinding),
                SelectCapitalShips value => string.IsNullOrWhiteSpace(value.PlanetBinding),
                SelectStarfighters value => string.IsNullOrWhiteSpace(value.PlanetBinding),
                SelectRegiments value => string.IsNullOrWhiteSpace(value.PlanetBinding),
                SelectBuildings value => string.IsNullOrWhiteSpace(value.PlanetBinding),
                SelectManufacturingOrders value => string.IsNullOrWhiteSpace(value.PlanetBinding),
                _ => false,
            };
            if (!supportsScope)
                throw new InvalidOperationException(
                    $"Event '{gameEvent.InstanceID}' ForEach requires a deterministic node selector without activation bindings."
                );
        }

        /// <summary>
        /// Checks whether two triggers expose the same binding contract.
        /// </summary>
        /// <param name="first">The first binding contract.</param>
        /// <param name="second">The second binding contract.</param>
        /// <returns>True when both contracts expose the same names and value types.</returns>
        private static bool HaveSameBindings(
            IReadOnlyDictionary<string, Type> first,
            IReadOnlyDictionary<string, Type> second
        ) =>
            first.Count == second.Count
            && first.All(pair =>
                second.TryGetValue(pair.Key, out Type valueType) && valueType == pair.Value
            );

        /// <summary>
        /// Validates the immutable scheduling contract for one event before gameplay begins.
        /// </summary>
        /// <param name="gameEvent">The event whose authored schedule is validated.</param>
        private static void ValidateSchedule(GameEvent gameEvent)
        {
            GameEventSchedule schedule = gameEvent.Schedule;
            if (schedule == null)
                return;

            int configuredModes =
                (schedule.At == null ? 0 : 1)
                + (schedule.Every == null ? 0 : 1)
                + (schedule.RandomDelay == null ? 0 : 1)
                + (schedule.RandomInterval == null ? 0 : 1)
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
                schedule.RandomDelay != null
                && (
                    schedule.RandomDelay.MinimumTicks < 1
                    || schedule.RandomDelay.MaximumTicks < schedule.RandomDelay.MinimumTicks
                )
            )
                throw new InvalidOperationException(
                    $"Event '{gameEvent.InstanceID}' RandomDelay requires a positive ordered tick range."
                );
            if (
                schedule.RandomInterval != null
                && (
                    schedule.RandomInterval.MinimumTicks < 1
                    || schedule.RandomInterval.MaximumTicks < schedule.RandomInterval.MinimumTicks
                )
            )
                throw new InvalidOperationException(
                    $"Event '{gameEvent.InstanceID}' RandomInterval requires a positive ordered tick range."
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
        /// Applies all eligible scheduled events and returns their aggregate results.
        /// </summary>
        /// <param name="gameEvents">The events to evaluate.</param>
        /// <returns>Results produced by events that activated.</returns>
        public List<GameResult> ProcessScheduledEvents(List<GameEvent> gameEvents)
        {
            List<GameResult> allResults = new List<GameResult>();
            List<GameEvent> eventsToRemove = new List<GameEvent>();

            foreach (GameEvent gameEvent in gameEvents.ToArray())
            {
                if (HasResultTrigger(gameEvent))
                    continue;

                if (gameEvent.ForEach != null)
                    allResults.AddRange(ProcessScopedEvent(gameEvent));
                else if (TryActivateEvent(gameEvent, null, null, out List<GameResult> eventResults))
                    allResults.AddRange(eventResults);
                if (_game.EventRuntime.GetState(gameEvent.InstanceID).IsComplete)
                    eventsToRemove.Add(gameEvent);
            }

            foreach (GameEvent eventToRemove in eventsToRemove)
                _game.RemoveEvent(eventToRemove);

            return allResults;
        }

        /// <summary>
        /// Applies a scheduled event independently to every currently selected scope target.
        /// </summary>
        /// <param name="gameEvent">The scoped event definition.</param>
        /// <returns>Results produced by all targets that activated.</returns>
        private List<GameResult> ProcessScopedEvent(GameEvent gameEvent)
        {
            GameEventState eventState = _game.EventRuntime.GetState(gameEvent.InstanceID);
            if (HasReachedMaximumActivations(gameEvent, eventState))
                return new List<GameResult>();
            eventState.ScopedStates ??= new Dictionary<string, GameEventState>();
            GameEventScope scope = gameEvent.ForEach;
            HashSet<ISceneNode> currentTargets = SelectScopedTargets(scope);
            List<ISceneNode> targets = currentTargets
                .OrderBy(target => target.InstanceID, StringComparer.Ordinal)
                .ToList();
            RemoveAbsentScopedStates(eventState, currentTargets);
            bool refreshTargets = false;
            List<GameResult> results = new List<GameResult>();
            foreach (ISceneNode target in targets)
            {
                if (!CanActivate(gameEvent, eventState))
                    break;
                if (refreshTargets)
                {
                    currentTargets = SelectScopedTargets(scope);
                    RemoveAbsentScopedStates(eventState, currentTargets);
                    refreshTargets = false;
                }
                if (!currentTargets.Contains(target))
                    continue;
                if (
                    !eventState.ScopedStates.TryGetValue(
                        target.InstanceID,
                        out GameEventState targetState
                    )
                )
                {
                    targetState = new GameEventState();
                    eventState.ScopedStates.Add(target.InstanceID, targetState);
                }

                GameEvaluationContext context = new GameEvaluationContext(
                    gameEvent.InstanceID,
                    sourceEventInstanceID: gameEvent.InstanceID,
                    sourceKind: "Event"
                );
                context.Bind(scope.As, target);
                if (
                    TryActivateEvent(
                        gameEvent,
                        null,
                        null,
                        out List<GameResult> targetResults,
                        targetState,
                        context,
                        initializeRelativeToCurrentTick: true
                    )
                )
                {
                    results.AddRange(targetResults);
                    refreshTargets = true;
                }
            }
            return results;
        }

        /// <summary>
        /// Resolves the current membership of a validated, deterministic event scope.
        /// </summary>
        /// <param name="scope">The scope whose selector is evaluated.</param>
        /// <returns>The distinct, identified scene nodes currently in the scope.</returns>
        private HashSet<ISceneNode> SelectScopedTargets(GameEventScope scope) =>
            scope
                .Selectors.SelectMany(selector =>
                    GameSelectorResolver.Select(selector, _game, _provider, null)
                )
                .Where(target => target != null && !string.IsNullOrWhiteSpace(target.InstanceID))
                .ToHashSet();

        /// <summary>
        /// Cancels absent targets' pending timers while retaining permanent completion records.
        /// </summary>
        /// <param name="eventState">The event's aggregate and target-specific state.</param>
        /// <param name="targets">The targets currently selected by the scope.</param>
        private static void RemoveAbsentScopedStates(
            GameEventState eventState,
            IEnumerable<ISceneNode> targets
        )
        {
            HashSet<string> targetIDs = targets
                .Select(target => target.InstanceID)
                .ToHashSet(StringComparer.Ordinal);
            foreach (
                string id in eventState
                    .ScopedStates.Where(pair =>
                        !pair.Value.IsComplete && !targetIDs.Contains(pair.Key)
                    )
                    .Select(pair => pair.Key)
                    .ToArray()
            )
                eventState.ScopedStates.Remove(id);
        }

        /// <summary>
        /// Synchronizes planet-scoped schedule state with completed ownership transitions.
        /// </summary>
        /// <param name="results">The ownership transitions to apply in result order.</param>
        public void SynchronizeScopedTargets(IReadOnlyList<PlanetOwnershipChangedResult> results)
        {
            foreach (
                PlanetOwnershipChangedResult result in results
                    ?? Array.Empty<PlanetOwnershipChangedResult>()
            )
            {
                if (result?.Planet == null || string.IsNullOrWhiteSpace(result.Planet.InstanceID))
                    continue;

                foreach (GameEvent gameEvent in _game.GetEventPool())
                {
                    if (
                        gameEvent.ForEach?.Selectors.SingleOrDefault() is not SelectPlanets selector
                    )
                        continue;

                    bool selectedBefore = GameSelectorResolver.Matches(
                        selector,
                        result.Planet,
                        result.PreviousOwner?.InstanceID
                    );
                    bool selectedAfter = GameSelectorResolver.Matches(
                        selector,
                        result.Planet,
                        result.NewOwner?.InstanceID
                    );
                    if (selectedBefore == selectedAfter)
                        continue;

                    GameEventState eventState = _game.EventRuntime.GetState(gameEvent.InstanceID);
                    if (eventState.IsComplete)
                        continue;
                    eventState.ScopedStates ??= new Dictionary<string, GameEventState>();
                    if (
                        eventState.ScopedStates.TryGetValue(
                            result.Planet.InstanceID,
                            out GameEventState existingState
                        ) && existingState.IsComplete
                    )
                        continue;
                    if (!selectedAfter)
                    {
                        eventState.ScopedStates.Remove(result.Planet.InstanceID);
                        continue;
                    }

                    GameEventState targetState = new GameEventState();
                    eventState.ScopedStates[result.Planet.InstanceID] = targetState;
                    InitializeSchedule(gameEvent, targetState, result.Tick);
                }
            }
        }

        /// <summary>
        /// Applies events whose authored trigger type matches a newly produced simulation result.
        /// </summary>
        /// <param name="results">The results.</param>
        /// <returns>The result of handle results.</returns>
        public List<GameResult> ProcessTriggeredEvents(IReadOnlyList<GameResult> results)
        {
            List<GameResult> eventResults = new List<GameResult>();
            if (results == null)
                return eventResults;

            Dictionary<Type, List<(GameEvent Event, GameTrigger Trigger)>> triggerIndex =
                BuildTriggerIndex();

            foreach (GameResult triggerResult in results)
            {
                List<(GameEvent Event, GameTrigger Trigger)> candidates = triggerIndex
                    .Where(entry => entry.Key.IsInstanceOfType(triggerResult))
                    .SelectMany(entry => entry.Value)
                    .ToList();
                HashSet<GameEvent> processedEvents = new HashSet<GameEvent>();
                foreach ((GameEvent gameEvent, GameTrigger trigger) in candidates)
                {
                    if (
                        processedEvents.Contains(gameEvent)
                        || !_game.GetEventPool().Contains(gameEvent)
                        || !GameTriggerMatcher.Matches(trigger, triggerResult)
                    )
                        continue;
                    processedEvents.Add(gameEvent);
                    if (
                        !TryActivateEvent(
                            gameEvent,
                            trigger,
                            triggerResult,
                            out List<GameResult> reactions
                        )
                    )
                        continue;

                    eventResults.AddRange(reactions);
                    if (_game.EventRuntime.GetState(gameEvent.InstanceID).IsComplete)
                        _game.RemoveEvent(gameEvent);
                }
            }

            return eventResults;
        }

        /// <summary>
        /// Builds trigger index.
        /// </summary>
        /// <returns>The constructed trigger index.</returns>
        private Dictionary<Type, List<(GameEvent Event, GameTrigger Trigger)>> BuildTriggerIndex()
        {
            Dictionary<Type, List<(GameEvent Event, GameTrigger Trigger)>> index = new();
            foreach (GameEvent gameEvent in _game.GetEventPool())
            {
                foreach (GameTrigger trigger in gameEvent.Triggers)
                {
                    if (
                        !index.TryGetValue(
                            GameTriggerMatcher.GetResultType(trigger),
                            out List<(GameEvent, GameTrigger)> entries
                        )
                    )
                    {
                        entries = new List<(GameEvent, GameTrigger)>();
                        index.Add(GameTriggerMatcher.GetResultType(trigger), entries);
                    }
                    entries.Add((gameEvent, trigger));
                }
            }
            return index;
        }

        /// <summary>
        /// Evaluates a single game event and activates it when every requirement is met.
        /// </summary>
        /// <param name="gameEvent">The event to process.</param>
        /// <param name="trigger">The trigger definition that matched the result, if any.</param>
        /// <param name="triggerResult">The simulation result that activated the event, if any.</param>
        /// <param name="results">Receives results produced by the event.</param>
        /// <param name="stateOverride">Optional target-specific schedule state.</param>
        /// <param name="contextOverride">Optional context containing a target-scope binding.</param>
        /// <param name="initializeRelativeToCurrentTick">Whether a newly armed schedule starts from the current tick.</param>
        /// <returns>True when the event activated; otherwise false.</returns>
        private bool TryActivateEvent(
            GameEvent gameEvent,
            GameTrigger trigger,
            GameResult triggerResult,
            out List<GameResult> results,
            GameEventState stateOverride = null,
            GameEvaluationContext contextOverride = null,
            bool initializeRelativeToCurrentTick = false
        )
        {
            GameEventState state =
                stateOverride ?? _game.EventRuntime.GetState(gameEvent.InstanceID);
            GameEventState eventState = _game.EventRuntime.GetState(gameEvent.InstanceID);
            if (state.IsComplete || !CanActivate(gameEvent, eventState))
            {
                results = new List<GameResult>();
                return false;
            }

            if (
                !InitializeSchedule(
                    gameEvent,
                    state,
                    initializeRelativeToCurrentTick ? _game.CurrentTick : null
                )
            )
            {
                results = new List<GameResult>();
                return false;
            }

            GameEvaluationContext context =
                contextOverride
                ?? new GameEvaluationContext(
                    gameEvent?.InstanceID,
                    triggerResult: triggerResult,
                    trigger: trigger,
                    sourceEventInstanceID: gameEvent?.InstanceID,
                    sourceKind: "Event"
                );
            foreach (GameBinding binding in gameEvent.Bindings)
                GameBindingResolver.Bind(binding, _game, _provider, context);
            if (ShouldCompleteSchedule(gameEvent, state, context))
            {
                results = new List<GameResult>();
                return false;
            }
            if (_game.CurrentTick < state.NextEligibleTick)
            {
                results = new List<GameResult>();
                return false;
            }

            if (!AreConditionsMet(gameEvent, _game, context))
            {
                results = new List<GameResult>();
                return false;
            }

            results = _actions.Execute(gameEvent.Actions, context);

            state.ActivationCount++;
            state.LastActivationTick = _game.CurrentTick;
            if (state != eventState)
            {
                eventState.ActivationCount++;
                eventState.LastActivationTick = _game.CurrentTick;
            }
            if (
                gameEvent.Schedule is GameEventSchedule configuredSchedule
                && !IsRecurring(configuredSchedule)
            )
                state.IsComplete = true;
            bool isComplete =
                HasReachedMaximumActivations(gameEvent, eventState) || state.IsComplete;
            if (!isComplete)
            {
                GetRepeatRange(gameEvent, out int minimum, out int maximum);
                state.NextEligibleTick = _game.CurrentTick + RollRange(minimum, maximum);
            }
            return true;
        }

        /// <summary>
        /// Permanently completes a recurring schedule when all of its terminal conditions are met.
        /// </summary>
        /// <param name="gameEvent">The game event.</param>
        /// <param name="state">The state.</param>
        /// <param name="context">The context.</param>
        /// <returns>True when the complete schedule condition is met; otherwise false.</returns>
        private bool ShouldCompleteSchedule(
            GameEvent gameEvent,
            GameEventState state,
            GameEvaluationContext context
        )
        {
            if (state.IsComplete)
                return true;
            GameEventSchedule schedule = gameEvent.Schedule;
            IReadOnlyList<GameConditional> until = schedule == null ? null : GetUntil(schedule);
            state.IsComplete =
                until?.Count > 0
                && until.All(condition =>
                    GameConditionEvaluator.IsMet(
                        condition,
                        new GameConditionContext(_game, context)
                    )
                );
            return state.IsComplete;
        }

        /// <summary>
        /// Marks an event complete after it reaches its authored activation limit.
        /// </summary>
        /// <param name="gameEvent">The event definition.</param>
        /// <param name="state">The event's current runtime state.</param>
        /// <returns>True when the reached maximum activations condition is met; otherwise false.</returns>
        private static bool HasReachedMaximumActivations(GameEvent gameEvent, GameEventState state)
        {
            int? maximum = gameEvent.MaximumActivations;
            if (maximum.HasValue && state.ActivationCount >= maximum.Value)
                state.IsComplete = true;
            return state.IsComplete;
        }

        /// <summary>
        /// Returns whether an event activates from simulation results instead of tick scheduling.
        /// </summary>
        /// <param name="gameEvent">The event to inspect.</param>
        /// <returns>True when a typed result trigger is configured.</returns>
        private static bool HasResultTrigger(GameEvent gameEvent) => gameEvent.Triggers.Count > 0;

        /// <summary>
        /// Initializes an event's first eligible tick from its authored schedule.
        /// </summary>
        /// <param name="gameEvent">The event definition.</param>
        /// <param name="state">The persistent runtime state to initialize.</param>
        /// <param name="relativeTick">The tick from which an interval delay begins, when present.</param>
        /// <returns>True when the schedule is initialized or was already initialized; otherwise false.</returns>
        private bool InitializeSchedule(
            GameEvent gameEvent,
            GameEventState state,
            int? relativeTick = null
        )
        {
            if (state.IsInitialized)
                return true;

            AfterEvent after = gameEvent.Schedule?.After;
            if (after != null)
            {
                GameEventState predecessor = _game.EventRuntime.GetState(after.EventInstanceID);
                if (predecessor.ActivationCount == 0)
                    return false;

                state.NextEligibleTick = checked(predecessor.LastActivationTick + after.DelayTicks);
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
                    && predecessorStates.Any(predecessor => predecessor.ActivationCount == 0)
                )
                    return false;
                List<GameEventState> completed = predecessorStates
                    .Where(predecessor => predecessor.ActivationCount > 0)
                    .ToList();
                if (completed.Count == 0)
                    return false;

                int dependencyTick = isAfterAll
                    ? completed.Max(predecessor => predecessor.LastActivationTick)
                    : completed.Min(predecessor => predecessor.LastActivationTick);
                state.NextEligibleTick = checked(dependencyTick + dependencies.DelayTicks);
                state.IsInitialized = true;
                return true;
            }

            GetInitialRange(gameEvent, out int minimum, out int maximum);
            int initialDelay = RollRange(minimum, maximum);
            state.NextEligibleTick =
                relativeTick.HasValue && gameEvent.Schedule?.At == null
                    ? checked(relativeTick.Value + initialDelay)
                    : initialDelay;
            state.IsInitialized = true;
            return true;
        }

        /// <summary>
        /// Gets the inclusive tick range for an event's first activation.
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
            GetInitialRange(gameEvent.Schedule, out minimum, out maximum);
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
            GetRepeatRange(gameEvent.Schedule, out minimum, out maximum);
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

        /// <summary>
        /// Checks whether the event remains below its authored activation limit.
        /// </summary>
        /// <param name="gameEvent">The authored event definition.</param>
        /// <param name="state">The event's current runtime state.</param>
        /// <returns>True when the event can activate.</returns>
        private static bool CanActivate(GameEvent gameEvent, GameEventState state) =>
            !state.IsComplete
            && (
                !gameEvent.MaximumActivations.HasValue
                || state.ActivationCount < gameEvent.MaximumActivations.Value
            );

        /// <summary>
        /// Returns true if all conditions accept the supplied evaluation context.
        /// </summary>
        /// <param name="gameEvent">The event whose conditions are evaluated.</param>
        /// <param name="game">The current game state.</param>
        /// <param name="context">The scoped target, trigger, state, and runtime bindings.</param>
        /// <returns>True if every conditional is satisfied.</returns>
        private static bool AreConditionsMet(
            GameEvent gameEvent,
            GameRoot game,
            GameEvaluationContext context
        )
        {
            foreach (GameConditional conditional in gameEvent.Conditionals)
            {
                if (!GameConditionEvaluator.IsMet(conditional, game, context))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Gets the inclusive delay range for an event's first activation.
        /// </summary>
        /// <param name="schedule">The authored schedule being interpreted.</param>
        /// <param name="minimum">Receives the minimum initial delay.</param>
        /// <param name="maximum">Receives the maximum initial delay.</param>
        private static void GetInitialRange(
            GameEventSchedule schedule,
            out int minimum,
            out int maximum
        )
        {
            if (schedule.At != null)
            {
                minimum = maximum = schedule.At.Tick;
                return;
            }

            if (schedule.Every != null)
            {
                minimum = maximum = schedule.Every.InitialDelayTicks;
                return;
            }

            if (schedule.After != null || schedule.AfterAll != null || schedule.AfterAny != null)
            {
                minimum = maximum =
                    (schedule.AfterAll ?? schedule.AfterAny)?.DelayTicks
                    ?? schedule.After.DelayTicks;
                return;
            }

            if (schedule.RandomDelay != null)
            {
                GetRange(schedule.RandomDelay, out minimum, out maximum);
                return;
            }

            GetRange(schedule.RandomInterval, out minimum, out maximum);
        }

        /// <summary>
        /// Gets the inclusive delay range between activations of a repeatable event.
        /// </summary>
        /// <param name="schedule">The authored schedule being interpreted.</param>
        /// <param name="minimum">Receives the minimum repeat delay.</param>
        /// <param name="maximum">Receives the maximum repeat delay.</param>
        private static void GetRepeatRange(
            GameEventSchedule schedule,
            out int minimum,
            out int maximum
        )
        {
            if (schedule.Every != null)
            {
                minimum = maximum = schedule.Every.Ticks;
                return;
            }

            GetRange(schedule.RandomInterval, out minimum, out maximum);
        }

        /// <summary>
        /// Returns the configured inclusive delay range after load-time validation.
        /// </summary>
        /// <param name="schedule">The authored schedule being interpreted.</param>
        /// <param name="minimum">Receives the minimum delay.</param>
        /// <param name="maximum">Receives the maximum delay.</param>
        private static void GetRange(RandomDelay schedule, out int minimum, out int maximum)
        {
            minimum = schedule.MinimumTicks;
            maximum = schedule.MaximumTicks;
        }

        /// <summary>
        /// Returns the configured inclusive interval range after load-time validation.
        /// </summary>
        /// <param name="schedule">The authored schedule being interpreted.</param>
        /// <param name="minimum">Receives the minimum interval.</param>
        /// <param name="maximum">Receives the maximum interval.</param>
        private static void GetRange(RandomInterval schedule, out int minimum, out int maximum)
        {
            minimum = schedule.MinimumTicks;
            maximum = schedule.MaximumTicks;
        }

        /// <summary>
        /// Returns whether the authored schedule permits repeated activations.
        /// </summary>
        /// <param name="schedule">The authored schedule.</param>
        /// <returns>Whether a recurring interval is configured.</returns>
        private static bool IsRecurring(GameEventSchedule schedule) =>
            schedule.Every != null || schedule.RandomInterval != null;

        /// <summary>
        /// Gets the conditions that permanently end the recurring schedule.
        /// </summary>
        /// <param name="schedule">The authored schedule.</param>
        /// <returns>The configured terminal conditions, or an empty collection.</returns>
        private static IReadOnlyList<GameConditional> GetUntil(GameEventSchedule schedule)
        {
            if (schedule.Every != null)
                return schedule.Every.Until;
            if (schedule.RandomInterval != null)
                return schedule.RandomInterval.Until;
            return Array.Empty<GameConditional>();
        }
    }
}
