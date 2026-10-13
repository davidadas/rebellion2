using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

        private readonly Func<IEnumerable<GameResult>, bool, List<GameResult>> _processResults;
        private readonly Action<List<GameResult>> _processMessages;
        private readonly List<BlockadeChangedResult> _deferredBlockadeResults = new();
        private readonly List<GameResult> _deferredMessageResults = new();
        private GameTickRuntime _runtime;
        private bool _tickInProgress;
        private TickExecutionState _tickState;

        public bool IsSettled => _tickState == TickExecutionState.Idle;
        public bool IsBusy => _tickInProgress || !IsSettled;

        public event Action TickCompleted;

        /// <summary>
        /// Raised when an incremental tick step changes state that presentation may render.
        /// </summary>
        public event Action TickProgressed;

        public event Action CombatDecisionRequired;
        internal event Action CombatResumed;

        /// <summary>
        /// Creates tick scheduling with the existing result-release boundaries.
        /// </summary>
        /// <param name="processResults">Resolves and presents one result batch at its existing release boundary.</param>
        /// <param name="processMessages">Releases the messages of an already resolved batch.</param>
        internal GameTickProcessor(
            Func<IEnumerable<GameResult>, bool, List<GameResult>> processResults,
            Action<List<GameResult>> processMessages
        )
        {
            _processResults =
                processResults ?? throw new ArgumentNullException(nameof(processResults));
            _processMessages =
                processMessages ?? throw new ArgumentNullException(nameof(processMessages));
        }

        /// <summary>
        /// Prepares and replaces the feature processors used by subsequent ticks.
        /// </summary>
        /// <param name="services">The game's runtime service scope.</param>
        internal void ConnectRuntime(IServiceLocator services)
        {
            ConnectRuntime(new GameTickRuntime(services));
        }

        /// <summary>
        /// Publishes an already prepared runtime without constructing or resolving services.
        /// </summary>
        /// <param name="runtime">The complete replacement tick dependencies.</param>
        internal void ConnectRuntime(GameTickRuntime runtime)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        /// <summary>
        /// Clears pending combat scheduling during replacement without releasing an active iterator's guard.
        /// </summary>
        internal void Reset()
        {
            _deferredBlockadeResults.Clear();
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
                || _runtime.Game.GetGameSpeed() == TickSpeed.Paused
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
            _runtime.Game.CurrentTick++;
            _runtime.Messages.ProcessTick(_runtime.Game);
            GameLogger.Debug("Tick: " + _runtime.Game.CurrentTick);

            _runtime.FactionAutomation.ProcessTick(_runtime.Game);
            ProcessResults(_runtime.ResourceProduction.ProcessTick(_runtime.Game));
            List<GameResult> manufacturingResults = new List<GameResult>();
            foreach (
                IReadOnlyList<GameResult> manufacturingStep in _runtime.Manufacturing.ProcessTickIncrementally(
                    _runtime.Game
                )
            )
            {
                manufacturingResults.AddRange(manufacturingStep);
                if (!manufacturingStep.OfType<ManufacturingPointsCompletedResult>().Any())
                    continue;

                TickProgressed?.Invoke();
                yield return null;
            }
            ProcessResults(manufacturingResults);
            // Refill capacity released by completed orders before tick observers render idle lanes.
            _runtime.FactionAutomation.ProcessTick(_runtime.Game);
            ProcessResults(_runtime.Maintenance.ProcessTick(_runtime.Game));
            ProcessResults(_runtime.Recovery.ProcessTick(_runtime.Game));
            ProcessResults(_runtime.Captive.ProcessTick(_runtime.Game));

            List<BlockadeChangedResult> blockadeTransitions = new List<BlockadeChangedResult>();
            List<GameResult> movementResults = ProcessMovementPhaseResults(
                _runtime.Movement.ProcessTick(_runtime.Game),
                blockadeTransitions
            );

            List<GameResult> combatResults = ProcessMovementPhaseResults(
                _runtime.SpaceCombat.ProcessTick(_runtime.Game),
                blockadeTransitions
            );

            List<GameResult> waypointResults = ProcessAvailableWaypointContinuations(
                blockadeTransitions
            );
            List<GameResult> blockadeResults = ResolveMovementPhaseBlockadeTransitions(
                blockadeTransitions
            );
            _runtime.FogOfWarObserver.RefreshVisibleKnowledge();

            List<GameResult> movementPhaseResults = CombineResults(
                movementResults,
                combatResults,
                waypointResults,
                blockadeResults
            );
            if (_runtime.SpaceCombatCommands.HasPendingDecision)
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
            ProcessMissionPhase();
            ProcessResults(_runtime.GameEvents.ProcessTick(_runtime.Game));
            _runtime.Naming.ProcessTick(_runtime.Game);
            List<GameResult> aiResults = new List<GameResult>();
            foreach (object step in _runtime.AIDirector.ProcessTickIncrementally(aiResults))
                yield return step;
            ProcessResults(aiResults);
            ProcessDeferredBlockadeResults();

            ProcessResults(_runtime.PlanetaryControl.ProcessTick(_runtime.Game));
            ProcessResults(_runtime.Uprising.ProcessTick(_runtime.Game));

            ProcessResults(_runtime.Research.ProcessTick(_runtime.Game));
            ProcessResults(_runtime.Jedi.ProcessTick(_runtime.Game));
            ProcessResults(_runtime.Victory.ProcessTick(_runtime.Game));
            _runtime.FogOfWarObserver.RefreshVisibleKnowledge();
            _tickState = TickExecutionState.Idle;
            TickCompleted?.Invoke();
        }

        /// <summary>
        /// Settles each mission before advancing the next while deferring their messages until the phase ends.
        /// </summary>
        private void ProcessMissionPhase()
        {
            List<GameResult> orderedResults = new List<GameResult>();
            List<GameResult> reactionResults = new List<GameResult>();
            List<GameResult> deferredResults = new List<GameResult>();
            foreach (
                (
                    IReadOnlyList<GameResult> Results,
                    bool RequiresImmediateSettlement
                ) batch in _runtime.Missions.ProcessTickIncrementally(_runtime.Game)
            )
            {
                List<GameResult> sourceResults = batch
                    .Results.Where(result => result != null)
                    .ToList();
                orderedResults.AddRange(sourceResults);
                if (!batch.RequiresImmediateSettlement)
                {
                    deferredResults.AddRange(sourceResults);
                    continue;
                }

                List<GameResult> settledResults = ProcessResults(
                    sourceResults,
                    processMessages: false
                );
                reactionResults.AddRange(settledResults.Skip(sourceResults.Count));
            }

            if (deferredResults.Count > 0)
            {
                List<GameResult> settledDeferredResults = ProcessResults(
                    deferredResults,
                    processMessages: false
                );
                reactionResults.AddRange(settledDeferredResults.Skip(deferredResults.Count));
            }

            orderedResults.AddRange(reactionResults);
            _processMessages(orderedResults);
        }

        /// <summary>
        /// Resolves the pending combat encounter and resumes ticking.
        /// </summary>
        /// <param name="autoResolve">Whether to auto-resolve instead of tactical combat.</param>
        /// <returns>The space combat result generated by the encounter, when present.</returns>
        public SpaceCombatResult ResolveCombat(bool autoResolve)
        {
            List<GameResult> combatResults = _runtime.SpaceCombatCommands.ResolvePending(
                autoResolve
            );
            return CompleteCombatResolution(combatResults);
        }

        /// <summary>
        /// Resolves a pending retreat and routes its results before resuming ticks.
        /// </summary>
        /// <param name="retreatingFactionInstanceId">The faction withdrawing from combat.</param>
        /// <returns>The resulting space-combat summary, or null when retreat is unavailable.</returns>
        public SpaceCombatResult ResolveCombatRetreat(string retreatingFactionInstanceId)
        {
            List<GameResult> combatResults = _runtime.SpaceCombatCommands.ResolvePendingRetreat(
                retreatingFactionInstanceId
            );
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
                List<BlockadeChangedResult> blockadeTransitions = new List<BlockadeChangedResult>();
                combatResults = ProcessMovementPhaseResults(combatResults, blockadeTransitions);
                SpaceCombatResult completedCombat = combatResults
                    .OfType<SpaceCombatResult>()
                    .FirstOrDefault();

                List<GameResult> waypointResults = ProcessAvailableWaypointContinuations(
                    blockadeTransitions
                );
                List<GameResult> additionalCombatResults = ProcessMovementPhaseResults(
                    _runtime.SpaceCombat.ProcessTick(_runtime.Game),
                    blockadeTransitions
                );
                List<GameResult> blockadeResults = ResolveMovementPhaseBlockadeTransitions(
                    blockadeTransitions
                );
                _runtime.FogOfWarObserver.RefreshVisibleKnowledge();
                _deferredMessageResults.AddRange(combatResults);
                _deferredMessageResults.AddRange(waypointResults);
                _deferredMessageResults.AddRange(additionalCombatResults);
                _deferredMessageResults.AddRange(blockadeResults);

                if (_runtime.SpaceCombatCommands.HasPendingDecision)
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
            List<BlockadeChangedResult> blockadeTransitions = new List<BlockadeChangedResult>();
            List<GameResult> combatResults = ProcessMovementPhaseResults(
                _runtime.SpaceCombat.ProcessTick(_runtime.Game),
                blockadeTransitions
            );
            combatResults.AddRange(ResolveMovementPhaseBlockadeTransitions(blockadeTransitions));
            _runtime.FogOfWarObserver.RefreshVisibleKnowledge();
            if (_runtime.SpaceCombatCommands.HasPendingDecision)
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
        /// <param name="blockadeTransitions">The collection receiving unpublished blockade transitions.</param>
        /// <returns>The results produced while starting the next route legs.</returns>
        private List<GameResult> ProcessAvailableWaypointContinuations(
            ICollection<BlockadeChangedResult> blockadeTransitions
        )
        {
            if (_runtime.SpaceCombatCommands.HasPendingDecision)
                return new List<GameResult>();

            return ProcessMovementPhaseResults(
                _runtime.MovementCommands.ContinueFleetWaypointRoutes(),
                blockadeTransitions
            );
        }

        /// <summary>
        /// Settles non-blockade movement-phase results while retaining blockade facts until all
        /// automatic waypoint continuations have changed the live orbital state.
        /// </summary>
        /// <param name="results">The movement-phase results to settle.</param>
        /// <param name="blockadeTransitions">The collection receiving unpublished blockade transitions.</param>
        /// <returns>The settled non-blockade results and their reactions.</returns>
        private List<GameResult> ProcessMovementPhaseResults(
            IEnumerable<GameResult> results,
            ICollection<BlockadeChangedResult> blockadeTransitions
        )
        {
            if (blockadeTransitions == null)
                throw new ArgumentNullException(nameof(blockadeTransitions));

            List<GameResult> sourceResults = (results ?? Enumerable.Empty<GameResult>())
                .Where(result => result != null)
                .ToList();
            foreach (
                BlockadeChangedResult transition in sourceResults.OfType<BlockadeChangedResult>()
            )
                blockadeTransitions.Add(transition);

            return ProcessResults(
                sourceResults.Where(result => result is not BlockadeChangedResult),
                processMessages: false
            );
        }

        /// <summary>
        /// Publishes net blockade changes after waypoint continuation and defers transient starts
        /// until strategic processing has completed.
        /// </summary>
        /// <param name="blockadeTransitions">The blockade facts produced during movement and combat.</param>
        /// <returns>The immediately settled blockade results and their reactions.</returns>
        private List<GameResult> ResolveMovementPhaseBlockadeTransitions(
            IReadOnlyCollection<BlockadeChangedResult> blockadeTransitions
        )
        {
            if (blockadeTransitions == null || blockadeTransitions.Count == 0)
                return new List<GameResult>();

            List<BlockadeChangedResult> immediateResults = new List<BlockadeChangedResult>();
            foreach (
                IGrouping<
                    Rebellion.Game.Galaxy.Planet,
                    BlockadeChangedResult
                > planetTransitions in blockadeTransitions
                    .Where(result => result?.Planet != null)
                    .GroupBy(result => result.Planet)
            )
            {
                List<BlockadeChangedResult> orderedTransitions = planetTransitions.ToList();
                bool wasBlockaded = !orderedTransitions[0].Blockaded;
                bool isBlockaded = planetTransitions.Key.IsBlockaded();
                if (wasBlockaded != isBlockaded)
                {
                    BlockadeChangedResult finalTransition = orderedTransitions.Last(result =>
                        result.Blockaded == isBlockaded
                    );
                    immediateResults.Add(finalTransition);
                    continue;
                }

                if (!wasBlockaded)
                {
                    BlockadeChangedResult transientStart = orderedTransitions.FirstOrDefault(
                        result => result.Blockaded
                    );
                    if (transientStart != null)
                        _deferredBlockadeResults.Add(transientStart);
                }
            }

            return ProcessResults(immediateResults, processMessages: false);
        }

        /// <summary>
        /// Publishes transient blockade-start facts after AI planning, when their live blockade is
        /// already gone and cannot trigger movement reactions.
        /// </summary>
        private void ProcessDeferredBlockadeResults()
        {
            if (_deferredBlockadeResults.Count == 0)
                return;

            List<BlockadeChangedResult> results = new List<BlockadeChangedResult>(
                _deferredBlockadeResults
            );
            _deferredBlockadeResults.Clear();
            ProcessResults(results);
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
