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
    internal class PlanetaryAssaultResolver
    {
        private readonly GameRoot _game;
        private readonly IRandomNumberProvider _provider;
        private readonly PlanetaryControlSystem _ownership;

        public PlanetaryAssaultResolver(
            GameRoot game,
            IRandomNumberProvider provider,
            PlanetaryControlSystem ownership
        )
        {
            _game = game;
            _provider = provider;
            _ownership = ownership;
        }

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

            SetCombatState(attackingFleets, defendingPlanet, true);
            try
            {
                ResolveDefenseFire(defendingPlanet, attackers, result);
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
                SetCombatState(attackingFleets, defendingPlanet, false);
            }
        }

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

        private static void SetCombatState(List<Fleet> attackers, Planet planet, bool isInCombat)
        {
            foreach (Fleet fleet in attackers)
                fleet.IsInCombat = isInCombat;

            foreach (Fleet fleet in planet.Fleets)
                fleet.IsInCombat = isInCombat;
        }

        private bool IsBlockedByShields(Planet planet)
        {
            int activeShieldCount = planet
                .GetAllBuildings()
                .Count(building =>
                    IsActive(building)
                    && building.DefenseFacilityClass == DefenseFacilityClass.Shield
                );
            return activeShieldCount >= _game.Config.Combat.AssaultShieldGeneratorLimit;
        }

        private void ResolveDefenseFire(
            Planet planet,
            List<AssaultTroop> attackers,
            PlanetaryAssaultResult result
        )
        {
            int initialAttackerCount = attackers.Count;
            int divisor = _game.Config.Combat.AssaultDefenseFireDivisor;

            foreach (
                Building facility in planet
                    .GetAllBuildings()
                    .Where(building => IsActive(building) && IsDefenseFacility(building))
            )
            {
                if (GetSurvivingAttackers(attackers).Count == 0)
                    break;

                int chance = facility.WeaponPower / divisor;
                if (!RollPercent(chance))
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
                GameConfig.CombatConfig config = _game.Config.Combat;

                if (score <= config.AssaultDefenderWinsMaximum)
                {
                    result.DestroyedAttackerRegiments.Add(attacker.Regiment);
                    _game.DetachNode(attacker.Regiment);
                }
                else if (score >= config.AssaultAttackerWinsMinimum)
                {
                    result.DestroyedDefenderRegiments.Add(defender);
                    _game.DetachNode(defender);
                }
            }

            return actualDuels;
        }

        private int CalculateContestScore(AssaultTroop attacker, Regiment defender, Planet planet)
        {
            GameConfig.CombatConfig config = _game.Config.Combat;
            Fleet fleet = attacker.Ship.GetParentOfType<Fleet>();
            int attackerLeadership = GetLeadership(
                fleet?.GetOfficers(),
                OfficerRank.General,
                fleet?.GetOwnerInstanceID()
            );
            int defenderLeadership = GetLeadership(
                planet.GetAllOfficers(),
                OfficerRank.General,
                planet.GetOwnerInstanceID()
            );
            int attackerBonus = attackerLeadership / config.AssaultGeneralLeadershipDivisor;
            int defenderBonus = defenderLeadership / config.AssaultGeneralLeadershipDivisor;
            int roll = _provider.NextInt(0, config.AssaultContestRollMaximum + 1);
            return roll
                + attacker.Regiment.AttackRating
                + attackerBonus
                - defender.DefenseRating
                - defenderBonus;
        }

        private void ResolveCollateralDamage(
            Planet planet,
            int trialCount,
            PlanetaryAssaultResult result
        )
        {
            int successfulTrials = 0;
            for (int trial = 0; trial < trialCount; trial++)
            {
                if (RollPercent(_game.Config.Combat.AssaultCollateralDamagePercent))
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

        private static List<CollateralTarget> BuildCollateralTargets(Planet planet)
        {
            List<CollateralTarget> targets = planet
                .GetAllBuildings()
                .Where(IsActive)
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
            int garrisonRequirement = _game.Config.Combat.AssaultCaptureGarrisonCount;

            foreach (AssaultTroop assaultTroop in survivingAttackers.Take(garrisonRequirement))
            {
                _game.MoveNode(assaultTroop.Regiment, planet);
                result.LandedRegiments.Add(assaultTroop.Regiment);
            }

            result.Success = true;
        }

        private static List<AssaultTroop> SnapshotAttackers(IEnumerable<Fleet> fleets)
        {
            return fleets
                .SelectMany(fleet => fleet.CapitalShips)
                .Where(IsActive)
                .SelectMany(ship =>
                    ship.Regiments.Where(IsActive)
                        .Select(regiment => new AssaultTroop { Regiment = regiment, Ship = ship })
                )
                .ToList();
        }

        private static List<Regiment> GetActiveDefenders(Planet planet, string defenderId)
        {
            return planet
                .GetAllRegiments()
                .Where(regiment =>
                    IsActive(regiment) && regiment.GetOwnerInstanceID() == defenderId
                )
                .ToList();
        }

        private static List<AssaultTroop> GetSurvivingAttackers(IEnumerable<AssaultTroop> attackers)
        {
            return attackers.Where(attacker => attacker.Regiment.GetParent() != null).ToList();
        }

        private static List<Regiment> GetSurvivingDefenders(IEnumerable<Regiment> defenders)
        {
            return defenders.Where(defender => defender.GetParent() != null).ToList();
        }

        private bool RollPercent(int chance)
        {
            return _provider.NextInt(0, 100) < chance;
        }

        private static int GetLeadership(
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

        private static bool IsActive(IManufacturable unit)
        {
            return unit.ManufacturingStatus == ManufacturingStatus.Complete
                && unit.Movement == null;
        }

        private static bool IsActive(CapitalShip ship)
        {
            return IsActive((IManufacturable)ship) && ship.CurrentHullStrength > 0;
        }

        private static bool IsDefenseFacility(Building building)
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
