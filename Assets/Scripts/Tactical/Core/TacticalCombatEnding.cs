using System;
using System.Collections.Generic;

namespace Rebellion.Tactical
{
    /// <summary>
    /// Captures one faction's active-root count, withdrawal state, and roster departure facts.
    /// </summary>
    internal readonly struct TacticalCombatForceAssessment
    {
        public int ActiveCount { get; }
        public int WithdrawalState { get; }
        public bool StillActive { get; }
        public bool HasDeparted { get; }

        /// <summary>
        /// Captures the distinct active-root and roster observations of one combat faction.
        /// </summary>
        /// <param name="activeCount">The active tactical count, including ongoing departures and attack runs.</param>
        /// <param name="withdrawalState">The faction's stored withdrawal lifecycle state.</param>
        /// <param name="stillActive">Whether an ordinary active or departing roster member remains.</param>
        /// <param name="hasDeparted">Whether a roster member has completed departure.</param>
        public TacticalCombatForceAssessment(
            int activeCount,
            int withdrawalState,
            bool stillActive,
            bool hasDeparted
        )
        {
            ActiveCount = activeCount;
            WithdrawalState = withdrawalState;
            StillActive = stillActive;
            HasDeparted = hasDeparted;
        }
    }

    /// <summary>
    /// Captures the final force assessment and tracks the delay before combat ends.
    /// </summary>
    internal sealed class TacticalCombatEnding
    {
        private readonly bool[] _flags = new bool[7];
        private bool _assessmentCaptured;

        public IReadOnlyList<bool> Flags { get; }
        public float RemainingMilliseconds { get; private set; }
        public bool IsComplete { get; private set; }
        public bool ImmediateResultRequested => _flags[6];

        /// <summary>
        /// Restores the authoritative terminal flags without running local ending callbacks.
        /// </summary>
        /// <param name="flags">The seven validated terminal flags.</param>
        internal void ApplySnapshotFlags(bool[] flags) => Array.Copy(flags, _flags, _flags.Length);

        internal TacticalCombatEnding() => Flags = Array.AsReadOnly(_flags);

        /// <summary>
        /// Captures a terminal assessment once after active-object movement and counting complete.
        /// The caller admits this transition only while the encounter lifecycle is active.
        /// </summary>
        /// <param name="alliance">The Alliance active-root and roster assessment.</param>
        /// <param name="empire">The Empire active-root and roster assessment.</param>
        /// <param name="stationOperational">Whether the Empire station remains operational.</param>
        /// <param name="stationDeparted">Whether the station has completed departure.</param>
        /// <returns>Whether this call begins the ending lifecycle.</returns>
        public bool TryBegin(
            TacticalCombatForceAssessment alliance,
            TacticalCombatForceAssessment empire,
            bool stationOperational,
            bool stationDeparted
        )
        {
            if (
                _assessmentCaptured
                || IsComplete
                || alliance.ActiveCount != 0 && (empire.ActiveCount != 0 || stationOperational)
            )
                return false;

            _assessmentCaptured = true;
            RemainingMilliseconds = 4000;
            _flags[0] = IsWithdrawing(alliance) && !alliance.StillActive && alliance.HasDeparted;
            _flags[1] = empire.ActiveCount == 0 && !stationOperational;
            _flags[2] =
                IsWithdrawing(empire)
                && !empire.StillActive
                && (empire.HasDeparted || stationDeparted);
            _flags[3] = alliance.ActiveCount == 0;
            if (_flags[1] && _flags[3])
            {
                _flags[1] = false;
                _flags[3] = false;
            }

            return true;
        }

        /// <summary>
        /// Consumes one whole-second terminal tick at dispatch phase 5, after the pause guard.
        /// </summary>
        /// <returns>Whether the encounter is ready to return to strategic play.</returns>
        public bool AdvanceTerminalTick()
        {
            if (IsComplete || !_assessmentCaptured)
                return IsComplete;
            RemainingMilliseconds -= 1000;
            if (!(RemainingMilliseconds > 0))
                IsComplete = true;
            return IsComplete;
        }

        /// <summary>
        /// Records an admitted explicit finish without adding a terminal cue or waiting interval.
        /// </summary>
        public void FinishWithoutAssessment()
        {
            _flags[6] = true;
            IsComplete = true;
        }

        /// <summary>
        /// Tests the faction states that record an attempted or completed withdrawal.
        /// </summary>
        /// <param name="assessment">The captured faction assessment.</param>
        /// <returns>Whether its withdrawal lifecycle participates in terminal departure flags.</returns>
        private static bool IsWithdrawing(TacticalCombatForceAssessment assessment) =>
            assessment.WithdrawalState is 3 or 4 or 5;

        /// <summary>
        /// Selects the player's terminal cue in withdrawal-before-victory priority order.
        /// </summary>
        /// <param name="factionIndex">The authored faction index receiving the notification.</param>
        /// <returns>The authored notification, or null when no terminal cue applies.</returns>
        public int? GetTerminalNotification(int factionIndex)
        {
            if (factionIndex == 0)
            {
                if (_flags[1] && _flags[2])
                    return 151;
                if (_flags[0])
                    return 150;
                if (_flags[1])
                    return 152;
                if (_flags[3])
                    return 153;
            }
            else
            {
                if (_flags[3] && _flags[0])
                    return 281;
                if (_flags[2])
                    return 280;
                if (_flags[3])
                    return 282;
                if (_flags[1])
                    return 283;
            }
            return null;
        }
    }
}
