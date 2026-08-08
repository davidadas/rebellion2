using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Events;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
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
            ReconcileEffects(gameEvents);
            List<GameResult> allResults = new List<GameResult>();
            List<GameEvent> eventsToRemove = new List<GameEvent>();

            foreach (GameEvent gameEvent in gameEvents.ToArray())
            {
                if (gameEvent is ForceDiscoveryRule)
                    continue;

                if (!string.IsNullOrWhiteSpace(gameEvent.TriggerResultType))
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

        private void ReconcileEffects(IReadOnlyList<GameEvent> gameEvents)
        {
            HashSet<string> activeModifierKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (GameEvent gameEvent in gameEvents)
            {
                if (gameEvent?.Effects == null)
                    continue;

                for (int index = 0; index < gameEvent.Effects.Count; index++)
                {
                    GameEffect effect = gameEvent.Effects[index];
                    if (effect == null)
                        continue;

                    string modifierKey =
                        $"{GameEffect.ModifierKeyPrefix}{gameEvent.InstanceID}:effect:{index}";
                    activeModifierKeys.Add(modifierKey);
                    effect.Reconcile(_game, modifierKey);
                }
            }

            foreach (Officer officer in _game.GetRegisteredSceneNodesByType<Officer>())
            {
                officer.RemoveRatingModifiersExcept(
                    GameEffect.ModifierKeyPrefix,
                    activeModifierKeys
                );
            }
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
                    if (gameEvent is ForceDiscoveryRule)
                        continue;

                    if (
                        !string.Equals(
                            gameEvent.TriggerResultType,
                            triggerResult.GetType().Name,
                            StringComparison.Ordinal
                        )
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

            if (!gameEvent.AreConditionsMet(_game, triggerResult))
            {
                results = new List<GameResult>();
                return false;
            }

            GameLogger.Log($"Executing game event: {gameEvent.GetDisplayName()}");
            GameEventExecutionContext context = new GameEventExecutionContext(
                gameEvent,
                state,
                scopeTarget,
                triggerResult
            );
            results = gameEvent.Execute(_game, _provider, context);
            state.ExecutionCount++;
            state.LastExecutionTick = _game.CurrentTick;
            if (gameEvent.IsRepeatable)
            {
                state.NextEligibleTick =
                    _game.CurrentTick
                    + RollDelay(gameEvent.RepeatDelayTicks, gameEvent.RepeatDelayRandomTicks);
            }
            _game.AddCompletedEvent(gameEvent);
            return true;
        }

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
        /// Initializes an event's absolute first eligible tick from its data-defined delay.
        /// </summary>
        /// <param name="gameEvent">The event definition.</param>
        /// <param name="state">The persistent runtime state to initialize.</param>
        /// <param name="initializeFromCurrentTick">
        /// Whether the schedule begins relative to the current game tick.
        /// </param>
        private void InitializeSchedule(
            GameEvent gameEvent,
            GameEventState state,
            bool initializeFromCurrentTick
        )
        {
            if (state.IsInitialized)
                return;

            state.NextEligibleTick =
                (initializeFromCurrentTick ? _game.CurrentTick : 0)
                + RollDelay(gameEvent.InitialDelayTicks, gameEvent.InitialDelayRandomTicks);
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
        /// Rolls a non-negative base delay plus an inclusive zero-based random spread.
        /// </summary>
        /// <param name="baseTicks">The guaranteed delay.</param>
        /// <param name="randomTicks">The maximum additional random delay.</param>
        /// <returns>The rolled delay in ticks.</returns>
        private int RollDelay(int baseTicks, int randomTicks)
        {
            if (baseTicks < 0)
                throw new InvalidOperationException("Game event delays cannot be negative.");
            if (randomTicks < 0)
                throw new InvalidOperationException("Game event random delays cannot be negative.");

            return baseTicks + (randomTicks == 0 ? 0 : _provider.NextInt(0, randomTicks + 1));
        }
    }
}
