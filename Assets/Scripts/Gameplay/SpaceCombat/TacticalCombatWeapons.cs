using System;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Owns four directional weapon batteries and their ordered recharge queue.
    /// </summary>
    public sealed class TacticalCombatWeapons
    {
        private const float _shotScale = 0.26666668f;
        private const float _laserCapitalScale = 0.167f;
        private readonly int[,] _strengths;
        private readonly float[] _ranges;
        private readonly float[] _charge = new float[4];
        private readonly float[] _maximumCharge = new float[4];
        private readonly uint[] _ready = new uint[4];
        private readonly int[] _rechargeOrder = new int[4];
        private int _rechargeCount;

        /// <summary>
        /// Captures clockwise batteries with laser, ion, and turbolaser strengths in each row.
        /// </summary>
        /// <param name="batteryStrengths">The four-by-three battery strength matrix.</param>
        /// <param name="weaponRanges">The laser, ion, and turbolaser ranges.</param>
        /// <param name="weaponDamage">The initial weapon-system damage level.</param>
        /// <param name="isFighter">Whether to initialize shared squadron battery state.</param>
        public TacticalCombatWeapons(
            int[,] batteryStrengths,
            float[] weaponRanges,
            uint weaponDamage,
            bool isFighter = false
        )
        {
            if (batteryStrengths == null)
                throw new ArgumentNullException(nameof(batteryStrengths));
            if (batteryStrengths.GetLength(0) != 4 || batteryStrengths.GetLength(1) != 3)
                throw new ArgumentException(
                    "Four batteries with three weapon strengths are required.",
                    nameof(batteryStrengths)
                );
            if (weaponRanges == null)
                throw new ArgumentNullException(nameof(weaponRanges));
            if (weaponRanges.Length != 3)
                throw new ArgumentException(
                    "Three weapon ranges are required.",
                    nameof(weaponRanges)
                );
            _strengths = (int[,])batteryStrengths.Clone();
            _ranges = (float[])weaponRanges.Clone();
            for (int arc = 0; arc < 4; arc++)
            {
                float total = (float)(
                    (double)_strengths[arc, 0] + _strengths[arc, 1] + _strengths[arc, 2]
                );
                _charge[arc] = total;
                _ready[arc] = 1;
                _rechargeOrder[arc] = -1;
                if (isFighter || total <= 0)
                    continue;
                if (weaponDamage < 4)
                {
                    _ready[arc] = unchecked((uint)BitConverter.SingleToInt32Bits(total));
                }
                else
                {
                    ClearBattery((TacticalWeaponArc)arc);
                }
            }
        }

        /// <summary>
        /// Checks whether a battery can acquire a new target and fire.
        /// </summary>
        /// <param name="arc">The battery to inspect.</param>
        /// <returns>Whether its recharge is complete.</returns>
        public bool IsReady(TacticalWeaponArc arc)
        {
            return _ready[(int)arc] == 1;
        }

        /// <summary>
        /// Gets the charge currently held by one battery.
        /// </summary>
        /// <param name="arc">The battery to inspect.</param>
        /// <returns>The current weapon charge.</returns>
        public float GetCharge(TacticalWeaponArc arc)
        {
            return _charge[(int)arc];
        }

        /// <summary>
        /// Gets the stored target used when recharging one battery.
        /// </summary>
        /// <param name="arc">The battery to inspect.</param>
        /// <returns>The battery's recharge target.</returns>
        public float GetMaximumCharge(TacticalWeaponArc arc)
        {
            return _maximumCharge[(int)arc];
        }

        /// <summary>
        /// Gets one weapon's authored strength on a battery.
        /// </summary>
        /// <param name="arc">The firing battery.</param>
        /// <param name="weapon">The weapon to inspect.</param>
        /// <returns>The authored weapon strength.</returns>
        public int GetStrength(TacticalWeaponArc arc, TacticalWeaponType weapon)
        {
            return _strengths[(int)arc, (int)weapon];
        }

        /// <summary>
        /// Gets the range of a battery weapon.
        /// </summary>
        /// <param name="weapon">The weapon to inspect.</param>
        /// <returns>The weapon range in tactical distance units.</returns>
        public float GetRange(TacticalWeaponType weapon)
        {
            return _ranges[(int)weapon];
        }

        /// <summary>
        /// Calculates a weapon's attack strength against a designated target.
        /// </summary>
        /// <param name="arc">The firing battery.</param>
        /// <param name="weapon">The battery weapon.</param>
        /// <param name="targetIsFighter">Whether the designated target is a fighter squadron.</param>
        /// <returns>The queued weapon attack strength.</returns>
        public float GetTargetDamage(
            TacticalWeaponArc arc,
            TacticalWeaponType weapon,
            bool targetIsFighter
        )
        {
            if (weapon == TacticalWeaponType.IonCannon && targetIsFighter)
                return 0;
            float targetScale = GetTargetScale(weapon, targetIsFighter);
            return (float)((double)_shotScale * targetScale * GetStrength(arc, weapon));
        }

        /// <summary>
        /// Gets the rounded per-mount scalar used before recharge and mount count are applied.
        /// </summary>
        /// <param name="weapon">The battery weapon.</param>
        /// <param name="targetIsFighter">Whether the designated target is a fighter squadron.</param>
        /// <returns>The single-precision lane scalar.</returns>
        internal float GetRangeScalar(TacticalWeaponType weapon, bool targetIsFighter)
        {
            if (weapon == TacticalWeaponType.IonCannon && targetIsFighter)
                return 0;
            float targetScale = GetTargetScale(weapon, targetIsFighter);
            return (float)((double)_shotScale * targetScale);
        }

        /// <summary>
        /// Selects the configured lane multiplier for the target category.
        /// </summary>
        /// <param name="weapon">The battery weapon.</param>
        /// <param name="targetIsFighter">Whether the target is a fighter squadron.</param>
        /// <returns>The target-category multiplier.</returns>
        private static float GetTargetScale(TacticalWeaponType weapon, bool targetIsFighter) =>
            weapon == TacticalWeaponType.LaserCannon
                ? targetIsFighter
                    ? 1
                    : _laserCapitalScale
                : targetIsFighter
                    ? 0.5f
                    : 1;

        /// <summary>
        /// Calculates an opportunity shot's complete attack strength while scanning nearby targets.
        /// </summary>
        /// <param name="arc">The firing battery.</param>
        /// <param name="weapon">The battery weapon.</param>
        /// <returns>The opportunity-shot attack strength before hull scaling.</returns>
        public double GetOpportunityDamage(TacticalWeaponArc arc, TacticalWeaponType weapon)
        {
            if (weapon == TacticalWeaponType.IonCannon)
                return 0;
            float targetScale = weapon == TacticalWeaponType.Turbolaser ? 0.5f : 1;
            return (double)_shotScale * targetScale * GetStrength(arc, weapon);
        }

        /// <summary>
        /// Consumes the authored strength of a fired lane and schedules its battery for recharge.
        /// All valid lanes on a selected battery may discharge during the same firing command.
        /// </summary>
        /// <param name="arc">The firing battery.</param>
        /// <param name="weapon">The fired battery weapon.</param>
        public void Discharge(TacticalWeaponArc arc, TacticalWeaponType weapon)
        {
            int index = (int)arc;
            _charge[index] = (float)((double)_charge[index] - GetStrength(arc, weapon));
            _ready[index] = 0;
            QueueRecharge(index);
        }

        /// <summary>
        /// Clears a battery's charge after an ion or weapon-system disruption.
        /// </summary>
        /// <param name="arc">The affected battery.</param>
        public void ClearBattery(TacticalWeaponArc arc)
        {
            int index = (int)arc;
            _ready[index] = 0;
            _charge[index] = 0;
            QueueRecharge(index);
        }

        /// <summary>
        /// Advances queued batteries using the unit's recharge budget.
        /// </summary>
        /// <param name="seconds">The elapsed recharge interval in seconds.</param>
        /// <param name="rechargeRate">The unit's available recharge per second.</param>
        public void Advance(float seconds, double rechargeRate)
        {
            double available = rechargeRate * seconds;
            for (int order = 0; order < _rechargeCount && available > 0; order++)
            {
                int arc = _rechargeOrder[order];
                double remaining = (double)_maximumCharge[arc] - _charge[arc];
                if (double.IsNaN(remaining) || available < remaining)
                    _charge[arc] = (float)(available + _charge[arc]);
                else
                {
                    _charge[arc] = _maximumCharge[arc];
                    available -= remaining;
                    _ready[arc] = 1;
                }
            }

            for (int order = 0; order < _rechargeCount; order++)
            {
                if (_ready[_rechargeOrder[order]] != 1)
                    continue;
                for (int shift = order; shift < 3; shift++)
                    _rechargeOrder[shift] = _rechargeOrder[shift + 1];
                if (_rechargeCount == 4)
                    _rechargeOrder[3] = 0;
                _rechargeCount--;
            }
        }

        /// <summary>
        /// Adds one battery to the recharge queue if it is not already present.
        /// </summary>
        /// <param name="arc">The zero-based clockwise battery index.</param>
        private void QueueRecharge(int arc)
        {
            for (int index = 0; index < _rechargeCount; index++)
                if (_rechargeOrder[index] == arc)
                    return;
            _rechargeOrder[_rechargeCount++] = arc;
        }
    }
}
