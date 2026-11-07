using System;
using System.Collections.Generic;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Identifies the admitted withdrawal's countdown, departure, and scuttling phases.
    /// </summary>
    public enum TacticalCombatWithdrawalPhase
    {
        Waiting = 2,
        Departing = 3,
        Scuttling = 4,
    }

    /// <summary>
    /// Provides live readiness and participation facts for an ordered withdrawal member.
    /// </summary>
    public interface ITacticalCombatWithdrawalMember
    {
        int Index { get; }
        bool IsActiveMember { get; }
        bool IsAwaitingDeparture { get; }

        /// <summary>
        /// Refreshes withdrawal readiness from the member's current movement budgets.
        /// </summary>
        /// <returns>Whether both required budgets are ordered and nonzero.</returns>
        bool CheckWithdrawalReadiness();
    }

    /// <summary>
    /// Advances an admitted withdrawal while preserving ordered member checks and commands.
    /// </summary>
    public sealed class TacticalCombatWithdrawal
    {
        private readonly int _factionIndex;
        private readonly Action _prepareStationWithdrawal;
        private readonly Action _requestStationWithdrawal;

        public TacticalCombatWithdrawalPhase Phase { get; private set; } =
            TacticalCombatWithdrawalPhase.Waiting;
        public float RemainingMilliseconds { get; private set; }

        /// <summary>
        /// Captures an admitted withdrawal before its group setup and final delay selection.
        /// </summary>
        /// <param name="factionIndex">The authored faction index.</param>
        /// <param name="remainingMilliseconds">The captured route-derived withdrawal delay.</param>
        /// <param name="prepareStationWithdrawal">Drains display commands and closes the station control.</param>
        /// <param name="requestStationWithdrawal">Queues the station's departure command.</param>
        public TacticalCombatWithdrawal(
            int factionIndex,
            float remainingMilliseconds,
            Action prepareStationWithdrawal,
            Action requestStationWithdrawal
        )
        {
            if (factionIndex is not 0 and not 1)
                throw new ArgumentOutOfRangeException(nameof(factionIndex));
            _factionIndex = factionIndex;
            RemainingMilliseconds = remainingMilliseconds;
            _prepareStationWithdrawal =
                prepareStationWithdrawal
                ?? throw new ArgumentNullException(nameof(prepareStationWithdrawal));
            _requestStationWithdrawal =
                requestStationWithdrawal
                ?? throw new ArgumentNullException(nameof(requestStationWithdrawal));
        }

        /// <summary>
        /// Applies the departure minimum after the withdrawal's group commands are prepared.
        /// </summary>
        /// <param name="routeMilliseconds">The current route-derived delay.</param>
        /// <param name="factionIndex">The authored faction index.</param>
        /// <param name="stationOperational">Whether the battle station is operational.</param>
        /// <returns>The selected delay, replacing unordered values with the applicable minimum.</returns>
        public static float CalculateInitialDelay(
            float routeMilliseconds,
            int factionIndex,
            bool stationOperational
        )
        {
            float minimum = factionIndex == 1 && stationOperational ? 150000 : 30000;
            return routeMilliseconds > minimum ? routeMilliseconds : minimum;
        }

        /// <summary>
        /// Applies the departure minimum after the caller has prepared its withdrawal groups.
        /// </summary>
        /// <param name="stationOperational">Whether the station remains operational after group setup.</param>
        public void SelectInitialDelay(bool stationOperational) =>
            RemainingMilliseconds = CalculateInitialDelay(
                RemainingMilliseconds,
                _factionIndex,
                stationOperational
            );

        /// <summary>
        /// Advances the countdown before candidate maintenance and station launch-distance updates.
        /// </summary>
        /// <param name="elapsedMilliseconds">The admitted whole-tick interval.</param>
        /// <param name="handlesObjectTransitions">Whether this side processes individual object transitions.</param>
        /// <param name="stationOperational">Whether the battle station is operational.</param>
        /// <param name="isPlayerFaction">Whether this side belongs to the player.</param>
        /// <returns>Whether the caller should continue individual object processing.</returns>
        public bool AdvanceCountdown(
            float elapsedMilliseconds,
            bool handlesObjectTransitions,
            bool stationOperational,
            bool isPlayerFaction
        )
        {
            if (Phase == TacticalCombatWithdrawalPhase.Waiting)
            {
                double remaining = (double)RemainingMilliseconds - elapsedMilliseconds;
                RemainingMilliseconds = (float)remaining;
                if (!(remaining > 0))
                {
                    Phase = TacticalCombatWithdrawalPhase.Departing;
                    if (!handlesObjectTransitions)
                        return false;
                    if (_factionIndex == 1 && stationOperational)
                    {
                        if (isPlayerFaction)
                            _prepareStationWithdrawal();
                        _requestStationWithdrawal();
                    }
                }
            }
            return handlesObjectTransitions;
        }

        /// <summary>
        /// Checks capital members before fighters after the caller's intervening maintenance phase.
        /// </summary>
        /// <param name="capitals">The side's ordered capital chain.</param>
        /// <param name="fighters">The side's ordered deployed fighter chain.</param>
        /// <param name="requestScuttle">Queues an unready member's scuttle command.</param>
        public void AdvanceMembers(
            IReadOnlyList<ITacticalCombatWithdrawalMember> capitals,
            IReadOnlyList<ITacticalCombatWithdrawalMember> fighters,
            Action<int> requestScuttle
        )
        {
            if (Phase != TacticalCombatWithdrawalPhase.Departing)
                return;
            if (capitals == null)
                throw new ArgumentNullException(nameof(capitals));
            if (fighters == null)
                throw new ArgumentNullException(nameof(fighters));
            if (requestScuttle == null)
                throw new ArgumentNullException(nameof(requestScuttle));
            if (HasReadyMember(capitals) || HasReadyMember(fighters))
                return;

            Phase = TacticalCombatWithdrawalPhase.Scuttling;
            RequestScuttles(capitals, requestScuttle);
            RequestScuttles(fighters, requestScuttle);
        }

        /// <summary>
        /// Refreshes readiness before reading the current membership and departure facts.
        /// </summary>
        /// <param name="members">The ordered chain to inspect.</param>
        /// <returns>Whether a qualifying ready member remains.</returns>
        private static bool HasReadyMember(IReadOnlyList<ITacticalCombatWithdrawalMember> members)
        {
            for (int index = 0; index < members.Count; index++)
            {
                ITacticalCombatWithdrawalMember member = members[index];
                if (
                    member.CheckWithdrawalReadiness()
                    && (member.IsActiveMember || member.IsAwaitingDeparture)
                )
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Rechecks every member and queues unready members without adding a membership filter.
        /// </summary>
        /// <param name="members">The ordered chain to inspect again.</param>
        /// <param name="requestScuttle">Queues the member's scuttle command.</param>
        private static void RequestScuttles(
            IReadOnlyList<ITacticalCombatWithdrawalMember> members,
            Action<int> requestScuttle
        )
        {
            for (int index = 0; index < members.Count; index++)
            {
                ITacticalCombatWithdrawalMember member = members[index];
                if (!member.CheckWithdrawalReadiness())
                    requestScuttle(member.Index);
            }
        }
    }
}
