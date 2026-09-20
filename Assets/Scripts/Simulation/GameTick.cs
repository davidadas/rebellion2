using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Results;
using Rebellion.Util.Logging;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Advances one simulation tick through its ordered phases and combat continuations.
    /// </summary>
    public sealed class GameTick
    {
        private enum ExecutionState
        {
            Idle,
            Processing,
            AwaitingCombatDecision,
        }

        private readonly GameRoot _game;
        private readonly SimulationFeatures _features;
        private readonly GameResults _results;
        private readonly List<GameResult> _deferredMessageResults = new List<GameResult>();
        private bool _isExecuting;
        private ExecutionState _state;

        /// <summary>
        /// Raised after a complete tick settles.
        /// </summary>
        public event Action Completed;

        /// <summary>
        /// Raised when tick execution pauses for player combat input.
        /// </summary>
        public event Action CombatDecisionRequired;

        /// <summary>
        /// Gets whether no tick or combat decision is outstanding.
        /// </summary>
        public bool IsSettled => _state == ExecutionState.Idle;

        /// <summary>
        /// Gets whether a new tick can begin.
        /// </summary>
        public bool CanStart =>
            !_isExecuting && IsSettled && _game.GetGameSpeed() != TickSpeed.Paused;

        /// <summary>
        /// Creates a tick runner for one simulation session.
        /// </summary>
        /// <param name="game">The authoritative game state.</param>
        /// <param name="features">The session feature graph.</param>
        /// <param name="results">The session result pipeline.</param>
        internal GameTick(GameRoot game, SimulationFeatures features, GameResults results)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _features = features ?? throw new ArgumentNullException(nameof(features));
            _results = results ?? throw new ArgumentNullException(nameof(results));
        }

        /// <summary>
        /// Executes one complete tick unless combat requires player input.
        /// </summary>
        public void ExecuteImmediately()
        {
            IEnumerator execution = ExecuteIncrementally();
            try
            {
                while (execution.MoveNext()) { }
            }
            finally
            {
                (execution as IDisposable)?.Dispose();
            }
        }

        /// <summary>
        /// Executes one tick and yields after each incremental AI phase.
        /// </summary>
        /// <returns>The incremental tick sequence.</returns>
        public IEnumerator ExecuteIncrementally()
        {
            if (!CanStart)
                yield break;

            _isExecuting = true;
            _state = ExecutionState.Processing;
            try
            {
                foreach (object step in ExecuteCore())
                    yield return step;
            }
            finally
            {
                _isExecuting = false;
                if (_state == ExecutionState.Processing)
                    _state = ExecutionState.Idle;
            }
        }

        /// <summary>
        /// Resolves pending combat and resumes the interrupted tick.
        /// </summary>
        /// <param name="autoResolve">Whether the encounter should use automatic resolution.</param>
        /// <returns>The completed combat summary, when combat was pending.</returns>
        public SpaceCombatResult ResolveCombat(bool autoResolve)
        {
            return CompleteCombat(_features.SpaceCombat.ResolvePending(autoResolve));
        }

        /// <summary>
        /// Resolves a pending retreat and resumes the interrupted tick.
        /// </summary>
        /// <param name="retreatingFactionInstanceID">The faction withdrawing from combat.</param>
        /// <returns>The completed combat summary, or null when retreat is unavailable.</returns>
        public SpaceCombatResult ResolveRetreat(string retreatingFactionInstanceID)
        {
            List<GameResult> combatResults = _features.SpaceCombat.ResolvePendingRetreat(
                retreatingFactionInstanceID
            );
            return combatResults == null ? null : CompleteCombat(combatResults);
        }

        /// <summary>
        /// Reconstructs unresolved combat represented by loaded state without advancing time.
        /// </summary>
        internal void ReconcileLoadedState()
        {
            List<GameResult> combatResults = _results.Resolve(
                _features.SpaceCombat.ProcessTick(),
                createMessages: false
            );
            if (_features.SpaceCombat.HasPendingDecision)
            {
                StoreDeferredMessages(combatResults);
                AwaitCombatDecision();
                return;
            }
            _results.DeliverMessages(combatResults);
        }

        /// <summary>
        /// Applies advisor automation immediately for one faction.
        /// </summary>
        /// <param name="faction">The faction whose delegated work should run.</param>
        internal void ProcessAutomation(Faction faction)
        {
            _features.Automation.ProcessFaction(faction);
            _features.Naming.ProcessFaction(faction);
        }

        /// <summary>
        /// Executes phases through movement and combat.
        /// </summary>
        /// <returns>The incremental tick sequence.</returns>
        private IEnumerable<object> ExecuteCore()
        {
            _game.CurrentTick++;
            _features.Messages.ProcessTick();
            GameLogger.Debug("Tick: " + _game.CurrentTick);

            ProcessEconomyAndUnits();
            List<GameResult> movementPhase = ProcessMovementAndCombat();
            if (_features.SpaceCombat.HasPendingDecision)
            {
                StoreDeferredMessages(movementPhase);
                AwaitCombatDecision();
                yield break;
            }
            _results.DeliverMessages(movementPhase);

            foreach (object step in ProcessMissionsAndStrategy())
                yield return step;
        }

        /// <summary>
        /// Processes automation, economy, maintenance, recovery, and captivity.
        /// </summary>
        private void ProcessEconomyAndUnits()
        {
            _features.Automation.ProcessTick();
            _results.Resolve(_features.Resources.ProcessTick());
            _results.Resolve(_features.Manufacturing.ProcessTick());
            _features.Automation.ProcessTick();
            _results.Resolve(_features.Maintenance.ProcessTick());
            _results.Resolve(_features.Recovery.ProcessTick());
            _results.Resolve(_features.Captives.ProcessTick());
        }

        /// <summary>
        /// Processes movement, combat detection, and available waypoint continuations.
        /// </summary>
        /// <returns>The ordered movement-phase results.</returns>
        private List<GameResult> ProcessMovementAndCombat()
        {
            return Combine(
                _results.Resolve(_features.Movement.ProcessTick(), createMessages: false),
                _results.Resolve(_features.SpaceCombat.ProcessTick(), createMessages: false),
                ContinueWaypoints()
            );
        }

        /// <summary>
        /// Processes missions, authored events, AI, and strategic state.
        /// </summary>
        /// <returns>The incremental AI phase sequence.</returns>
        private IEnumerable<object> ProcessMissionsAndStrategy()
        {
            _results.Resolve(_features.Missions.ProcessTick());
            _results.Resolve(_features.Events.ProcessEvents(_game.GetEventPool()));
            _features.Naming.ProcessTick();

            List<GameResult> aiResults = new List<GameResult>();
            foreach (object step in _features.AI.ProcessTickIncrementally(aiResults))
                yield return step;
            _results.Resolve(aiResults);

            _results.Resolve(_features.Blockades.ProcessTick());
            _results.Resolve(_features.PlanetaryControl.ProcessTick());
            _results.Resolve(_features.Uprisings.ProcessTick());
            _results.Resolve(_features.Research.ProcessTick());
            _results.Resolve(_features.Jedi.ProcessTick());
            _results.Resolve(_features.Victory.ProcessTick());
            _state = ExecutionState.Idle;
            Completed?.Invoke();
        }

        /// <summary>
        /// Routes combat results and resumes remaining phases.
        /// </summary>
        /// <param name="combatResults">The raw combat results.</param>
        /// <returns>The completed combat summary, when present.</returns>
        private SpaceCombatResult CompleteCombat(List<GameResult> combatResults)
        {
            _isExecuting = true;
            _state = ExecutionState.Processing;
            try
            {
                combatResults = _results.Resolve(combatResults, createMessages: false);
                SpaceCombatResult completed = combatResults
                    .OfType<SpaceCombatResult>()
                    .FirstOrDefault();
                List<GameResult> waypoints = ContinueWaypoints();
                List<GameResult> additionalCombat = _results.Resolve(
                    _features.SpaceCombat.ProcessTick(),
                    createMessages: false
                );
                _deferredMessageResults.AddRange(combatResults);
                _deferredMessageResults.AddRange(waypoints);
                _deferredMessageResults.AddRange(additionalCombat);

                if (_features.SpaceCombat.HasPendingDecision)
                {
                    AwaitCombatDecision();
                    return completed;
                }

                _results.DeliverMessages(TakeDeferredMessages());
                IEnumerator remaining = ProcessMissionsAndStrategy().GetEnumerator();
                try
                {
                    while (remaining.MoveNext()) { }
                }
                finally
                {
                    (remaining as IDisposable)?.Dispose();
                }
                return completed;
            }
            finally
            {
                _isExecuting = false;
                if (_state == ExecutionState.Processing)
                    _state = ExecutionState.Idle;
            }
        }

        /// <summary>
        /// Continues waypoint routes when no combat decision blocks movement.
        /// </summary>
        /// <returns>The resulting movement facts.</returns>
        private List<GameResult> ContinueWaypoints()
        {
            if (_features.SpaceCombat.HasPendingDecision)
                return new List<GameResult>();
            return _results.Resolve(
                _features.Movement.ContinueFleetWaypointRoutes(),
                createMessages: false
            );
        }

        /// <summary>
        /// Stores movement-phase facts until combat settles.
        /// </summary>
        /// <param name="results">The facts whose messages must be deferred.</param>
        private void StoreDeferredMessages(IEnumerable<GameResult> results)
        {
            _deferredMessageResults.Clear();
            if (results != null)
                _deferredMessageResults.AddRange(results);
        }

        /// <summary>
        /// Marks execution as waiting for player combat input.
        /// </summary>
        private void AwaitCombatDecision()
        {
            _state = ExecutionState.AwaitingCombatDecision;
            CombatDecisionRequired?.Invoke();
        }

        /// <summary>
        /// Takes and clears facts waiting for combat resolution.
        /// </summary>
        /// <returns>The deferred factual results.</returns>
        private List<GameResult> TakeDeferredMessages()
        {
            List<GameResult> results = new List<GameResult>(_deferredMessageResults);
            _deferredMessageResults.Clear();
            return results;
        }

        /// <summary>
        /// Combines ordered result batches.
        /// </summary>
        /// <param name="batches">The result batches.</param>
        /// <returns>One ordered result list.</returns>
        private static List<GameResult> Combine(params List<GameResult>[] batches)
        {
            List<GameResult> results = new List<GameResult>();
            foreach (List<GameResult> batch in batches)
            {
                if (batch != null)
                    results.AddRange(batch);
            }
            return results;
        }
    }
}
