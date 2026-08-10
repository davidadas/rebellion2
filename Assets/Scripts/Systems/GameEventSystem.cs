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
                                planet,
                                out List<GameResult> scopedResults
                            )
                        )
                            allResults.AddRange(scopedResults);
                    }
                }
                else if (TryProcessEvent(gameEvent, null, null, out List<GameResult> globalResults))
                {
                    allResults.AddRange(globalResults);
                    if (!gameEvent.IsRepeatable)
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
                    if (
                        !MatchesTrigger(gameEvent, triggerResult)
                        || !TryProcessEvent(
                            gameEvent,
                            triggerResult,
                            null,
                            out List<GameResult> reactions
                        )
                    )
                        continue;

                    eventResults.AddRange(reactions);
                    if (gameEvent.SuppressTriggerMessage)
                        triggerResult.SuppressDefaultMessage = true;
                    SuppressSourceMessages(gameEvent, triggerResult, results);
                    if (!gameEvent.IsRepeatable)
                        _game.RemoveEvent(gameEvent);
                }
            }

            return eventResults;
        }

        /// <summary>
        /// Executes a single game event if its conditions are met.
        /// </summary>
        /// <param name="gameEvent">The event to process.</param>
        /// <param name="triggerResult">The simulation result that activated the event, if any.</param>
        /// <param name="scopeTarget">The planet whose independent schedule is being processed.</param>
        /// <param name="results">Receives results produced by the event.</param>
        /// <returns>True when the event executed; otherwise false.</returns>
        private bool TryProcessEvent(
            GameEvent gameEvent,
            GameResult triggerResult,
            Planet scopeTarget,
            out List<GameResult> results
        )
        {
            GameEventState state =
                scopeTarget == null
                    ? _game.GetEventState(gameEvent.InstanceID)
                    : _game.GetEventState(gameEvent.InstanceID, scopeTarget.InstanceID);
            InitializeSchedule(gameEvent, state, scopeTarget != null);
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
                triggerResult
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
            if (gameEvent.IsRepeatable)
            {
                GetRepeatRange(gameEvent, out int minimum, out int maximum);
                state.NextEligibleTick = _game.CurrentTick + RollRange(minimum, maximum);
            }
            _game.AddCompletedEvent(gameEvent);
            return true;
        }

        private static bool HasResultTrigger(GameEvent gameEvent) =>
            !string.IsNullOrWhiteSpace(gameEvent.Trigger)
            || !string.IsNullOrWhiteSpace(gameEvent.TriggerResultType);

        private static bool MatchesTrigger(GameEvent gameEvent, GameResult result) =>
            !string.IsNullOrWhiteSpace(gameEvent.Trigger)
                ? GameEventTriggerRegistry.Matches(gameEvent.Trigger, result)
                : GameEventTriggerRegistry.MatchesLegacyTypeName(
                    gameEvent.TriggerResultType,
                    result
                );

        private static void SuppressSourceMessages(
            GameEvent gameEvent,
            GameResult triggerResult,
            IReadOnlyList<GameResult> sourceResults
        )
        {
            if (!gameEvent.SuppressSourceMessages || triggerResult == null)
                return;

            string sourceEventId = triggerResult.SourceEventInstanceID;
            foreach (GameResult result in sourceResults)
            {
                if (
                    result != null
                    && (
                        string.IsNullOrWhiteSpace(sourceEventId)
                            ? ReferenceEquals(result, triggerResult)
                            : result.SourceEventInstanceID == sourceEventId
                    )
                )
                    result.SuppressDefaultMessage = true;
            }
        }

        /// <summary>
        /// Initializes an event's absolute first eligible tick from its authored schedule.
        /// </summary>
        /// <param name="gameEvent">The event definition.</param>
        /// <param name="state">The persistent runtime state to initialize.</param>
        /// <param name="relativeToCurrentTick">
        /// Whether the first delay begins at the current tick instead of campaign tick zero.
        /// </param>
        private void InitializeSchedule(
            GameEvent gameEvent,
            GameEventState state,
            bool relativeToCurrentTick
        )
        {
            if (state.IsInitialized)
                return;

            GetInitialRange(gameEvent, out int minimum, out int maximum);
            state.NextEligibleTick =
                (relativeToCurrentTick ? _game.CurrentTick : 0) + RollRange(minimum, maximum);
            state.IsInitialized = true;
        }

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

        private static bool IsPlanetScopeEligible(GameEvent gameEvent, Planet planet)
        {
            return gameEvent.PlanetScopeOwnership switch
            {
                PlanetScopeOwnership.Owned => !string.IsNullOrWhiteSpace(planet.OwnerInstanceID),
                PlanetScopeOwnership.Neutral => string.IsNullOrWhiteSpace(planet.OwnerInstanceID),
                _ => true,
            };
        }

        private void ActivatePlanetScope(GameEvent gameEvent, Planet planet)
        {
            GameEventState state = _game.GetEventState(gameEvent.InstanceID, planet.InstanceID);
            state.IsScopeActive = true;
        }

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

        private static void GetRepeatRange(GameEvent gameEvent, out int minimum, out int maximum)
        {
            if (gameEvent.Schedule == null)
            {
                minimum = maximum = 0;
                return;
            }
            gameEvent.Schedule.GetRepeatRange(out minimum, out maximum);
        }

        private int RollRange(int minimum, int maximum)
        {
            return minimum == maximum ? minimum : _provider.NextInt(minimum, maximum + 1);
        }
    }
}
