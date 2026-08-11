using System;
using System.Numerics;

namespace Rebellion.Game.Tactical
{
    /// <summary>
    /// Resolves the tractor-beam attack coupled to capital-ship laser fire.
    /// </summary>
    internal static class TacticalTractorBeamSystem
    {
        /// <summary>
        /// Gets the additional tractor attack strength produced by eligible laser-cannon fire.
        /// </summary>
        /// <param name="source">The capital ship producing tractor strength.</param>
        /// <param name="target">The fighter unit struck by the associated laser attack.</param>
        /// <param name="weaponAttack">The weapon-family attack that triggered the check.</param>
        /// <param name="attackStrength">The resulting conventional tractor attack strength.</param>
        /// <returns>True when the source fires its tractor beam.</returns>
        public static bool TryGetAttackStrength(
            TacticalUnitState source,
            TacticalUnitState target,
            TacticalAttack weaponAttack,
            out int attackStrength
        )
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            bool canFire =
                source.IsActive
                && target?.IsActive == true
                && source.Kind == TacticalUnitKind.CapitalShip
                && target.Kind == TacticalUnitKind.Fighters
                && source.Side != target.Side
                && target.Shields == 0
                && weaponAttack.WeaponType == TacticalWeaponType.LaserCannon
                && source.EffectiveTractorBeamPower > 0f
                && Vector3.Distance(source.Position, target.Position) <= source.TractorBeamRange;
            attackStrength = canFire ? Math.Max(1, (int)source.EffectiveTractorBeamPower) : 0;
            return canFire;
        }
    }
}
