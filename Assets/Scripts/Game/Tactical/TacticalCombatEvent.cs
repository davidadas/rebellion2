using System;
using System.Numerics;

namespace Rebellion.Game.Tactical
{
    /// <summary>
    /// Identifies a discrete tactical event that requires battle presentation.
    /// </summary>
    public enum TacticalCombatEventKind
    {
        /// <summary>A weapon attack travels from one tactical unit to another.</summary>
        WeaponImpact = 0,

        /// <summary>A tactical unit is destroyed by an attack.</summary>
        UnitDestroyed = 1,

        /// <summary>A tactical unit completes its retreat from the battlefield.</summary>
        UnitWithdrawn = 2,

        /// <summary>A fighter group returns to its carrier.</summary>
        FightersRecovered = 3,
    }

    /// <summary>
    /// Captures one simulation event without coupling tactical state to Unity presentation.
    /// </summary>
    public sealed class TacticalCombatEvent
    {
        /// <summary>Gets the event category.</summary>
        public TacticalCombatEventKind Kind { get; }

        /// <summary>Gets the unit that produced the event.</summary>
        public TacticalUnitState Source { get; }

        /// <summary>Gets the affected unit when the event has a separate target.</summary>
        public TacticalUnitState Target { get; }

        /// <summary>Gets the weapon family used by a weapon-impact event.</summary>
        public TacticalWeaponType? WeaponType { get; }

        /// <summary>Gets the event origin captured when the event occurred.</summary>
        public Vector3 SourcePosition { get; }

        /// <summary>Gets the event destination captured when the event occurred.</summary>
        public Vector3 TargetPosition { get; }

        /// <summary>
        /// Initializes one immutable event and captures its presentation positions.
        /// </summary>
        /// <param name="kind">The event category.</param>
        /// <param name="source">The unit producing the event.</param>
        /// <param name="target">The affected unit, when distinct from the source.</param>
        /// <param name="weaponType">The weapon family, for weapon impacts.</param>
        private TacticalCombatEvent(
            TacticalCombatEventKind kind,
            TacticalUnitState source,
            TacticalUnitState target,
            TacticalWeaponType? weaponType
        )
        {
            Kind = kind;
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target;
            WeaponType = weaponType;
            SourcePosition = source.Position;
            TargetPosition = target?.Position ?? source.Position;
        }

        /// <summary>
        /// Creates a weapon-impact event between two tactical units.
        /// </summary>
        /// <param name="source">The firing unit.</param>
        /// <param name="target">The struck unit.</param>
        /// <param name="weaponType">The fired weapon family.</param>
        /// <returns>The immutable weapon-impact event.</returns>
        public static TacticalCombatEvent WeaponImpact(
            TacticalUnitState source,
            TacticalUnitState target,
            TacticalWeaponType weaponType
        )
        {
            return new TacticalCombatEvent(
                TacticalCombatEventKind.WeaponImpact,
                source,
                target ?? throw new ArgumentNullException(nameof(target)),
                weaponType
            );
        }

        /// <summary>
        /// Creates a unit lifecycle event at the unit's current position.
        /// </summary>
        /// <param name="kind">The destruction, withdrawal, or recovery category.</param>
        /// <param name="unit">The affected tactical unit.</param>
        /// <returns>The immutable lifecycle event.</returns>
        public static TacticalCombatEvent UnitLifecycle(
            TacticalCombatEventKind kind,
            TacticalUnitState unit
        )
        {
            if (
                kind != TacticalCombatEventKind.UnitDestroyed
                && kind != TacticalCombatEventKind.UnitWithdrawn
                && kind != TacticalCombatEventKind.FightersRecovered
            )
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            return new TacticalCombatEvent(kind, unit, null, null);
        }
    }
}
