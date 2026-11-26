using System;
using Rebellion.Game;
using Rebellion.Game.Units;

namespace Rebellion.Gameplay
{
    /// <summary>
    /// Owns a combatant's weapon-bank snapshot and calculates sustained damage rates.
    /// </summary>
    internal sealed class SpaceCombatDamageCalculator
    {
        private static readonly int[] DamageLaneOrder = { 1, 0, 2 };

        private readonly GameConfig.SpaceCombatConfig _config;
        private readonly double[][] _banks;
        private readonly bool _isStarfighter;
        private readonly float _recharge;
        private readonly double _torpedoes;
        private readonly float _maximumHull;
        private readonly float _hullPerCraft;

        /// <summary>
        /// Gets whether the captured banks contain laser cannons.
        /// </summary>
        internal bool HasLasers { get; }

        /// <summary>
        /// Gets whether the captured banks contain any primary weapons.
        /// </summary>
        internal bool HasWeapons { get; }

        /// <summary>
        /// Gets the greatest range among primary weapon categories with active banks.
        /// </summary>
        internal double MaximumActiveRange { get; }

        /// <summary>
        /// Captures a capital ship's firing arcs and recharge rating for one battle.
        /// </summary>
        /// <param name="ship">The firing ship.</param>
        /// <param name="config">The combat rules.</param>
        internal SpaceCombatDamageCalculator(CapitalShip ship, GameConfig.SpaceCombatConfig config)
        {
            _config = config;
            _maximumHull = Math.Max(ship.MaxHullStrength, 1);
            _banks = new double[4][];
            for (int arc = 0; arc < _banks.Length; arc++)
            {
                _banks[arc] = new double[]
                {
                    GetWeaponCount(ship, PrimaryWeaponType.LaserCannon, arc),
                    GetWeaponCount(ship, PrimaryWeaponType.IonCannon, arc),
                    GetWeaponCount(ship, PrimaryWeaponType.Turbolaser, arc),
                };
                if (_banks[arc][0] > 0)
                {
                    HasLasers = true;
                    MaximumActiveRange = Math.Max(
                        MaximumActiveRange,
                        GetWeaponRange(ship, PrimaryWeaponType.LaserCannon)
                    );
                }
                if (_banks[arc][1] > 0)
                {
                    MaximumActiveRange = Math.Max(
                        MaximumActiveRange,
                        GetWeaponRange(ship, PrimaryWeaponType.IonCannon)
                    );
                }
                if (_banks[arc][2] > 0)
                {
                    MaximumActiveRange = Math.Max(
                        MaximumActiveRange,
                        GetWeaponRange(ship, PrimaryWeaponType.Turbolaser)
                    );
                }
                if (_banks[arc][0] + _banks[arc][1] + _banks[arc][2] > 0)
                    HasWeapons = true;
            }
            _recharge = (float)(
                Math.Max(ship.WeaponRecharge, 0)
                * (double)(float)Math.Max(config.AutoResolveCapitalWeaponRechargeMultiplier, 0)
            );
        }

        /// <summary>
        /// Captures a squadron's full weapon bank and torpedo capacity for one battle.
        /// </summary>
        /// <param name="fighter">The firing squadron.</param>
        /// <param name="config">The combat rules.</param>
        internal SpaceCombatDamageCalculator(
            Starfighter fighter,
            GameConfig.SpaceCombatConfig config
        )
        {
            _config = config;
            _isStarfighter = true;
            int maximumCraft = Math.Max(fighter.MaxSquadronSize, 0);
            _hullPerCraft = (float)Math.Max(config.AutoResolveFighterHullPerCraft, 1);
            _maximumHull = (float)(maximumCraft * (double)_hullPerCraft);
            _banks = new[]
            {
                new double[]
                {
                    (double)Math.Max(fighter.LaserCannon, 0) * maximumCraft,
                    (double)Math.Max(fighter.IonCannon, 0) * maximumCraft,
                    0,
                },
            };
            HasLasers = _banks[0][0] > 0;
            HasWeapons = _banks[0][0] + _banks[0][1] > 0;
            MaximumActiveRange = Math.Max(
                HasLasers ? Math.Max(fighter.LaserRange, 0) : 0,
                _banks[0][1] > 0 ? Math.Max(fighter.IonRange, 0) : 0
            );
            _torpedoes = (double)Math.Max(fighter.Torpedoes, 0) * maximumCraft;
            _recharge = (float)Math.Max(config.AutoResolveFighterWeaponRechargeMultiplier, 0);
        }

        /// <summary>
        /// Calculates the strongest usable arc's output over its recharge time.
        /// </summary>
        /// <param name="hullFraction">The surviving fraction of the full combatant.</param>
        /// <param name="targetsFighters">Whether the target is a squadron.</param>
        /// <param name="maneuverRatio">The firing unit's maneuverability divided by the target's.</param>
        /// <param name="targetHasShields">Whether the target still has shields.</param>
        /// <param name="weaponDamage">The number of damaged capital-ship weapon systems.</param>
        /// <returns>The damage applied per combat step.</returns>
        internal double GetDamageRate(
            double hullFraction,
            bool targetsFighters,
            double maneuverRatio,
            bool targetHasShields,
            int weaponDamage = 0
        )
        {
            float recharge = (float)GetRechargeRate(hullFraction, weaponDamage);
            if (recharge <= 0 || hullFraction <= 0 || _maximumHull <= 0)
                return 0;

            double[] bank = SelectStrongestBank(targetsFighters);
            if (bank == null)
                return 0;

            double hull = (float)(_maximumHull * hullFraction);
            double bankSize = bank[0] + bank[1] + bank[2];
            if (_isStarfighter)
            {
                bankSize = Math.Truncate(ScaleForHull((float)bankSize, hull));
            }

            float normalizedCount = (float)(bankSize / recharge);
            if (normalizedCount <= 0)
                return 0;

            float turnRatio = Math.Max(
                (float)maneuverRatio,
                (float)Math.Max(_config.AutoResolveMinimumManeuverRatio, 0)
            );
            float damage = 0;
            foreach (int lane in DamageLaneOrder)
            {
                if (bank[lane] <= 0)
                    continue;

                float laneDamage = GetLaneDamage(bank, lane, targetsFighters);
                if (targetsFighters && lane != 1)
                    laneDamage = (float)((double)laneDamage * turnRatio);
                double contribution = _isStarfighter ? ScaleForHull(laneDamage, hull) : laneDamage;
                damage = (float)(damage + contribution);
            }

            if (_isStarfighter && !targetsFighters && !targetHasShields && _torpedoes > 0)
            {
                float torpedoDamage = (float)(
                    _torpedoes * (float)Math.Max(_config.AutoResolveTorpedoDamagePerStrength, 0)
                );
                torpedoDamage = (float)ScaleForHull(torpedoDamage, hull);
                damage = (float)(damage + ScaleForHull(torpedoDamage, hull));
            }

            return damage / (double)normalizedCount;
        }

        /// <summary>
        /// Calculates the available weapon recharge after hull and system damage.
        /// </summary>
        /// <param name="hullFraction">The surviving fraction of the full combatant.</param>
        /// <param name="weaponDamage">The number of damaged capital-ship weapon systems.</param>
        /// <returns>The recharge available per combat step.</returns>
        internal double GetRechargeRate(double hullFraction, int weaponDamage = 0)
        {
            if (hullFraction <= 0 || _maximumHull <= 0)
                return 0;

            double hull = (float)(_maximumHull * hullFraction);
            if (_isStarfighter)
                return hull * _recharge / _hullPerCraft;

            float scaledRecharge = (float)ScaleForHull(_recharge, hull);
            return Math.Max(
                scaledRecharge - (double)_recharge * 0.25 * Math.Clamp(weaponDamage, 0, 4),
                0
            );
        }

        /// <summary>
        /// Selects the first arc with the greatest weighted output for the target category.
        /// </summary>
        /// <param name="targetsFighters">Whether the target is a squadron.</param>
        /// <returns>The selected bank, or null when no bank can damage this target category.</returns>
        private double[] SelectStrongestBank(bool targetsFighters)
        {
            double[] selected = null;
            float maximum = 0;
            foreach (double[] bank in _banks)
            {
                double damage =
                    (double)GetLaneDamage(bank, 2, targetsFighters)
                    + GetLaneDamage(bank, 1, targetsFighters)
                    + GetLaneDamage(bank, 0, targetsFighters);
                if (damage > maximum)
                {
                    maximum = (float)damage;
                    selected = bank;
                }
            }
            return selected;
        }

        /// <summary>
        /// Calculates one weapon category's weighted output before maneuver and hull scaling.
        /// </summary>
        /// <param name="bank">The selected firing arc's weapon counts.</param>
        /// <param name="lane">The laser, ion, or turbolaser category index.</param>
        /// <param name="targetsFighters">Whether the target is a squadron.</param>
        /// <returns>The category's rounded damage output.</returns>
        private float GetLaneDamage(double[] bank, int lane, bool targetsFighters)
        {
            if (targetsFighters && lane == 1)
                return 0;

            float factor = 1;
            if (lane == 0 && !targetsFighters)
                factor = (float)Math.Max(_config.LaserCannonCapitalDamageMultiplier, 0);
            else if (lane == 2 && targetsFighters)
                factor = (float)Math.Max(_config.TurbolaserFighterDamageMultiplier, 0);

            float damageScale = (float)Math.Max(_config.AutoResolveWeaponDamagePerStrength, 0);
            return (float)((double)damageScale * factor * bank[lane]);
        }

        /// <summary>
        /// Scales a rounded rating by the surviving hull while retaining the intermediate product.
        /// </summary>
        /// <param name="value">The rating to scale.</param>
        /// <param name="hull">The remaining hull strength.</param>
        /// <returns>The surviving portion of the rating.</returns>
        private double ScaleForHull(float value, double hull)
        {
            return hull * value / _maximumHull;
        }

        /// <summary>
        /// Reads the number of weapons in one firing arc.
        /// </summary>
        /// <param name="ship">The firing ship.</param>
        /// <param name="weapon">The weapon category.</param>
        /// <param name="arc">The firing arc.</param>
        /// <returns>The nonnegative weapon count.</returns>
        private static int GetWeaponCount(CapitalShip ship, PrimaryWeaponType weapon, int arc) =>
            Math.Max(ship.GetPrimaryWeaponStrength(weapon, (PrimaryWeaponArc)arc), 0);

        /// <summary>
        /// Reads the nonnegative range of one capital-ship weapon category.
        /// </summary>
        /// <param name="ship">The firing ship.</param>
        /// <param name="weapon">The weapon category.</param>
        /// <returns>The category's configured range.</returns>
        private static int GetWeaponRange(CapitalShip ship, PrimaryWeaponType weapon) =>
            Math.Max(ship.GetPrimaryWeaponRange(weapon), 0);
    }
}
