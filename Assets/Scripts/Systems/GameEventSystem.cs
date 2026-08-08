using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Events;
using Rebellion.Game.Results;
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
                if (
                    !string.IsNullOrWhiteSpace(gameEvent.TriggerResultType)
                    || !TryProcessEvent(gameEvent, null, out List<GameResult> results)
                )
                    continue;

                allResults.AddRange(results);
                if (!gameEvent.IsRepeatable)
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

            foreach (GameResult triggerResult in results)
            {
                foreach (GameEvent gameEvent in _game.GetEventPool().ToArray())
                {
                    if (
                        !string.Equals(
                            gameEvent.TriggerResultType,
                            triggerResult.GetType().Name,
                            StringComparison.Ordinal
                        )
                        || !TryProcessEvent(
                            gameEvent,
                            triggerResult,
                            out List<GameResult> reactions
                        )
                    )
                        continue;

                    eventResults.AddRange(reactions);
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
        /// <param name="results">Receives results produced by the event.</param>
        /// <returns>True when the event executed; otherwise false.</returns>
        private bool TryProcessEvent(
            GameEvent gameEvent,
            GameResult triggerResult,
            out List<GameResult> results
        )
        {
            GameEventState state = _game.GetEventState(gameEvent.InstanceID);
            InitializeSchedule(gameEvent, state);
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
            results = gameEvent.Execute(_game, _provider);
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

        /// <summary>
        /// Initializes an event's absolute first eligible tick from its data-defined delay.
        /// </summary>
        /// <param name="gameEvent">The event definition.</param>
        /// <param name="state">The persistent runtime state to initialize.</param>
        private void InitializeSchedule(GameEvent gameEvent, GameEventState state)
        {
            if (state.IsInitialized)
                return;

            state.NextEligibleTick = RollDelay(
                gameEvent.InitialDelayTicks,
                gameEvent.InitialDelayRandomTicks
            );
            state.IsInitialized = true;
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
