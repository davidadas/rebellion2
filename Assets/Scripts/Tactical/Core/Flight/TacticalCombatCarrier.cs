using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Rebellion.Game.Combat;
using Rebellion.Util.Random;

namespace Rebellion.Tactical
{
    /// <summary>
    /// Owns a carrier's launch queue, recovered squadrons, and reserved recovery berths.
    /// </summary>
    public sealed class TacticalCombatCarrier
    {
        private readonly List<CombatUnit> _launchQueue = new();
        private readonly List<CombatUnit> _recovered = new();
        private readonly Dictionary<CombatUnit, Recovery> _approaching = new();
        private int _occupied;
        private int _reserved;
        private float _nextLaunch;

        public CombatUnit Unit { get; }
        public int Occupied => _occupied;
        public int Reserved => _reserved;
        public bool IsFullAndLaunched =>
            _reserved == Unit.FighterCapacity && _launchQueue.Count == 0;
        public IReadOnlyList<CombatUnit> Docked { get; }
        public IReadOnlyList<CombatUnit> Recovered { get; }
        public Vector3 HangarPosition =>
            Unit.IsBattleStation
                ? Unit.Position
                : new(Unit.Position.X, Unit.Position.Y - Unit.HalfHeight, Unit.Position.Z);
        public bool IsAvailable =>
            Unit.IsBattleStation
                ? Unit.IsAlive && Unit.Status == CombatUnitStatus.Active
                : Unit.IsTargetable;
        public event Action<TacticalCombatCarrier, CombatUnit> LaunchRequested;

        /// <summary>
        /// Creates the fighter operations state for one capital ship.
        /// </summary>
        /// <param name="unit">The carrier's tactical unit.</param>
        public TacticalCombatCarrier(CombatUnit unit)
        {
            Unit = unit ?? throw new ArgumentNullException(nameof(unit));
            Docked = _launchQueue.AsReadOnly();
            Recovered = _recovered.AsReadOnly();
            if (unit.IsFighter)
                throw new ArgumentException("A carrier must be a capital ship.", nameof(unit));
        }

        /// <summary>
        /// Reserves an initial berth and schedules the first launch when a squadron enters the carrier.
        /// </summary>
        /// <param name="fighter">The initially docked squadron.</param>
        /// <param name="systemTime">The captured command time at attachment.</param>
        /// <param name="random">The encounter's shared launch-timing stream.</param>
        /// <returns>Whether the carrier has capacity for the squadron.</returns>
        public bool AddDocked(CombatUnit fighter, float systemTime, IRandomNumberProvider random)
        {
            if (_reserved >= Unit.FighterCapacity)
                return false;
            _launchQueue.Add(fighter);
            _occupied++;
            _reserved++;
            fighter.CarrierID = Unit.InstanceID;
            fighter.Status = CombatUnitStatus.Docked;
            if (_nextLaunch == 0 || float.IsNaN(_nextLaunch))
                _nextLaunch = (float)(5d + random.NextInt(0, 9) + systemTime);
            return true;
        }

        /// <summary>
        /// Schedules and requests launches from the front of the carrier's queue.
        /// </summary>
        /// <param name="systemTime">The command queue's elapsed whole seconds.</param>
        /// <param name="random">The battle's launch-timing stream.</param>
        public void Advance(float systemTime, IRandomNumberProvider random)
        {
            if (_launchQueue.Count == 0 || !IsAvailable)
                return;
            if (_nextLaunch == 0 || float.IsNaN(_nextLaunch))
            {
                _nextLaunch = (float)(5d + random.NextInt(0, 9) + systemTime);
                return;
            }
            if (systemTime < _nextLaunch)
                return;
            LaunchRequested?.Invoke(this, _launchQueue[0]);
            if (_occupied > 0)
                _nextLaunch = (float)(5d + random.NextInt(0, 9) + systemTime);
        }

        /// <summary>
        /// Applies a launch command after checking that the squadron remains in the queue.
        /// </summary>
        /// <param name="fighter">The squadron being launched.</param>
        /// <returns>Whether the launch changed the squadron to active.</returns>
        public bool Launch(CombatUnit fighter)
        {
            if (!IsAvailable || !_launchQueue.Remove(fighter))
                return false;
            _occupied--;
            _reserved--;
            fighter.Position = HangarPosition;
            fighter.Status = CombatUnitStatus.Active;
            return true;
        }

        /// <summary>
        /// Reserves a recovery berth before the recovery command enters the queue.
        /// </summary>
        /// <param name="fighter">The approaching squadron.</param>
        /// <returns>The recovery holding the berth, or null when no berth is available.</returns>
        internal Recovery ReserveRecovery(CombatUnit fighter)
        {
            if (!IsAvailable || _reserved >= Unit.FighterCapacity)
                return null;
            _reserved++;
            if (!_approaching.TryGetValue(fighter, out Recovery recovery))
            {
                recovery = new Recovery(this, fighter);
                _approaching.Add(fighter, recovery);
            }
            return recovery;
        }

        /// <summary>
        /// Moves an approaching squadron into the carrier after its recovery command.
        /// </summary>
        /// <param name="fighter">The squadron at the hangar entrance.</param>
        /// <returns>Whether the recovery was applied.</returns>
        public bool Recover(CombatUnit fighter)
        {
            if (!IsAvailable || !_approaching.Remove(fighter))
                return false;
            _recovered.Add(fighter);
            _occupied++;
            fighter.Status = CombatUnitStatus.Recovered;
            return true;
        }

        /// <summary>
        /// Releases an approaching or docked squadron when it is destroyed or reassigned.
        /// </summary>
        /// <param name="fighter">The departing squadron.</param>
        public void Detach(CombatUnit fighter)
        {
            if (_approaching.Remove(fighter))
                _reserved--;
            if (_launchQueue.Remove(fighter) || _recovered.Remove(fighter))
            {
                _occupied--;
                _reserved--;
            }
        }

        /// <summary>
        /// Attaches a surviving squadron while the encounter result is assembled.
        /// </summary>
        /// <param name="fighter">The surviving squadron.</param>
        /// <returns>Whether this carrier accepted the squadron.</returns>
        public bool AttachSurvivor(CombatUnit fighter)
        {
            if (_occupied >= Unit.FighterCapacity)
                return false;
            _occupied++;
            fighter.CarrierID = Unit.InstanceID;
            fighter.Status = CombatUnitStatus.Docked;
            if (!_recovered.Contains(fighter))
                _recovered.Add(fighter);
            return true;
        }

        /// <summary>
        /// Removes all carrier-owned lists after their destruction consequences are applied.
        /// </summary>
        public void Clear()
        {
            _launchQueue.Clear();
            _recovered.Clear();
            _approaching.Clear();
            _occupied = 0;
            _reserved = 0;
        }

        /// <summary>
        /// Captures launch order and berth counters without consuming random draws.
        /// </summary>
        /// <returns>The carrier's saved state.</returns>
        internal ActiveBattle.CarrierState CaptureState() =>
            new()
            {
                UnitID = Unit.InstanceID,
                Occupied = _occupied,
                Reserved = _reserved,
                NextLaunch = _nextLaunch,
                LaunchQueue = _launchQueue.ConvertAll(unit => unit.InstanceID),
                Recovered = _recovered.ConvertAll(unit => unit.InstanceID),
                Recoveries = _approaching
                    .Select(pair => new ActiveBattle.RecoveryState
                    {
                        FighterID = pair.Key.InstanceID,
                        Phase = pair.Value.Phase,
                        HoldsReservation = true,
                    })
                    .ToList(),
            };

        /// <summary>
        /// Restores current berths and the session's index of assigned recovery carriers.
        /// </summary>
        /// <param name="state">The saved carrier state.</param>
        /// <param name="getUnit">Resolves identities into the loaded battle roster.</param>
        /// <param name="recoveries">The session's index by approaching or recovered fighter.</param>
        internal void RestoreState(
            ActiveBattle.CarrierState state,
            Func<string, CombatUnit> getUnit,
            Dictionary<CombatUnit, Recovery> recoveries
        )
        {
            _occupied = state.Occupied;
            _reserved = state.Reserved;
            _nextLaunch = state.NextLaunch;
            _launchQueue.AddRange(state.LaunchQueue.Select(getUnit));
            _recovered.AddRange(state.Recovered.Select(getUnit));
            foreach (ActiveBattle.RecoveryState assignment in state.Recoveries)
            {
                CombatUnit fighter = getUnit(assignment.FighterID);
                Recovery recovery = new(this, fighter) { Phase = assignment.Phase };
                if (assignment.HoldsReservation)
                    _approaching.Add(fighter, recovery);
                recoveries.Add(fighter, recovery);
            }
        }

        internal sealed class Recovery
        {
            private readonly CombatUnit _fighter;

            internal ActiveBattle.RecoveryPhase Phase { get; set; }

            public TacticalCombatCarrier Carrier { get; }
            public bool IsAssigned => Phase != ActiveBattle.RecoveryPhase.Reserved;

            /// <summary>
            /// Records the fighter and carrier associated with one reserved berth.
            /// </summary>
            /// <param name="carrier">The carrier reserving a recovery berth.</param>
            /// <param name="fighter">The fighter squadron associated with the berth.</param>
            internal Recovery(TacticalCombatCarrier carrier, CombatUnit fighter)
            {
                Carrier = carrier;
                _fighter = fighter;
            }

            /// <summary>
            /// Applies the queued carrier assignment so the fighter can approach.
            /// </summary>
            public void BeginApproach()
            {
                Phase = ActiveBattle.RecoveryPhase.Approaching;
                _fighter.CarrierID = Carrier.Unit.InstanceID;
            }

            /// <summary>
            /// Admits a landing when the assigned fighter is close enough to the hangar.
            /// </summary>
            /// <returns>Whether the landing command should be queued.</returns>
            public bool TryQueueLanding()
            {
                if (Phase != ActiveBattle.RecoveryPhase.Approaching)
                    return false;

                float distance = TacticalCombatGeometry.GetRecoveryDistance(
                    _fighter.Position,
                    Carrier.HangarPosition
                );
                // The original also admits a landing when the distance is NaN.
                if (distance >= 2)
                    return false;

                Phase = ActiveBattle.RecoveryPhase.LandingQueued;
                return true;
            }
        }
    }
}
