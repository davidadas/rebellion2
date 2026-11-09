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
    }

    /// <summary>
    /// Describes one presentation-relevant occurrence in a tactical battle.
    /// </summary>
    public sealed class TacticalBattleEvent
    {
        public TacticalBattleEventKind Kind { get; }

        public TacticalBattleUnit Unit { get; }

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
    }
}
