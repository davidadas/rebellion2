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
        public static float GetAttackRate(
            TacticalCombatUnit unit,
            TacticalCombatUnit target,
            bool targetIsFighter = false
        )
        {
            float recharge = unit.Systems.GetWeaponRecharge();
            if (recharge == 0)
                return 0;
            bool fighter = target?.IsFighter ?? targetIsFighter;
            TacticalWeaponArc? selected = TacticalCombatFireControl.GetStrongestArc(unit, fighter);
            if (selected == null)
                return 0;
            TacticalWeaponArc arc = selected.Value;
            int total =
                unit.Weapons.GetStrength(arc, TacticalWeaponType.LaserCannon)
                + unit.Weapons.GetStrength(arc, TacticalWeaponType.IonCannon)
                + unit.Weapons.GetStrength(arc, TacticalWeaponType.Turbolaser);
            if (unit.IsFighter)
                total = (int)((double)unit.Systems.Hull * total / unit.Systems.MaximumHull);
            float normalized = (float)((double)total / recharge);
            if (normalized <= 0)
                return 0;
            float maneuverRatio = Math.Max(
                (float)((double)unit.GetManeuverability() / (target?.GetManeuverability() ?? 6)),
                0.1f
            );
            float damage = 0;
            TacticalWeaponType[] order = _attackOrder;
            for (int index = 0; index < order.Length; index++)
            {
                TacticalWeaponType weapon = order[index];
                float value = unit.Weapons.GetTargetDamage(arc, weapon, fighter);
                if (fighter && weapon != TacticalWeaponType.IonCannon)
                    value = (float)((double)value * maneuverRatio);
                if (unit.IsFighter)
                    value = (float)((double)unit.Systems.Hull * value / unit.Systems.MaximumHull);
                damage = (float)((double)damage + value);
            }
            if (unit.IsFighter && target?.IsFighter == false && target.Systems.Shields == 0)
                damage = (float)(
                    (double)unit.Systems.Hull * unit.GetTorpedoDamage() / unit.Systems.MaximumHull
                    + damage
                );
            return (float)((double)damage / normalized);
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
                if (range <= 0)
                    continue;
                float score = (float)(
                    (double)unit.Systems.GetWeaponRecharge()
                    * unit.Weapons.GetTargetDamage(selected.Value, weapon, targetIsFighter)
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
