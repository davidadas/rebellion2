using System;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Identifies something that happened during a tactical battle step that presentation may
    /// react to.
    /// </summary>
    public enum TacticalBattleEventKind
    {
        WeaponFired,
        UnitHit,
        UnitDestroyed,
        UnitWithdrew,
        SuperlaserFired,
        StationDestroyed,
        StationWithdrew,
        TrenchRunSucceeded,
        TrenchRunFailed,
    }

    /// <summary>
    /// Describes one presentation-relevant occurrence in a tactical battle.
    /// </summary>
    public sealed class TacticalBattleEvent
    {
        public TacticalBattleEventKind Kind { get; }

        public TacticalBattleUnit Unit { get; }

        /// <summary>
        /// Gets the battle station the event concerns, or null for an event about a unit alone.
        /// </summary>
        public TacticalBattleStation Station { get; }

        /// <summary>
        /// Creates one battle event.
        /// </summary>
        /// <param name="kind">What happened.</param>
        /// <param name="unit">The unit that fired, was hit, was destroyed, or withdrew.</param>
        internal TacticalBattleEvent(TacticalBattleEventKind kind, TacticalBattleUnit unit)
        {
            Kind = kind;
            Unit = unit ?? throw new ArgumentNullException(nameof(unit));
        }

        /// <summary>
        /// Creates one battle event about a battle station.
        /// </summary>
        /// <param name="kind">What happened.</param>
        /// <param name="station">The station that fired, was destroyed, withdrew, or was attacked.</param>
        /// <param name="unit">The capital ship a superlaser shot was fired at, or null.</param>
        internal TacticalBattleEvent(
            TacticalBattleEventKind kind,
            TacticalBattleStation station,
            TacticalBattleUnit unit = null
        )
        {
            Kind = kind;
            Station = station ?? throw new ArgumentNullException(nameof(station));
            Unit = unit;
        }
    }
}
