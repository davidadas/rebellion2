using System;
using System.Collections.Generic;
using Rebellion.Game.Results;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Preserves writable scheduler continuations and their existing result-release boundaries.
    /// </summary>
    public sealed partial class GameTickProcessor
    {
        /// <summary>
        /// Captures a continuation without consuming results or accepting an active iterator.
        /// </summary>
        /// <returns>The exact writable phase and its deferred facts.</returns>
        internal GameTickRecoveryState CaptureRecoveryState()
        {
            if (!CanAcceptPlayerActions || _tickState == TickExecutionState.Processing)
                throw new InvalidOperationException(
                    "An executing tick cannot be captured for recovery."
                );
            return new GameTickRecoveryState
            {
                Phase =
                    _tickState == TickExecutionState.Idle
                        ? GameTickRecoveryPhase.Idle
                        : GameTickRecoveryPhase.AwaitingCombatDecision,
                DeferredMessageResults = new List<GameResult>(_deferredMessageResults),
                DeferredBlockadeResults = new List<BlockadeChangedResult>(_deferredBlockadeResults),
            };
        }

        /// <summary>
        /// Restores a writable continuation without running another phase or issuing a decision event.
        /// </summary>
        /// <param name="state">The captured scheduler continuation.</param>
        internal void RestoreRecoveryState(GameTickRecoveryState state)
        {
            if (!CanAcceptPlayerActions)
                throw new InvalidOperationException(
                    "An executing tick cannot receive recovery state."
                );
            if (state?.DeferredMessageResults == null || state.DeferredBlockadeResults == null)
                throw new ArgumentException(
                    "Complete tick recovery facts are required.",
                    nameof(state)
                );
            TickExecutionState phase = state.Phase switch
            {
                GameTickRecoveryPhase.Idle => TickExecutionState.Idle,
                GameTickRecoveryPhase.AwaitingCombatDecision =>
                    TickExecutionState.AwaitingCombatDecision,
                _ => throw new ArgumentException(
                    "The recovered tick phase is unsupported.",
                    nameof(state)
                ),
            };
            _deferredMessageResults.Clear();
            _deferredMessageResults.AddRange(state.DeferredMessageResults);
            _deferredBlockadeResults.Clear();
            _deferredBlockadeResults.AddRange(state.DeferredBlockadeResults);
            _tickState = phase;
        }
    }
}
