using System;
using System.Collections.Generic;
using System.Numerics;
using Rebellion.Util.Random;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Owns a carrier's launch queue, recovered squadrons, and reserved recovery berths.
    /// </summary>
    public sealed class TacticalCombatCarrier
    {
        private readonly List<TacticalCombatUnit> _launchQueue = new();
        private readonly List<TacticalCombatUnit> _recovered = new();
        private readonly HashSet<TacticalCombatUnit> _approaching = new();
        private int _occupied;
        private int _reserved;
        private float _nextLaunch;

        public TacticalCombatUnit Unit { get; }
        public int Occupied => _occupied;
        public int Reserved => _reserved;
        public bool IsFullAndLaunched =>
            _reserved == Unit.FighterCapacity && _launchQueue.Count == 0;
        public IReadOnlyList<TacticalCombatUnit> Docked { get; }
        public IReadOnlyList<TacticalCombatUnit> Recovered { get; }
        public Vector3 HangarPosition =>
            Unit.IsBattleStation
                ? Unit.Position
                : new(Unit.Position.X, Unit.Position.Y - Unit.HalfHeight, Unit.Position.Z);
        public bool IsAvailable =>
            Unit.IsBattleStation
                ? Unit.Systems.IsAlive && Unit.Status == TacticalCombatUnitStatus.Active
                : Unit.IsTargetable;
        public event Action<TacticalCombatCarrier, TacticalCombatUnit> LaunchRequested;

        /// <summary>
        /// Creates the fighter operations state for one capital ship.
        /// </summary>
        /// <param name="unit">The carrier's tactical unit.</param>
        public TacticalCombatCarrier(TacticalCombatUnit unit)
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
        public bool AddDocked(
            TacticalCombatUnit fighter,
            float systemTime,
            IRandomNumberProvider random
        )
        {
            if (_reserved >= Unit.FighterCapacity)
                return false;
            _launchQueue.Add(fighter);
            _occupied++;
            _reserved++;
            fighter.CarrierID = Unit.InstanceID;
            fighter.Status = TacticalCombatUnitStatus.Docked;
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
        public bool Launch(TacticalCombatUnit fighter)
        {
            if (!IsAvailable || !_launchQueue.Remove(fighter))
                return false;
            _occupied--;
            _reserved--;
            fighter.Position = HangarPosition;
            fighter.Status = TacticalCombatUnitStatus.Active;
            return true;
        }

        /// <summary>
        /// Reserves a recovery berth before the recovery command enters the queue.
        /// </summary>
        /// <param name="fighter">The approaching squadron.</param>
        /// <returns>Whether a berth was reserved.</returns>
        public bool Reserve(TacticalCombatUnit fighter)
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
        public bool Recover(TacticalCombatUnit fighter)
        {
            if (!IsAvailable || !_approaching.Remove(fighter))
                return false;
            _recovered.Add(fighter);
            _occupied++;
            fighter.Status = TacticalCombatUnitStatus.Recovered;
            return true;
        }

        /// <summary>
        /// Releases an approaching or docked squadron when it is destroyed or reassigned.
        /// </summary>
        /// <param name="fighter">The departing squadron.</param>
        public void Detach(TacticalCombatUnit fighter)
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
        public bool AttachSurvivor(TacticalCombatUnit fighter)
        {
            if (_occupied >= Unit.FighterCapacity)
                return false;
            _occupied++;
            fighter.CarrierID = Unit.InstanceID;
            fighter.Status = TacticalCombatUnitStatus.Docked;
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
