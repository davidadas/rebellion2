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

            if (IsBlockedByShields(defendingPlanet))
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
                return result;
            }
            finally
            {
                SetAssaultCombatState(attackingFleets, defendingPlanet, false);
            }
        }

        /// <summary>
        /// Determines whether the supplied fleets can begin an assault at the planet.
        /// </summary>
        /// <param name="fleets">Fleets attempting the assault.</param>
        /// <param name="planet">Planet being assaulted.</param>
        /// <returns>True when every fleet is stationary, colocated, and owned by one faction.</returns>
        private static bool CanAssault(List<Fleet> fleets, Planet planet)
        {
            if (planet == null || fleets?.Any() != true || fleets.Any(fleet => fleet == null))
                return false;

            string ownerId = fleets[0].GetOwnerInstanceID();
            return !string.IsNullOrEmpty(ownerId)
                && fleets.All(fleet =>
                    fleet.GetOwnerInstanceID() == ownerId
                    && fleet.Movement == null
                    && fleet.GetParent() == planet
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

            foreach (Fleet fleet in planet.Fleets)
                fleet.IsInCombat = isInCombat;
        }

        /// <summary>
        /// Determines whether active planetary shields prevent an assault.
        /// </summary>
        /// <param name="planet">Planet whose shield facilities are evaluated.</param>
        /// <returns>True when the active shield count meets the configured limit.</returns>
        private bool IsBlockedByShields(Planet planet)
        {
            int activeShieldCount = planet
                .GetAllBuildings()
                .Count(building =>
                    IsActiveAssaultUnit(building)
                    && building.DefenseFacilityClass == DefenseFacilityClass.Shield
                );
            return activeShieldCount >= _game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit;
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
                _game.DetachNode(target);
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
                    _game.DetachNode(attacker.Regiment);
                }
                else if (score >= config.AttackerWinsMinimum)
                {
                    result.DestroyedDefenderRegiments.Add(defender);
                    _game.DetachNode(defender);
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
            int attackerLeadership = GetAssaultLeadership(
                fleet?.GetOfficers(),
                OfficerRank.General,
                fleet?.GetOwnerInstanceID()
            );
            int defenderLeadership = GetAssaultLeadership(
                planet.GetAllOfficers(),
                OfficerRank.General,
                planet.GetOwnerInstanceID()
            );
            int attackerBonus = attackerLeadership / config.GeneralLeadershipDivisor;
            int defenderBonus = defenderLeadership / config.GeneralLeadershipDivisor;
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
                .Where(IsActiveAssaultUnit)
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
                    _game.DetachNode(building);
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
                .SelectMany(fleet => fleet.CapitalShips)
                .Where(IsActiveAssaultUnit)
                .SelectMany(ship =>
                    ship.Regiments.Where(IsActiveAssaultUnit)
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
        /// <returns>The commander's leadership rating, or zero when none is eligible.</returns>
        private static int GetAssaultLeadership(
            IEnumerable<Officer> officers,
            OfficerRank rank,
            string ownerId
        )
        {
            Officer commander = officers?.FirstOrDefault(officer =>
                officer.CurrentRank == rank
                && officer.GetOwnerInstanceID() == ownerId
                && !officer.IsKilled
            );
            return commander?.GetEffectiveRating(OfficerRating.Leadership) ?? 0;
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
