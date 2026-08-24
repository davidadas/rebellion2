using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

namespace Rebellion.Systems
{
    /// <summary>
    /// Resolves planetary assaults and captures.
    /// </summary>
    public class PlanetaryAssaultSystem
    {
        private readonly GameRoot _game;
        private readonly IRandomNumberProvider _provider;
        private readonly PlanetaryControlSystem _ownership;

        /// <summary>
        /// Raised after an immediate planetary-assault command produces results.
        /// </summary>
        public event Action<IReadOnlyList<GameResult>> ResultsProduced;

        /// <summary>
        /// Creates the planetary-assault system.
        /// </summary>
        /// <param name="game">Active game state.</param>
        /// <param name="provider">Random-number provider used by assault resolution.</param>
        /// <param name="ownership">Planetary control system used to capture planets.</param>
        public PlanetaryAssaultSystem(
            GameRoot game,
            IRandomNumberProvider provider,
            PlanetaryControlSystem ownership
        )
        {
            _game = game;
            _provider = provider;
            _ownership = ownership ?? throw new ArgumentNullException(nameof(ownership));
        }

        /// <summary>
        /// Executes a validated planetary-assault command and publishes its results.
        /// </summary>
        /// <param name="attackingFleets">The attacking fleets.</param>
        /// <param name="targetPlanet">The assault target planet.</param>
        /// <returns>The assault result, or null when the assault cannot execute.</returns>
        public PlanetaryAssaultResult TryExecute(
            IReadOnlyList<Fleet> attackingFleets,
            Planet targetPlanet
        )
        {
            if (targetPlanet == null)
                return null;

            List<Fleet> fleets =
                attackingFleets?.Where(fleet => fleet != null).ToList() ?? new List<Fleet>();
            if (!CanExecute(fleets, targetPlanet))
                return null;

            PlanetaryAssaultResult result = Execute(fleets, targetPlanet);
            List<GameResult> results = new List<GameResult> { result };
            results.AddRange(result.Events);
            if (result.OwnershipChange != null)
                results.Add(result.OwnershipChange);

            ResultsProduced?.Invoke(results);
            return result;
        }

        /// <summary>
        /// Runs the planetary-assault pipeline against a defending planet.
        /// </summary>
        /// <param name="attackingFleets">Fleets performing the assault (all must share a faction).</param>
        /// <param name="defendingPlanet">Planet being assaulted.</param>
        /// <returns>Assault outcome, including destroyed units and any ownership change.</returns>
        public PlanetaryAssaultResult Execute(List<Fleet> attackingFleets, Planet defendingPlanet)
        {
            PlanetaryAssaultResult result = new PlanetaryAssaultResult
            {
                Planet = defendingPlanet,
                Tick = _game.CurrentTick,
            };

            if (!CanAssault(attackingFleets, defendingPlanet))
                return result;

            string attackerId = attackingFleets[0].GetOwnerInstanceID();
            string defenderId = defendingPlanet.GetOwnerInstanceID();
            result.AttackingFaction = _game.GetFactionByOwnerInstanceID(attackerId);
            result.AttackerOwnerInstanceID = attackerId;
            result.DefenderOwnerInstanceID = defenderId;
            result.AttackingUnits.AddRange(CombatUnitSnapshot.CaptureFleetUnits(attackingFleets));
            result.DefendingUnits.AddRange(
                CombatUnitSnapshot.CapturePlanetUnits(defendingPlanet, defenderId)
            );

            if (
                IsBlockedByShields(
                    defendingPlanet,
                    _game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit
                )
            )
            {
                result.BlockedByShields = true;
                return result;
            }

            List<AssaultTroop> attackers = SnapshotAttackers(attackingFleets);
            List<Regiment> defenders = GetActiveDefenders(defendingPlanet, defenderId);
            result.InitialAttackerRegimentCount = attackers.Count;
            result.InitialDefenderRegimentCount = defenders.Count;
            if (attackers.Count == 0)
                return result;

            SetAssaultCombatState(attackingFleets, defendingPlanet, true);
            try
            {
                ResolveAssaultDefenseFire(defendingPlanet, attackers, result);
                int actualDuels = ResolveGroundCombat(
                    defendingPlanet,
                    attackers,
                    defenders,
                    result
                );
                ResolveCollateralDamage(defendingPlanet, actualDuels, result);
                CapturePlanet(
                    defendingPlanet,
                    result.AttackingFaction,
                    attackers,
                    defenders,
                    result
                );

                result.RemainingAttackerRegimentCount = GetSurvivingAttackers(attackers).Count;
                result.RemainingDefenderRegimentCount = GetSurvivingDefenders(defenders).Count;
                if (result.DestroyedDefenderRegiments.Count > 0 || result.LandedRegiments.Count > 0)
                {
                    result.Events.Add(
                        new PlanetGarrisonChangedResult
                        {
                            Planet = defendingPlanet,
                            Tick = _game.CurrentTick,
                        }
                    );
                }
                return result;
            }
            finally
            {
                RecordUnitOutcomes(result);
                SetAssaultCombatState(attackingFleets, defendingPlanet, false);
            }
        }

        /// <summary>
        /// Determines whether the supplied fleets can execute a planetary assault.
        /// </summary>
        /// <param name="fleets">Fleets attempting the assault.</param>
        /// <param name="planet">Planet being assaulted.</param>
        /// <returns>True when the fleets contain ready troops and shields do not block them.</returns>
        public bool CanExecute(IReadOnlyList<Fleet> fleets, Planet planet)
        {
            return CanAssault(fleets, planet)
                && !IsBlockedByShields(
                    planet,
                    _game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit
                )
                && SnapshotAttackers(fleets).Count > 0;
        }

        /// <summary>
        /// Determines whether the supplied fleets can begin an assault at the planet.
        /// </summary>
        /// <param name="fleets">Fleets attempting the assault.</param>
        /// <param name="planet">Planet being assaulted.</param>
        /// <returns>True when every fleet is stationary, colocated, and owned by one faction.</returns>
        private static bool CanAssault(IReadOnlyList<Fleet> fleets, Planet planet)
        {
            if (
                planet?.IsDestroyed != false
                || fleets?.Any() != true
                || fleets.Any(fleet => fleet == null)
            )
                return false;

            string ownerId = fleets[0].GetOwnerInstanceID();
            return !string.IsNullOrEmpty(ownerId)
                && planet?.GetOwnerInstanceID() != ownerId
                && fleets.All(fleet =>
                    fleet.GetOwnerInstanceID() == ownerId
                    && fleet.Movement == null
                    && !fleet.IsInCombat
                    && fleet.GetParent() == planet
                );
        }

        /// <summary>
        /// Records which captured units were destroyed during the assault.
        /// </summary>
        /// <param name="result">The completed planetary-assault result.</param>
        private static void RecordUnitOutcomes(PlanetaryAssaultResult result)
        {
            CombatUnitSnapshot.RecordOutcomes(
                result.AttackingUnits,
                null,
                result.DestroyedAttackerRegiments
            );
            CombatUnitSnapshot.RecordOutcomes(
                result.DefendingUnits,
                null,
                result
                    .DestroyedDefenderRegiments.Cast<ISceneNode>()
                    .Concat(result.CollateralDestroyedBuildings)
            );
        }

        /// <summary>
        /// Sets the combat state for the attacking fleets and fleets stationed at the planet.
        /// </summary>
        /// <param name="attackers">Fleets performing the assault.</param>
        /// <param name="planet">Planet where the assault is occurring.</param>
        /// <param name="isInCombat">Whether the affected fleets are in combat.</param>
        private static void SetAssaultCombatState(
            List<Fleet> attackers,
            Planet planet,
            bool isInCombat
        )
        {
            foreach (Fleet fleet in attackers)
                fleet.IsInCombat = isInCombat;

            foreach (Fleet fleet in planet.GetChildren<Fleet>())
                fleet.IsInCombat = isInCombat;
        }

        /// <summary>
        /// Determines whether active planetary shields prevent an assault.
        /// </summary>
        /// <param name="planet">Planet whose shield facilities are evaluated.</param>
        /// <param name="shieldGeneratorLimit">Active shield count that blocks an assault.</param>
        /// <returns>True when the active shield count meets the configured limit.</returns>
        public static bool IsBlockedByShields(Planet planet, int shieldGeneratorLimit)
        {
            if (planet == null)
                return false;

            int activeShieldCount = planet
                .GetAllBuildings()
                .Count(building =>
                    IsActiveAssaultUnit(building)
                    && building.DefenseFacilityClass == DefenseFacilityClass.Shield
                );
            return activeShieldCount >= shieldGeneratorLimit;
        }

        /// <summary>
        /// Resolves planetary defense-facility fire against the assault force.
        /// </summary>
        /// <param name="planet">Planet containing the defending facilities.</param>
        /// <param name="attackers">Assault troops available as targets.</param>
        /// <param name="result">Assault result receiving destroyed attackers.</param>
        private void ResolveAssaultDefenseFire(
            Planet planet,
            List<AssaultTroop> attackers,
            PlanetaryAssaultResult result
        )
        {
            int initialAttackerCount = attackers.Count;
            int divisor = _game.Config.Combat.PlanetaryAssault.DefenseFireDivisor;

            foreach (
                Building facility in planet
                    .GetAllBuildings()
                    .Where(building =>
                        IsActiveAssaultUnit(building) && IsAssaultDefenseFacility(building)
                    )
            )
            {
                if (GetSurvivingAttackers(attackers).Count == 0)
                    break;

                int chance = facility.WeaponPower / divisor;
                if (!RollAssaultPercent(chance))
                    continue;

                List<AssaultTroop> survivors = GetSurvivingAttackers(attackers);
                int targetIndex = _provider.NextInt(0, initialAttackerCount);
                if (targetIndex >= survivors.Count)
                    continue;

                Regiment target = survivors[targetIndex].Regiment;
                result.DestroyedAttackerRegiments.Add(target);
                _game.DeleteNode(target);
            }
        }

        /// <summary>
        /// Resolves each surviving attacker's ground-combat attempt.
        /// </summary>
        /// <param name="planet">Planet where ground combat occurs.</param>
        /// <param name="attackers">Assault troops taking turns.</param>
        /// <param name="defenders">Defending regiments available as targets.</param>
        /// <param name="result">Assault result receiving destroyed regiments.</param>
        /// <returns>The number of attacker-defender contests that occurred.</returns>
        private int ResolveGroundCombat(
            Planet planet,
            List<AssaultTroop> attackers,
            List<Regiment> defenders,
            PlanetaryAssaultResult result
        )
        {
            int initialDefenderCount = defenders.Count;
            int actualDuels = 0;
            List<AssaultTroop> attackerTurnOrder = GetSurvivingAttackers(attackers);

            foreach (AssaultTroop attacker in attackerTurnOrder)
            {
                if (attacker.Regiment.GetParent() == null)
                    continue;

                List<Regiment> survivingDefenders = GetSurvivingDefenders(defenders);
                if (survivingDefenders.Count == 0 || initialDefenderCount == 0)
                    break;

                int defenderIndex = _provider.NextInt(0, initialDefenderCount);
                if (defenderIndex >= survivingDefenders.Count)
                    continue;

                Regiment defender = survivingDefenders[defenderIndex];
                actualDuels++;
                int score = CalculateContestScore(attacker, defender, planet);
                GameConfig.PlanetaryAssaultConfig config = _game.Config.Combat.PlanetaryAssault;

                if (score <= config.DefenderWinsMaximum)
                {
                    result.DestroyedAttackerRegiments.Add(attacker.Regiment);
                    _game.DeleteNode(attacker.Regiment);
                }
                else if (score >= config.AttackerWinsMinimum)
                {
                    result.DestroyedDefenderRegiments.Add(defender);
                    _game.DeleteNode(defender);
                }
            }

            return actualDuels;
        }

        /// <summary>
        /// Calculates the outcome score for one ground-combat contest.
        /// </summary>
        /// <param name="attacker">Attacking regiment and its carrier.</param>
        /// <param name="defender">Defending regiment.</param>
        /// <param name="planet">Planet supplying the defending command staff.</param>
        /// <returns>The contest score used to determine casualties.</returns>
        private int CalculateContestScore(AssaultTroop attacker, Regiment defender, Planet planet)
        {
            GameConfig.PlanetaryAssaultConfig config = _game.Config.Combat.PlanetaryAssault;
            Fleet fleet = attacker.Ship.GetParentOfType<Fleet>();
            int attackerBonus = GetLeadershipBonus(
                fleet?.GetOfficers(),
                OfficerRank.General,
                fleet?.GetOwnerInstanceID(),
                config
            );
            int defenderBonus = GetLeadershipBonus(
                planet.GetAllOfficers(),
                OfficerRank.General,
                planet.GetOwnerInstanceID(),
                config
            );
            int roll = _provider.NextInt(0, config.ContestRollMaximum + 1);
            return roll
                + attacker.Regiment.AttackRating
                + attackerBonus
                - defender.DefenseRating
                - defenderBonus;
        }

        /// <summary>
        /// Resolves collateral-damage trials generated by ground combat.
        /// </summary>
        /// <param name="planet">Planet containing potential collateral targets.</param>
        /// <param name="trialCount">Number of collateral-damage trials.</param>
        /// <param name="result">Assault result receiving collateral damage.</param>
        private void ResolveCollateralDamage(
            Planet planet,
            int trialCount,
            PlanetaryAssaultResult result
        )
        {
            int successfulTrials = 0;
            for (int trial = 0; trial < trialCount; trial++)
            {
                if (
                    RollAssaultPercent(_game.Config.Combat.PlanetaryAssault.CollateralDamagePercent)
                )
                    successfulTrials++;
            }

            for (int trial = 0; trial < successfulTrials; trial++)
            {
                List<CollateralTarget> targets = BuildCollateralTargets(planet);
                if (targets.Count == 0)
                    break;

                ApplyCollateralTarget(planet, targets[_provider.NextInt(0, targets.Count)], result);
            }
        }

        /// <summary>
        /// Builds the currently valid collateral targets on a planet.
        /// </summary>
        /// <param name="planet">Planet to inspect.</param>
        /// <returns>Active facilities and damageable energy pools.</returns>
        private static List<CollateralTarget> BuildCollateralTargets(Planet planet)
        {
            List<CollateralTarget> targets = planet
                .GetAllBuildings()
                .Where(building =>
                    building.BuildingType != BuildingType.Headquarters
                    && IsActiveAssaultUnit(building)
                )
                .Select(building =>
                {
                    return new CollateralTarget
                    {
                        Type = CollateralTargetType.Building,
                        Entity = building,
                    };
                })
                .ToList();

            if (planet.EnergyCapacity > 0)
                targets.Add(new CollateralTarget { Type = CollateralTargetType.EnergyCapacity });
            if (planet.AllocatedEnergy > 0)
                targets.Add(new CollateralTarget { Type = CollateralTargetType.AllocatedEnergy });

            return targets;
        }

        /// <summary>
        /// Applies one collateral-damage result to its selected target.
        /// </summary>
        /// <param name="planet">Planet containing the target.</param>
        /// <param name="target">Collateral target to damage or destroy.</param>
        /// <param name="result">Assault result receiving the applied damage.</param>
        private void ApplyCollateralTarget(
            Planet planet,
            CollateralTarget target,
            PlanetaryAssaultResult result
        )
        {
            switch (target.Type)
            {
                case CollateralTargetType.Building:
                    Building building = (Building)target.Entity;
                    result.CollateralDestroyedBuildings.Add(building);
                    _game.DeleteNode(building);
                    break;
                case CollateralTargetType.EnergyCapacity:
                    planet.EnergyCapacity--;
                    result.EnergyCapacityDamage++;
                    break;
                case CollateralTargetType.AllocatedEnergy:
                    planet.AllocatedEnergy--;
                    result.AllocatedEnergyDamage++;
                    break;
            }
        }

        /// <summary>
        /// Transfers an undefended planet and lands the required surviving garrison.
        /// </summary>
        /// <param name="planet">Planet to capture.</param>
        /// <param name="attacker">Faction performing the assault.</param>
        /// <param name="attackers">Assault troops that may form the garrison.</param>
        /// <param name="defenders">Defending regiments used to verify the victory.</param>
        /// <param name="result">Assault result receiving ownership and landing details.</param>
        private void CapturePlanet(
            Planet planet,
            Faction attacker,
            List<AssaultTroop> attackers,
            List<Regiment> defenders,
            PlanetaryAssaultResult result
        )
        {
            List<AssaultTroop> survivingAttackers = GetSurvivingAttackers(attackers);
            if (survivingAttackers.Count == 0 || GetSurvivingDefenders(defenders).Count > 0)
                return;

            result.OwnershipChange = _ownership.TransferPlanet(planet, attacker);
            int garrisonRequirement = _game.Config.Combat.PlanetaryAssault.CaptureGarrisonCount;

            foreach (AssaultTroop assaultTroop in survivingAttackers.Take(garrisonRequirement))
            {
                _game.MoveNode(assaultTroop.Regiment, planet);
                result.LandedRegiments.Add(assaultTroop.Regiment);
            }

            result.Success = true;
        }

        /// <summary>
        /// Captures the active carried regiments participating in an assault.
        /// </summary>
        /// <param name="fleets">Fleets supplying assault troops.</param>
        /// <returns>Active regiments paired with their carrier ships.</returns>
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
        /// <param name="planet">Planet containing the defenders.</param>
        /// <param name="defenderId">Defending faction instance ID.</param>
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
        /// Returns assault troops whose regiments remain attached to the scene graph.
        /// </summary>
        /// <param name="attackers">Assault troops to inspect.</param>
        /// <returns>The surviving assault troops.</returns>
        private static List<AssaultTroop> GetSurvivingAttackers(IEnumerable<AssaultTroop> attackers)
        {
            return attackers.Where(attacker => attacker.Regiment.GetParent() != null).ToList();
        }

        /// <summary>
        /// Returns defending regiments that remain attached to the scene graph.
        /// </summary>
        /// <param name="defenders">Defending regiments to inspect.</param>
        /// <returns>The surviving defending regiments.</returns>
        private static List<Regiment> GetSurvivingDefenders(IEnumerable<Regiment> defenders)
        {
            return defenders.Where(defender => defender.GetParent() != null).ToList();
        }

        /// <summary>
        /// Rolls a percentage chance for an assault event.
        /// </summary>
        /// <param name="chance">Percentage chance threshold.</param>
        /// <returns>True when the roll succeeds.</returns>
        private bool RollAssaultPercent(int chance)
        {
            return _provider.NextInt(0, 100) < chance;
        }

        /// <summary>
        /// Returns the leadership rating of the first eligible commander.
        /// </summary>
        /// <param name="officers">Officers to search.</param>
        /// <param name="rank">Required command rank.</param>
        /// <param name="ownerId">Required faction instance ID.</param>
        /// <param name="config">Planetary assault configuration.</param>
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
        /// <param name="fleets">Fleets supplying the assault force.</param>
        /// <param name="planet">Planet being assaulted.</param>
        /// <param name="config">Planetary assault rules.</param>
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
        /// Calculates the probability distribution for casualties caused by defense facilities.
        /// </summary>
        /// <param name="attackerCount">Number of regiments attempting to land.</param>
        /// <param name="facilities">Facilities firing on the landing force.</param>
        /// <param name="config">Planetary assault rules.</param>
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
        /// <param name="attacker">Attacking regiment and its transport.</param>
        /// <param name="defenders">Regiments defending the planet.</param>
        /// <param name="defenderBonus">Leadership bonus applied to every defender.</param>
        /// <param name="config">Planetary assault rules.</param>
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
        /// <param name="attackerWinProbabilities">Contest win probability for each attacker.</param>
        /// <param name="defenderCount">Number of defending regiments.</param>
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
        /// Determines whether a manufacturable unit is complete and stationary.
        /// </summary>
        /// <param name="unit">Unit to inspect.</param>
        /// <returns>True when the unit can participate in an assault.</returns>
        private static bool IsActiveAssaultUnit(IManufacturable unit)
        {
            return unit.ManufacturingStatus == ManufacturingStatus.Complete
                && unit.Movement == null;
        }

        /// <summary>
        /// Determines whether a capital ship can supply assault troops.
        /// </summary>
        /// <param name="ship">Capital ship to inspect.</param>
        /// <returns>True when the ship is active and has remaining hull strength.</returns>
        private static bool IsActiveAssaultUnit(CapitalShip ship)
        {
            return IsActiveAssaultUnit((IManufacturable)ship) && ship.CurrentHullStrength > 0;
        }

        /// <summary>
        /// Determines whether a building participates in assault defense fire.
        /// </summary>
        /// <param name="building">Building to inspect.</param>
        /// <returns>True when the building is a planetary defense facility.</returns>
        private static bool IsAssaultDefenseFacility(Building building)
        {
            return building.DefenseFacilityClass
                is DefenseFacilityClass.KDY
                    or DefenseFacilityClass.LNR
                    or DefenseFacilityClass.Shield
                    or DefenseFacilityClass.DeathStarShield;
        }

        private class AssaultTroop
        {
            public Regiment Regiment;
            public CapitalShip Ship;
        }

        private class CollateralTarget
        {
            public CollateralTargetType Type;
            public IGameEntity Entity;
        }

        private enum CollateralTargetType
        {
            Building,
            EnergyCapacity,
            AllocatedEnergy,
        }
    }
}
