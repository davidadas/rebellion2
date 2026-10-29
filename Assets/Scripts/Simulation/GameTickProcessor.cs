using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Util.DependencyInjection;
using Rebellion.Util.Logging;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Executes the ordered tick phases and suspends or resumes them around player combat decisions.
    /// </summary>
    public sealed partial class GameTickProcessor
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
        private readonly SimulationPhaseTrace _timings;
        private readonly List<BlockadeChangedResult> _deferredBlockadeResults = new();
        private readonly List<GameResult> _deferredMessageResults = new();
        private AIDirector _aiDirector;
        private ITickProcessor _captive;
        private ITickProcessor _factionAutomation;
        private FogOfWarObserver _fogOfWarObserver;
        private ITickProcessor _gameEvents;
        private GameRoot _game;
        private ITickProcessor _jedi;
        private ITickProcessor _maintenance;
        private ManufacturingTickProcessor _manufacturing;
        private ITickProcessor _messages;
        private MissionTickProcessor _missions;
        private MovementCommands _movementCommands;
        private ITickProcessor _movement;
        private ITickProcessor _naming;
        private ITickProcessor _planetaryControl;
        private ITickProcessor _recovery;
        private ITickProcessor _research;
        private ITickProcessor _resourceProduction;
        private SpaceCombatCommands _spaceCombatCommands;
        private ITickProcessor _spaceCombat;
        private ITickProcessor _uprising;
        private ITickProcessor _victory;
        private bool _tickInProgress;
        private TickExecutionState _tickState;

        public bool IsSettled => _tickState == TickExecutionState.Idle;
        public bool IsBusy => _tickInProgress || !IsSettled;
        internal Func<bool> CanStartTick { get; set; }

        /// <summary>
        /// Gets whether a player operation can run outside an active incremental simulation step.
        /// A pending combat decision accepts its resolving operation while remaining unsaveable.
        /// </summary>
        public bool CanAcceptPlayerActions => !_tickInProgress;

        public event Action TickCompleted;

        /// <summary>
        /// Raised when an incremental manufacturing step changes the producing planet's queue.
        /// </summary>
        public event Action<Planet> TickProgressed;

        public event Action CombatDecisionRequired;
        internal event Action CombatResumed;

        /// <summary>
        /// Creates tick scheduling with the existing result-release boundaries.
        /// </summary>
        /// <param name="processResults">Resolves and presents one result batch at its existing release boundary.</param>
        /// <param name="processMessages">Releases the messages of an already resolved batch.</param>
        /// <param name="timings">The session's optional phase recorder.</param>
        internal GameTickProcessor(
            Func<IEnumerable<GameResult>, bool, List<GameResult>> processResults,
            Action<List<GameResult>> processMessages,
            SimulationPhaseTrace timings = null
        )
        {
            _processResults =
                processResults ?? throw new ArgumentNullException(nameof(processResults));
            _processMessages =
                processMessages ?? throw new ArgumentNullException(nameof(processMessages));
            _timings = timings ?? new SimulationPhaseTrace();
        }

        /// <summary>
        /// Replaces the feature processors and runtime services used by subsequent ticks.
        /// </summary>
        /// <param name="services">The newly constructed runtime service scope.</param>
        internal void ConnectRuntime(IServiceLocator services)
        {
            ConnectRuntimeCore(services, initializeResearchTimers: true);
        }

        /// <summary>
        /// Connects recovered services while retaining every captured research timer and random position.
        /// </summary>
        /// <param name="services">The recovered service scope.</param>
        internal void ConnectRecoveryRuntime(IServiceLocator services)
        {
            ConnectRuntimeCore(services, initializeResearchTimers: false);
        }

        /// <summary>
        /// Constructs stateless tick processors for the selected session initialization boundary.
        /// </summary>
        /// <param name="services">The owning service scope.</param>
        /// <param name="initializeResearchTimers">Whether this is normal game initialization.</param>
        private void ConnectRuntimeCore(IServiceLocator services, bool initializeResearchTimers)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            _game = services.GetService<GameRoot>();
            ResearchTickProcessor research = new ResearchTickProcessor(
                services.GetService<Rebellion.Util.Random.IRandomNumberProvider>()
            );
            if (initializeResearchTimers)
                research.InitializeTimers(_game);
            _research = research;

            _movementCommands = services.GetService<MovementCommands>();
            _fogOfWarObserver = services.GetService<FogOfWarObserver>();
            _spaceCombatCommands = services.GetService<SpaceCombatCommands>();
            _messages = new MessageTickProcessor();
            _factionAutomation = new FactionAutomationTickProcessor(
                services.GetService<FactionAutomationCommands>()
            );
            SmugglingCommands smugglingCommands = services.GetService<SmugglingCommands>();
            SmugglingTickProcessor smuggling = new(smugglingCommands);
            _resourceProduction = new ResourceProductionTickProcessor(smuggling, smugglingCommands);
            _manufacturing = new ManufacturingTickProcessor(
                services.GetService<ManufacturingCommands>(),
                _movementCommands
            );
            _maintenance = new MaintenanceTickProcessor(services.GetService<MaintenanceCommands>());
            _recovery = new RecoveryTickProcessor();
            _captive = new CaptiveTickProcessor(services.GetService<CaptiveCommands>());
            _movement = new MovementTickProcessor(_movementCommands);
            _spaceCombat = new SpaceCombatTickProcessor(_spaceCombatCommands);
            _missions = new MissionTickProcessor(services.GetService<MissionResolver>());
            _gameEvents = new GameEventTickProcessor(services.GetService<GameEventCommands>());
            _naming = new NamingTickProcessor(services.GetService<NamingCommands>());
            _aiDirector = services.GetService<AIDirector>();
            _planetaryControl = new PlanetaryControlTickProcessor(
                services.GetService<PlanetaryControlCommands>()
            );
            _uprising = new UprisingTickProcessor(services.GetService<UprisingResolver>());
            _jedi = new JediTickProcessor(services.GetService<JediCommands>());
            _victory = new VictoryTickProcessor(
                services.GetService<VictoryCommands>(),
                services.GetService<VictoryQueries>()
            );
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
        /// Runs one game tick and yields between completed phase groups, production points, and AI phases.
        /// </summary>
        /// <returns>The ordered simulation steps, retaining the mutation guard across every yield.</returns>
        public IEnumerator ProcessTickIncrementally()
        {
            if (
                _tickInProgress
                || CanStartTick?.Invoke() == false
                || _tickState != TickExecutionState.Idle
                || _game.GetGameSpeed() == TickSpeed.Paused
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
        /// <returns>The ordered steps through completed simulation phase groups.</returns>
        private IEnumerable<object> ProcessTickCore()
        {
            _game.CurrentTick++;
            ProcessSystem(SimulationPhase.Messages, _messages);
            GameLogger.Debug("Tick: " + _game.CurrentTick);

            ProcessSystem(SimulationPhase.AutomationBeforeProduction, _factionAutomation);
            ProcessResults(ProcessSystem(SimulationPhase.ResourceProduction, _resourceProduction));
            List<GameResult> manufacturingResults = new List<GameResult>();
            using (
                IEnumerator<IReadOnlyList<GameResult>> manufacturing = _manufacturing
                    .ProcessTickIncrementally(_game)
                    .GetEnumerator()
            )
            {
                while (AdvanceTimed(manufacturing, SimulationPhase.ManufacturingStep))
                {
                    IReadOnlyList<GameResult> manufacturingStep = manufacturing.Current;
                    manufacturingResults.AddRange(manufacturingStep);
                    ManufacturingPointsCompletedResult progress = manufacturingStep
                        .OfType<ManufacturingPointsCompletedResult>()
                        .FirstOrDefault();
                    if (progress == null)
                        continue;

                    using (
                        _timings.Measure(
                            SimulationPhase.ManufacturingProgressNotification,
                            _game.CurrentTick
                        )
                    )
                        TickProgressed?.Invoke(progress.Context as Planet);
                    yield return null;
                }
            }
            ProcessResults(manufacturingResults);
            // Refill capacity released by completed orders before tick observers render idle lanes.
            ProcessSystem(SimulationPhase.AutomationRefill, _factionAutomation);
            yield return null;

            ProcessResults(ProcessSystem(SimulationPhase.Maintenance, _maintenance));
            ProcessResults(ProcessSystem(SimulationPhase.Recovery, _recovery));
            ProcessResults(ProcessSystem(SimulationPhase.Captives, _captive));
            yield return null;

            List<BlockadeChangedResult> blockadeTransitions = new List<BlockadeChangedResult>();
            List<GameResult> movementResults = ProcessMovementPhaseResults(
                ProcessSystem(SimulationPhase.Movement, _movement),
                blockadeTransitions
            );

            List<GameResult> combatResults = ProcessMovementPhaseResults(
                ProcessSystem(SimulationPhase.SpaceCombat, _spaceCombat),
                blockadeTransitions
            );

            List<GameResult> waypointResults = ProcessAvailableWaypointContinuations(
                blockadeTransitions
            );
            List<GameResult> blockadeResults = ResolveMovementPhaseBlockadeTransitions(
                blockadeTransitions
            );
            RefreshKnowledge(SimulationPhase.KnowledgeAfterMovement);

            List<GameResult> movementPhaseResults = CombineResults(
                movementResults,
                combatResults,
                waypointResults,
                blockadeResults
            );
            if (_spaceCombatCommands.HasPendingDecision)
            {
                StoreDeferredMessageResults(movementPhaseResults);
                BeginPendingCombatDecision();
                yield break;
            }

            ProcessMessageResults(movementPhaseResults);
            yield return null;

            foreach (object step in ProcessRemainingTickPhases())
                yield return step;
        }

        /// <summary>
        /// Processes the phases that run after movement and space combat have settled.
        /// </summary>
        /// <returns>The ordered steps through completed simulation and AI phases.</returns>
        private IEnumerable<object> ProcessRemainingTickPhases()
        {
            ProcessMissionPhase();
            ProcessResults(ProcessSystem(SimulationPhase.Events, _gameEvents));
            ProcessSystem(SimulationPhase.Naming, _naming);
            yield return null;

            List<GameResult> aiResults = new List<GameResult>();
            using (
                IEnumerator<object> ai = _aiDirector
                    .ProcessTickIncrementally(aiResults)
                    .GetEnumerator()
            )
            {
                while (AdvanceTimed(ai, SimulationPhase.AIStep))
                    yield return ai.Current;
            }
            ProcessResults(aiResults);
            ProcessDeferredBlockadeResults();

            ProcessResults(ProcessSystem(SimulationPhase.PlanetaryControl, _planetaryControl));
            ProcessResults(ProcessSystem(SimulationPhase.Uprising, _uprising));

            ProcessResults(ProcessSystem(SimulationPhase.Research, _research));
            ProcessResults(ProcessSystem(SimulationPhase.Jedi, _jedi));
            ProcessResults(ProcessSystem(SimulationPhase.Victory, _victory));
            yield return null;

            RefreshKnowledge(SimulationPhase.KnowledgeAfterTick);
            yield return null;

            _tickState = TickExecutionState.Idle;
            using (_timings.Measure(SimulationPhase.TickCompletedNotification, _game.CurrentTick))
                TickCompleted?.Invoke();
        }

        /// <summary>
        /// Settles each mission before advancing the next while deferring their messages until the phase ends.
        /// </summary>
        private void ProcessMissionPhase()
        {
            using var timing = _timings.Measure(SimulationPhase.Missions, _game.CurrentTick);
            List<GameResult> orderedResults = new List<GameResult>();
            List<GameResult> reactionResults = new List<GameResult>();
            List<GameResult> deferredResults = new List<GameResult>();
            foreach (
                (
                    IReadOnlyList<GameResult> Results,
                    bool RequiresImmediateSettlement
                ) batch in _missions.ProcessTickIncrementally(_game)
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
            ProcessMessageResults(orderedResults);
        }

        /// <summary>
        /// Resolves the pending combat encounter and resumes ticking.
        /// </summary>
        /// <param name="autoResolve">Whether to auto-resolve instead of tactical combat.</param>
        /// <returns>The space combat result generated by the encounter, when present.</returns>
        public SpaceCombatResult ResolveCombat(bool autoResolve)
        {
            List<GameResult> combatResults;
            using (_timings.Measure(SimulationPhase.CombatResolution, _game.CurrentTick))
                combatResults = _spaceCombatCommands.ResolvePending(autoResolve);
            return CompleteCombatResolution(combatResults);
        }

        /// <summary>
        /// Records a human combat response, keeping the day suspended until every required response arrives.
        /// </summary>
        /// <param name="factionID">The authenticated responding faction.</param>
        /// <param name="retreat">Whether that faction chooses withdrawal.</param>
        /// <param name="result">The completed battle result, or null while waiting for another response.</param>
        /// <returns>Whether the response was accepted.</returns>
        public bool TrySubmitCombatResponse(
            string factionID,
            bool retreat,
            out SpaceCombatResult result
        )
        {
            result = null;
            List<GameResult> results;
            using (_timings.Measure(SimulationPhase.CombatResolution, _game.CurrentTick))
                if (!_spaceCombatCommands.TrySubmitResponse(factionID, retreat, out results))
                    return false;
            if (results != null)
                result = CompleteCombatResolution(results);
            return true;
        }

        /// <summary>
        /// Resolves a pending retreat and routes its results before resuming ticks.
        /// </summary>
        /// <param name="retreatingFactionInstanceId">The faction withdrawing from combat.</param>
        /// <returns>The resulting space-combat summary, or null when retreat is unavailable.</returns>
        public SpaceCombatResult ResolveCombatRetreat(string retreatingFactionInstanceId)
        {
            List<GameResult> combatResults;
            using (_timings.Measure(SimulationPhase.CombatResolution, _game.CurrentTick))
                combatResults = _spaceCombatCommands.ResolvePendingRetreat(
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
            using var timing = _timings.Measure(
                SimulationPhase.CombatContinuation,
                _game.CurrentTick
            );
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
                    ProcessSystem(SimulationPhase.SpaceCombat, _spaceCombat),
                    blockadeTransitions
                );
                List<GameResult> blockadeResults = ResolveMovementPhaseBlockadeTransitions(
                    blockadeTransitions
                );
                RefreshKnowledge(SimulationPhase.KnowledgeAfterMovement);
                _deferredMessageResults.AddRange(combatResults);
                _deferredMessageResults.AddRange(waypointResults);
                _deferredMessageResults.AddRange(additionalCombatResults);
                _deferredMessageResults.AddRange(blockadeResults);

                if (_spaceCombatCommands.HasPendingDecision)
                {
                    BeginPendingCombatDecision();
                    return completedCombat;
                }

                ProcessMessageResults(TakeDeferredMessageResults());
                IEnumerator remainingTick = ProcessRemainingTickPhases().GetEnumerator();
                try
                {
                    while (remainingTick.MoveNext()) { }
                }
                finally
                {
                    (remainingTick as IDisposable)?.Dispose();
                }

                using (
                    _timings.Measure(SimulationPhase.CombatResumedNotification, _game.CurrentTick)
                )
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
            using var timing = _timings.Measure(
                SimulationPhase.LoadedStateReconciliation,
                _game.CurrentTick
            );
            List<BlockadeChangedResult> blockadeTransitions = new List<BlockadeChangedResult>();
            List<GameResult> combatResults = ProcessMovementPhaseResults(
                ProcessSystem(SimulationPhase.SpaceCombat, _spaceCombat),
                blockadeTransitions
            );
            combatResults.AddRange(ResolveMovementPhaseBlockadeTransitions(blockadeTransitions));
            RefreshKnowledge(SimulationPhase.KnowledgeAfterMovement);
            if (_spaceCombatCommands.HasPendingDecision)
            {
                StoreDeferredMessageResults(combatResults);
                BeginPendingCombatDecision();
                return;
            }

            ProcessMessageResults(combatResults);
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
            using var timing = _timings.Measure(
                SimulationPhase.WaypointContinuation,
                _game.CurrentTick
            );
            if (_spaceCombatCommands.HasPendingDecision)
                return new List<GameResult>();

            return ProcessMovementPhaseResults(
                _movementCommands.ContinueFleetWaypointRoutes(),
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
            using var timing = _timings.Measure(
                SimulationPhase.MovementResultSettlement,
                _game.CurrentTick
            );
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
            using var timing = _timings.Measure(
                SimulationPhase.BlockadeSettlement,
                _game.CurrentTick
            );
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
            using var timing = _timings.Measure(
                SimulationPhase.DeferredBlockades,
                _game.CurrentTick
            );
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
            using (_timings.Measure(SimulationPhase.CombatDecisionNotification, _game.CurrentTick))
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
            using var timing = _timings.Measure(SimulationPhase.ResultDelivery, _game.CurrentTick);
            return _processResults(results, processMessages);
        }

        /// <summary>
        /// Measures one synchronous processor independently of the following result delivery.
        /// </summary>
        /// <param name="phase">The processor's fixed measurement label.</param>
        /// <param name="processor">The existing domain processor.</param>
        /// <returns>The processor's unchanged result collection.</returns>
        private IReadOnlyList<GameResult> ProcessSystem(
            SimulationPhase phase,
            ITickProcessor processor
        )
        {
            using var timing = _timings.Measure(phase, _game.CurrentTick);
            return processor.ProcessTick(_game);
        }

        /// <summary>
        /// Measures one iterator advance without including time suspended at a presentation yield.
        /// </summary>
        /// <typeparam name="T">The existing iterator's step type.</typeparam>
        /// <param name="steps">The existing iterator.</param>
        /// <param name="phase">The iterator's fixed measurement label.</param>
        /// <returns>The iterator's unchanged completion result.</returns>
        private bool AdvanceTimed<T>(IEnumerator<T> steps, SimulationPhase phase)
        {
            using var timing = _timings.Measure(phase, _game.CurrentTick);
            return steps.MoveNext();
        }

        /// <summary>
        /// Measures the existing visibility refresh without querying entities for diagnostics.
        /// </summary>
        /// <param name="phase">The existing refresh boundary's fixed label.</param>
        private void RefreshKnowledge(SimulationPhase phase)
        {
            using var timing = _timings.Measure(phase, _game.CurrentTick);
            _fogOfWarObserver.RefreshVisibleKnowledge();
        }

        /// <summary>
        /// Measures release of a settled batch's messages independently of its domain phase.
        /// </summary>
        /// <param name="results">The unchanged already-settled result batch.</param>
        private void ProcessMessageResults(List<GameResult> results)
        {
            using var timing = _timings.Measure(SimulationPhase.MessageDelivery, _game.CurrentTick);
            _processMessages(results);
        }
    }
}
