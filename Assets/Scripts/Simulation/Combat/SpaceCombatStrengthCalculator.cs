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
        private const int _capitalFirepowerScale = 100;
        private const int _laserCapitalFirepowerDivisor = 6;
        private const int _fighterTargetLaserWeight = 10;
        private const int _fighterTargetTurbolaserWeight = 5;
        private const int _fighterSpeedDivisor = 8;

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
        /// Returns a capital ship's strongest-arc firepower against capital ships.
        /// </summary>
        /// <param name="ship">The capital ship to inspect.</param>
        /// <returns>The capital-target firepower.</returns>
        internal static int GetCapitalShipFirepowerAgainstCapitalShips(CapitalShip ship)
        {
            return GetStrongestArcFirepower(
                ship,
                turbolaserWeight: _capitalFirepowerScale * _laserCapitalFirepowerDivisor,
                ionCannonWeight: _capitalFirepowerScale * _laserCapitalFirepowerDivisor,
                laserCannonWeight: _capitalFirepowerScale,
                weightDivisor: _laserCapitalFirepowerDivisor,
                maneuverMultiplier: 1
            );
        }

        /// <summary>
        /// Returns a capital ship's strongest-arc firepower against starfighters.
        /// </summary>
        /// <param name="ship">The capital ship to inspect.</param>
        /// <returns>The fighter-target firepower.</returns>
        internal static int GetCapitalShipFirepowerAgainstStarfighters(CapitalShip ship)
        {
            return GetStrongestArcFirepower(
                ship,
                turbolaserWeight: _fighterTargetTurbolaserWeight,
                ionCannonWeight: 0,
                laserCannonWeight: _fighterTargetLaserWeight,
                weightDivisor: 1,
                maneuverMultiplier: Math.Max(0, ship?.Maneuverability ?? 0)
            );
        }

        /// <summary>
        /// Returns whether a ship is an armed escort without bombardment or interdiction capability.
        /// </summary>
        /// <param name="ship">The capital ship to inspect.</param>
        /// <returns>True when the ship satisfies the armed escort capability.</returns>
        internal static bool IsArmedEscort(CapitalShip ship)
        {
            return ship is { Bombardment: <= 0, HasGravityWell: false }
                && GetCapitalShipFirepowerAgainstStarfighters(ship) > 0;
        }

        /// <summary>
        /// Returns a ready starfighter squadron's firepower against capital ships.
        /// </summary>
        /// <param name="fighter">The starfighter squadron to inspect.</param>
        /// <returns>The capital-target firepower.</returns>
        internal static int GetStarfighterFirepowerAgainstCapitalShips(Starfighter fighter)
        {
            return
                fighter?.ManufacturingStatus == ManufacturingStatus.Complete
                && fighter.Movement == null
                ? GetStarfighterFirepower(
                    fighter,
                    fighter.CurrentSquadronSize,
                    targetsFighters: false
                )
                : 0;
        }

        /// <summary>
        /// Returns a ready starfighter squadron's firepower against starfighters.
        /// </summary>
        /// <param name="fighter">The starfighter squadron to inspect.</param>
        /// <returns>The fighter-target firepower.</returns>
        internal static int GetStarfighterFirepowerAgainstStarfighters(Starfighter fighter)
        {
            return
                fighter?.ManufacturingStatus == ManufacturingStatus.Complete
                && fighter.Movement == null
                ? GetStarfighterFirepower(
                    fighter,
                    fighter.CurrentSquadronSize,
                    targetsFighters: true
                )
                : 0;
        }

        /// <summary>
        /// Returns a starfighter squadron's projected firepower against capital ships.
        /// </summary>
        /// <param name="fighter">The starfighter squadron to inspect.</param>
        /// <returns>The projected capital-target firepower.</returns>
        internal static int GetProjectedStarfighterFirepowerAgainstCapitalShips(Starfighter fighter)
        {
            if (fighter == null)
                return 0;

            int squadronSize =
                fighter.ManufacturingStatus == ManufacturingStatus.Complete
                    ? fighter.CurrentSquadronSize
                    : fighter.MaxSquadronSize;
            return GetStarfighterFirepower(fighter, squadronSize, targetsFighters: false);
        }

        /// <summary>
        /// Returns a starfighter squadron's projected firepower against starfighters.
        /// </summary>
        /// <param name="fighter">The starfighter squadron to inspect.</param>
        /// <returns>The projected fighter-target firepower.</returns>
        internal static int GetProjectedStarfighterFirepowerAgainstStarfighters(Starfighter fighter)
        {
            if (fighter == null)
                return 0;

            int squadronSize =
                fighter.ManufacturingStatus == ManufacturingStatus.Complete
                    ? fighter.CurrentSquadronSize
                    : fighter.MaxSquadronSize;
            return GetStarfighterFirepower(fighter, squadronSize, targetsFighters: true);
        }

        /// <summary>
        /// Calculates strongest-arc firepower using target-specific weapon weights.
        /// </summary>
        /// <param name="ship">The capital ship to inspect.</param>
        /// <param name="turbolaserWeight">Turbolaser target weight.</param>
        /// <param name="ionCannonWeight">Ion-cannon target weight.</param>
        /// <param name="laserCannonWeight">Laser-cannon target weight.</param>
        /// <param name="weightDivisor">Common divisor applied after weighting.</param>
        /// <param name="maneuverMultiplier">Target-tracking multiplier.</param>
        /// <returns>The strongest-arc firepower.</returns>
        private static int GetStrongestArcFirepower(
            CapitalShip ship,
            int turbolaserWeight,
            int ionCannonWeight,
            int laserCannonWeight,
            int weightDivisor,
            int maneuverMultiplier
        )
        {
            if (ship == null)
                return 0;

            long strongestWeighted = 0;
            int strongestWeaponCount = 0;
            foreach (PrimaryWeaponArc arc in CapitalShip.PrimaryWeaponArcs)
            {
                int arcIndex = (int)arc;
                int turbolasers = GetWeaponArcValue(ship, PrimaryWeaponType.Turbolaser, arcIndex);
                int ionCannons = GetWeaponArcValue(ship, PrimaryWeaponType.IonCannon, arcIndex);
                int laserCannons = GetWeaponArcValue(ship, PrimaryWeaponType.LaserCannon, arcIndex);
                long weighted =
                    (long)turbolasers * turbolaserWeight
                    + (long)ionCannons * ionCannonWeight
                    + (long)laserCannons * laserCannonWeight;
                if (weighted <= strongestWeighted)
                    continue;

                strongestWeighted = weighted;
                strongestWeaponCount =
                    (turbolaserWeight > 0 ? turbolasers : 0)
                    + (ionCannonWeight > 0 ? ionCannons : 0)
                    + (laserCannonWeight > 0 ? laserCannons : 0);
            }

            if (strongestWeighted <= 0 || strongestWeaponCount <= 0)
                return 0;

            long value =
                strongestWeighted
                * Math.Max(0, ship.WeaponRecharge)
                * Math.Max(0, maneuverMultiplier)
                / strongestWeaponCount
                / Math.Max(1, weightDivisor);
            return value >= int.MaxValue ? int.MaxValue : (int)value;
        }

        /// <summary>
        /// Returns one weapon type's value in one firing arc.
        /// </summary>
        /// <param name="ship">The capital ship to inspect.</param>
        /// <param name="weaponType">The weapon type.</param>
        /// <param name="arcIndex">The firing-arc index.</param>
        /// <returns>The weapon count in the requested arc.</returns>
        private static int GetWeaponArcValue(
            CapitalShip ship,
            PrimaryWeaponType weaponType,
            int arcIndex
        )
        {
            return
                ship.PrimaryWeapons.TryGetValue(weaponType, out int[] values)
                && values != null
                && arcIndex >= 0
                && arcIndex < values.Length
                ? Math.Max(0, values[arcIndex])
                : 0;
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

            return fighter.CalculateCombatValue(fighter.CurrentSquadronSize);
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
            return fighter.CalculateCombatValue(squadronSize);
        }

        /// <summary>
        /// Returns the fleet-rating contribution of a carried fighter squadron.
        /// </summary>
        /// <param name="fighter">The fighter squadron to inspect.</param>
        /// <param name="squadronSize">The squadron size used by the estimate.</param>
        /// <param name="targetsFighters">Whether the target is a fighter squadron.</param>
        /// <returns>The fighter contribution.</returns>
        private static int GetStarfighterFirepower(
            Starfighter fighter,
            int squadronSize,
            bool targetsFighters
        )
        {
            int count = Math.Max(0, squadronSize);
            if (targetsFighters)
            {
                if (fighter.LaserCannon <= 0)
                    return 0;

                long baseValue =
                    (long)Math.Max(0, fighter.Agility) * count * _fighterTargetLaserWeight;
                long value =
                    baseValue
                    + (long)Math.Max(0, fighter.SublightSpeed) * baseValue / _fighterSpeedDivisor;
                return value >= int.MaxValue ? int.MaxValue : (int)value;
            }

            int ionCannons = Math.Max(0, fighter.IonCannon);
            int lasers = Math.Max(0, fighter.LaserCannon);
            int weaponCount = ionCannons + lasers;
            if (weaponCount <= 0)
                return 0;

            long weighted =
                (long)ionCannons * _capitalFirepowerScale * _laserCapitalFirepowerDivisor
                + (long)lasers * _capitalFirepowerScale;
            long capitalValue = weighted * count / weaponCount / _laserCapitalFirepowerDivisor;
            return capitalValue >= int.MaxValue ? int.MaxValue : (int)capitalValue;
        }
    }
}
