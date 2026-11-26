using System;

namespace Rebellion.Tactical
{
    /// <summary>
    /// Sequences encounter briefings and observes attack-run admission edges on unscaled time.
    /// </summary>
    internal sealed class TacticalCombatBriefingSequence
    {
        private const float _briefingIntervalMilliseconds = 6000;
        private const int _allianceOpeningMessage = 32;
        private const int _empireOpeningMessage = 154;
        private const int _stationBriefingMessage = 144;
        private const int _stationProtectionMessage = 147;
        private const int _fighterDisadvantageMessage = 148;
        private const int _allianceAttackRunAvailableMessage = 146;
        private const int _enemyAttackRunAvailableMessage = 284;
        private readonly int _playerFactionIndex;
        private readonly Action<int> _requestNotification;
        private BriefingStage _stage;
        private float _remainingMilliseconds;
        private bool _previousAdmission;

        /// <summary>
        /// Creates notification sequencing for the explicitly selected player faction.
        /// </summary>
        /// <param name="playerFactionIndex">The authored faction index: Alliance 0 or Empire 1.</param>
        /// <param name="requestNotification">Requests a one-shot authored notification.</param>
        public TacticalCombatBriefingSequence(
            int playerFactionIndex,
            Action<int> requestNotification
        )
        {
            if (playerFactionIndex is not 0 and not 1)
                throw new ArgumentOutOfRangeException(nameof(playerFactionIndex));
            _playerFactionIndex = playerFactionIndex;
            _requestNotification =
                requestNotification ?? throw new ArgumentNullException(nameof(requestNotification));
        }

        /// <summary>
        /// Advances briefings before the simulation pause guard and then observes admission edges.
        /// </summary>
        /// <param name="elapsedMilliseconds">The unscaled frame interval.</param>
        /// <param name="state">The station, active-root, chain, and order facts for this frame.</param>
        /// <param name="isArriving">Whether the arrival lifecycle still holds the opening briefing.</param>
        public void Advance(
            float elapsedMilliseconds,
            TacticalCombatBattleStation.AttackReadiness state,
            bool isArriving = false
        )
        {
            if (_stage == BriefingStage.WaitingForAdmission)
            {
                _remainingMilliseconds -= elapsedMilliseconds;
                if (_remainingMilliseconds > 0)
                    return;
                _stage = BriefingStage.ObservingAdmission;
            }
            if (_stage == BriefingStage.ObservingAdmission)
            {
                ObserveAdmission(state);
                return;
            }
            if (_stage == BriefingStage.Opening && isArriving)
                return;

            _remainingMilliseconds -= elapsedMilliseconds;
            if (_remainingMilliseconds > 0)
                return;
            _remainingMilliseconds = _briefingIntervalMilliseconds;
            _requestNotification(GetBriefingMessage());
            switch (_stage)
            {
                case BriefingStage.Opening:
                    _stage =
                        _playerFactionIndex == 0 && state.StationExists && state.StationOperational
                            ? BriefingStage.Station
                            : BriefingStage.WaitingForAdmission;
                    break;
                case BriefingStage.Station:
                    if (!state.StationExists)
                        _stage = BriefingStage.WaitingForAdmission;
                    else if (state.StationOperational && state.StationProtected)
                        _stage = BriefingStage.Protection;
                    else if (
                        state.AllianceFighterChainPopulated
                        && !state.CanAttackBattleStation
                        && !state.AttackRunActive
                        && state.AllianceActiveFightersOutsideAttackRuns
                            <= state.EmpireActiveFighters
                    )
                        _stage = BriefingStage.FighterBalance;
                    else
                        _stage = BriefingStage.WaitingForAdmission;
                    break;
                case BriefingStage.Protection:
                case BriefingStage.FighterBalance:
                    _stage = BriefingStage.WaitingForAdmission;
                    break;
            }
        }

        /// <summary>
        /// Chooses the briefing associated with the current sequencing stage.
        /// </summary>
        /// <returns>The authored notification identifier.</returns>
        private int GetBriefingMessage() =>
            _stage switch
            {
                BriefingStage.Opening => _playerFactionIndex == 0
                    ? _allianceOpeningMessage
                    : _empireOpeningMessage,
                BriefingStage.Station => _stationBriefingMessage,
                BriefingStage.Protection => _stationProtectionMessage,
                BriefingStage.FighterBalance => _fighterDisadvantageMessage,
                _ => throw new InvalidOperationException("The briefing stage is not active."),
            };

        /// <summary>
        /// Records every admission edge, including edges whose additional cue gates reject playback.
        /// </summary>
        /// <param name="state">The current global admission and fighter-chain facts.</param>
        private void ObserveAdmission(TacticalCombatBattleStation.AttackReadiness state)
        {
            bool admission = state.CanAttackBattleStation;
            if (_previousAdmission && !admission)
            {
                if (
                    _playerFactionIndex == 0
                    && state.AllianceFighterChainPopulated
                    && state.EmpireFighterChainPopulated
                    && !state.AttackRunActive
                    && !state.AttackRunOrdered
                )
                    _requestNotification(_fighterDisadvantageMessage);
            }
            else if (!_previousAdmission && admission)
            {
                if (_playerFactionIndex == 0)
                    _requestNotification(_allianceAttackRunAvailableMessage);
                else if (state.AllianceFighterChainPopulated)
                    _requestNotification(_enemyAttackRunAvailableMessage);
            }
            _previousAdmission = admission;
        }

        private enum BriefingStage
        {
            Opening,
            Station,
            Protection,
            FighterBalance,
            ObservingAdmission,
            WaitingForAdmission,
        }
    }
}
