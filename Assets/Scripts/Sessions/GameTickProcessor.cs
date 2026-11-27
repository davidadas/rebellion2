using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI;
using Rebellion.Game;
using Rebellion.Game.Combat;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Gameplay.GameEvents;
using Rebellion.Tactical;
using Rebellion.Util.DependencyInjection;
using Rebellion.Util.Logging;
using Rebellion.Util.Random;

namespace Rebellion.Gameplay
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

        /// <summary>
        /// Retains unpublished orbital transitions in first-seen planet order.
        /// </summary>
        private sealed class BlockadeTransitions
        {
            private readonly Dictionary<Planet, List<BlockadeChangedResult>> _byPlanet = new();
            private readonly List<List<BlockadeChangedResult>> _orderedGroups = new();

            /// <summary>
            /// Gets whether any orbital transitions still await their owning boundary.
            /// </summary>
            internal bool HasPending => _byPlanet.Count != 0;

            /// <summary>
            /// Records a transition without publishing an intermediate orbital state.
            /// </summary>
            /// <param name="transition">The unpublished transition.</param>
            internal void Add(BlockadeChangedResult transition)
            {
                if (transition?.Planet == null)
                    return;
                if (
                    !_byPlanet.TryGetValue(transition.Planet, out List<BlockadeChangedResult> group)
                )
                {
                    group = new List<BlockadeChangedResult>();
                    _byPlanet.Add(transition.Planet, group);
                    _orderedGroups.Add(group);
                }
                group.Add(transition);
            }

            /// <summary>
            /// Removes one planet's transitions for settlement at its encounter boundary.
            /// </summary>
            /// <param name="planet">The planet whose orbital changes have completed.</param>
            /// <returns>The unpublished transitions, or null when no change was recorded.</returns>
            internal List<BlockadeChangedResult> Take(Planet planet)
            {
                return
                    planet != null
                    && _byPlanet.Remove(planet, out List<BlockadeChangedResult> group)
                    ? group
                    : null;
            }

            /// <summary>
            /// Enumerates groups that have not been consumed by an encounter.
            /// </summary>
            /// <returns>The pending groups in first-seen order.</returns>
            internal IEnumerable<List<BlockadeChangedResult>> GetPendingGroups()
            {
                foreach (List<BlockadeChangedResult> group in _orderedGroups)
                    if (
                        _byPlanet.TryGetValue(
                            group[0].Planet,
                            out List<BlockadeChangedResult> pending
                        ) && ReferenceEquals(group, pending)
                    )
                        yield return group;
            }
        }

        private readonly Func<
            IEnumerable<GameResult>,
            bool,
            Func<IReadOnlyList<GameResult>>,
            Func<IReadOnlyList<GameResult>, List<GameResult>>,
            List<GameResult>
        > _processResults;
        private readonly Action<List<GameResult>> _processMessages;
        private readonly GameResultPipeline _pipeline;
        private IReadOnlyList<ActiveBattle.DeferredMessage> _restoredMessages;
        private MessageObserver _messageObserver;
        private readonly List<GameResult> _deferredMessageResults = new();
        private AIDirector _aiDirector;
        private ITickProcessor _captive;
        private ITickProcessor _advisorAutomation;
        private FogOfWarObserver _fogOfWarObserver;
        private ITickProcessor _gameEvents;
        private GameRoot _game;
        private ITickProcessor _jedi;
        private ITickProcessor _maintenance;
        private ManufacturingCommands _manufacturingCommands;
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
        private SpaceCombatAftermath _spaceCombatAftermath;
        private SpaceCombatTickProcessor _spaceCombat;
        private BlockadeTransitionResolver _blockadeTransitionResolver;
        private ITickProcessor _uprising;
        private bool _tickInProgress;
        private TickExecutionState _tickState;

        public bool IsSettled => _tickState == TickExecutionState.Idle;
        public bool IsBusy => _tickInProgress || !IsSettled;
        internal bool CanCaptureTacticalCombat =>
            !_tickInProgress
            && _tickState == TickExecutionState.AwaitingCombatDecision
            && _spaceCombatCommands.CanCaptureTacticalCombat;
        internal Func<bool> CanStartTick { get; set; }

        /// <summary>
        /// Gets or sets the session callback completing queued gameplay reactions at writable boundaries.
        /// </summary>
        internal Action CompletingOperation { get; set; }

        /// <summary>
        /// Gets whether a player operation can run outside an active incremental simulation step.
        /// A pending combat decision accepts the operation that resolves it.
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
        /// Creates the campaign tick scheduler over the session's result and message delivery owners.
        /// </summary>
        /// <param name="pipeline">The session's existing result pipeline.</param>
        internal GameTickProcessor(GameResultPipeline pipeline)
            : this(pipeline.ProcessResults, pipeline.ProcessMessageReactions)
        {
            _pipeline = pipeline;
        }

        /// <summary>
        /// Creates tick scheduling with the existing result-release boundaries.
        /// </summary>
        /// <param name="processResults">Resolves and presents one result batch at its existing release boundary.</param>
        /// <param name="processMessages">Releases the messages of an already resolved batch.</param>
        internal GameTickProcessor(
            Func<
                IEnumerable<GameResult>,
                bool,
                Func<IReadOnlyList<GameResult>>,
                Func<IReadOnlyList<GameResult>, List<GameResult>>,
                List<GameResult>
            > processResults,
            Action<List<GameResult>> processMessages
        )
        {
            _processResults =
                processResults ?? throw new ArgumentNullException(nameof(processResults));
            _processMessages =
                processMessages ?? throw new ArgumentNullException(nameof(processMessages));
        }

        /// <summary>
        /// Connects the active world's processors and initializes any unscheduled research timers.
        /// </summary>
        /// <param name="services">The newly constructed session services.</param>
        internal void ConnectRuntime(IServiceLocator services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            _game = services.GetService<GameRoot>();
            ResearchTickProcessor research = new ResearchTickProcessor(
                services.GetService<IRandomNumberProvider>()
            );
            research.InitializeTimers(_game);
            _research = research;

            _movementCommands = services.GetService<MovementCommands>();
            _fogOfWarObserver = services.GetService<FogOfWarObserver>();
            _spaceCombatCommands = services.GetService<SpaceCombatCommands>();
            _spaceCombatAftermath = services.GetService<SpaceCombatAftermath>();
            _messageObserver = services.GetService<MessageObserver>();
            _blockadeTransitionResolver = new BlockadeTransitionResolver(_game);
            _messages = new MessageTickProcessor();
            _advisorAutomation = new AdvisorAutomationTickProcessor(
                services.GetService<AdvisorAutomationCommands>()
            );
            SmugglingCommands smugglingCommands = services.GetService<SmugglingCommands>();
            _resourceProduction = new ResourceProductionTickProcessor(smugglingCommands);
            _manufacturingCommands = services.GetService<ManufacturingCommands>();
            _manufacturing = new ManufacturingTickProcessor(
                _manufacturingCommands,
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
        }

        /// <summary>
        /// Clears pending combat scheduling during replacement without releasing an active iterator's guard.
        /// </summary>
        internal void Reset()
        {
            _deferredMessageResults.Clear();
            _restoredMessages = null;
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
            _messages.ProcessTick(_game);
            GameLogger.Debug("Tick: " + _game.CurrentTick);

            _advisorAutomation.ProcessTick(_game);
            ProcessResults(_resourceProduction.ProcessTick(_game));
            List<GameResult> manufacturingResults = new List<GameResult>();
            using (
                IEnumerator<IReadOnlyList<GameResult>> manufacturing = _manufacturing
                    .ProcessTickIncrementally(_game)
                    .GetEnumerator()
            )
            {
                while (manufacturing.MoveNext())
                {
                    IReadOnlyList<GameResult> manufacturingStep = manufacturing.Current;
                    manufacturingResults.AddRange(manufacturingStep);
                    ManufacturingPointsCompletedResult progress = manufacturingStep
                        .OfType<ManufacturingPointsCompletedResult>()
                        .FirstOrDefault();
                    if (progress == null)
                        continue;

                    TickProgressed?.Invoke(progress.Context as Planet);
                    yield return null;
                }
            }
            ProcessResults(manufacturingResults);
            // Refill capacity released by completed orders before tick observers render idle lanes.
            _advisorAutomation.ProcessTick(_game);
            yield return null;

            ProcessResults(_maintenance.ProcessTick(_game));
            ProcessResults(_recovery.ProcessTick(_game));
            ProcessResults(_captive.ProcessTick(_game));
            yield return null;

            BlockadeTransitions blockadeTransitions = new();
            List<GameResult> movementResults = ProcessMovementPhaseResults(
                _movement.ProcessTick(_game),
                blockadeTransitions
            );

            List<GameResult> combatResults = ProcessSpaceCombatEncounters(blockadeTransitions);

            List<GameResult> waypointResults = ProcessAvailableWaypointContinuations(
                blockadeTransitions
            );
            List<GameResult> blockadeResults = ResolveMovementPhaseBlockadeTransitions(
                blockadeTransitions
            );
            _fogOfWarObserver.RefreshVisibleKnowledge();

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

            _processMessages(movementPhaseResults);
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
            ProcessResults(_gameEvents.ProcessTick(_game));
            _naming.ProcessTick(_game);
            yield return null;

            List<GameResult> aiResults = new List<GameResult>();
            using (
                IEnumerator<object> ai = _aiDirector
                    .ProcessTickIncrementally(aiResults)
                    .GetEnumerator()
            )
            {
                while (ai.MoveNext())
                    yield return ai.Current;
            }
            ProcessResults(aiResults);

            ProcessResults(_planetaryControl.ProcessTick(_game));
            ProcessResults(_uprising.ProcessTick(_game));

            ProcessResults(_research.ProcessTick(_game));
            ProcessResults(_jedi.ProcessTick(_game));
            _fogOfWarObserver.RefreshVisibleKnowledge();
            yield return null;

            CompletingOperation?.Invoke();
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
            _processMessages(orderedResults);
        }

        /// <summary>
        /// Resolves the pending combat encounter and resumes ticking.
        /// </summary>
        /// <param name="autoResolve">Whether to auto-resolve instead of tactical combat.</param>
        /// <returns>The space combat result generated by the encounter, when present.</returns>
        public SpaceCombatResult ResolveCombat(bool autoResolve)
        {
            List<GameResult> combatResults;

            combatResults = _spaceCombatCommands.ResolvePending(autoResolve);
            return CompleteCombatResolution(combatResults);
        }

        /// <summary>
        /// Commits manual combat and routes its results before resuming the suspended tick.
        /// </summary>
        /// <param name="session">The completed tactical encounter.</param>
        /// <returns>The routed battle summary.</returns>
        public SpaceCombatResult CompleteTacticalCombat(TacticalCombatSession session)
        {
            return CompleteCombatResolution(_spaceCombatCommands.CompleteTacticalCombat(session));
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

            combatResults = _spaceCombatCommands.ResolvePendingRetreat(retreatingFactionInstanceId);
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
                BlockadeTransitions blockadeTransitions = new();
                combatResults = ProcessCombatEncounterResults(combatResults, blockadeTransitions);
                SpaceCombatResult completedCombat = combatResults
                    .OfType<SpaceCombatResult>()
                    .FirstOrDefault();

                List<GameResult> waypointResults = ProcessAvailableWaypointContinuations(
                    blockadeTransitions
                );
                List<GameResult> additionalCombatResults = ProcessSpaceCombatEncounters(
                    blockadeTransitions
                );
                List<GameResult> blockadeResults = ResolveMovementPhaseBlockadeTransitions(
                    blockadeTransitions
                );
                _fogOfWarObserver.RefreshVisibleKnowledge();
                _deferredMessageResults.AddRange(combatResults);
                _deferredMessageResults.AddRange(waypointResults);
                _deferredMessageResults.AddRange(additionalCombatResults);
                _deferredMessageResults.AddRange(blockadeResults);

                if (_spaceCombatCommands.HasPendingDecision)
                {
                    BeginPendingCombatDecision();
                    return completedCombat;
                }

                List<GameResult> messageResults = TakeDeferredMessageResults();
                if (_restoredMessages?.Count > 0)
                {
                    _pipeline.ProcessMessageReactions(messageResults, _restoredMessages);
                    _restoredMessages = null;
                }
                else
                    _processMessages(messageResults);
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
            if (_spaceCombatCommands.RestoreSavedTacticalCombat())
            {
                _restoredMessages = _game.ActiveBattle.DeferredMessages;
                BeginPendingCombatDecision();
                return;
            }

            ReconcilePendingEncounters();
        }

        /// <summary>
        /// Captures the active battle and pending campaign message deliveries before serialization.
        /// </summary>
        internal void CaptureTacticalCombat()
        {
            _spaceCombatCommands.CaptureTacticalCombat();
            List<ActiveBattle.DeferredMessage> messages =
                _restoredMessages == null ? new() : new(_restoredMessages);
            messages.AddRange(_messageObserver.CaptureDeferredMessages(_deferredMessageResults));
            _game.ActiveBattle.DeferredMessages = messages;
        }

        /// <summary>
        /// Reconciles fleet encounters in a loaded strategy save.
        /// </summary>
        private void ReconcilePendingEncounters()
        {
            BlockadeTransitions blockadeTransitions = new();
            List<GameResult> combatResults = ProcessSpaceCombatEncounters(blockadeTransitions);
            combatResults.AddRange(ResolveMovementPhaseBlockadeTransitions(blockadeTransitions));
            _fogOfWarObserver.RefreshVisibleKnowledge();
            if (_spaceCombatCommands.HasPendingDecision)
            {
                StoreDeferredMessageResults(combatResults);
                BeginPendingCombatDecision();
                return;
            }

            _processMessages(combatResults);
        }

        /// <summary>
        /// Settles each detected encounter before requesting the next scanner batch.
        /// </summary>
        /// <param name="blockadeTransitions">The movement phase's unpublished orbital transitions.</param>
        /// <returns>The encounter facts and their completed reactions in delivery order.</returns>
        private List<GameResult> ProcessSpaceCombatEncounters(
            BlockadeTransitions blockadeTransitions
        )
        {
            List<GameResult> results = new();
            foreach (IReadOnlyList<GameResult> encounter in _spaceCombat.ProcessEncounters())
                results.AddRange(ProcessCombatEncounterResults(encounter, blockadeTransitions));
            return results;
        }

        /// <summary>
        /// Completes loss and withdrawal reactions before delivering the final encounter fact.
        /// The caller owns this outer boundary; it never runs inside a result-bus callback.
        /// </summary>
        /// <param name="encounter">The single completed encounter or pending player decision.</param>
        /// <param name="blockadeTransitions">The movement phase's unpublished orbital transitions.</param>
        /// <returns>The settled ordinary facts followed by the final encounter and its reactions.</returns>
        private List<GameResult> ProcessCombatEncounterResults(
            IReadOnlyList<GameResult> encounter,
            BlockadeTransitions blockadeTransitions
        )
        {
            List<GameResult> results = ProcessMovementPhaseResults(
                encounter.Where(result => result is not SpaceCombatResult),
                blockadeTransitions
            );
            results.AddRange(ProcessPendingEncounterResults(blockadeTransitions));
            foreach (SpaceCombatResult combat in encounter.OfType<SpaceCombatResult>())
            {
                results.AddRange(SettleCombatPlanetBlockade(combat.Planet, blockadeTransitions));
                results.AddRange(
                    ProcessResults(
                        new[] { combat },
                        processMessages: false,
                        collectPending: () =>
                            CollectPendingCombatResults(combat.Planet, blockadeTransitions),
                        afterReactions: combat.IsTacticalCombat
                            ? _spaceCombatAftermath.HandleResults
                            : null
                    )
                );
                results.AddRange(ProcessPendingEncounterResults(blockadeTransitions));
                results.AddRange(SettleCombatPlanetBlockade(combat.Planet, blockadeTransitions));
            }
            return results;
        }

        /// <summary>
        /// Drains command-owned facts generated by withdrawal or ordinary encounter reactions.
        /// Blockade facts remain unpublished until their owning orbital boundary has completed.
        /// </summary>
        /// <param name="blockadeTransitions">The movement phase's unpublished orbital transitions.</param>
        /// <returns>The queued facts and every reaction they produce.</returns>
        private List<GameResult> ProcessPendingEncounterResults(
            BlockadeTransitions blockadeTransitions
        )
        {
            List<GameResult> results = new();
            List<GameResult> pending;
            while ((pending = TakePendingCommandResults()) != null)
                results.AddRange(ProcessMovementPhaseResults(pending, blockadeTransitions));
            return results;
        }

        /// <summary>
        /// Collects completed command facts without delivering results or completing the session.
        /// </summary>
        /// <returns>The pending facts, or null when both command owners are already drained.</returns>
        private List<GameResult> TakePendingCommandResults()
        {
            if (!_manufacturingCommands.HasPendingResults && !_movementCommands.HasPendingResults)
                return null;
            List<GameResult> pending = _manufacturingCommands.TakePendingResults();
            pending.AddRange(_movementCommands.TakePendingResults());
            return pending;
        }

        /// <summary>
        /// Supplies final-result reactions to the bus before its after-reaction phase.
        /// Ordinary command facts settle before the encounter planet's held blockade is resolved;
        /// unrelated orbital changes remain owned by waypoint settlement.
        /// </summary>
        /// <param name="planet">The completed encounter's planet.</param>
        /// <param name="blockadeTransitions">The movement phase's unpublished orbital transitions.</param>
        /// <returns>The next ordinary facts, or an empty collection when the encounter is settled.</returns>
        private IReadOnlyList<GameResult> CollectPendingCombatResults(
            Planet planet,
            BlockadeTransitions blockadeTransitions
        )
        {
            List<GameResult> pending = TakePendingCommandResults();
            if (pending != null)
            {
                List<GameResult> ordinary = CollectMovementPhaseResults(
                    pending,
                    blockadeTransitions
                );
                if (ordinary.Count > 0)
                    return ordinary;
            }

            List<BlockadeChangedResult> transitions = blockadeTransitions.Take(planet);
            BlockadeChangedResult final =
                transitions == null
                    ? null
                    : _blockadeTransitionResolver.Resolve(planet, !transitions[0].Blockaded);
            return final == null ? Array.Empty<GameResult>() : new GameResult[] { final };
        }

        /// <summary>
        /// Settles the encounter planet's final blockade and resulting command facts without
        /// releasing unrelated waypoint transitions or reporting canceled intermediate states.
        /// </summary>
        /// <param name="planet">The completed encounter's planet.</param>
        /// <param name="blockadeTransitions">The movement phase's unpublished orbital transitions.</param>
        /// <returns>The final blockade facts and their completed reactions.</returns>
        private List<GameResult> SettleCombatPlanetBlockade(
            Planet planet,
            BlockadeTransitions blockadeTransitions
        )
        {
            List<GameResult> results = new();
            List<BlockadeChangedResult> transitions;
            while ((transitions = blockadeTransitions.Take(planet)) != null)
            {
                BlockadeChangedResult final = _blockadeTransitionResolver.Resolve(
                    planet,
                    !transitions[0].Blockaded
                );
                if (final != null)
                    results.AddRange(ProcessResults(new[] { final }, processMessages: false));
                results.AddRange(ProcessPendingEncounterResults(blockadeTransitions));
            }
            return results;
        }

        /// <summary>
        /// Continues waypoint routes when no combat decision is blocking movement.
        /// </summary>
        /// <param name="blockadeTransitions">The collection receiving unpublished blockade transitions.</param>
        /// <returns>The results produced while starting the next route legs.</returns>
        private List<GameResult> ProcessAvailableWaypointContinuations(
            BlockadeTransitions blockadeTransitions
        )
        {
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
            BlockadeTransitions blockadeTransitions
        )
        {
            return ProcessResults(
                CollectMovementPhaseResults(results, blockadeTransitions),
                processMessages: false
            );
        }

        /// <summary>
        /// Separates publishable facts from orbital transitions without invoking the result bus.
        /// </summary>
        /// <param name="results">The completed movement-phase facts.</param>
        /// <param name="blockadeTransitions">The collection receiving unpublished orbital transitions.</param>
        /// <returns>The ordinary facts in their existing order.</returns>
        private static List<GameResult> CollectMovementPhaseResults(
            IEnumerable<GameResult> results,
            BlockadeTransitions blockadeTransitions
        )
        {
            if (blockadeTransitions == null)
                throw new ArgumentNullException(nameof(blockadeTransitions));
            List<GameResult> ordinary = new();
            foreach (GameResult result in results ?? Enumerable.Empty<GameResult>())
                if (result is BlockadeChangedResult transition)
                    blockadeTransitions.Add(transition);
                else if (result != null)
                    ordinary.Add(result);
            return ordinary;
        }

        /// <summary>
        /// Publishes net blockade changes after waypoint continuation.
        /// </summary>
        /// <param name="blockadeTransitions">The blockade facts produced during movement and combat.</param>
        /// <returns>The immediately settled blockade results and their reactions.</returns>
        private List<GameResult> ResolveMovementPhaseBlockadeTransitions(
            BlockadeTransitions blockadeTransitions
        )
        {
            if (blockadeTransitions?.HasPending != true)
                return new List<GameResult>();

            List<BlockadeChangedResult> immediateResults = new List<BlockadeChangedResult>();
            foreach (
                List<BlockadeChangedResult> orderedTransitions in blockadeTransitions.GetPendingGroups()
            )
            {
                bool wasBlockaded = !orderedTransitions[0].Blockaded;
                bool isBlockaded = BlockadeQueries.IsBlockaded(_game, orderedTransitions[0].Planet);
                if (wasBlockaded != isBlockaded)
                {
                    BlockadeChangedResult finalTransition = orderedTransitions.Last(result =>
                        result.Blockaded == isBlockaded
                    );
                    immediateResults.Add(finalTransition);
                }
            }

            return ProcessResults(immediateResults, processMessages: false);
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
            CompletingOperation?.Invoke();
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
        /// <param name="collectPending">Drains commands caused by the current combat encounter.</param>
        /// <param name="afterReactions">Applies battle aftermath after losses, withdrawals, and blockades settle.</param>
        /// <returns>The batch extended with its reactions.</returns>
        private List<GameResult> ProcessResults(
            IEnumerable<GameResult> results,
            bool processMessages = true,
            Func<IReadOnlyList<GameResult>> collectPending = null,
            Func<IReadOnlyList<GameResult>, List<GameResult>> afterReactions = null
        )
        {
            return _processResults(results, processMessages, collectPending, afterReactions);
        }
    }
}
