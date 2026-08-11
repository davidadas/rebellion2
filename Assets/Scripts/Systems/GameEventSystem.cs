using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Events;
using Rebellion.Game.Galaxy;
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
                if (HasResultTrigger(gameEvent))
                    continue;

                if (gameEvent.Scope == GameEventScope.EachPlanet)
                {
                    foreach (Planet planet in GetPlanetScopeCandidates(gameEvent))
                    {
                        if (!IsPlanetScopeEligible(gameEvent, planet))
                        {
                            DeactivatePlanetScope(gameEvent, planet);
                            continue;
                        }

                        ActivatePlanetScope(gameEvent, planet);
                        if (
                            TryProcessEvent(
                                gameEvent,
                                null,
                                null,
                                planet,
                                out List<GameResult> scopedResults
                            )
                        )
                            allResults.AddRange(scopedResults);
                    }
                }
                else if (
                    TryProcessEvent(gameEvent, null, null, null, out List<GameResult> globalResults)
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

            foreach (GameResult triggerResult in results)
            {
                foreach (GameEvent gameEvent in _game.GetEventPool().ToArray())
                {
                    foreach (GameEventTrigger trigger in gameEvent.Triggers.ToArray())
                    {
                        if (
                            !GameEventTriggerRegistry.Matches(trigger.Event, triggerResult)
                            || !TryProcessEvent(
                                gameEvent,
                                trigger,
                                triggerResult,
                                null,
                                out List<GameResult> reactions
                            )
                        )
                            continue;

                        eventResults.AddRange(reactions);
                        if (!gameEvent.Repeats)
                            _game.RemoveEvent(gameEvent);
                        break;
                    }
                }
            }

            return eventResults;
        }

        /// <summary>
        /// Executes a single game event if its conditions are met.
        /// </summary>
        /// <param name="gameEvent">The event to process.</param>
        /// <param name="trigger">The trigger definition that matched the result, if any.</param>
        /// <param name="triggerResult">The simulation result that activated the event, if any.</param>
        /// <param name="scopeTarget">The planet whose independent schedule is being processed.</param>
        /// <param name="results">Receives results produced by the event.</param>
        /// <returns>True when the event executed; otherwise false.</returns>
        private bool TryProcessEvent(
            GameEvent gameEvent,
            GameEventTrigger trigger,
            GameResult triggerResult,
            Planet scopeTarget,
            out List<GameResult> results
        )
        {
            GameEventState state =
                scopeTarget == null
                    ? _game.GetEventState(gameEvent.InstanceID)
                    : _game.GetEventState(gameEvent.InstanceID, scopeTarget.InstanceID);
            if (!InitializeSchedule(gameEvent, state, scopeTarget != null))
            {
                results = new List<GameResult>();
                return false;
            }
            if (_game.CurrentTick < state.NextEligibleTick)
            {
                results = new List<GameResult>();
                return false;
            }

            ISceneNode executionTarget = scopeTarget ?? gameEvent.Target?.Resolve(_game, _provider);
            if (gameEvent.Target != null && executionTarget == null)
            {
                results = new List<GameResult>();
                return false;
            }

            GameEventExecutionContext context = new GameEventExecutionContext(
                gameEvent,
                state,
                executionTarget,
                triggerResult,
                trigger
            );
            if (!gameEvent.AreConditionsMet(_game, context))
            {
                results = new List<GameResult>();
                return false;
            }

            GameLogger.Log($"Executing game event: {gameEvent.GetDisplayName()}");
            results = gameEvent.Execute(_game, _provider, context);
            state.ExecutionCount++;
            state.LastExecutionTick = _game.CurrentTick;
            if (gameEvent.Repeats)
            {
                GetRepeatRange(gameEvent, out int minimum, out int maximum);
                state.NextEligibleTick = _game.CurrentTick + RollRange(minimum, maximum);
            }
            _game.AddCompletedEvent(gameEvent);
            return true;
        }

        /// <summary>
        /// Returns whether an event activates from simulation results instead of tick scheduling.
        /// </summary>
        /// <param name="gameEvent">The event to inspect.</param>
        /// <returns>True when a stable or legacy trigger is configured.</returns>
        private static bool HasResultTrigger(GameEvent gameEvent) => gameEvent.Triggers.Count > 0;

        /// <summary>
        /// Initializes an event's absolute first eligible tick from its authored schedule.
        /// </summary>
        /// <param name="gameEvent">The event definition.</param>
        /// <param name="state">The persistent runtime state to initialize.</param>
        /// <param name="relativeToCurrentTick">
        /// Whether the first delay begins at the current tick instead of campaign tick zero.
        /// </param>
        private bool InitializeSchedule(
            GameEvent gameEvent,
            GameEventState state,
            bool relativeToCurrentTick
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
                GameEventState predecessor = _game.GetEventState(after.EventInstanceID);
                if (predecessor.ExecutionCount == 0)
                    return false;

                state.NextEligibleTick = checked(predecessor.LastExecutionTick + after.DelayTicks);
                state.IsInitialized = true;
                return true;
            }

            GetInitialRange(gameEvent, out int minimum, out int maximum);
            state.NextEligibleTick =
                (relativeToCurrentTick ? _game.CurrentTick : 0) + RollRange(minimum, maximum);
            state.IsInitialized = true;
            return true;
        }

        /// <summary>
        /// Enumerates surviving planets that satisfy the event's optional system-type filter.
        /// </summary>
        /// <param name="gameEvent">The scoped event definition.</param>
        /// <returns>Candidate planets in stable instance-ID order.</returns>
        private IEnumerable<Planet> GetPlanetScopeCandidates(GameEvent gameEvent)
        {
            return _game
                .GetGalaxyMap()
                .PlanetSystems.Where(system =>
                    !gameEvent.FilterPlanetScopeSystemType
                    || system.SystemType == gameEvent.PlanetScopeSystemType
                )
                .SelectMany(system => system.Planets)
                .Where(planet => !planet.IsDestroyed)
                .OrderBy(planet => planet.InstanceID, StringComparer.Ordinal);
        }

        /// <summary>
        /// Returns whether a planet currently satisfies the event's ownership filter.
        /// </summary>
        /// <param name="gameEvent">The scoped event definition.</param>
        /// <param name="planet">The planet to evaluate.</param>
        /// <returns>True when the scope remains eligible.</returns>
        private static bool IsPlanetScopeEligible(GameEvent gameEvent, Planet planet)
        {
            return gameEvent.PlanetScopeOwnership switch
            {
                PlanetScopeOwnership.Owned => !string.IsNullOrWhiteSpace(planet.OwnerInstanceID),
                PlanetScopeOwnership.Neutral => string.IsNullOrWhiteSpace(planet.OwnerInstanceID),
                _ => true,
            };
        }

        /// <summary>
        /// Marks one planet's independent event schedule as active.
        /// </summary>
        /// <param name="gameEvent">The scoped event definition.</param>
        /// <param name="planet">The active scope planet.</param>
        private void ActivatePlanetScope(GameEvent gameEvent, Planet planet)
        {
            GameEventState state = _game.GetEventState(gameEvent.InstanceID, planet.InstanceID);
            state.IsScopeActive = true;
        }

        /// <summary>
        /// Disarms one planet's schedule so renewed eligibility starts a fresh delay.
        /// </summary>
        /// <param name="gameEvent">The scoped event definition.</param>
        /// <param name="planet">The ineligible scope planet.</param>
        private void DeactivatePlanetScope(GameEvent gameEvent, Planet planet)
        {
            if (
                !_game.TryGetEventState(
                    gameEvent.InstanceID,
                    planet.InstanceID,
                    out GameEventState state
                )
            )
                return;

            state.IsScopeActive = false;
            state.IsInitialized = false;
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
