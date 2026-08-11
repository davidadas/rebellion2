using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Rebellion.Game.Tactical
{
    /// <summary>
    /// Maintains tractor locks and applies their combined strength to tactical movement.
    /// </summary>
    internal sealed class TacticalTractorBeamSystem
    {
        private const int _maximumLocksPerTarget = 4;
        private readonly List<TacticalCombatEvent> events = new List<TacticalCombatEvent>();
        private readonly Dictionary<TacticalUnitState, TacticalUnitState> locks =
            new Dictionary<TacticalUnitState, TacticalUnitState>();

        /// <summary>
        /// Establishes, preserves, or releases one unit's tractor lock on its current target.
        /// </summary>
        /// <param name="source">The unit producing tractor strength.</param>
        /// <param name="target">The unit's current opposing target, if any.</param>
        public void UpdateLock(TacticalUnitState source, TacticalUnitState target)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (locks.TryGetValue(source, out TacticalUnitState currentTarget))
            {
                if (ReferenceEquals(currentTarget, target) && CanMaintainLock(source, target))
                    return;

                ReleaseLock(source, currentTarget);
            }

            if (!CanMaintainLock(source, target) || CountLocks(target) >= _maximumLocksPerTarget)
                return;

            locks.Add(source, target);
            events.Add(
                TacticalCombatEvent.TractorLock(TacticalCombatEventKind.TractorLock, source, target)
            );
        }

        /// <summary>
        /// Releases locks whose source or target can no longer sustain the relationship.
        /// </summary>
        public void ReleaseInvalidLocks()
        {
            foreach (
                KeyValuePair<TacticalUnitState, TacticalUnitState> tractorLock in locks.ToArray()
            )
            {
                if (!CanMaintainLock(tractorLock.Key, tractorLock.Value))
                    ReleaseLock(tractorLock.Key, tractorLock.Value);
            }
        }

        /// <summary>
        /// Gets the movement remaining after every active tractor lock is applied.
        /// </summary>
        /// <param name="unit">The unit whose movement is requested.</param>
        /// <param name="commandBudget">The movement supplied by the unit's tactical commander.</param>
        /// <returns>The nonnegative tactical movement budget.</returns>
        public float GetMovementSpeed(TacticalUnitState unit, float commandBudget = 0f)
        {
            if (unit == null)
                throw new ArgumentNullException(nameof(unit));

            float tractorStrength = locks
                .Where(tractorLock => ReferenceEquals(tractorLock.Value, unit))
                .Sum(tractorLock => tractorLock.Key.EffectiveTractorBeamPower);
            return Math.Max(0f, unit.GetEffectiveSublightSpeed(commandBudget) - tractorStrength);
        }

        /// <summary>
        /// Removes and returns every lock event produced since the previous drain.
        /// </summary>
        /// <returns>The lock events in simulation order.</returns>
        public IReadOnlyList<TacticalCombatEvent> DrainEvents()
        {
            TacticalCombatEvent[] result = events.ToArray();
            events.Clear();
            return result;
        }

        /// <summary>
        /// Determines whether a source can currently hold a target inside tractor range.
        /// </summary>
        /// <param name="source">The prospective locking unit.</param>
        /// <param name="target">The prospective locked unit.</param>
        /// <returns>True when the lock can be active.</returns>
        private static bool CanMaintainLock(TacticalUnitState source, TacticalUnitState target)
        {
            return source.IsActive
                && target?.IsActive == true
                && source.Side != target.Side
                && source.EffectiveTractorBeamPower > 0f
                && Vector3.Distance(source.Position, target.Position) <= source.TractorBeamRange;
        }

        /// <summary>
        /// Counts the sources already applying tractor strength to one target.
        /// </summary>
        /// <param name="target">The target whose incoming locks are counted.</param>
        /// <returns>The number of active incoming locks.</returns>
        private int CountLocks(TacticalUnitState target)
        {
            return locks.Count(tractorLock => ReferenceEquals(tractorLock.Value, target));
        }

        /// <summary>
        /// Removes one established lock and emits its release event.
        /// </summary>
        /// <param name="source">The locking unit.</param>
        /// <param name="target">The formerly locked unit.</param>
        private void ReleaseLock(TacticalUnitState source, TacticalUnitState target)
        {
            locks.Remove(source);
            events.Add(
                TacticalCombatEvent.TractorLock(
                    TacticalCombatEventKind.TractorRelease,
                    source,
                    target
                )
            );
        }
    }
}
