using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Systems;

namespace Rebellion.AI.Planners
{
    /// <summary>
    /// Builds production demand from faction state and current force needs.
    /// </summary>
    public sealed class AIProductionDemandGenerator
    {
        /// <summary>
        /// Returns production demand for the current AI turn.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Production demand generated for this faction.</returns>
        public List<AIProductionDemand> Generate(AITurnContext context)
        {
            List<AIProductionDemand> demands = new List<AIProductionDemand>();

            if (context?.Game == null || context.Faction == null || context.Assessment == null)
                return demands;

            AddResourceBalanceDemand(context, demands);
            AddPlanetaryDefenseDemands(context, demands);
            AddFleetSeedDemand(context, demands);
            AddFleetReinforcementDemands(context, demands);
            AddPlanetaryGarrisonDemands(context, demands);
            AddSpecialForcesDemands(context, demands);
            AddProductionFacilityDemands(context, demands);
            AddProductionFacilityUpgradeDemands(context, demands);

            return demands;
        }

        private void AddPlanetaryDefenseDemands(
            AITurnContext context,
            List<AIProductionDemand> demands
        )
        {
            foreach (
                Planet planet in context
                    .Assessment.OwnedPlanets.Where(IsOwnedUsablePlanet)
                    .OrderByDescending(context.Assessment.GetPlanetValue)
                    .ThenBy(planet => planet.InstanceID, StringComparer.Ordinal)
            )
                AddPlanetaryDefenseDemands(context, demands, planet);
        }

        private void AddPlanetaryDefenseDemands(
            AITurnContext context,
            List<AIProductionDemand> demands,
            Planet planet
        )
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            int availableEnergy = planet.GetAvailableEnergy();
            int shieldTarget = context.Game.Config.Combat.PlanetaryAssault.ShieldGeneratorLimit;
            int shieldCount = planet
                .GetAllBuildings()
                .Count(building =>
                    building.GetOwnerInstanceID() == context.Faction.InstanceID
                    && building.DefenseFacilityClass == DefenseFacilityClass.Shield
                );
            int shieldDeficit = System.Math.Max(0, shieldTarget - shieldCount);
            int shieldQuantity = System.Math.Min(shieldDeficit, availableEnergy);

            if (shieldQuantity > 0)
            {
                demands.Add(
                    CreatePlanetaryDefenseBuildingDemand(
                        context,
                        planet,
                        BuildingType.Defense,
                        shieldQuantity,
                        shieldTarget,
                        config.PlanetaryShieldDemandPercent
                    )
                );
                availableEnergy -= shieldQuantity;
            }

            int weaponCount = planet
                .GetAllBuildings()
                .Count(building =>
                    building.GetOwnerInstanceID() == context.Faction.InstanceID
                    && building.GetBuildingType() == BuildingType.Weapon
                );
            int weaponTarget = System.Math.Max(
                config.PlanetaryWeaponTargetCount,
                weaponCount + config.PlanetaryDefenseSurplusBatchSize
            );
            int weaponDeficit = weaponTarget - weaponCount;
            if (availableEnergy <= 0)
                return;

            demands.Add(
                CreatePlanetaryDefenseBuildingDemand(
                    context,
                    planet,
                    BuildingType.Weapon,
                    System.Math.Min(weaponDeficit, availableEnergy),
                    weaponTarget,
                    config.PlanetaryWeaponDemandPercent
                )
            );
        }

        private AIProductionDemand CreatePlanetaryDefenseBuildingDemand(
            AITurnContext context,
            Planet planet,
            BuildingType buildingType,
            int deficit,
            int targetCount,
            int baseDemandPercent
        )
        {
            return new AIProductionDemand(
                $"production:{context.Faction.InstanceID}:{AIProductionDemandKind.PlanetaryDefense}:{buildingType}:{planet.InstanceID}",
                AIProductionDemandKind.PlanetaryDefense,
                ManufacturingType.Building,
                buildingType,
                planet,
                deficit,
                GetPlanetaryDefensePressure(
                    context,
                    planet,
                    baseDemandPercent,
                    deficit,
                    targetCount
                )
            );
        }

        private void AddFleetSeedDemand(AITurnContext context, List<AIProductionDemand> demands)
        {
            int targetCount = GetTargetBattleFleetCount(context);
            int committedCount = context.Assessment.OwnedFleets.Count(IsCommittedBattleFleet);
            int maximumCount = context.Game.Config.AI.FleetDeployment.MaximumBattleFleetCount;
            if (committedCount >= maximumCount)
                return;

            int deficit = targetCount - committedCount;
            Planet unguardedHeadquarters = FindUnguardedHeadquarters(context);
            if (deficit <= 0 && unguardedHeadquarters == null)
                return;

            Planet destination = unguardedHeadquarters ?? FindFleetAssemblyPlanet(context);
            if (destination == null)
                return;

            int quantityNeeded = System.Math.Min(
                maximumCount - committedCount,
                System.Math.Max(1, deficit)
            );

            demands.Add(
                new AIProductionDemand(
                    $"production:{context.Faction.InstanceID}:FleetSeedCapitalShip",
                    AIProductionDemandKind.FleetSeedCapitalShip,
                    ManufacturingType.Ship,
                    BuildingType.None,
                    destination,
                    quantityNeeded,
                    GetDemandPressure(
                        context,
                        AIProductionDemandKind.FleetSeedCapitalShip,
                        quantityNeeded,
                        System.Math.Max(1, targetCount),
                        context.Game.Config.AI.Infrastructure.FleetSeedCapitalShipDemandPercent
                    ),
                    capitalShipRole: AICapitalShipProductionRole.General
                )
            );
        }

        private int GetTargetBattleFleetCount(AITurnContext context)
        {
            GameConfig.AIFleetDeploymentConfig config = context.Game.Config.AI.FleetDeployment;
            int operationalPlanetCount = context.Assessment.OwnedPlanets.Count(planet =>
                planet.IsColonized && !planet.IsDestroyed
            );
            int scaledTarget = CeilingDivide(operationalPlanetCount, config.PlanetsPerBattleFleet);
            int strategicRoleTarget =
                config.MaximumConcurrentAttackOrders
                + config.MaximumConcurrentColonizationOrders
                + (
                    context.Assessment.OwnedPlanets.Any(context.Assessment.IsFactionHeadquarters)
                        ? 1
                        : 0
                );

            return System.Math.Min(
                config.MaximumBattleFleetCount,
                System.Math.Max(
                    config.MinimumBattleFleetCount,
                    System.Math.Max(scaledTarget, strategicRoleTarget)
                )
            );
        }

        private Planet FindUnguardedHeadquarters(AITurnContext context)
        {
            return context
                .Assessment.OwnedPlanets.Where(planet =>
                    context.Assessment.IsFactionHeadquarters(planet)
                    && planet.IsColonized
                    && !planet.IsDestroyed
                    && !context.Assessment.HasCommittedHeadquartersFleet(planet)
                )
                .OrderByDescending(context.Assessment.GetPlanetValue)
                .ThenBy(planet => planet.InstanceID, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        private Planet FindFleetAssemblyPlanet(AITurnContext context)
        {
            return context
                .Assessment.OwnedPlanets.Where(planet => planet.IsColonized && !planet.IsDestroyed)
                .OrderByDescending(context.Assessment.IsFactionHeadquarters)
                .ThenByDescending(planet => planet.GetProductionRate(ManufacturingType.Ship))
                .ThenByDescending(context.Assessment.GetPlanetValue)
                .ThenBy(planet => planet.InstanceID, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        private static bool IsCommittedBattleFleet(Fleet fleet)
        {
            return fleet?.RoleType == FleetRoleType.Battle
                && fleet.CapitalShips.Any(ship =>
                    ship.ManufacturingStatus
                        is ManufacturingStatus.Complete
                            or ManufacturingStatus.Building
                );
        }

        private void AddSpecialForcesDemands(
            AITurnContext context,
            List<AIProductionDemand> demands
        )
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            List<SpecialForces> existingUnits =
                context.Faction.GetOwnedUnitsByType<SpecialForces>();

            foreach (
                SpecialForces template in context
                    .Faction.GetUnlockedTechnologies(ManufacturingType.Troop)
                    .Select(technology => technology.GetReference())
                    .OfType<SpecialForces>()
                    .OrderBy(template => template.GetTypeID(), StringComparer.Ordinal)
            )
            {
                int currentCount = existingUnits.Count(unit =>
                    unit.GetTypeID() == template.GetTypeID()
                );
                int deficit = config.SpecialForcesTargetCountPerType - currentCount;
                if (deficit <= 0)
                    continue;

                Planet destination = FindSpecialForcesDestination(context);
                if (destination == null)
                    return;

                demands.Add(
                    new AIProductionDemand(
                        $"production:{context.Faction.InstanceID}:SpecialForces:{template.GetTypeID()}",
                        AIProductionDemandKind.SpecialForces,
                        ManufacturingType.Troop,
                        BuildingType.None,
                        destination,
                        deficit,
                        GetDemandPressure(
                            context,
                            AIProductionDemandKind.SpecialForces,
                            deficit,
                            config.SpecialForcesTargetCountPerType,
                            config.SpecialForcesDemandPercent
                        ),
                        template.GetTypeID()
                    )
                );
            }
        }

        private Planet FindSpecialForcesDestination(AITurnContext context)
        {
            return context
                .Assessment.OwnedPlanets.Where(planet => planet.IsColonized && !planet.IsDestroyed)
                .OrderBy(planet => planet.SpecialForces.Count)
                .ThenByDescending(planet => planet.GetProductionRate(ManufacturingType.Troop))
                .ThenByDescending(context.Assessment.GetPlanetValue)
                .ThenBy(planet => planet.InstanceID, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        private void AddProductionFacilityDemands(
            AITurnContext context,
            List<AIProductionDemand> demands
        )
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            AddProductionFacilityDemand(
                context,
                demands,
                ManufacturingType.Ship,
                AIProductionDemandKind.Shipyard,
                BuildingType.Shipyard,
                config.ShipyardDemandPercent
            );
            AddProductionFacilityDemand(
                context,
                demands,
                ManufacturingType.Troop,
                AIProductionDemandKind.TrainingFacility,
                BuildingType.TrainingFacility,
                config.TrainingFacilityDemandPercent
            );
            AddProductionFacilityDemand(
                context,
                demands,
                ManufacturingType.Building,
                AIProductionDemandKind.ConstructionFacility,
                BuildingType.ConstructionFacility,
                config.ConstructionFacilityDemandPercent
            );
        }

        private void AddProductionFacilityUpgradeDemands(
            AITurnContext context,
            List<AIProductionDemand> demands
        )
        {
            foreach (
                Planet planet in context
                    .Assessment.OwnedPlanets.Where(IsOwnedUsablePlanet)
                    .OrderByDescending(context.Assessment.GetPlanetValue)
                    .ThenBy(planet => planet.InstanceID, StringComparer.Ordinal)
            )
            {
                AddProductionFacilityUpgradeDemand(
                    context,
                    demands,
                    planet,
                    BuildingType.ConstructionFacility
                );
                AddProductionFacilityUpgradeDemand(context, demands, planet, BuildingType.Shipyard);
                AddProductionFacilityUpgradeDemand(
                    context,
                    demands,
                    planet,
                    BuildingType.TrainingFacility
                );
            }
        }

        private void AddProductionFacilityUpgradeDemand(
            AITurnContext context,
            List<AIProductionDemand> demands,
            Planet planet,
            BuildingType buildingType
        )
        {
            if (HasPendingFacility(context, planet, buildingType))
                return;

            List<Building> activeFacilities = planet
                .GetAllBuildings()
                .Where(building =>
                    building.GetOwnerInstanceID() == context.Faction.InstanceID
                    && building.GetBuildingType() == buildingType
                    && building.GetManufacturingStatus() == ManufacturingStatus.Complete
                    && building.Movement == null
                    && building.GetProcessRate() > 0
                )
                .ToList();
            if (
                activeFacilities.Count
                <= context
                    .Game
                    .Config
                    .AI
                    .Infrastructure
                    .ProductionFacilityUpgradeMinimumRemainingCount
            )
                return;

            List<Building> unlockedFacilities = context
                .Faction.GetUnlockedTechnologies(ManufacturingType.Building)
                .Select(technology => technology.GetReference())
                .OfType<Building>()
                .Where(building =>
                    building.GetBuildingType() == buildingType
                    && building.HasAllowedOwnerInstanceID(context.Faction.InstanceID)
                )
                .ToList();
            Building replacement = activeFacilities
                .Where(current =>
                    unlockedFacilities.Any(candidate => candidate.IsProductionUpgradeFor(current))
                )
                .OrderByDescending(building => building.GetProcessRate())
                .ThenBy(building => building.ResearchOrder)
                .ThenBy(building => building.InstanceID, StringComparer.Ordinal)
                .FirstOrDefault();
            if (replacement == null)
                return;

            AIProductionDemand demand = new AIProductionDemand(
                $"production:{context.Faction.InstanceID}:{AIProductionDemandKind.BuildingUpgrade}:{buildingType}:{planet.InstanceID}:{replacement.InstanceID}",
                AIProductionDemandKind.BuildingUpgrade,
                ManufacturingType.Building,
                buildingType,
                planet,
                1,
                GetProductionFacilityUpgradePressure(context, planet)
            );
            demand.ReplacementBuilding = replacement;
            demands.Add(demand);
        }

        private double GetProductionFacilityUpgradePressure(AITurnContext context, Planet planet)
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            double pressure = config.ProductionFacilityUpgradeDemandPercent;
            double highestPlanetValue = context.Assessment.GetHighestOwnedPlanetValue();
            if (highestPlanetValue > 0)
            {
                pressure +=
                    config.ProductionFacilityUpgradeValuePressureWeight
                    * context.Assessment.GetPlanetValue(planet)
                    / highestPlanetValue;
            }

            if (context.Assessment.IsFactionHeadquarters(planet))
                pressure += config.ProductionFacilityUpgradeHeadquartersPressureBonus;

            return ClampPressure(pressure);
        }

        private void AddProductionFacilityDemand(
            AITurnContext context,
            List<AIProductionDemand> demands,
            ManufacturingType manufacturingType,
            AIProductionDemandKind kind,
            BuildingType buildingType,
            int baseDemandPercent
        )
        {
            AIProductionDemand primaryDemand = demands
                .Where(demand => demand.ManufacturingType == manufacturingType)
                .Where(demand => demand.Kind != kind)
                .OrderByDescending(demand => demand.Pressure)
                .ThenBy(demand => demand.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (primaryDemand == null)
                return;

            Planet target = FindFacilityTargetPlanet(context, primaryDemand, manufacturingType);
            if (
                target == null
                || !NeedsProductionFacility(context, demands, manufacturingType)
                || HasPendingFacility(context, target, buildingType)
            )
                return;

            int currentCount = GetOwnedFacilityCount(context, buildingType);
            demands.Add(
                new AIProductionDemand(
                    $"production:{context.Faction.InstanceID}:{kind}:{target.InstanceID}",
                    kind,
                    ManufacturingType.Building,
                    buildingType,
                    target,
                    1,
                    GetProductionFacilityPressure(context, kind, currentCount, baseDemandPercent),
                    primaryDemand.ProductTypeId,
                    primaryDemand.CapitalShipRole
                )
            );
        }

        private double GetProductionFacilityPressure(
            AITurnContext context,
            AIProductionDemandKind kind,
            int currentCount,
            int baseDemandPercent
        )
        {
            double pressure = GetDemandPressure(
                context,
                kind,
                1,
                currentCount + 1,
                baseDemandPercent
            );
            return kind == AIProductionDemandKind.TrainingFacility
                ? pressure
                    + context.Game.Config.AI.Infrastructure.TrainingFacilityBacklogPressureBonus
                : pressure;
        }

        private bool NeedsProductionFacility(
            AITurnContext context,
            IReadOnlyCollection<AIProductionDemand> demands,
            ManufacturingType manufacturingType
        )
        {
            int demandLaneCount = demands.Count(demand =>
                demand.ManufacturingType == manufacturingType && demand.QuantityNeeded > 0
            );
            return demandLaneCount
                > context.Assessment.GetAvailableProductionLaneCount(manufacturingType);
        }

        private bool HasPendingFacility(
            AITurnContext context,
            Planet target,
            BuildingType buildingType
        )
        {
            return target
                .GetAllBuildings()
                .Any(building =>
                    building.GetOwnerInstanceID() == context.Faction.InstanceID
                    && building.GetBuildingType() == buildingType
                    && (
                        building.GetManufacturingStatus() != ManufacturingStatus.Complete
                        || building.Movement != null
                    )
                );
        }

        private void AddPlanetaryGarrisonDemands(
            AITurnContext context,
            List<AIProductionDemand> demands
        )
        {
            foreach (Planet planet in context.Assessment.OwnedPlanets.Where(IsOwnedUsablePlanet))
                AddGarrisonRegimentReserveDemand(context, demands, planet);
        }

        /// <summary>
        /// Adds local garrison regiment demand for a planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        /// <param name="planet">The planet to inspect.</param>
        private void AddGarrisonRegimentReserveDemand(
            AITurnContext context,
            List<AIProductionDemand> demands,
            Planet planet
        )
        {
            int minimumTargetCount = GetTargetGarrisonRegimentReserveCount(context, planet);
            int currentCount = planet
                .GetAllRegiments()
                .Count(regiment => regiment.GetOwnerInstanceID() == context.Faction.InstanceID);
            int deficit = minimumTargetCount - currentCount;
            if (deficit <= 0)
                return;

            demands.Add(
                new AIProductionDemand(
                    $"production:{context.Faction.InstanceID}:{AIProductionDemandKind.GarrisonRegimentReserve}:{planet.InstanceID}",
                    AIProductionDemandKind.GarrisonRegimentReserve,
                    ManufacturingType.Troop,
                    BuildingType.None,
                    planet,
                    deficit,
                    GetPlanetaryDefensePressure(
                        context,
                        planet,
                        context.Game.Config.AI.Infrastructure.PlanetaryGarrisonDemandPercent,
                        deficit,
                        minimumTargetCount
                    )
                )
            );
        }

        /// <summary>
        /// Adds reinforcement demand for owned fleets.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        private void AddFleetReinforcementDemands(
            AITurnContext context,
            List<AIProductionDemand> demands
        )
        {
            foreach (Fleet fleet in GetPriorityReinforcementFleets(context))
            {
                AddFleetCapitalShipDemand(context, demands, fleet);
                AddFleetStarfighterDemand(context, demands, fleet);
                AddFleetRegimentDemand(context, demands, fleet);
            }
        }

        private IReadOnlyList<Fleet> GetPriorityReinforcementFleets(AITurnContext context)
        {
            List<Fleet> fleets = new List<Fleet>();
            AddPriorityFleet(fleets, GetPriorityDefenseFleet(context));

            Fleet attackFleet = GetPrimaryAttackFleet(context);
            AddPriorityFleet(fleets, attackFleet);
            AddPriorityFleet(fleets, GetPrimaryColonizationFleet(context));

            if (attackFleet == null)
                AddPriorityFleet(fleets, GetPrimaryFleetAssemblyFleet(context));

            return fleets;
        }

        private void AddPriorityFleet(List<Fleet> fleets, Fleet fleet)
        {
            if (fleet != null && fleets.All(candidate => candidate.InstanceID != fleet.InstanceID))
                fleets.Add(fleet);
        }

        private Fleet GetPriorityDefenseFleet(AITurnContext context)
        {
            return context
                .Assessment.OwnedFleets.Where(CanReinforceFleet)
                .Select(fleet => new { Fleet = fleet, Target = GetDefenseTarget(context, fleet) })
                .Where(candidate => candidate.Target != null)
                .OrderByDescending(candidate =>
                    context.Assessment.GetRequiredDefenseStrength(candidate.Target)
                    - context.Assessment.GetProjectedFleetCombatValue(candidate.Fleet)
                )
                .ThenBy(candidate => candidate.Fleet.InstanceID, StringComparer.Ordinal)
                .Select(candidate => candidate.Fleet)
                .FirstOrDefault();
        }

        private Fleet GetPrimaryAttackFleet(AITurnContext context)
        {
            return context
                .Assessment.AttackOrderedFleets.Where(CanReinforceFleet)
                .Select(fleet => new
                {
                    Fleet = fleet,
                    Target = GetAttackTargetPlanet(context, fleet),
                })
                .OrderByDescending(candidate => candidate.Target != null)
                .ThenByDescending(candidate =>
                    context.Assessment.GetOwnedSystemPresenceRatio(
                        context.Assessment.GetPlanetSystemId(candidate.Target)
                    )
                )
                .ThenByDescending(candidate => candidate.Target?.IsHeadquarters == true)
                .ThenByDescending(candidate =>
                    context.Assessment.GetFleetAttackCampaignReadinessGateCount(
                        candidate.Fleet,
                        candidate.Target
                    )
                )
                .ThenByDescending(candidate => context.Assessment.GetPlanetValue(candidate.Target))
                .ThenBy(candidate => candidate.Fleet.InstanceID, StringComparer.Ordinal)
                .Select(candidate => candidate.Fleet)
                .FirstOrDefault();
        }

        private Fleet GetPrimaryColonizationFleet(AITurnContext context)
        {
            return context
                .Assessment.ColonizationOrderedFleets.Where(CanReinforceFleet)
                .OrderByDescending(fleet => fleet.GetCurrentRegimentCount())
                .ThenByDescending(fleet => fleet.GetRegimentCapacity())
                .ThenBy(fleet => fleet.InstanceID, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        private Fleet GetPrimaryFleetAssemblyFleet(AITurnContext context)
        {
            return context
                .Assessment.OwnedFleets.Where(fleet =>
                    CanReinforceFleet(fleet)
                    && fleet.Order == null
                    && context.Assessment.CanFleetDepartHeadquarters(fleet)
                )
                .OrderByDescending(context.Assessment.GetProjectedFleetCombatValue)
                .ThenByDescending(fleet => fleet.GetRegimentCapacity())
                .ThenBy(fleet => fleet.InstanceID, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        /// <summary>
        /// Adds capital ship demand for a fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        /// <param name="fleet">The fleet to inspect.</param>
        private void AddFleetCapitalShipDemand(
            AITurnContext context,
            List<AIProductionDemand> demands,
            Fleet fleet
        )
        {
            Planet targetPlanet = GetAttackTargetPlanet(context, fleet);
            bool isColonizationOrder = fleet.Order?.OrderType == FleetOrderType.Colonize;
            Planet defenseTarget = GetDefenseTarget(context, fleet);
            bool isDefenseOrder = fleet.Order?.OrderType == FleetOrderType.Defend;
            if (
                targetPlanet == null
                && fleet.Order != null
                && !isColonizationOrder
                && defenseTarget == null
            )
                return;

            int projectedCombat = context.Assessment.GetProjectedFleetCombatValue(fleet);
            int targetCombat =
                targetPlanet != null
                    ? context.Assessment.GetRequiredAttackCampaignCombatStrength(targetPlanet)
                : defenseTarget != null
                    ? context.Assessment.GetRequiredDefenseStrength(defenseTarget)
                : isColonizationOrder ? projectedCombat
                : context.Game.Config.AI.FleetDeployment.MinimumAttackStrength;
            int combatDeficit = targetCombat - projectedCombat;
            int targetRegimentCapacity =
                isDefenseOrder ? 0
                : targetPlanet == null
                    ? context.Game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount
                : GetDesiredRegimentCount(context, fleet);
            int regimentCapacityDeficit = targetRegimentCapacity - fleet.GetRegimentCapacity();
            int projectedBombardment = context.Assessment.GetProjectedFleetBombardmentStrength(
                fleet
            );
            int targetBombardment =
                targetPlanet == null
                    ? 0
                    : context.Assessment.GetRequiredAttackCampaignBombardmentStrength(targetPlanet);
            int bombardmentDeficit = targetBombardment - projectedBombardment;
            AICapitalShipProductionRole capitalShipRole;
            int deficit;
            int target;
            if (regimentCapacityDeficit > 0)
            {
                capitalShipRole = AICapitalShipProductionRole.TroopTransport;
                deficit = regimentCapacityDeficit;
                target = targetRegimentCapacity;
            }
            else if (bombardmentDeficit > 0)
            {
                capitalShipRole = AICapitalShipProductionRole.Bombardment;
                deficit = bombardmentDeficit;
                target = targetBombardment;
            }
            else if (combatDeficit > 0)
            {
                capitalShipRole = AICapitalShipProductionRole.General;
                deficit = combatDeficit;
                target = targetCombat;
            }
            else
            {
                return;
            }

            demands.Add(
                CreateFleetDemand(
                    context,
                    AIProductionDemandKind.FleetCapitalShip,
                    ManufacturingType.Ship,
                    fleet,
                    deficit,
                    target,
                    context.Game.Config.AI.Infrastructure.FleetCapitalShipDemandPercent,
                    capitalShipRole
                )
            );
        }

        /// <summary>
        /// Adds starfighter demand for a fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        /// <param name="fleet">The fleet to inspect.</param>
        private void AddFleetStarfighterDemand(
            AITurnContext context,
            List<AIProductionDemand> demands,
            Fleet fleet
        )
        {
            int targetCount = GetTargetStarfighterCount(context, fleet);
            int deficit = targetCount - fleet.GetCurrentStarfighterCount();
            if (deficit <= 0)
                return;

            demands.Add(
                CreateFleetDemand(
                    context,
                    AIProductionDemandKind.FleetStarfighter,
                    ManufacturingType.Ship,
                    fleet,
                    deficit,
                    targetCount,
                    context.Game.Config.AI.Infrastructure.FleetStarfighterDemandPercent
                )
            );
        }

        /// <summary>
        /// Adds regiment demand for a fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        /// <param name="fleet">The fleet to inspect.</param>
        private void AddFleetRegimentDemand(
            AITurnContext context,
            List<AIProductionDemand> demands,
            Fleet fleet
        )
        {
            int targetCount = System.Math.Min(
                fleet.GetRegimentCapacity(),
                GetDesiredRegimentCount(context, fleet)
            );
            int deficit = targetCount - fleet.GetCurrentRegimentCount();
            if (deficit <= 0)
                return;

            demands.Add(
                CreateFleetDemand(
                    context,
                    AIProductionDemandKind.FleetRegiment,
                    ManufacturingType.Troop,
                    fleet,
                    deficit,
                    targetCount,
                    context.Game.Config.AI.Infrastructure.FleetRegimentDemandPercent
                )
            );
        }

        /// <summary>
        /// Adds mine and refinery demands.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        private void AddResourceBalanceDemand(
            AITurnContext context,
            List<AIProductionDemand> demands
        )
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            int economyBatchSize = GetEconomyBatchSize(context, config);
            int rawResourceNodes = context.Faction.GetTotalRawResourceNodes();
            int plannedMines = context.Faction.GetTotalRawMinedResources();
            int plannedRefineries = context.Faction.GetTotalRawRefinementCapacity();
            int mineDeficit = GetMineDeficit(
                rawResourceNodes,
                plannedMines,
                plannedRefineries,
                economyBatchSize
            );
            int refineryDeficit = GetRefineryDeficit(
                plannedMines,
                plannedRefineries,
                mineDeficit,
                economyBatchSize
            );
            int economyDemandPercent = GetEconomyDemandPercent(
                rawResourceNodes,
                plannedMines,
                config
            );
            List<Planet> mineTargets = FindMineTargetPlanets(context, mineDeficit).ToList();
            HashSet<string> mineTargetIds = new HashSet<string>(
                mineTargets.Select(planet => planet.InstanceID),
                StringComparer.Ordinal
            );
            List<Planet> refineryTargets = FindRefineryTargetPlanets(
                    context,
                    refineryDeficit,
                    mineTargetIds
                )
                .ToList();

            foreach (Planet target in mineTargets)
            {
                demands.Add(
                    CreateBuildingDemand(
                        context,
                        AIProductionDemandKind.Mine,
                        BuildingType.Mine,
                        target,
                        mineDeficit,
                        plannedMines + mineDeficit,
                        economyDemandPercent
                    )
                );
            }

            foreach (Planet target in refineryTargets)
            {
                demands.Add(
                    CreateBuildingDemand(
                        context,
                        AIProductionDemandKind.Refinery,
                        BuildingType.Refinery,
                        target,
                        refineryDeficit,
                        plannedRefineries + refineryDeficit,
                        economyDemandPercent
                    )
                );
            }
        }

        /// <summary>
        /// Returns how many mine demands should be generated.
        /// </summary>
        /// <param name="rawResourceNodes">Known raw resource nodes.</param>
        /// <param name="plannedMines">Current and queued mine capacity.</param>
        /// <param name="plannedRefineries">Current and queued refinery capacity.</param>
        /// <param name="economyBatchSize">Maximum economy batch size.</param>
        /// <returns>The mine deficit.</returns>
        private int GetMineDeficit(
            int rawResourceNodes,
            int plannedMines,
            int plannedRefineries,
            int economyBatchSize
        )
        {
            if (rawResourceNodes <= plannedMines)
                return 0;

            if (plannedRefineries > plannedMines)
                return System.Math.Min(
                    economyBatchSize,
                    System.Math.Min(
                        plannedRefineries - plannedMines,
                        rawResourceNodes - plannedMines
                    )
                );

            if (plannedRefineries == plannedMines)
                return System.Math.Min(economyBatchSize, rawResourceNodes - plannedMines);

            return 0;
        }

        /// <summary>
        /// Returns how many refinery demands should be generated.
        /// </summary>
        /// <param name="plannedMines">Current and queued mine capacity.</param>
        /// <param name="plannedRefineries">Current and queued refinery capacity.</param>
        /// <param name="selectedMineDeficit">Mine demand selected for this pass.</param>
        /// <param name="economyBatchSize">Maximum economy batch size.</param>
        /// <returns>The refinery deficit.</returns>
        private int GetRefineryDeficit(
            int plannedMines,
            int plannedRefineries,
            int selectedMineDeficit,
            int economyBatchSize
        )
        {
            int desiredRefineries = plannedMines + selectedMineDeficit;
            if (desiredRefineries <= plannedRefineries)
                return 0;

            return System.Math.Min(economyBatchSize, desiredRefineries - plannedRefineries);
        }

        /// <summary>
        /// Returns the demand pressure for economy buildings.
        /// </summary>
        /// <param name="rawResourceNodes">Known raw resource nodes.</param>
        /// <param name="plannedMines">Current and queued mine capacity.</param>
        /// <param name="config">AI infrastructure configuration.</param>
        /// <returns>The economy demand pressure.</returns>
        private int GetEconomyDemandPercent(
            int rawResourceNodes,
            int plannedMines,
            GameConfig.AIInfrastructureConfig config
        )
        {
            if (rawResourceNodes <= 0)
                return config.EconomyDemandPercent;

            int minedCoveragePercent = plannedMines * 100 / rawResourceNodes;
            if (minedCoveragePercent <= config.EconomySevereDeficitPercent)
                return config.EconomySevereDemandPercent;

            return config.EconomyDemandPercent;
        }

        /// <summary>
        /// Returns how many economy demands may be generated this turn.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="config">AI infrastructure configuration.</param>
        /// <returns>The economy batch size.</returns>
        private int GetEconomyBatchSize(
            AITurnContext context,
            GameConfig.AIInfrastructureConfig config
        )
        {
            int availableBuildingLanes = context.Assessment.GetAvailableProductionLaneCount(
                ManufacturingType.Building
            );
            int economyLaneBudget = availableBuildingLanes - config.EconomyCompetingNeedSlotReserve;
            return System.Math.Max(
                config.EconomyDefaultBatchSize,
                System.Math.Max(0, economyLaneBudget)
            );
        }

        /// <summary>
        /// Creates a building production demand.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="kind">Demand kind.</param>
        /// <param name="buildingType">Building type requested.</param>
        /// <param name="target">Planet receiving the building.</param>
        /// <param name="deficit">Current deficit.</param>
        /// <param name="targetCount">Target count.</param>
        /// <param name="baseDemandPercent">Base pressure for the demand.</param>
        /// <returns>The production demand.</returns>
        private AIProductionDemand CreateBuildingDemand(
            AITurnContext context,
            AIProductionDemandKind kind,
            BuildingType buildingType,
            Planet target,
            int deficit,
            int targetCount,
            int baseDemandPercent
        )
        {
            return new AIProductionDemand(
                $"production:{context.Faction.InstanceID}:{kind}:{target.InstanceID}",
                kind,
                ManufacturingType.Building,
                buildingType,
                target,
                deficit,
                GetDemandPressure(context, kind, deficit, targetCount, baseDemandPercent)
            );
        }

        /// <summary>
        /// Creates a fleet unit production demand.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="kind">Demand kind.</param>
        /// <param name="manufacturingType">Manufacturing type required.</param>
        /// <param name="fleet">Fleet receiving the unit.</param>
        /// <param name="deficit">Current deficit.</param>
        /// <param name="targetCount">Target count.</param>
        /// <param name="baseDemandPercent">Base pressure for the demand.</param>
        /// <param name="capitalShipRole">Capital ship role required by the demand.</param>
        /// <returns>The production demand.</returns>
        private AIProductionDemand CreateFleetDemand(
            AITurnContext context,
            AIProductionDemandKind kind,
            ManufacturingType manufacturingType,
            Fleet fleet,
            int deficit,
            int targetCount,
            int baseDemandPercent,
            AICapitalShipProductionRole capitalShipRole = AICapitalShipProductionRole.None
        )
        {
            return new AIProductionDemand(
                $"production:{context.Faction.InstanceID}:{kind}:{fleet.InstanceID}",
                kind,
                manufacturingType,
                BuildingType.None,
                fleet,
                deficit,
                GetFleetDemandPressure(
                    context,
                    kind,
                    fleet,
                    deficit,
                    targetCount,
                    baseDemandPercent
                ),
                capitalShipRole: capitalShipRole
            );
        }

        /// <summary>
        /// Returns mine destination planets.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="count">Maximum number of planets to return.</param>
        /// <returns>Mine destination planets.</returns>
        private IEnumerable<Planet> FindMineTargetPlanets(AITurnContext context, int count)
        {
            if (count <= 0)
                return Enumerable.Empty<Planet>();

            return GetEconomyDestinationPlanets(context)
                .Where(planet => planet.GetUnminedResourceNodeCount() > 0)
                .OrderByDescending(planet => planet.GetUnminedResourceNodeCount())
                .ThenByDescending(planet => planet.GetAvailableEnergy())
                .ThenBy(planet => planet.InstanceID)
                .Take(count);
        }

        /// <summary>
        /// Returns refinery destination planets.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="count">Maximum number of planets to return.</param>
        /// <param name="excludedPlanetIds">Planet ids already selected for mine demand.</param>
        /// <returns>Refinery destination planets.</returns>
        private IEnumerable<Planet> FindRefineryTargetPlanets(
            AITurnContext context,
            int count,
            HashSet<string> excludedPlanetIds
        )
        {
            if (count <= 0)
                return Enumerable.Empty<Planet>();

            List<Planet> preferredTargets = GetEconomyDestinationPlanets(context)
                .Where(planet => !excludedPlanetIds.Contains(planet.InstanceID))
                .OrderBy(planet => planet.GetTotalBuildingTypeCount(BuildingType.Refinery))
                .ThenByDescending(planet => planet.GetAvailableEnergy())
                .ThenBy(planet => planet.InstanceID)
                .Take(count)
                .ToList();

            if (preferredTargets.Count >= count)
                return preferredTargets;

            preferredTargets.AddRange(
                GetEconomyDestinationPlanets(context)
                    .Where(planet => excludedPlanetIds.Contains(planet.InstanceID))
                    .OrderBy(planet => planet.GetTotalBuildingTypeCount(BuildingType.Refinery))
                    .ThenByDescending(planet => planet.GetAvailableEnergy())
                    .ThenBy(planet => planet.InstanceID)
                    .Take(count - preferredTargets.Count)
            );

            return preferredTargets;
        }

        private Planet FindFacilityTargetPlanet(
            AITurnContext context,
            AIProductionDemand primaryDemand,
            ManufacturingType manufacturingType
        )
        {
            Planet demandPlanet = GetDemandPlanet(context, primaryDemand);
            List<Planet> candidates = GetBuildingDestinationPlanets(context).ToList();
            Planet existingHub = candidates
                .Where(planet => planet.GetProductionFacilityCount(manufacturingType) > 0)
                .OrderByDescending(planet => GetAvailableFacilityExpansionEnergy(context, planet))
                .ThenByDescending(planet => planet.GetProductionRate(manufacturingType))
                .ThenBy(planet => demandPlanet == null ? 0 : demandPlanet.GetRawDistanceTo(planet))
                .ThenBy(planet => planet.InstanceID)
                .FirstOrDefault();
            if (existingHub != null)
                return existingHub;

            return candidates
                .OrderByDescending(planet => GetAvailableFacilityExpansionEnergy(context, planet))
                .ThenByDescending(context.Assessment.GetPlanetValue)
                .ThenBy(planet => demandPlanet == null ? 0 : demandPlanet.GetRawDistanceTo(planet))
                .ThenBy(planet => planet.InstanceID)
                .FirstOrDefault();
        }

        private int GetAvailableFacilityExpansionEnergy(AITurnContext context, Planet planet)
        {
            return System.Math.Max(
                0,
                planet.GetAvailableEnergy()
                    - context.Assessment.GetPlanetaryDefenseEnergyDeficit(planet)
            );
        }

        private Planet GetDemandPlanet(AITurnContext context, AIProductionDemand demand)
        {
            return demand?.DestinationPlanet
                ?? context.Assessment.GetFleetPlanet(demand?.DestinationFleet);
        }

        /// <summary>
        /// Returns planets that can receive buildings.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Building destination planets.</returns>
        private IEnumerable<Planet> GetBuildingDestinationPlanets(AITurnContext context)
        {
            return context.Assessment.OwnedPlanets.Where(planet =>
                IsOwnedUsablePlanet(planet)
                && planet.GetAvailableEnergy()
                    > context.Assessment.GetPlanetaryDefenseEnergyDeficit(planet)
            );
        }

        /// <summary>
        /// Returns planets that can receive economy buildings.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Economy destination planets.</returns>
        private IEnumerable<Planet> GetEconomyDestinationPlanets(AITurnContext context)
        {
            return context.Assessment.OwnedPlanets.Where(planet =>
                IsOwnedUsablePlanet(planet)
                && planet.GetAvailableEnergy()
                    > context.Assessment.GetPlanetaryDefenseEnergyDeficit(planet)
            );
        }

        /// <summary>
        /// Returns whether a planet is an owned usable colony.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>True if the planet is usable.</returns>
        private bool IsOwnedUsablePlanet(Planet planet)
        {
            return planet?.IsColonized == true && !planet.IsDestroyed;
        }

        /// <summary>
        /// Returns current and queued facility count for a building type.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="buildingType">Building type to count.</param>
        /// <returns>The owned facility count.</returns>
        private int GetOwnedFacilityCount(AITurnContext context, BuildingType buildingType)
        {
            return context.Assessment.OwnedPlanets.Sum(planet =>
                planet.GetTotalBuildingTypeCount(buildingType)
            );
        }

        /// <summary>
        /// Returns pressure for non-fleet production demand.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="kind">Demand kind.</param>
        /// <param name="deficit">Current deficit.</param>
        /// <param name="targetCount">Target count.</param>
        /// <param name="baseDemandPercent">Base pressure for the demand.</param>
        /// <returns>The demand pressure.</returns>
        private double GetDemandPressure(
            AITurnContext context,
            AIProductionDemandKind kind,
            int deficit,
            int targetCount,
            int baseDemandPercent
        )
        {
            double pressure = GetBasePressure(baseDemandPercent, deficit, targetCount);

            if (kind is AIProductionDemandKind.Mine or AIProductionDemandKind.Refinery)
                pressure += GetEconomyMaintenancePressure(context);

            return ClampPressure(pressure);
        }

        private double GetPlanetaryDefensePressure(
            AITurnContext context,
            Planet planet,
            int baseDemandPercent,
            int deficit,
            int targetCount
        )
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            double highestPlanetValue = context.Assessment.GetHighestOwnedPlanetValue();
            double pressure =
                baseDemandPercent
                + config.PlanetaryDefenseDeficitPressureWeight
                    * deficit
                    / System.Math.Max(1, targetCount);

            if (highestPlanetValue > 0)
            {
                pressure +=
                    config.PlanetaryDefenseValuePressureWeight
                    * context.Assessment.GetPlanetValue(planet)
                    / highestPlanetValue;
            }

            if (context.Assessment.IsFactionHeadquarters(planet))
                pressure += config.PlanetaryDefenseHeadquartersPressureBonus;

            if (context.Assessment.GetPlanetDefenseThreatStrength(planet) > 0)
                pressure += config.PlanetaryDefenseThreatPressureBonus;

            return ClampPressure(pressure);
        }

        /// <summary>
        /// Returns pressure for fleet production demand.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="kind">Demand kind.</param>
        /// <param name="fleet">Fleet receiving production.</param>
        /// <param name="deficit">Current deficit.</param>
        /// <param name="targetCount">Target count.</param>
        /// <param name="baseDemandPercent">Base pressure for the demand.</param>
        /// <returns>The fleet demand pressure.</returns>
        private double GetFleetDemandPressure(
            AITurnContext context,
            AIProductionDemandKind kind,
            Fleet fleet,
            int deficit,
            int targetCount,
            int baseDemandPercent
        )
        {
            double pressure = GetBasePressure(baseDemandPercent, deficit, targetCount);
            Planet targetPlanet = GetAttackTargetPlanet(context, fleet);

            if (targetPlanet != null)
            {
                pressure += GetTargetValuePressure(context, targetPlanet);
                pressure += GetFleetReadinessPressure(context, kind, fleet, targetPlanet);
                pressure += GetFinalReadinessGatePressure(context, fleet, targetPlanet, deficit);
            }

            if (kind == AIProductionDemandKind.FleetStarfighter)
                pressure += GetStarfighterFillPressure(context, fleet, targetCount);

            return ClampPressure(pressure);
        }

        /// <summary>
        /// Returns base pressure for a demand.
        /// </summary>
        /// <param name="baseDemandPercent">Base pressure for the demand.</param>
        /// <param name="deficit">Current deficit.</param>
        /// <param name="targetCount">Target count.</param>
        /// <returns>The base pressure.</returns>
        private double GetBasePressure(int baseDemandPercent, int deficit, int targetCount)
        {
            int deficitPercent = deficit * 100 / System.Math.Max(1, targetCount);
            return System.Math.Min(100, baseDemandPercent + deficitPercent);
        }

        /// <summary>
        /// Returns extra economy pressure from maintenance headroom.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The economy maintenance pressure.</returns>
        private double GetEconomyMaintenancePressure(AITurnContext context)
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            int headroom = context.Faction.ProjectedMaintenanceHeadroom;
            int reserve = context
                .Game
                .Config
                .AI
                .Selection
                .MinimumMaintenanceHeadroomAfterProduction;

            if (headroom < 0)
                return config.EconomyMaintenanceShortfallPressure;

            if (headroom >= reserve)
                return 0;

            return config.EconomyMaintenanceReservePressure
                * (reserve - headroom)
                / System.Math.Max(1, reserve);
        }

        /// <summary>
        /// Returns extra pressure from target planet value.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="targetPlanet">The target planet.</param>
        /// <returns>The target value pressure.</returns>
        private double GetTargetValuePressure(AITurnContext context, Planet targetPlanet)
        {
            double highestValue = context.Assessment.GetHighestEnemyPlanetValue();
            if (highestValue <= 0)
                return 0;

            return context.Game.Config.AI.Infrastructure.FleetTargetValuePressureWeight
                * context.Assessment.GetPlanetValue(targetPlanet)
                / highestValue;
        }

        /// <summary>
        /// Returns extra pressure from fleet readiness gaps.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="kind">Demand kind.</param>
        /// <param name="fleet">Fleet receiving production.</param>
        /// <param name="targetPlanet">Fleet attack target.</param>
        /// <returns>The fleet readiness pressure.</returns>
        private double GetFleetReadinessPressure(
            AITurnContext context,
            AIProductionDemandKind kind,
            Fleet fleet,
            Planet targetPlanet
        )
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            int requiredCombat = context.Assessment.GetRequiredAttackCampaignCombatStrength(
                targetPlanet
            );
            int requiredRegiments = context.Assessment.GetRequiredAttackCampaignRegimentCount(
                targetPlanet
            );
            double combatReadiness = GetFulfillmentRatio(
                context.Assessment.GetProjectedFleetCombatValue(fleet),
                requiredCombat
            );
            double regimentReadiness = GetFulfillmentRatio(
                context.Assessment.GetFleetLoadedRegimentCount(fleet),
                requiredRegiments
            );
            double capacityReadiness = GetFulfillmentRatio(
                context.Assessment.GetFleetRegimentCapacity(fleet),
                requiredRegiments
            );

            return kind switch
            {
                AIProductionDemandKind.FleetRegiment => config.FleetReadinessPressureWeight
                    * (combatReadiness + capacityReadiness)
                    / 2,
                AIProductionDemandKind.FleetCapitalShip => config.FleetReadinessPressureWeight
                    * (regimentReadiness + capacityReadiness)
                    / 2,
                AIProductionDemandKind.FleetStarfighter => config.FleetReadinessPressureWeight
                    * (combatReadiness + regimentReadiness + capacityReadiness)
                    / 3,
                _ => 0,
            };
        }

        /// <summary>
        /// Returns extra pressure when a fleet is near final readiness.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">Fleet receiving production.</param>
        /// <param name="targetPlanet">Fleet attack target.</param>
        /// <param name="deficit">Current deficit.</param>
        /// <returns>The final readiness pressure.</returns>
        private double GetFinalReadinessGatePressure(
            AITurnContext context,
            Fleet fleet,
            Planet targetPlanet,
            int deficit
        )
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            if (deficit > config.FleetFinalReadinessGateUnitCount)
                return 0;

            int requiredCombat = context.Assessment.GetRequiredAttackCampaignCombatStrength(
                targetPlanet
            );
            int requiredRegiments = context.Assessment.GetRequiredAttackCampaignRegimentCount(
                targetPlanet
            );
            bool combatReady =
                context.Assessment.GetProjectedFleetCombatValue(fleet) >= requiredCombat;
            bool capacityReady =
                context.Assessment.GetFleetRegimentCapacity(fleet) >= requiredRegiments;
            bool bombardmentReady =
                context.Assessment.GetProjectedFleetBombardmentStrength(fleet)
                >= context.Assessment.GetRequiredAttackCampaignBombardmentStrength(targetPlanet);

            if (!combatReady || !capacityReady || !bombardmentReady)
                return 0;

            return config.FleetFinalReadinessGatePressure
                * (config.FleetFinalReadinessGateUnitCount - deficit + 1)
                / config.FleetFinalReadinessGateUnitCount;
        }

        /// <summary>
        /// Returns extra pressure for filling starfighter capacity.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">Fleet receiving starfighters.</param>
        /// <param name="targetCount">Target starfighter count.</param>
        /// <returns>The starfighter fill pressure.</returns>
        private double GetStarfighterFillPressure(
            AITurnContext context,
            Fleet fleet,
            int targetCount
        )
        {
            if (fleet == null || targetCount <= 0)
                return 0;

            int loadedCount = context.Assessment.GetFleetLoadedStarfighterCount(fleet);
            return context.Game.Config.AI.Infrastructure.FleetStarfighterFillPressureWeight
                * (targetCount - loadedCount)
                / targetCount;
        }

        /// <summary>
        /// Returns a bounded fulfillment ratio.
        /// </summary>
        /// <param name="value">Current value.</param>
        /// <param name="target">Target value.</param>
        /// <returns>The bounded fulfillment ratio.</returns>
        private double GetFulfillmentRatio(double value, double target)
        {
            if (target <= 0)
                return 1;

            return System.Math.Max(0, System.Math.Min(1, value / target));
        }

        /// <summary>
        /// Clamps pressure to the scoring range.
        /// </summary>
        /// <param name="pressure">Pressure to clamp.</param>
        /// <returns>The clamped pressure.</returns>
        private double ClampPressure(double pressure)
        {
            return System.Math.Max(0, System.Math.Min(100, pressure));
        }

        /// <summary>
        /// Returns whether a fleet can receive reinforcement demand.
        /// </summary>
        /// <param name="fleet">Fleet to inspect.</param>
        /// <returns>True if the fleet can receive reinforcement.</returns>
        private bool CanReinforceFleet(Fleet fleet)
        {
            return fleet != null
                && fleet.RoleType == FleetRoleType.Battle
                && (
                    HasPresentOrUnderConstructionCapitalShips(fleet)
                    || fleet.Order?.OrderType is FleetOrderType.Attack or FleetOrderType.Defend
                );
        }

        private Planet GetDefenseTarget(AITurnContext context, Fleet fleet)
        {
            if (fleet?.Order?.OrderType != FleetOrderType.Defend)
                return null;

            Planet target = context.Assessment.GetKnownPlanet(fleet.Order.TargetPlanetId);
            return
                context.Assessment.IsOwnedPlanet(target)
                && context.Assessment.GetRequiredDefenseStrength(target) > 0
                ? target
                : null;
        }

        /// <summary>
        /// Returns whether a fleet has capital ships present or being built.
        /// </summary>
        /// <param name="fleet">Fleet to inspect.</param>
        /// <returns>True if the fleet has present or under-construction capital ships.</returns>
        private static bool HasPresentOrUnderConstructionCapitalShips(Fleet fleet)
        {
            return fleet?.CapitalShips.Any(IsCommittedCapitalShip) == true;
        }

        /// <summary>
        /// Returns whether a capital ship is present or being built.
        /// </summary>
        /// <param name="capitalShip">The capital ship to inspect.</param>
        /// <returns>True if the capital ship is present or under construction.</returns>
        private static bool IsCommittedCapitalShip(CapitalShip capitalShip)
        {
            return capitalShip?.ManufacturingStatus
                is ManufacturingStatus.Complete
                    or ManufacturingStatus.Building;
        }

        /// <summary>
        /// Returns target starfighter count for a fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">Fleet to inspect.</param>
        /// <returns>The target starfighter count.</returns>
        private int GetTargetStarfighterCount(AITurnContext context, Fleet fleet)
        {
            int capacity = fleet.GetStarfighterCapacity();
            return System.Math.Min(
                capacity,
                ScaleByPercent(
                    capacity,
                    context.Game.Config.AI.Infrastructure.StarfighterParentFillPercent
                )
            );
        }

        /// <summary>
        /// Returns desired regiment count for a fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">Fleet to inspect.</param>
        /// <returns>The desired regiment count.</returns>
        private int GetDesiredRegimentCount(AITurnContext context, Fleet fleet)
        {
            if (fleet.Order?.OrderType == FleetOrderType.Defend)
                return 0;

            if (fleet.Order?.OrderType == FleetOrderType.Colonize)
            {
                return context.Game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultRegimentCount;
            }

            int capacity = fleet.GetRegimentCapacity();
            int fillTarget = ScaleByPercent(
                capacity,
                context.Game.Config.AI.Infrastructure.AssaultRegimentLoadPercent
            );
            Planet targetPlanet = GetAttackTargetPlanet(context, fleet);
            if (targetPlanet != null)
                fillTarget = System.Math.Max(
                    fillTarget,
                    context.Assessment.GetRequiredAttackCampaignRegimentCount(targetPlanet)
                );

            if (
                targetPlanet != null
                && context.Assessment.GetProjectedFleetRegimentAttackStrength(fleet)
                    < context.Assessment.GetRequiredAttackCampaignRegimentStrength(targetPlanet)
            )
            {
                fillTarget = System.Math.Max(fillTarget, fleet.GetCurrentRegimentCount() + 1);
            }

            return fillTarget;
        }

        /// <summary>
        /// Returns garrison regiment reserve target for a planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">Planet to inspect.</param>
        /// <returns>The target garrison regiment reserve count.</returns>
        private int GetTargetGarrisonRegimentReserveCount(AITurnContext context, Planet planet)
        {
            int stabilityTarget = UprisingSystem.CalculateGarrisonRequirement(
                planet,
                context.Faction,
                context.Game.Config.AI.Garrison
            );

            return System.Math.Max(
                context.Game.Config.Combat.PlanetaryAssault.CaptureGarrisonCount,
                stabilityTarget
            );
        }

        /// <summary>
        /// Returns the active attack target for a fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">Fleet to inspect.</param>
        /// <returns>The attack target planet, or null.</returns>
        private Planet GetAttackTargetPlanet(AITurnContext context, Fleet fleet)
        {
            string targetPlanetId = fleet.Order?.TargetPlanetId;
            if (
                fleet.Order?.OrderType != FleetOrderType.Attack
                || string.IsNullOrEmpty(targetPlanetId)
            )
                return null;

            Planet targetPlanet = context.Assessment.GetKnownPlanet(targetPlanetId);
            string targetOwnerId = targetPlanet?.GetOwnerInstanceID();
            if (string.IsNullOrEmpty(targetOwnerId) || targetOwnerId == context.Faction.InstanceID)
                return null;

            return targetPlanet;
        }

        /// <summary>
        /// Scales an integer by a percent value and rounds up.
        /// </summary>
        /// <param name="value">Value to scale.</param>
        /// <param name="percent">Percent to apply.</param>
        /// <returns>The scaled value.</returns>
        private int ScaleByPercent(int value, int percent)
        {
            return (value * percent + 99) / 100;
        }

        /// <summary>
        /// Divides two integers and rounds up.
        /// </summary>
        /// <param name="value">Value to divide.</param>
        /// <param name="divisor">Divisor to use.</param>
        /// <returns>The rounded-up quotient.</returns>
        private int CeilingDivide(int value, int divisor)
        {
            if (divisor <= 0)
                return 0;

            return (value + divisor - 1) / divisor;
        }
    }
}
