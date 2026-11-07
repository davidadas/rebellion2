using System;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Captures the battle facts used by station briefings and global attack-run admission.
    /// Fighter counts describe the active tactical root, independently of selection and withdrawal.
    /// </summary>
    public readonly struct TacticalCombatDirectorState
    {
        public bool IsArriving { get; }
        public bool StationExists { get; }
        public bool StationOperational { get; }
        public bool StationProtected { get; }
        public bool AttackRunActive { get; }
        public bool AllianceFighterChainPopulated { get; }
        public bool EmpireFighterChainPopulated { get; }
        public bool AllianceAttackRunOrdered { get; }
        public int AllianceActiveFighters { get; }
        public int EmpireActiveFighters { get; }
        public int AllianceActiveFightersOutsideAttackRuns { get; }

        public bool CanAttackBattleStation =>
            StationExists
            && StationOperational
            && !StationProtected
            && !AttackRunActive
            && AllianceActiveFighters > EmpireActiveFighters
            && !AllianceAttackRunOrdered;

        /// <summary>
        /// Captures the explicit station, root membership, and group-order facts for one frame.
        /// </summary>
        /// <param name="isArriving">Whether the arrival lifecycle is active.</param>
        /// <param name="stationExists">Whether a battle-station record exists.</param>
        /// <param name="stationOperational">Whether that station is operational.</param>
        /// <param name="stationProtected">Whether that station's protection gate is set.</param>
        /// <param name="attackRunActive">Whether an attack-run helper currently exists.</param>
        /// <param name="allianceFighterChainPopulated">Whether the Alliance fighter chain is nonempty.</param>
        /// <param name="empireFighterChainPopulated">Whether the Empire fighter chain is nonempty.</param>
        /// <param name="allianceAttackRunOrdered">Whether an Alliance group requests or performs an attack run.</param>
        /// <param name="allianceActiveFighters">Every Alliance fighter in the active root.</param>
        /// <param name="empireActiveFighters">Every Empire fighter in the active root.</param>
        /// <param name="allianceActiveFightersOutsideAttackRuns">Alliance active fighters excluding attack-run members.</param>
        public TacticalCombatDirectorState(
            bool isArriving,
            bool stationExists,
            bool stationOperational,
            bool stationProtected,
            bool attackRunActive,
            bool allianceFighterChainPopulated,
            bool empireFighterChainPopulated,
            bool allianceAttackRunOrdered,
            int allianceActiveFighters,
            int empireActiveFighters,
            int allianceActiveFightersOutsideAttackRuns
        )
        {
            IsArriving = isArriving;
            StationExists = stationExists;
            StationOperational = stationOperational;
            StationProtected = stationProtected;
            AttackRunActive = attackRunActive;
            AllianceFighterChainPopulated = allianceFighterChainPopulated;
            EmpireFighterChainPopulated = empireFighterChainPopulated;
            AllianceAttackRunOrdered = allianceAttackRunOrdered;
            AllianceActiveFighters = allianceActiveFighters;
            EmpireActiveFighters = empireActiveFighters;
            AllianceActiveFightersOutsideAttackRuns = allianceActiveFightersOutsideAttackRuns;
        }
    }

    /// <summary>
    /// Sequences encounter briefings and observes attack-run admission edges on unscaled time.
    /// </summary>
    public sealed class TacticalCombatDirector
    {
        private readonly int _playerFactionIndex;
        private readonly Action<int> _requestNotification;
        private int _stage;
        private float _remainingMilliseconds;
        private bool _previousAdmission;

        /// <summary>
        /// Creates notification sequencing for the explicitly selected player faction.
        /// </summary>
        /// <param name="playerFactionIndex">The authored faction index: Alliance 0 or Empire 1.</param>
        /// <param name="requestNotification">Requests a one-shot authored notification.</param>
        public TacticalCombatDirector(int playerFactionIndex, Action<int> requestNotification)
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
        public void Advance(float elapsedMilliseconds, TacticalCombatDirectorState state)
        {
            if (_stage == 1000)
            {
                _remainingMilliseconds -= elapsedMilliseconds;
                if (_remainingMilliseconds > 0)
                    return;
                _stage = 4;
            }
            if (_stage == 4)
            {
                ObserveAdmission(state);
                return;
            }
            if (_stage == 0 && state.IsArriving)
                return;

            _remainingMilliseconds -= elapsedMilliseconds;
            if (_remainingMilliseconds > 0)
                return;
            _remainingMilliseconds = 6000;
            _requestNotification(GetBriefingMessage());
            switch (_stage)
            {
                case 0:
                    _stage =
                        _playerFactionIndex == 0 && state.StationExists && state.StationOperational
                            ? 1
                            : 1000;
                    break;
                case 1:
                    if (!state.StationExists)
                        _stage = 1000;
                    else if (state.StationOperational && state.StationProtected)
                        _stage = 2;
                    else if (
                        state.AllianceFighterChainPopulated
                        && !state.CanAttackBattleStation
                        && !state.AttackRunActive
                        && state.AllianceActiveFightersOutsideAttackRuns
                            <= state.EmpireActiveFighters
                    )
                        _stage = 3;
                    else
                        _stage = 1000;
                    break;
                case 2:
                case 3:
                    _stage = 1000;
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
                0 => _playerFactionIndex == 0 ? 32 : 154,
                1 => 144,
                2 => 147,
                3 => 148,
                _ => throw new InvalidOperationException("The briefing stage is not active."),
            };

        /// <summary>
        /// Records every admission edge, including edges whose additional cue gates reject playback.
        /// </summary>
        /// <param name="state">The current global admission and fighter-chain facts.</param>
        private void ObserveAdmission(TacticalCombatDirectorState state)
        {
            bool admission = state.CanAttackBattleStation;
            if (_previousAdmission && !admission)
            {
                if (
                    _playerFactionIndex == 0
                    && state.AllianceFighterChainPopulated
                    && state.EmpireFighterChainPopulated
                    && !state.AttackRunActive
                    && !state.AllianceAttackRunOrdered
                )
                    _requestNotification(148);
            }
            else if (!_previousAdmission && admission)
            {
                if (_playerFactionIndex == 0)
                    _requestNotification(146);
                else if (state.AllianceFighterChainPopulated)
                    _requestNotification(284);
            }
            _previousAdmission = admission;
        }
    }
}
