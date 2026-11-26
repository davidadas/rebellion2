using System;
using System.Collections.Generic;
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
        private readonly HashSet<CombatUnit> _approaching = new();
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
        /// <returns>Whether a berth was reserved.</returns>
        public bool Reserve(CombatUnit fighter)
        {
            if (!IsAvailable || _reserved >= Unit.FighterCapacity)
                return false;
            _reserved++;
            _approaching.Add(fighter);
            return true;
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
    }
}
