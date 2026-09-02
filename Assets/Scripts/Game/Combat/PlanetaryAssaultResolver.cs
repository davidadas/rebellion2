using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.Util.Common;

namespace Rebellion.Game.Combat
{
    /// <summary>
    /// Resolves planetary-assault casualties, collateral damage, and capture outcomes.
    /// </summary>
    public sealed class PlanetaryAssaultResolver
    {
        private readonly GameConfig.PlanetaryAssaultConfig _config;
        private readonly IRandomNumberProvider _provider;

        /// <summary>
        /// Creates a planetary-assault resolver.
        /// </summary>
        /// <param name="config">The planetary-assault resolution parameters.</param>
        /// <param name="provider">The random-number provider used during resolution.</param>
        internal PlanetaryAssaultResolver(
            GameConfig.PlanetaryAssaultConfig config,
            IRandomNumberProvider provider
        )
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <summary>
        /// Resolves an assault without modifying the live game state.
        /// </summary>
        /// <param name="attackingFleets">The fleets supplying assault troops.</param>
        /// <param name="defendingPlanet">The planet and garrison being assaulted.</param>
        /// <returns>The casualties, collateral damage, and capture outcome.</returns>
        internal PlanetaryAssaultResolution Resolve(
            IReadOnlyList<Fleet> attackingFleets,
            Planet defendingPlanet
        )
        {
            if (attackingFleets == null)
                throw new ArgumentNullException(nameof(attackingFleets));
            if (defendingPlanet == null)
                throw new ArgumentNullException(nameof(defendingPlanet));

            List<AssaultTroop> attackers = SnapshotAttackers(attackingFleets);
            List<Regiment> defenders = GetActiveDefenders(
                defendingPlanet,
                defendingPlanet.GetOwnerInstanceID()
            );
            List<Regiment> destroyedAttackers = new List<Regiment>();
            List<Regiment> destroyedDefenders = new List<Regiment>();
            List<Building> destroyedBuildings = new List<Building>();

            ResolveDefenseFire(defendingPlanet, attackers, destroyedAttackers);
            int actualDuels = ResolveGroundCombat(
                defendingPlanet,
                attackers,
                defenders,
                destroyedAttackers,
                destroyedDefenders
            );
            ResolveCollateralDamage(
                defendingPlanet,
                actualDuels,
                destroyedBuildings,
                out int energyCapacityDamage,
                out int allocatedEnergyDamage
            );

            List<AssaultTroop> survivingAttackers = GetSurvivingAttackers(
                attackers,
                destroyedAttackers
            );
            int survivingDefenderCount = GetSurvivingDefenders(defenders, destroyedDefenders).Count;
            bool capturesPlanet = survivingAttackers.Count > 0 && survivingDefenderCount == 0;
            List<Regiment> regimentsToLand = capturesPlanet
                ? survivingAttackers
                    .Take(_config.CaptureGarrisonCount)
                    .Select(attacker => attacker.Regiment)
                    .ToList()
                : new List<Regiment>();

            return new PlanetaryAssaultResolution(
                attackers.Count,
                survivingAttackers.Count,
                defenders.Count,
                survivingDefenderCount,
                energyCapacityDamage,
                allocatedEnergyDamage,
                capturesPlanet,
                destroyedAttackers,
                destroyedDefenders,
                destroyedBuildings,
                regimentsToLand
            );
        }

        /// <summary>
        /// Determines whether the supplied fleets contain any active carried regiments.
        /// </summary>
        /// <param name="fleets">The fleets to inspect.</param>
        /// <returns>True when at least one regiment can participate in an assault.</returns>
        internal static bool HasReadyAttackers(IEnumerable<Fleet> fleets)
        {
            return fleets != null && SnapshotAttackers(fleets).Count > 0;
        }

        /// <summary>
        /// Determines whether active planetary shields prevent an assault.
        /// </summary>
        /// <param name="planet">The planet whose shield facilities are evaluated.</param>
        /// <param name="shieldGeneratorLimit">The active shield count that blocks an assault.</param>
        /// <returns>True when the active shield count meets the configured limit.</returns>
        public static bool IsBlockedByShields(Planet planet, int shieldGeneratorLimit)
        {
            if (planet == null)
                return false;

            int activeShieldCount = planet
                .GetAllBuildings()
                .Count(building =>
                    IsActiveAssaultUnit(building) && building.IsPlanetaryShieldGenerator()
                );
            return activeShieldCount >= shieldGeneratorLimit;
        }

        /// <summary>
        /// Returns the leadership rating of the first eligible commander.
        /// </summary>
        /// <param name="officers">The officers to search.</param>
        /// <param name="rank">The required command rank.</param>
        /// <param name="ownerId">The required faction instance ID.</param>
        /// <param name="config">The planetary-assault configuration.</param>
        /// <returns>The commander's leadership bonus, or zero when none is eligible.</returns>
        public static int GetLeadershipBonus(
            IEnumerable<Officer> officers,
            OfficerRank rank,
            string ownerId,
            GameConfig.PlanetaryAssaultConfig config
        )
        {
            if (config == null)
                return 0;

            Officer commander = officers?.FirstOrDefault(officer =>
                officer.CurrentRank == rank
                && officer.GetOwnerInstanceID() == ownerId
                && !officer.IsKilled
            );
            int leadership = commander?.GetEffectiveRating(OfficerRating.Leadership) ?? 0;
            return leadership / config.GeneralLeadershipDivisor;
        }

        /// <summary>
        /// Estimates the chance that an immediate planetary assault captures its target.
        /// </summary>
        /// <param name="fleets">The fleets supplying the assault force.</param>
        /// <param name="planet">The planet being assaulted.</param>
        /// <param name="config">The planetary-assault rules.</param>
        /// <returns>The estimated success chance from zero through one hundred.</returns>
        public static int EstimateSuccessPercent(
            IReadOnlyList<Fleet> fleets,
            Planet planet,
            GameConfig.PlanetaryAssaultConfig config
        )
        {
            if (fleets?.Any() != true || planet == null || config == null)
                return 0;

            List<AssaultTroop> attackers = SnapshotAttackers(fleets);
            if (attackers.Count == 0)
                return 0;

            string defenderId = planet.GetOwnerInstanceID();
            List<Regiment> defenders = GetActiveDefenders(planet, defenderId);
            List<Building> defenseFacilities = planet
                .GetAllBuildings()
                .Where(building =>
                    IsActiveAssaultUnit(building) && IsAssaultDefenseFacility(building)
                )
                .ToList();
            double[] casualtyProbabilities = CalculateDefenseFireCasualtyProbabilities(
                attackers.Count,
                defenseFacilities,
                config
            );
            int defenderBonus = GetLeadershipBonus(
                planet.GetAllOfficers(),
                OfficerRank.General,
                defenderId,
                config
            );
            List<double> attackerWinProbabilities = attackers
                .Select(attacker =>
                    GetMinimumContestWinProbability(attacker, defenders, defenderBonus, config)
                )
                .OrderBy(probability => probability)
                .ToList();

            double successProbability = 0;
            for (int casualties = 0; casualties < casualtyProbabilities.Length; casualties++)
            {
                int survivorCount = attackers.Count - casualties;
                if (survivorCount <= 0 || casualtyProbabilities[casualties] <= 0)
                    continue;

                double groundSuccessProbability = CalculateGroundSuccessProbability(
                    attackerWinProbabilities.Take(survivorCount),
                    defenders.Count
                );
                successProbability += casualtyProbabilities[casualties] * groundSuccessProbability;
            }

            return Math.Clamp((int)Math.Floor(successProbability * 100), 0, 100);
        }

        /// <summary>
        /// Resolves planetary defense-facility fire against the assault force.
        /// </summary>
        /// <param name="planet">The planet containing the defending facilities.</param>
        /// <param name="attackers">The assault troops available as targets.</param>
        /// <param name="destroyedAttackers">The collection receiving destroyed attackers.</param>
        private void ResolveDefenseFire(
            Planet planet,
            List<AssaultTroop> attackers,
            List<Regiment> destroyedAttackers
        )
        {
            int initialAttackerCount = attackers.Count;
            foreach (
                Building facility in planet
                    .GetAllBuildings()
                    .Where(building =>
                        IsActiveAssaultUnit(building) && IsAssaultDefenseFacility(building)
                    )
            )
            {
                if (GetSurvivingAttackers(attackers, destroyedAttackers).Count == 0)
                    break;

                int chance = facility.WeaponPower / _config.DefenseFireDivisor;
                if (!RollPercent(chance))
                    continue;

                List<AssaultTroop> survivors = GetSurvivingAttackers(attackers, destroyedAttackers);
                int targetIndex = _provider.NextInt(0, initialAttackerCount);
                if (targetIndex >= survivors.Count)
                    continue;

                destroyedAttackers.Add(survivors[targetIndex].Regiment);
            }
        }

        /// <summary>
        /// Resolves each surviving attacker's ground-combat attempt.
        /// </summary>
        /// <param name="planet">The planet where ground combat occurs.</param>
        /// <param name="attackers">The assault troops taking turns.</param>
        /// <param name="defenders">The defending regiments available as targets.</param>
        /// <param name="destroyedAttackers">The collection receiving destroyed attackers.</param>
        /// <param name="destroyedDefenders">The collection receiving destroyed defenders.</param>
        /// <returns>The number of attacker-defender contests that occurred.</returns>
        private int ResolveGroundCombat(
            Planet planet,
            List<AssaultTroop> attackers,
            List<Regiment> defenders,
            List<Regiment> destroyedAttackers,
            List<Regiment> destroyedDefenders
        )
        {
            int initialDefenderCount = defenders.Count;
            int actualDuels = 0;
            List<AssaultTroop> attackerTurnOrder = GetSurvivingAttackers(
                attackers,
                destroyedAttackers
            );

            foreach (AssaultTroop attacker in attackerTurnOrder)
            {
                if (destroyedAttackers.Contains(attacker.Regiment))
                    continue;

                List<Regiment> survivingDefenders = GetSurvivingDefenders(
                    defenders,
                    destroyedDefenders
                );
                if (survivingDefenders.Count == 0 || initialDefenderCount == 0)
                    break;

                int defenderIndex = _provider.NextInt(0, initialDefenderCount);
                if (defenderIndex >= survivingDefenders.Count)
                    continue;

                Regiment defender = survivingDefenders[defenderIndex];
                actualDuels++;
                int score = CalculateContestScore(attacker, defender, planet);
                if (score <= _config.DefenderWinsMaximum)
                    destroyedAttackers.Add(attacker.Regiment);
                else if (score >= _config.AttackerWinsMinimum)
                    destroyedDefenders.Add(defender);
            }

            return actualDuels;
        }

        /// <summary>
        /// Calculates the outcome score for one ground-combat contest.
        /// </summary>
        /// <param name="attacker">The attacking regiment and its carrier.</param>
        /// <param name="defender">The defending regiment.</param>
        /// <param name="planet">The planet supplying the defending command staff.</param>
        /// <returns>The contest score used to determine casualties.</returns>
        private int CalculateContestScore(AssaultTroop attacker, Regiment defender, Planet planet)
        {
            Fleet fleet = attacker.Ship.GetParentOfType<Fleet>();
            int attackerBonus = GetLeadershipBonus(
                fleet?.GetOfficers(),
                OfficerRank.General,
                fleet?.GetOwnerInstanceID(),
                _config
            );
            int defenderBonus = GetLeadershipBonus(
                planet.GetAllOfficers(),
                OfficerRank.General,
                planet.GetOwnerInstanceID(),
                _config
            );
            int roll = _provider.NextInt(0, _config.ContestRollMaximum + 1);
            return roll
                + attacker.Regiment.AttackRating
                + attackerBonus
                - defender.DefenseRating
                - defenderBonus;
        }

        /// <summary>
        /// Resolves collateral-damage trials generated by ground combat.
        /// </summary>
        /// <param name="planet">The planet containing potential collateral targets.</param>
        /// <param name="trialCount">The number of collateral-damage trials.</param>
        /// <param name="destroyedBuildings">The collection receiving destroyed buildings.</param>
        /// <param name="energyCapacityDamage">Receives the planet's lost energy capacity.</param>
        /// <param name="allocatedEnergyDamage">Receives the planet's lost allocated energy.</param>
        private void ResolveCollateralDamage(
            Planet planet,
            int trialCount,
            List<Building> destroyedBuildings,
            out int energyCapacityDamage,
            out int allocatedEnergyDamage
        )
        {
            int successfulTrials = 0;
            for (int trial = 0; trial < trialCount; trial++)
            {
                if (RollPercent(_config.CollateralDamagePercent))
                    successfulTrials++;
            }

            energyCapacityDamage = 0;
            allocatedEnergyDamage = 0;
            for (int trial = 0; trial < successfulTrials; trial++)
            {
                List<CollateralTarget> targets = BuildCollateralTargets(
                    planet,
                    destroyedBuildings,
                    energyCapacityDamage,
                    allocatedEnergyDamage
                );
                if (targets.Count == 0)
                    break;

                CollateralTarget target = targets[_provider.NextInt(0, targets.Count)];
                switch (target.Type)
                {
                    case CollateralTargetType.Building:
                        destroyedBuildings.Add(target.Building);
                        break;
                    case CollateralTargetType.EnergyCapacity:
                        energyCapacityDamage++;
                        break;
                    case CollateralTargetType.AllocatedEnergy:
                        allocatedEnergyDamage++;
                        break;
                }
            }
        }

        /// <summary>
        /// Builds the collateral targets remaining after earlier resolved damage.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <param name="destroyedBuildings">Buildings already selected for destruction.</param>
        /// <param name="energyCapacityDamage">Energy-capacity damage already resolved.</param>
        /// <param name="allocatedEnergyDamage">Allocated-energy damage already resolved.</param>
        /// <returns>The remaining facilities and damageable energy pools.</returns>
        private static List<CollateralTarget> BuildCollateralTargets(
            Planet planet,
            IReadOnlyCollection<Building> destroyedBuildings,
            int energyCapacityDamage,
            int allocatedEnergyDamage
        )
        {
            List<CollateralTarget> targets = planet
                .GetAllBuildings()
                .Where(building =>
                    building.BuildingType != BuildingType.Headquarters
                    && IsActiveAssaultUnit(building)
                    && !destroyedBuildings.Contains(building)
                )
                .Select(building => new CollateralTarget
                {
                    Type = CollateralTargetType.Building,
                    Building = building,
                })
                .ToList();

            if (planet.EnergyCapacity - energyCapacityDamage > 0)
                targets.Add(new CollateralTarget { Type = CollateralTargetType.EnergyCapacity });
            if (planet.AllocatedEnergy - allocatedEnergyDamage > 0)
                targets.Add(new CollateralTarget { Type = CollateralTargetType.AllocatedEnergy });

            return targets;
        }

        /// <summary>
        /// Captures the active carried regiments participating in an assault.
        /// </summary>
        /// <param name="fleets">The fleets supplying assault troops.</param>
        /// <returns>The active regiments paired with their carrier ships.</returns>
        private static List<AssaultTroop> SnapshotAttackers(IEnumerable<Fleet> fleets)
        {
            return fleets
                .SelectMany(fleet => fleet.GetChildren<CapitalShip>())
                .Where(IsActiveAssaultUnit)
                .SelectMany(ship =>
                    ship.GetChildren<Regiment>()
                        .Where(IsActiveAssaultUnit)
                        .Select(regiment => new AssaultTroop { Regiment = regiment, Ship = ship })
                )
                .ToList();
        }

        /// <summary>
        /// Returns active defending regiments owned by the specified faction.
        /// </summary>
        /// <param name="planet">The planet containing the defenders.</param>
        /// <param name="defenderId">The defending faction instance ID.</param>
        /// <returns>The active defending regiments.</returns>
        private static List<Regiment> GetActiveDefenders(Planet planet, string defenderId)
        {
            return planet
                .GetAllRegiments()
                .Where(regiment =>
                    IsActiveAssaultUnit(regiment) && regiment.GetOwnerInstanceID() == defenderId
                )
                .ToList();
        }

        /// <summary>
        /// Returns assault troops not selected for destruction.
        /// </summary>
        /// <param name="attackers">The assault troops to inspect.</param>
        /// <param name="destroyedAttackers">The destroyed attacking regiments.</param>
        /// <returns>The surviving assault troops.</returns>
        private static List<AssaultTroop> GetSurvivingAttackers(
            IEnumerable<AssaultTroop> attackers,
            IReadOnlyCollection<Regiment> destroyedAttackers
        )
        {
            return attackers
                .Where(attacker => !destroyedAttackers.Contains(attacker.Regiment))
                .ToList();
        }

        /// <summary>
        /// Returns defending regiments not selected for destruction.
        /// </summary>
        /// <param name="defenders">The defending regiments to inspect.</param>
        /// <param name="destroyedDefenders">The destroyed defending regiments.</param>
        /// <returns>The surviving defending regiments.</returns>
        private static List<Regiment> GetSurvivingDefenders(
            IEnumerable<Regiment> defenders,
            IReadOnlyCollection<Regiment> destroyedDefenders
        )
        {
            return defenders.Where(defender => !destroyedDefenders.Contains(defender)).ToList();
        }

        /// <summary>
        /// Calculates the probability distribution for casualties caused by defense facilities.
        /// </summary>
        /// <param name="attackerCount">The number of regiments attempting to land.</param>
        /// <param name="facilities">The facilities firing on the landing force.</param>
        /// <param name="config">The planetary-assault rules.</param>
        /// <returns>The probability of suffering each possible casualty count.</returns>
        private static double[] CalculateDefenseFireCasualtyProbabilities(
            int attackerCount,
            IEnumerable<Building> facilities,
            GameConfig.PlanetaryAssaultConfig config
        )
        {
            double[] probabilities = new double[attackerCount + 1];
            probabilities[0] = 1;
            foreach (Building facility in facilities)
            {
                double fireChance =
                    Math.Clamp(facility.WeaponPower / config.DefenseFireDivisor, 0, 100) / 100.0;
                double[] next = new double[attackerCount + 1];
                for (int casualties = 0; casualties <= attackerCount; casualties++)
                {
                    double stateProbability = probabilities[casualties];
                    if (stateProbability <= 0)
                        continue;

                    double targetChance = (double)(attackerCount - casualties) / attackerCount;
                    double killChance = fireChance * targetChance;
                    next[casualties] += stateProbability * (1 - killChance);
                    if (casualties < attackerCount)
                        next[casualties + 1] += stateProbability * killChance;
                }

                probabilities = next;
            }

            return probabilities;
        }

        /// <summary>
        /// Calculates an attacker's lowest win probability against the available defenders.
        /// </summary>
        /// <param name="attacker">The attacking regiment and its transport.</param>
        /// <param name="defenders">The regiments defending the planet.</param>
        /// <param name="defenderBonus">The leadership bonus applied to every defender.</param>
        /// <param name="config">The planetary-assault rules.</param>
        /// <returns>The attacker's lowest contest win probability.</returns>
        private static double GetMinimumContestWinProbability(
            AssaultTroop attacker,
            IReadOnlyList<Regiment> defenders,
            int defenderBonus,
            GameConfig.PlanetaryAssaultConfig config
        )
        {
            if (defenders.Count == 0)
                return 1;

            Fleet fleet = attacker.Ship.GetParentOfType<Fleet>();
            int attackerBonus = GetLeadershipBonus(
                fleet?.GetOfficers(),
                OfficerRank.General,
                fleet?.GetOwnerInstanceID(),
                config
            );
            int possibleRolls = config.ContestRollMaximum + 1;
            return defenders.Min(defender =>
            {
                int minimumWinningRoll =
                    config.AttackerWinsMinimum
                    - attacker.Regiment.AttackRating
                    - attackerBonus
                    + defender.DefenseRating
                    + defenderBonus;
                int winningRolls = Math.Clamp(
                    possibleRolls - Math.Max(0, minimumWinningRoll),
                    0,
                    possibleRolls
                );
                return (double)winningRolls / possibleRolls;
            });
        }

        /// <summary>
        /// Calculates the probability that the surviving attackers defeat every defender.
        /// </summary>
        /// <param name="attackerWinProbabilities">The contest win probability for each attacker.</param>
        /// <param name="defenderCount">The number of defending regiments.</param>
        /// <returns>The probability of defeating every defender.</returns>
        private static double CalculateGroundSuccessProbability(
            IEnumerable<double> attackerWinProbabilities,
            int defenderCount
        )
        {
            if (defenderCount == 0)
                return 1;

            double[] probabilities = new double[defenderCount + 1];
            probabilities[0] = 1;
            foreach (double contestWinProbability in attackerWinProbabilities)
            {
                double[] next = new double[defenderCount + 1];
                for (int defeated = 0; defeated <= defenderCount; defeated++)
                {
                    double stateProbability = probabilities[defeated];
                    if (stateProbability <= 0)
                        continue;

                    if (defeated == defenderCount)
                    {
                        next[defeated] += stateProbability;
                        continue;
                    }

                    double targetChance = (double)(defenderCount - defeated) / defenderCount;
                    double defeatChance = contestWinProbability * targetChance;
                    next[defeated] += stateProbability * (1 - defeatChance);
                    next[defeated + 1] += stateProbability * defeatChance;
                }

                probabilities = next;
            }

            return probabilities[defenderCount];
        }

        /// <summary>
        /// Rolls a percentage chance for an assault event.
        /// </summary>
        /// <param name="chance">The percentage chance threshold.</param>
        /// <returns>True when the roll succeeds.</returns>
        private bool RollPercent(int chance)
        {
            return _provider.NextInt(0, 100) < chance;
        }

        /// <summary>
        /// Determines whether a manufacturable unit is complete and stationary.
        /// </summary>
        /// <param name="unit">The unit to inspect.</param>
        /// <returns>True when the unit can participate in an assault.</returns>
        private static bool IsActiveAssaultUnit(IManufacturable unit)
        {
            return unit.ManufacturingStatus == ManufacturingStatus.Complete
                && unit.Movement == null;
        }

        /// <summary>
        /// Determines whether a capital ship can supply assault troops.
        /// </summary>
        /// <param name="ship">The capital ship to inspect.</param>
        /// <returns>True when the ship is active and has remaining hull strength.</returns>
        private static bool IsActiveAssaultUnit(CapitalShip ship)
        {
            return IsActiveAssaultUnit((IManufacturable)ship) && ship.CurrentHullStrength > 0;
        }

        /// <summary>
        /// Determines whether a building participates in assault defense fire.
        /// </summary>
        /// <param name="building">The building to inspect.</param>
        /// <returns>True when the building is a planetary defense facility.</returns>
        private static bool IsAssaultDefenseFacility(Building building)
        {
            return building.IsDefenseFacility();
        }

        private sealed class AssaultTroop
        {
            internal Regiment Regiment;
            internal CapitalShip Ship;
        }

        private sealed class CollateralTarget
        {
            internal CollateralTargetType Type;
            internal Building Building;
        }

        private enum CollateralTargetType
        {
            Building,
            EnergyCapacity,
            AllocatedEnergy,
        }
    }
}
