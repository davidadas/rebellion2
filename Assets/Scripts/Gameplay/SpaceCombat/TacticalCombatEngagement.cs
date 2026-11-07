using System;
using System.Collections.Generic;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Selects engagement distances from both sides' strongest batteries and recharge rates.
    /// </summary>
    public sealed class TacticalCombatEngagement
    {
        private static readonly TacticalWeaponType[] _attackOrder =
        {
            TacticalWeaponType.IonCannon,
            TacticalWeaponType.LaserCannon,
            TacticalWeaponType.Turbolaser,
        };
        private static readonly TacticalWeaponType[] _rangeOrder =
        {
            TacticalWeaponType.IonCannon,
            TacticalWeaponType.Turbolaser,
            TacticalWeaponType.LaserCannon,
        };
        private readonly List<WeaponRange> _weapons = new();
        private readonly List<float> _boundaries = new();
        public float MinimumRange { get; private set; }
        public float MaximumRange { get; private set; }
        public float MovementDistance { get; private set; } = 5;

        /// <summary>
        /// Recomputes the group's engagement window for its current primary target.
        /// </summary>
        /// <param name="members">The group's ordered member roster.</param>
        /// <param name="target">The primary target, if one is assigned.</param>
        /// <param name="closeAttack">Whether the group is using close attack.</param>
        public void Update(
            IReadOnlyList<TacticalCombatUnit> members,
            TacticalCombatUnit target,
            bool closeAttack
        )
        {
            if (closeAttack)
            {
                MinimumRange = 0;
                MaximumRange = 2.5f;
                MovementDistance = 5;
                return;
            }
            MinimumRange = 0;
            MaximumRange = 0;
            MovementDistance = 5;
            if (target == null || members.Count == 0)
                return;
            _weapons.Clear();
            _boundaries.Clear();
            float largestMemberRange = 0;
            for (int index = 0; index < members.Count; index++)
            {
                TacticalCombatUnit member = members[index];
                AppendRanges(member, target.IsFighter, false);
                largestMemberRange = Math.Max(
                    largestMemberRange,
                    TacticalCombatFireControl.GetMaximumRange(member)
                );
            }
            AppendRanges(target, members[0].IsFighter, true);
            _boundaries.Sort();
            float targetRange = TacticalCombatFireControl.GetMaximumRange(target);
            float excessRange = 0;
            float bestScore = -float.MaxValue;
            float lower = 0;
            for (int index = 0; index < _boundaries.Count; index++)
            {
                float upper = _boundaries[index];
                if (upper <= lower)
                    continue;
                float score = 0;
                for (int weapon = 0; weapon < _weapons.Count; weapon++)
                    if (_weapons[weapon].Range >= upper)
                        score = (float)((double)score + _weapons[weapon].Score);
                if (excessRange <= upper && targetRange < upper)
                    excessRange = upper - targetRange;
                if (bestScore <= score && upper <= largestMemberRange)
                {
                    bestScore = score;
                    MinimumRange = lower;
                    MaximumRange = upper;
                }
                lower = upper;
            }
            if (excessRange > 0)
                MovementDistance = excessRange;
            else if (bestScore != -float.MaxValue)
                MovementDistance = (float)(
                    ((double)MaximumRange - MinimumRange) * (1f / 3f) + MinimumRange
                );
            if (largestMemberRange == 0)
            {
                float fallback = Math.Max(targetRange * 1.2f, 10);
                MovementDistance = fallback;
                MinimumRange = fallback;
                MaximumRange = 999999;
            }
            if (MaximumRange == 0)
                MaximumRange = 10;
            MinimumRange = Math.Max(MinimumRange, 5);
        }

        /// <summary>
        /// Estimates sustained attack strength for target assignment and side assessment.
        /// </summary>
        /// <param name="unit">The attacking unit.</param>
        /// <param name="target">The specific target, or null for a category assessment.</param>
        /// <param name="targetIsFighter">The target category used when no target is provided.</param>
        /// <returns>The normalized attack budget.</returns>
        public static double GetAttackRate(
            TacticalCombatUnit unit,
            TacticalCombatUnit target,
            bool targetIsFighter = false
        )
        {
            double liveRecharge = unit.Systems.GetWeaponRecharge();
            float recharge = (float)liveRecharge;
            if (liveRecharge == 0 || double.IsNaN(liveRecharge))
                return 0;
            bool fighter = target?.IsFighter ?? targetIsFighter;
            TacticalWeaponArc? selected = TacticalCombatFireControl.GetStrongestArc(unit, fighter);
            if (selected == null)
                return 0;
            TacticalWeaponArc arc = selected.Value;
            uint total = unchecked(
                (uint)(
                    unit.Weapons.GetStrength(arc, TacticalWeaponType.LaserCannon)
                    + unit.Weapons.GetStrength(arc, TacticalWeaponType.IonCannon)
                    + unit.Weapons.GetStrength(arc, TacticalWeaponType.Turbolaser)
                )
            );
            if (unit.IsFighter)
            {
                float count = total;
                total = unchecked(
                    (uint)(long)((double)unit.Systems.Hull * count / unit.Systems.MaximumHull)
                );
            }
            double liveNormalized = (double)total / recharge;
            float normalized = (float)liveNormalized;
            if (!(liveNormalized > 0))
                return 0;
            float maneuverRatio = (float)(
                (double)unit.GetManeuverability() / (target?.GetManeuverability() ?? 6)
            );
            if (maneuverRatio <= 0.1f)
                maneuverRatio = 0.1f;
            float damage = 0;
            for (int index = 0; index < _attackOrder.Length; index++)
            {
                TacticalWeaponType weapon = _attackOrder[index];
                if (unit.Weapons.GetStrength(arc, weapon) <= 0)
                    continue;
                float value = unit.Weapons.GetTargetDamage(arc, weapon, fighter);
                if (fighter && weapon != TacticalWeaponType.IonCannon)
                    value = (float)((double)value * maneuverRatio);
                double contribution = unit.IsFighter
                    ? (double)unit.Systems.Hull * value / unit.Systems.MaximumHull
                    : value;
                damage = index == 0 ? (float)contribution : (float)(contribution + damage);
            }
            if (target != null)
            {
                double torpedo = unit.GetTorpedoAttack();
                if (torpedo != 0 && !double.IsNaN(torpedo) && unit.IsFighter && !target.IsFighter)
                {
                    double shields = target.Systems.GetShieldFraction();
                    if (shields == 0 || double.IsNaN(shields))
                    {
                        float roundedTorpedo = (float)unit.GetTorpedoAttack();
                        damage = (float)(
                            (double)unit.Systems.Hull * roundedTorpedo / unit.Systems.MaximumHull
                            + damage
                        );
                    }
                }
            }
            return (double)damage / normalized;
        }

        /// <summary>
        /// Adds strongest-battery weapon intervals to the engagement score profile.
        /// </summary>
        /// <param name="unit">The firing unit.</param>
        /// <param name="targetIsFighter">The opposing unit's category.</param>
        /// <param name="subtract">Whether to subtract the opposing unit's attack profile.</param>
        private void AppendRanges(TacticalCombatUnit unit, bool targetIsFighter, bool subtract)
        {
            TacticalWeaponArc? selected = TacticalCombatFireControl.GetStrongestArc(
                unit,
                targetIsFighter
            );
            if (selected == null)
                return;
            TacticalWeaponType[] order = _rangeOrder;
            for (int index = 0; index < order.Length; index++)
            {
                TacticalWeaponType weapon = order[index];
                if (unit.Weapons.GetStrength(selected.Value, weapon) == 0)
                    continue;
                float range = unit.Weapons.GetRange(weapon);
                float scalar = unit.Weapons.GetRangeScalar(weapon, targetIsFighter);
                float score = (float)(
                    unit.Systems.GetWeaponRecharge()
                    * scalar
                    * unit.Weapons.GetStrength(selected.Value, weapon)
                );
                _weapons.Add(new WeaponRange(range, subtract ? -score : score));
                _boundaries.Add(range);
            }
        }

        /// <summary>
        /// Stores a weapon interval's maximum range and additive engagement score.
        /// </summary>
        private readonly struct WeaponRange
        {
            public float Range { get; }
            public float Score { get; }

            /// <summary>
            /// Captures one weapon's range and score.
            /// </summary>
            /// <param name="range">The interval's upper range.</param>
            /// <param name="score">The additive engagement score.</param>
            public WeaponRange(float range, float score)
            {
                Range = range;
                Score = score;
            }
        }
    }
}
