using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.Game;
using Rebellion.Game.Results;
using Rebellion.Util.DependencyInjection;
using Rebellion.Util.Logging;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Executes the ordered tick phases and suspends or resumes them around player combat decisions.
    /// </summary>
    public sealed class GameTickProcessor
    {
        /// <summary>
        /// Identifies whether tick execution is idle, advancing a tick, or waiting for combat input.
        /// </summary>
        private enum TickExecutionState
        {
            Idle,
            Processing,
            AwaitingCombatDecision,
        }

        private readonly IServiceLocator _services;
        private readonly Func<GameEventExecutor> _getGameEventExecutor;
        private readonly Func<IEnumerable<GameResult>, bool, List<GameResult>> _processResults;
        private readonly Action<List<GameResult>> _processMessages;
        private readonly List<GameResult> _deferredMessageResults = new();
        private bool _tickInProgress;
        private TickExecutionState _tickState;

        public bool IsSettled => _tickState == TickExecutionState.Idle;
        public bool IsBusy => _tickInProgress || !IsSettled;

        public event Action TickCompleted;
        public event Action CombatDecisionRequired;
        internal event Action CombatResumed;

        /// <summary>
        /// Creates tick scheduling with current runtime dependencies and the existing result-release boundaries.
        /// The locator follows the active game when a save replaces it in place.
        /// </summary>
        /// <param name="services">Resolves commands and queries for the current game.</param>
        /// <param name="getGameEventExecutor">Returns the current game event runtime.</param>
        /// <param name="processResults">Resolves and presents one result batch at its existing release boundary.</param>
        /// <param name="processMessages">Releases the messages of an already resolved batch.</param>
        internal GameTickProcessor(
            IServiceLocator services,
            Func<GameEventExecutor> getGameEventExecutor,
            Func<IEnumerable<GameResult>, bool, List<GameResult>> processResults,
            Action<List<GameResult>> processMessages
        )
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _getGameEventExecutor =
                getGameEventExecutor
                ?? throw new ArgumentNullException(nameof(getGameEventExecutor));
            _processResults =
                processResults ?? throw new ArgumentNullException(nameof(processResults));
            _processMessages =
                processMessages ?? throw new ArgumentNullException(nameof(processMessages));
        }

        /// <summary>
        /// Clears pending combat scheduling during replacement without releasing an active iterator's guard.
        /// </summary>
        internal void Reset()
        {
            _deferredMessageResults.Clear();
            _tickState = TickExecutionState.Idle;
        }

        /// <summary>
        /// Runs one game tick.
        /// </summary>
        public void ProcessTick()
        {
            IEnumerator tick = ProcessTickIncrementally();
            try
            {
                while (tick.MoveNext()) { }
            }
            finally
            {
                (tick as IDisposable)?.Dispose();
            }
        }

        /// <summary>
        /// Runs one game tick and yields after each AI phase.
        /// </summary>
        /// <returns>A sequence containing one step per completed AI phase.</returns>
        public IEnumerator ProcessTickIncrementally()
        {
            if (
                _tickInProgress
                || _tickState != TickExecutionState.Idle
                || _services.GetService<GameRoot>().GetGameSpeed() == TickSpeed.Paused
            )
                yield break;

            _tickInProgress = true;
            _tickState = TickExecutionState.Processing;
            try
            {
                foreach (object step in ProcessTickCore())
                    yield return step;
            }
            finally
            {
                _tickInProgress = false;
                if (_tickState == TickExecutionState.Processing)
                    _tickState = TickExecutionState.Idle;
            }
        }

        /// <summary>
        /// Processes the systems contained in one game tick.
        /// </summary>
        /// <returns>A sequence containing one step per completed AI phase.</returns>
        private IEnumerable<object> ProcessTickCore()
        {
            _services.GetService<GameRoot>().CurrentTick++;
            _services.GetService<MessageCommands>().ProcessTick();
            GameLogger.Debug("Tick: " + _services.GetService<GameRoot>().CurrentTick);

            _services.GetService<FactionAutomationCommands>().ProcessTick();
            ProcessResults(_services.GetService<ResourceProductionCommands>().ProcessTick());
            ProcessResults(_services.GetService<ManufacturingCommands>().ProcessTick());
            // Refill capacity released by completed orders before tick observers render idle lanes.
            _services.GetService<FactionAutomationCommands>().ProcessTick();
            ProcessResults(_services.GetService<MaintenanceCommands>().ProcessTick());
            ProcessResults(_services.GetService<RecoveryCommands>().ProcessTick());
            ProcessResults(_services.GetService<CaptiveCommands>().ProcessTick());

            List<GameResult> movementResults = ProcessResults(
                _services.GetService<MovementCommands>().ProcessTick(),
                processMessages: false
            );

            List<GameResult> combatResults = ProcessResults(
                _services.GetService<SpaceCombatCommands>().ProcessTick(),
                processMessages: false
            );

            List<GameResult> waypointResults = ProcessAvailableWaypointContinuations();

            List<GameResult> movementPhaseResults = CombineResults(
                movementResults,
                combatResults,
                waypointResults
            );
            if (_services.GetService<SpaceCombatCommands>().HasPendingDecision)
            {
                StoreDeferredMessageResults(movementPhaseResults);
                BeginPendingCombatDecision();
                yield break;
            }

            _processMessages(movementPhaseResults);

            foreach (object step in ProcessRemainingTickPhases())
                yield return step;
        }

        /// <summary>
        /// Processes the phases that run after movement and space combat have settled.
        /// </summary>
        /// <returns>A sequence containing one step per completed AI phase.</returns>
        private IEnumerable<object> ProcessRemainingTickPhases()
        {
            ProcessResults(_services.GetService<MissionCommands>().ProcessTick());
            ProcessResults(
                _getGameEventExecutor()
                    .ProcessEvents(_services.GetService<GameRoot>().GetEventPool())
            );
            _services.GetService<NamingCommands>().ProcessTick();
            List<GameResult> aiResults = new List<GameResult>();
            foreach (
                object step in _services
                    .GetService<AIDirector>()
                    .ProcessTickIncrementally(aiResults)
            )
                yield return step;
            ProcessResults(aiResults);

            ProcessResults(_services.GetService<BlockadeCommands>().ProcessTick());
            ProcessResults(_services.GetService<PlanetaryControlCommands>().ProcessTick());
            ProcessResults(_services.GetService<UprisingCommands>().ProcessTick());

            ProcessResults(_services.GetService<ResearchCommands>().ProcessTick());
            ProcessResults(_services.GetService<JediCommands>().ProcessTick());
            ProcessResults(_services.GetService<VictoryCommands>().ProcessTick());
            _tickState = TickExecutionState.Idle;
            TickCompleted?.Invoke();
        }

        /// <summary>
        /// Resolves the pending combat encounter and resumes ticking.
        /// </summary>
        /// <param name="autoResolve">Whether to auto-resolve instead of tactical combat.</param>
        /// <returns>The space combat result generated by the encounter, when present.</returns>
        public SpaceCombatResult ResolveCombat(bool autoResolve)
        {
            List<GameResult> combatResults = _services
                .GetService<SpaceCombatCommands>()
                .ResolvePending(autoResolve);
            return CompleteCombatResolution(combatResults);
        }

        /// <summary>
        /// Resolves a pending retreat and routes its results before resuming ticks.
        /// </summary>
        /// <param name="retreatingFactionInstanceId">The faction withdrawing from combat.</param>
        /// <returns>The resulting space-combat summary, or null when retreat is unavailable.</returns>
        public SpaceCombatResult ResolveCombatRetreat(string retreatingFactionInstanceId)
        {
            List<GameResult> combatResults = _services
                .GetService<SpaceCombatCommands>()
                .ResolvePendingRetreat(retreatingFactionInstanceId);
            if (combatResults == null)
                return null;

            return CompleteCombatResolution(combatResults);
        }

        /// <summary>
        /// Routes resolved combat results and restores the tick timer.
        /// </summary>
        /// <param name="combatResults">The results produced by combat resolution.</param>
        /// <returns>The space-combat result in the routed batch, when present.</returns>
        private SpaceCombatResult CompleteCombatResolution(List<GameResult> combatResults)
        {
            _tickInProgress = true;
            _tickState = TickExecutionState.Processing;
            try
            {
                combatResults = ProcessResults(combatResults, processMessages: false);
                SpaceCombatResult completedCombat = combatResults
                    .OfType<SpaceCombatResult>()
                    .FirstOrDefault();

                List<GameResult> waypointResults = ProcessAvailableWaypointContinuations();
                List<GameResult> additionalCombatResults = ProcessResults(
                    _services.GetService<SpaceCombatCommands>().ProcessTick(),
                    processMessages: false
                );
                _deferredMessageResults.AddRange(combatResults);
                _deferredMessageResults.AddRange(waypointResults);
                _deferredMessageResults.AddRange(additionalCombatResults);

                if (_services.GetService<SpaceCombatCommands>().HasPendingDecision)
                {
                    BeginPendingCombatDecision();
                    return completedCombat;
                }

                _processMessages(TakeDeferredMessageResults());
                IEnumerator remainingTick = ProcessRemainingTickPhases().GetEnumerator();
                try
                {
                    while (remainingTick.MoveNext()) { }
                }
                finally
                {
                    (remainingTick as IDisposable)?.Dispose();
                }

                CombatResumed?.Invoke();
                return completedCombat;
            }
            finally
            {
                _tickInProgress = false;
                if (_tickState == TickExecutionState.Processing)
                    _tickState = TickExecutionState.Idle;
            }
        }

        /// <summary>
        /// Reconstructs unresolved space combat represented by loaded fleet locations without
        /// advancing the game tick.
        /// </summary>
        internal void ReconcileLoadedState()
        {
            List<GameResult> combatResults = ProcessResults(
                _services.GetService<SpaceCombatCommands>().ProcessTick(),
                processMessages: false
            );
            if (_services.GetService<SpaceCombatCommands>().HasPendingDecision)
            {
                StoreDeferredMessageResults(combatResults);
                BeginPendingCombatDecision();
                return;
            }

            _processMessages(combatResults);
        }

        /// <summary>
        /// Continues waypoint routes when no combat decision is blocking movement.
        /// </summary>
        /// <returns>The results produced while starting the next route legs.</returns>
        private List<GameResult> ProcessAvailableWaypointContinuations()
        {
            if (_services.GetService<SpaceCombatCommands>().HasPendingDecision)
                return new List<GameResult>();

            return ProcessResults(
                _services.GetService<MovementCommands>().ContinueFleetWaypointRoutes(),
                processMessages: false
            );
        }

        /// <summary>
        /// Stores movement and combat results until the pending combat decision is resolved.
        /// </summary>
        /// <param name="results">The results whose messages must wait for combat resolution.</param>
        private void StoreDeferredMessageResults(List<GameResult> results)
        {
            _deferredMessageResults.Clear();
            if (results != null)
                _deferredMessageResults.AddRange(results);
        }

        /// <summary>
        /// Suspends the active tick and notifies presentation that player combat input is required.
        /// </summary>
        private void BeginPendingCombatDecision()
        {
            _tickState = TickExecutionState.AwaitingCombatDecision;
            CombatDecisionRequired?.Invoke();
        }

        /// <summary>
        /// Returns and clears movement and combat results waiting on a combat decision.
        /// </summary>
        /// <returns>The pending message result batch.</returns>
        private List<GameResult> TakeDeferredMessageResults()
        {
            List<GameResult> results = new List<GameResult>(_deferredMessageResults);
            _deferredMessageResults.Clear();
            return results;
        }

        /// <summary>
        /// Combines result batches while preserving their original order.
        /// </summary>
        /// <param name="resultBatches">The result batches to combine.</param>
        /// <returns>A single ordered result list.</returns>
        private static List<GameResult> CombineResults(params List<GameResult>[] resultBatches)
        {
            List<GameResult> results = new List<GameResult>();
            foreach (List<GameResult> resultBatch in resultBatches)
            {
                if (resultBatch != null)
                    results.AddRange(resultBatch);
            }

            return results;
        }

        /// <summary>
        /// Releases a tick result batch using the current delivery boundary.
        /// </summary>
        /// <param name="results">The results produced by one phase.</param>
        /// <param name="processMessages">Whether messages may be generated now.</param>
        /// <returns>The batch extended with its reactions.</returns>
        private List<GameResult> ProcessResults(
            IEnumerable<GameResult> results,
            bool processMessages = true
        )
        {
            return _processResults(results, processMessages);
        }
    }
}
