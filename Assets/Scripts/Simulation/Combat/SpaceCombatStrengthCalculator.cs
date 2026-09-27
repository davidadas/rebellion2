using System;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Units;

namespace Rebellion.Simulation
{
    /// <summary>
    /// Calculates strategic force strength using the weapon effectiveness applied by space combat.
    /// </summary>
    internal static class SpaceCombatStrengthCalculator
    {
        /// <summary>
        /// Returns the ready strength of a fleet against capital ships.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <param name="config">The space-combat rules.</param>
        /// <returns>The fleet's capital-target combat value.</returns>
        internal static int GetFleetCombatValueAgainstCapitalShips(
            Fleet fleet,
            GameConfig.SpaceCombatConfig config
        )
        {
            if (fleet == null || config == null)
                return 0;

            return fleet
                .GetChildren<CapitalShip>()
                .Sum(ship => GetCapitalShipCombatValueAgainstCapitalShips(ship, config));
        }

        /// <summary>
        /// Returns the projected strength of a fleet against capital ships.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <param name="config">The space-combat rules.</param>
        /// <returns>The fleet's projected capital-target combat value.</returns>
        internal static int GetProjectedFleetCombatValueAgainstCapitalShips(
            Fleet fleet,
            GameConfig.SpaceCombatConfig config
        )
        {
            if (fleet == null || config == null)
                return 0;

            return fleet
                .GetChildren<CapitalShip>()
                .Sum(ship => GetProjectedCapitalShipCombatValueAgainstCapitalShips(ship, config));
        }

        /// <summary>
        /// Returns one ready capital ship's strength against capital ships, including its fighters.
        /// </summary>
        /// <param name="ship">The capital ship to inspect.</param>
        /// <param name="config">The space-combat rules.</param>
        /// <returns>The ship group's capital-target combat value.</returns>
        private static int GetCapitalShipCombatValueAgainstCapitalShips(
            CapitalShip ship,
            GameConfig.SpaceCombatConfig config
        )
        {
            if (
                ship == null
                || ship.ManufacturingStatus != ManufacturingStatus.Complete
                || ship.Movement != null
                || (ship.MaxHullStrength > 0 && ship.CurrentHullStrength <= 0)
            )
                return 0;

            return CalculateCapitalShipCombatValue(ship, ship.CurrentHullStrength, config)
                + ship.GetChildren<Starfighter>()
                    .Where(fighter => fighter != null)
                    .Sum(GetReadyCarriedStarfighterCombatValue);
        }

        /// <summary>
        /// Returns one projected capital ship's strength against capital ships, including its fighters.
        /// </summary>
        /// <param name="ship">The capital ship to inspect.</param>
        /// <param name="config">The space-combat rules.</param>
        /// <returns>The ship group's projected capital-target combat value.</returns>
        internal static int GetProjectedCapitalShipCombatValueAgainstCapitalShips(
            CapitalShip ship,
            GameConfig.SpaceCombatConfig config
        )
        {
            if (
                ship == null
                || (
                    ship.ManufacturingStatus == ManufacturingStatus.Complete
                    && ship.MaxHullStrength > 0
                    && ship.CurrentHullStrength <= 0
                )
            )
                return 0;

            int hullStrength =
                ship.ManufacturingStatus == ManufacturingStatus.Complete
                    ? ship.CurrentHullStrength
                    : ship.MaxHullStrength;
            return CalculateCapitalShipCombatValue(ship, hullStrength, config)
                + ship.GetChildren<Starfighter>()
                    .Where(fighter => fighter != null)
                    .Sum(GetProjectedCarriedStarfighterCombatValue);
        }

        /// <summary>
        /// Returns a capital-ship weapon's damage multiplier for the selected target type.
        /// </summary>
        /// <param name="weaponType">The primary weapon type.</param>
        /// <param name="targetsFighters">Whether the target is a fighter squadron.</param>
        /// <param name="laserCannonCapitalDamageMultiplier">
        /// The configured capital-target damage multiplier for laser cannons.
        /// </param>
        /// <returns>The target-specific damage multiplier.</returns>
        internal static double GetPrimaryWeaponTargetMultiplier(
            PrimaryWeaponType weaponType,
            bool targetsFighters,
            double laserCannonCapitalDamageMultiplier
        )
        {
            if (weaponType == PrimaryWeaponType.IonCannon && targetsFighters)
                return 0;
            if (weaponType == PrimaryWeaponType.LaserCannon && !targetsFighters)
                return Math.Max(0, laserCannonCapitalDamageMultiplier);
            return 1;
        }

        /// <summary>
        /// Calculates one capital ship's durability-adjusted value against capital ships.
        /// </summary>
        /// <param name="ship">The capital ship to inspect.</param>
        /// <param name="hullStrength">The hull strength used by the estimate.</param>
        /// <param name="config">The space-combat rules.</param>
        /// <returns>The ship's capital-target combat value.</returns>
        private static int CalculateCapitalShipCombatValue(
            CapitalShip ship,
            int hullStrength,
            GameConfig.SpaceCombatConfig config
        )
        {
            double attackStrength = ship.PrimaryWeapons.Sum(weaponEntry =>
                GetPrimaryWeaponStrength(weaponEntry.Value)
                * GetPrimaryWeaponTargetMultiplier(
                    weaponEntry.Key,
                    targetsFighters: false,
                    config.LaserCannonCapitalDamageMultiplier
                )
            );
            long durability = Math.Max(0L, hullStrength) + Math.Max(0L, ship.MaxShieldStrength);
            if (attackStrength <= 0 || durability <= 0)
                return attackStrength >= int.MaxValue ? int.MaxValue : (int)attackStrength;

            double combatValue = Math.Sqrt(attackStrength * durability);
            return combatValue >= int.MaxValue ? int.MaxValue : (int)combatValue;
        }

        /// <summary>
        /// Returns primary weapon strength without including the range value.
        /// </summary>
        /// <param name="weaponValues">The weapon arc and range values.</param>
        /// <returns>The summed weapon strength.</returns>
        private static int GetPrimaryWeaponStrength(int[] weaponValues)
        {
            if (weaponValues == null)
                return 0;

            int strength = 0;
            foreach (PrimaryWeaponArc weaponArc in CapitalShip.PrimaryWeaponArcs)
            {
                int weaponArcIndex = (int)weaponArc;
                if (weaponArcIndex < weaponValues.Length)
                    strength += weaponValues[weaponArcIndex];
            }

            return strength;
        }

        /// <summary>
        /// Returns the ready strength of a carried fighter squadron.
        /// </summary>
        /// <param name="fighter">The fighter squadron to inspect.</param>
        /// <returns>The squadron's ready combat value.</returns>
        private static int GetReadyCarriedStarfighterCombatValue(Starfighter fighter)
        {
            if (
                fighter.ManufacturingStatus != ManufacturingStatus.Complete
                || fighter.Movement != null
            )
                return 0;

            return GetCarriedStarfighterCombatValue(fighter, fighter.CurrentSquadronSize);
        }

        /// <summary>
        /// Returns the projected strength of a carried fighter squadron.
        /// </summary>
        /// <param name="fighter">The fighter squadron to inspect.</param>
        /// <returns>The squadron's projected combat value.</returns>
        private static int GetProjectedCarriedStarfighterCombatValue(Starfighter fighter)
        {
            int squadronSize =
                fighter.ManufacturingStatus == ManufacturingStatus.Complete
                    ? fighter.CurrentSquadronSize
                    : fighter.MaxSquadronSize;
            return GetCarriedStarfighterCombatValue(fighter, squadronSize);
        }

        /// <summary>
        /// Returns the fleet-rating contribution of a carried fighter squadron.
        /// </summary>
        /// <param name="fighter">The fighter squadron to inspect.</param>
        /// <param name="squadronSize">The squadron size used by the estimate.</param>
        /// <returns>The fighter contribution.</returns>
        private static int GetCarriedStarfighterCombatValue(Starfighter fighter, int squadronSize)
        {
            int weaponStrength = fighter.GetWeaponStrength();
            return fighter.MaxSquadronSize > 0
                ? weaponStrength * Math.Max(0, squadronSize) / fighter.MaxSquadronSize
                : weaponStrength;
        }
    }
}
