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

        /// <summary>A Death Star superlaser strikes an opposing tactical object.</summary>
        SuperlaserFired = 4,

        /// <summary>A held fighter group launches from its capital ship.</summary>
        FightersDeployed = 5,

        /// <summary>A tactical unit establishes a tractor lock on an opposing unit.</summary>
        TractorLock = 6,

        /// <summary>A tactical unit releases its tractor lock.</summary>
        TractorRelease = 7,
    }

    /// <summary>
    /// Identifies the target state produced by a resolved tactical weapon impact.
    /// </summary>
    public enum TacticalImpactState
    {
        /// <summary>The weapon expended its damage against the target's shields.</summary>
        Shield = 0,

        /// <summary>The weapon reached the target beneath its shields.</summary>
        Hull = 1,

        /// <summary>The weapon destroyed the target.</summary>
        Destroyed = 2,
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

        /// <summary>Gets the target state produced by a weapon impact.</summary>
        public TacticalImpactState ImpactState { get; }

        /// <summary>Gets whether the attack penetrated the target's remaining shields.</summary>
        public bool PenetratedShields => ImpactState != TacticalImpactState.Shield;

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
        /// <param name="impactState">The target state produced by the impact.</param>
        private TacticalCombatEvent(
            TacticalCombatEventKind kind,
            TacticalUnitState source,
            TacticalUnitState target,
            TacticalWeaponType? weaponType,
            TacticalImpactState impactState = TacticalImpactState.Shield
        )
        {
            Kind = kind;
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target;
            WeaponType = weaponType;
            ImpactState = impactState;
            SourcePosition = source.Position;
            TargetPosition = target?.Position ?? source.Position;
        }

        /// <summary>
        /// Creates a weapon-impact event between two tactical units.
        /// </summary>
        /// <param name="source">The firing unit.</param>
        /// <param name="target">The struck unit.</param>
        /// <param name="weaponType">The fired weapon family.</param>
        /// <param name="impactState">The target state produced by the impact.</param>
        /// <returns>The immutable weapon-impact event.</returns>
        public static TacticalCombatEvent WeaponImpact(
            TacticalUnitState source,
            TacticalUnitState target,
            TacticalWeaponType weaponType,
            TacticalImpactState impactState = TacticalImpactState.Shield
        )
        {
            return new TacticalCombatEvent(
                TacticalCombatEventKind.WeaponImpact,
                source,
                target ?? throw new ArgumentNullException(nameof(target)),
                weaponType,
                impactState
            );
        }

        /// <summary>
        /// Creates a Death Star superlaser event between the firing station and its target.
        /// </summary>
        /// <param name="source">The firing Death Star.</param>
        /// <param name="target">The destroyed opposing tactical object.</param>
        /// <returns>The immutable superlaser event.</returns>
        public static TacticalCombatEvent SuperlaserFired(
            TacticalUnitState source,
            TacticalUnitState target
        )
        {
            return new TacticalCombatEvent(
                TacticalCombatEventKind.SuperlaserFired,
                source,
                target ?? throw new ArgumentNullException(nameof(target)),
                null
            );
        }

        /// <summary>
        /// Creates a tractor-lock lifecycle event between its source and target.
        /// </summary>
        /// <param name="kind">Whether the tractor lock is established or released.</param>
        /// <param name="source">The unit producing the tractor beam.</param>
        /// <param name="target">The unit affected by the tractor beam.</param>
        /// <returns>The immutable tractor event.</returns>
        public static TacticalCombatEvent TractorLock(
            TacticalCombatEventKind kind,
            TacticalUnitState source,
            TacticalUnitState target
        )
        {
            if (
                kind != TacticalCombatEventKind.TractorLock
                && kind != TacticalCombatEventKind.TractorRelease
            )
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            return new TacticalCombatEvent(
                kind,
                source,
                target ?? throw new ArgumentNullException(nameof(target)),
                null
            );
        }

        /// <summary>
        /// Creates a unit lifecycle event at the unit's current position.
        /// </summary>
        /// <param name="kind">The deployment, destruction, withdrawal, or recovery category.</param>
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
                && kind != TacticalCombatEventKind.FightersDeployed
            )
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            return new TacticalCombatEvent(kind, unit, null, null);
        }
    }
}
