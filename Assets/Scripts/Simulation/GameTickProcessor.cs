using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI;
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
        private readonly List<GameResult> _deferredMessageResults = new();
        private AIDirector _aiDirector;
        private ITickProcessor _blockade;
        private ITickProcessor _captive;
        private ITickProcessor _factionAutomation;
        private FogOfWarCommands _fogOfWarCommands;
        private GameEventExecutor _gameEventExecutor;
        private GameRoot _game;
        private ITickProcessor _jedi;
        private ITickProcessor _maintenance;
        private ITickProcessor _manufacturing;
        private ITickProcessor _messages;
        private ITickProcessor _missions;
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

        public event Action TickCompleted;
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
        /// Replaces the feature processors and runtime services used by subsequent ticks.
        /// </summary>
        /// <param name="services">The newly constructed runtime service scope.</param>
        internal void ConnectRuntime(IServiceLocator services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            _game = services.GetService<GameRoot>();
            ResearchTickProcessor research = new ResearchTickProcessor(
                services.GetService<Rebellion.Util.Random.IRandomNumberProvider>()
            );
            research.InitializeTimers(_game);
            _research = research;

            _movementCommands = services.GetService<MovementCommands>();
            _fogOfWarCommands = services.GetService<FogOfWarCommands>();
            _spaceCombatCommands = services.GetService<SpaceCombatCommands>();
            _messages = new MessageTickProcessor();
            _factionAutomation = new FactionAutomationTickProcessor(
                services.GetService<FactionAutomationCommands>()
            );
            SmugglingCommands smugglingCommands = services.GetService<SmugglingCommands>();
            SmugglingTickProcessor smuggling = new(smugglingCommands);
            _resourceProduction = new ResourceProductionTickProcessor(smuggling, smugglingCommands);
            _manufacturing = new ManufacturingTickProcessor(
                services.GetService<ManufacturingCommands>()
            );
            _maintenance = new MaintenanceTickProcessor(services.GetService<MaintenanceCommands>());
            _recovery = new RecoveryTickProcessor();
            _captive = new CaptiveTickProcessor(services.GetService<CaptiveCommands>());
            _movement = new MovementTickProcessor(_movementCommands);
            _spaceCombat = new SpaceCombatTickProcessor(_spaceCombatCommands);
            _missions = new MissionTickProcessor(services.GetService<MissionCommands>());
            _gameEventExecutor = services.GetService<GameEventExecutor>();
            _naming = new NamingTickProcessor(services.GetService<NamingCommands>());
            _aiDirector = services.GetService<AIDirector>();
            _blockade = new BlockadeTickProcessor(services.GetService<BlockadeCommands>());
            _planetaryControl = new PlanetaryControlTickProcessor(
                services.GetService<PlanetaryControlCommands>()
            );
            _uprising = new UprisingTickProcessor(services.GetService<UprisingCommands>());
            _jedi = new JediTickProcessor(services.GetService<JediCommands>());
            _victory = new VictoryTickProcessor(services.GetService<VictoryCommands>());
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
        /// <returns>A sequence containing one step per completed AI phase.</returns>
        private IEnumerable<object> ProcessTickCore()
        {
            _game.CurrentTick++;
            _messages.ProcessTick(_game);
            GameLogger.Debug("Tick: " + _game.CurrentTick);

            _factionAutomation.ProcessTick(_game);
            ProcessResults(_resourceProduction.ProcessTick(_game));
            ProcessResults(_manufacturing.ProcessTick(_game));
            // Refill capacity released by completed orders before tick observers render idle lanes.
            _factionAutomation.ProcessTick(_game);
            ProcessResults(_maintenance.ProcessTick(_game));
            ProcessResults(_recovery.ProcessTick(_game));
            ProcessResults(_captive.ProcessTick(_game));

            List<GameResult> movementResults = ProcessResults(
                _movement.ProcessTick(_game),
                processMessages: false
            );

            List<GameResult> combatResults = ProcessResults(
                _spaceCombat.ProcessTick(_game),
                processMessages: false
            );

            List<GameResult> waypointResults = ProcessAvailableWaypointContinuations();
            _fogOfWarCommands.RefreshVisibleKnowledge();

            List<GameResult> movementPhaseResults = CombineResults(
                movementResults,
                combatResults,
                waypointResults
            );
            if (_spaceCombatCommands.HasPendingDecision)
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
            ProcessResults(_missions.ProcessTick(_game));
            ProcessResults(_gameEventExecutor.ProcessEvents(_game.GetEventPool()));
            _naming.ProcessTick(_game);
            List<GameResult> aiResults = new List<GameResult>();
            foreach (object step in _aiDirector.ProcessTickIncrementally(aiResults))
                yield return step;
            ProcessResults(aiResults);

            ProcessResults(_blockade.ProcessTick(_game));
            ProcessResults(_planetaryControl.ProcessTick(_game));
            ProcessResults(_uprising.ProcessTick(_game));

            ProcessResults(_research.ProcessTick(_game));
            ProcessResults(_jedi.ProcessTick(_game));
            ProcessResults(_victory.ProcessTick(_game));
            _fogOfWarCommands.RefreshVisibleKnowledge();
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
            List<GameResult> combatResults = _spaceCombatCommands.ResolvePending(autoResolve);
            return CompleteCombatResolution(combatResults);
        }

        /// <summary>
        /// Resolves a pending retreat and routes its results before resuming ticks.
        /// </summary>
        /// <param name="retreatingFactionInstanceId">The faction withdrawing from combat.</param>
        /// <returns>The resulting space-combat summary, or null when retreat is unavailable.</returns>
        public SpaceCombatResult ResolveCombatRetreat(string retreatingFactionInstanceId)
        {
            List<GameResult> combatResults = _spaceCombatCommands.ResolvePendingRetreat(
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
                combatResults = ProcessResults(combatResults, processMessages: false);
                SpaceCombatResult completedCombat = combatResults
                    .OfType<SpaceCombatResult>()
                    .FirstOrDefault();

                List<GameResult> waypointResults = ProcessAvailableWaypointContinuations();
                List<GameResult> additionalCombatResults = ProcessResults(
                    _spaceCombat.ProcessTick(_game),
                    processMessages: false
                );
                _fogOfWarCommands.RefreshVisibleKnowledge();
                _deferredMessageResults.AddRange(combatResults);
                _deferredMessageResults.AddRange(waypointResults);
                _deferredMessageResults.AddRange(additionalCombatResults);

                if (_spaceCombatCommands.HasPendingDecision)
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
                _spaceCombat.ProcessTick(_game),
                processMessages: false
            );
            _fogOfWarCommands.RefreshVisibleKnowledge();
            if (_spaceCombatCommands.HasPendingDecision)
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
            if (_spaceCombatCommands.HasPendingDecision)
                return new List<GameResult>();

            return ProcessResults(
                _movementCommands.ContinueFleetWaypointRoutes(),
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
