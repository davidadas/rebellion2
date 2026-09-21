using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.Game;
using Rebellion.Game.Results;
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

        private readonly Func<GameRoot> _getGame;
        private readonly Func<MessageCommands> _getMessageCommands;
        private readonly Func<FactionAutomationCommands> _getFactionAutomationCommands;
        private readonly Func<ResourceProductionCommands> _getResourceProductionCommands;
        private readonly Func<ManufacturingCommands> _getManufacturingCommands;
        private readonly Func<MaintenanceCommands> _getMaintenanceCommands;
        private readonly Func<RecoveryCommands> _getRecoveryCommands;
        private readonly Func<CaptiveCommands> _getCaptiveCommands;
        private readonly Func<MovementCommands> _getMovementCommands;
        private readonly Func<SpaceCombatCommands> _getSpaceCombatCommands;
        private readonly Func<MissionCommands> _getMissionCommands;
        private readonly Func<GameEventExecutor> _getGameEventExecutor;
        private readonly Func<NamingCommands> _getNamingCommands;
        private readonly Func<AIDirector> _getAIDirector;
        private readonly Func<BlockadeCommands> _getBlockadeCommands;
        private readonly Func<PlanetaryControlCommands> _getPlanetaryControlCommands;
        private readonly Func<UprisingCommands> _getUprisingCommands;
        private readonly Func<ResearchCommands> _getResearchCommands;
        private readonly Func<JediCommands> _getJediCommands;
        private readonly Func<VictoryCommands> _getVictoryCommands;
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
        /// Providers preserve in-place game replacement while an iterator is suspended.
        /// </summary>
        /// <param name="getGame">Returns the current game graph.</param>
        /// <param name="getMessageCommands">Returns the current message runtime.</param>
        /// <param name="getFactionAutomationCommands">Returns the current faction automation runtime.</param>
        /// <param name="getResourceProductionCommands">Returns the current resource production runtime.</param>
        /// <param name="getManufacturingCommands">Returns the current manufacturing runtime.</param>
        /// <param name="getMaintenanceCommands">Returns the current maintenance runtime.</param>
        /// <param name="getRecoveryCommands">Returns the current recovery runtime.</param>
        /// <param name="getCaptiveCommands">Returns the current captive runtime.</param>
        /// <param name="getMovementCommands">Returns the current movement runtime.</param>
        /// <param name="getSpaceCombatCommands">Returns the current space combat runtime.</param>
        /// <param name="getMissionCommands">Returns the current mission runtime.</param>
        /// <param name="getGameEventExecutor">Returns the current game event runtime.</param>
        /// <param name="getNamingCommands">Returns the current naming runtime.</param>
        /// <param name="getAIDirector">Returns the current ai runtime.</param>
        /// <param name="getBlockadeCommands">Returns the current blockade runtime.</param>
        /// <param name="getPlanetaryControlCommands">Returns the current planetary control runtime.</param>
        /// <param name="getUprisingCommands">Returns the current uprising runtime.</param>
        /// <param name="getResearchCommands">Returns the current research runtime.</param>
        /// <param name="getJediCommands">Returns the current jedi runtime.</param>
        /// <param name="getVictoryCommands">Returns the current victory runtime.</param>
        /// <param name="processResults">Resolves and presents one result batch at its existing release boundary.</param>
        /// <param name="processMessages">Releases the messages of an already resolved batch.</param>
        internal GameTickProcessor(
            Func<GameRoot> getGame,
            Func<MessageCommands> getMessageCommands,
            Func<FactionAutomationCommands> getFactionAutomationCommands,
            Func<ResourceProductionCommands> getResourceProductionCommands,
            Func<ManufacturingCommands> getManufacturingCommands,
            Func<MaintenanceCommands> getMaintenanceCommands,
            Func<RecoveryCommands> getRecoveryCommands,
            Func<CaptiveCommands> getCaptiveCommands,
            Func<MovementCommands> getMovementCommands,
            Func<SpaceCombatCommands> getSpaceCombatCommands,
            Func<MissionCommands> getMissionCommands,
            Func<GameEventExecutor> getGameEventExecutor,
            Func<NamingCommands> getNamingCommands,
            Func<AIDirector> getAIDirector,
            Func<BlockadeCommands> getBlockadeCommands,
            Func<PlanetaryControlCommands> getPlanetaryControlCommands,
            Func<UprisingCommands> getUprisingCommands,
            Func<ResearchCommands> getResearchCommands,
            Func<JediCommands> getJediCommands,
            Func<VictoryCommands> getVictoryCommands,
            Func<IEnumerable<GameResult>, bool, List<GameResult>> processResults,
            Action<List<GameResult>> processMessages
        )
        {
            _getGame = getGame ?? throw new ArgumentNullException(nameof(getGame));
            _getMessageCommands =
                getMessageCommands ?? throw new ArgumentNullException(nameof(getMessageCommands));
            _getFactionAutomationCommands =
                getFactionAutomationCommands
                ?? throw new ArgumentNullException(nameof(getFactionAutomationCommands));
            _getResourceProductionCommands =
                getResourceProductionCommands
                ?? throw new ArgumentNullException(nameof(getResourceProductionCommands));
            _getManufacturingCommands =
                getManufacturingCommands
                ?? throw new ArgumentNullException(nameof(getManufacturingCommands));
            _getMaintenanceCommands =
                getMaintenanceCommands
                ?? throw new ArgumentNullException(nameof(getMaintenanceCommands));
            _getRecoveryCommands =
                getRecoveryCommands ?? throw new ArgumentNullException(nameof(getRecoveryCommands));
            _getCaptiveCommands =
                getCaptiveCommands ?? throw new ArgumentNullException(nameof(getCaptiveCommands));
            _getMovementCommands =
                getMovementCommands ?? throw new ArgumentNullException(nameof(getMovementCommands));
            _getSpaceCombatCommands =
                getSpaceCombatCommands
                ?? throw new ArgumentNullException(nameof(getSpaceCombatCommands));
            _getMissionCommands =
                getMissionCommands ?? throw new ArgumentNullException(nameof(getMissionCommands));
            _getGameEventExecutor =
                getGameEventExecutor
                ?? throw new ArgumentNullException(nameof(getGameEventExecutor));
            _getNamingCommands =
                getNamingCommands ?? throw new ArgumentNullException(nameof(getNamingCommands));
            _getAIDirector =
                getAIDirector ?? throw new ArgumentNullException(nameof(getAIDirector));
            _getBlockadeCommands =
                getBlockadeCommands ?? throw new ArgumentNullException(nameof(getBlockadeCommands));
            _getPlanetaryControlCommands =
                getPlanetaryControlCommands
                ?? throw new ArgumentNullException(nameof(getPlanetaryControlCommands));
            _getUprisingCommands =
                getUprisingCommands ?? throw new ArgumentNullException(nameof(getUprisingCommands));
            _getResearchCommands =
                getResearchCommands ?? throw new ArgumentNullException(nameof(getResearchCommands));
            _getJediCommands =
                getJediCommands ?? throw new ArgumentNullException(nameof(getJediCommands));
            _getVictoryCommands =
                getVictoryCommands ?? throw new ArgumentNullException(nameof(getVictoryCommands));
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
                || _getGame().GetGameSpeed() == TickSpeed.Paused
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
            _getGame().CurrentTick++;
            _getMessageCommands().ProcessTick();
            GameLogger.Debug("Tick: " + _getGame().CurrentTick);

            _getFactionAutomationCommands().ProcessTick();
            ProcessResults(_getResourceProductionCommands().ProcessTick());
            ProcessResults(_getManufacturingCommands().ProcessTick());
            // Refill capacity released by completed orders before tick observers render idle lanes.
            _getFactionAutomationCommands().ProcessTick();
            ProcessResults(_getMaintenanceCommands().ProcessTick());
            ProcessResults(_getRecoveryCommands().ProcessTick());
            ProcessResults(_getCaptiveCommands().ProcessTick());

            List<GameResult> movementResults = ProcessResults(
                _getMovementCommands().ProcessTick(),
                processMessages: false
            );

            List<GameResult> combatResults = ProcessResults(
                _getSpaceCombatCommands().ProcessTick(),
                processMessages: false
            );

            List<GameResult> waypointResults = ProcessAvailableWaypointContinuations();

            List<GameResult> movementPhaseResults = CombineResults(
                movementResults,
                combatResults,
                waypointResults
            );
            if (_getSpaceCombatCommands().HasPendingDecision)
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
            ProcessResults(_getMissionCommands().ProcessTick());
            ProcessResults(_getGameEventExecutor().ProcessEvents(_getGame().GetEventPool()));
            _getNamingCommands().ProcessTick();
            List<GameResult> aiResults = new List<GameResult>();
            foreach (object step in _getAIDirector().ProcessTickIncrementally(aiResults))
                yield return step;
            ProcessResults(aiResults);

            ProcessResults(_getBlockadeCommands().ProcessTick());
            ProcessResults(_getPlanetaryControlCommands().ProcessTick());
            ProcessResults(_getUprisingCommands().ProcessTick());

            ProcessResults(_getResearchCommands().ProcessTick());
            ProcessResults(_getJediCommands().ProcessTick());
            ProcessResults(_getVictoryCommands().ProcessTick());
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
            List<GameResult> combatResults = _getSpaceCombatCommands().ResolvePending(autoResolve);
            return CompleteCombatResolution(combatResults);
        }

        /// <summary>
        /// Resolves a pending retreat and routes its results before resuming ticks.
        /// </summary>
        /// <param name="retreatingFactionInstanceId">The faction withdrawing from combat.</param>
        /// <returns>The resulting space-combat summary, or null when retreat is unavailable.</returns>
        public SpaceCombatResult ResolveCombatRetreat(string retreatingFactionInstanceId)
        {
            List<GameResult> combatResults = _getSpaceCombatCommands()
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
                    _getSpaceCombatCommands().ProcessTick(),
                    processMessages: false
                );
                _deferredMessageResults.AddRange(combatResults);
                _deferredMessageResults.AddRange(waypointResults);
                _deferredMessageResults.AddRange(additionalCombatResults);

                if (_getSpaceCombatCommands().HasPendingDecision)
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
                _getSpaceCombatCommands().ProcessTick(),
                processMessages: false
            );
            if (_getSpaceCombatCommands().HasPendingDecision)
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
            if (_getSpaceCombatCommands().HasPendingDecision)
                return new List<GameResult>();

            return ProcessResults(
                _getMovementCommands().ContinueFleetWaypointRoutes(),
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
