using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Planners.Demand;
using Rebellion.AI.Scoring;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.Systems;
using Rebellion.Util.Common;

namespace Rebellion.AI.Planners
{
    /// <summary>
    /// Builds production demand from faction state and current force needs.
    /// </summary>
    public sealed class AIProductionDemandGenerator
    {
        private static readonly AIDemandSource _colonyDemandSource = new AIColonyDemandSource();
        private static readonly AIDemandSource _specialForcesDemandSource =
            new AISpecialForcesDemandSource();

        /// <summary>
        /// Returns production demand for the current AI turn.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Production demand generated for this faction.</returns>
        public List<AIDemand> Generate(AITurnContext context)
        {
            List<AIDemand> demands = new List<AIDemand>();

            if (context?.Game == null || context.Faction == null || context.Assessment == null)
                return demands;

            _colonyDemandSource.AddDemands(context, demands);
            AddResourceBalanceDemand(context, demands);
            AddPlanetaryDefenseDemands(context, demands);
            AddPlanetaryStarfighterDemands(context, demands);
            AddFleetSeedDemand(context, demands);
            AddColonizationFleetSeedDemand(context, demands);
            AddFleetReinforcementDemands(context, demands);
            AddPlanetaryGarrisonDemands(context, demands);
            _specialForcesDemandSource.AddDemands(context, demands);
            AIFacilityAllocationPolicy facilityPolicy = context.FacilityAllocation;
            AddProductionFacilityDemands(
                context,
                demands,
                new AIInfrastructurePlacementScorer(context),
                facilityPolicy
            );
            AddProductionFacilityUpgradeDemands(context, demands, facilityPolicy);

            return demands;
        }

        /// <summary>
        /// Adds static-defense demands for owned planets.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        private void AddPlanetaryDefenseDemands(AITurnContext context, List<AIDemand> demands)
        {
            foreach (
                Planet planet in context
                    .Assessment.OwnedPlanets.Where(IsOwnedUsablePlanet)
                    .OrderByDescending(context.Assessment.GetPlanetValue)
                    .ThenBy(planet => planet.InstanceID, StringComparer.Ordinal)
            )
                AddPlanetaryDefenseDemands(context, demands, planet);
        }

        /// <summary>
        /// Adds static-defense demands for one planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        /// <param name="planet">The planet to evaluate.</param>
        private void AddPlanetaryDefenseDemands(
            AITurnContext context,
            List<AIDemand> demands,
            Planet planet
        )
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            int availableEnergy = planet.GetAvailableEnergy();
            int shieldTarget = context.Assessment.GetPlanetaryShieldTargetCount(planet);
            int shieldCount = context
                .Assessment.GetPlanetBuildings(planet)
                .Count(building =>
                    building.GetOwnerInstanceID() == context.Faction.InstanceID
                    && building.IsPlanetaryShieldGenerator()
                );
            int shieldDeficit = Math.Max(0, shieldTarget - shieldCount);
            int shieldQuantity = Math.Min(shieldDeficit, availableEnergy);

            if (shieldQuantity > 0)
            {
                demands.Add(
                    CreatePlanetaryDefenseBuildingDemand(
                        context,
                        planet,
                        BuildingType.Defense,
                        shieldQuantity,
                        shieldTarget,
                        config.PlanetaryShieldDemandPercent,
                        shieldCount == 0
                    )
                );
                availableEnergy -= shieldQuantity;
            }

            int weaponCount = context
                .Assessment.GetPlanetBuildings(planet)
                .Count(building =>
                    building.GetOwnerInstanceID() == context.Faction.InstanceID
                    && building.GetBuildingType() == BuildingType.Weapon
                );
            int weaponTarget = context.Assessment.GetPlanetaryWeaponTargetCount(
                planet,
                weaponCount
            );
            int weaponDeficit = weaponTarget - weaponCount;
            if (weaponDeficit <= 0 || availableEnergy <= 0)
                return;

            demands.Add(
                CreatePlanetaryDefenseBuildingDemand(
                    context,
                    planet,
                    BuildingType.Weapon,
                    Math.Min(weaponDeficit, availableEnergy),
                    weaponTarget,
                    config.PlanetaryWeaponDemandPercent
                )
            );
        }

        /// <summary>
        /// Returns whether a planet has finished its static defense minimums: the shield
        /// generator limit and the baseline weapon emplacement target.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>True when the planet's static defense minimums are complete.</returns>
        private static bool HasCompletedStaticDefense(AITurnContext context, Planet planet)
        {
            int shieldTarget = context.Assessment.GetPlanetaryShieldTargetCount(planet);
            int shieldCount = 0;
            int weaponCount = 0;
            foreach (Building building in context.Assessment.GetPlanetBuildings(planet))
            {
                if (building.GetOwnerInstanceID() != context.Faction.InstanceID)
                    continue;

                if (building.IsPlanetaryShieldGenerator())
                    shieldCount++;
                else if (building.GetBuildingType() == BuildingType.Weapon)
                    weaponCount++;
            }

            int weaponTarget = context.Assessment.GetPlanetaryWeaponTargetCount(
                planet,
                weaponCount
            );
            return shieldCount >= shieldTarget && weaponCount >= weaponTarget;
        }

        /// <summary>
        /// Creates one planetary-defense building demand.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The destination planet.</param>
        /// <param name="buildingType">The requested defense type.</param>
        /// <param name="deficit">The remaining unit deficit.</param>
        /// <param name="targetCount">The desired unit count.</param>
        /// <param name="baseDemandPercent">The base demand pressure.</param>
        /// <param name="isInitialShield">Whether this establishes the first shield.</param>
        /// <returns>The defense demand.</returns>
        private AIDemand CreatePlanetaryDefenseBuildingDemand(
            AITurnContext context,
            Planet planet,
            BuildingType buildingType,
            int deficit,
            int targetCount,
            int baseDemandPercent,
            bool isInitialShield = false
        )
        {
            return new AIDemand(
                AIDemand.CreateId(
                    context.Faction.InstanceID,
                    AIDemandKind.PlanetaryDefense,
                    buildingType,
                    planet.InstanceID
                ),
                AIDemandKind.PlanetaryDefense,
                ManufacturingType.Building,
                buildingType,
                planet,
                deficit,
                GetPlanetaryDefensePressure(
                    context,
                    planet,
                    baseDemandPercent,
                    deficit,
                    targetCount,
                    isInitialShield
                )
            );
        }

        /// <summary>
        /// Adds planetary starfighter reserve demands.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        private void AddPlanetaryStarfighterDemands(AITurnContext context, List<AIDemand> demands)
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            foreach (
                Planet planet in context
                    .Assessment.OwnedPlanets.Where(IsOwnedUsablePlanet)
                    .OrderByDescending(context.Assessment.GetPlanetValue)
                    .ThenBy(planet => planet.InstanceID, StringComparer.Ordinal)
            )
            {
                if (
                    context.Game.Config.AI.NonCapitalSummary.RequireStaticDefenseBeforeStarfighters
                    && !HasProductionInfrastructure(context, planet)
                    && !HasCompletedStaticDefense(context, planet)
                )
                    continue;

                int committedCount = GetOwnedStarfighterCount(context, planet);
                int targetCount = GetPlanetaryStarfighterRequirement(context, planet);
                int deficit = targetCount - committedCount;
                if (deficit <= 0)
                    continue;

                demands.Add(
                    new AIDemand(
                        AIDemand.CreateId(
                            context.Faction.InstanceID,
                            AIDemandKind.PlanetaryStarfighterReserve,
                            planet.InstanceID
                        ),
                        AIDemandKind.PlanetaryStarfighterReserve,
                        ManufacturingType.Ship,
                        BuildingType.None,
                        planet,
                        deficit,
                        GetPlanetaryDefensePressure(
                            context,
                            planet,
                            config.PlanetaryStarfighterDemandPercent,
                            deficit,
                            targetCount
                        )
                    )
                );
            }
        }

        /// <summary>
        /// Returns the strategic and threat-responsive starfighter requirement for a planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The planet to assess.</param>
        /// <returns>The number of starfighters required at the planet.</returns>
        private static int GetPlanetaryStarfighterRequirement(AITurnContext context, Planet planet)
        {
            GameConfig.AINonCapitalSummaryConfig config = context.Game.Config.AI.NonCapitalSummary;
            bool hasProductionInfrastructure = HasProductionInfrastructure(context, planet);
            int baseline = planet.IsHeadquarters
                ? config.StarfighterRequirementHeadquarters
                : hasProductionInfrastructure
                    ? Math.Max(12, config.StarfighterRequirementInfrastructure)
                    : config.StarfighterRequirementDefault;
            if (!planet.IsHeadquarters && !context.Assessment.IsPlanetThreatened(planet))
            {
                if (!hasProductionInfrastructure)
                    baseline = IntegerMath.ScaleByPercent(
                        baseline,
                        config.InteriorStarfighterBaselinePercent
                    );
            }
            int requiredDefenseStrength = context.Assessment.GetRequiredPlanetDefenseStrength(
                planet
            );
            int fighterStrength = GetStrongestAvailableStarfighterStrength(context);
            int threatReinforcement =
                fighterStrength > 0
                    ? IntegerMath.DivideRoundedUp(requiredDefenseStrength, fighterStrength)
                    : 0;
            return baseline + threatReinforcement;
        }

        /// <summary>
        /// Returns whether the planet contains strategic production infrastructure.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>True when the planet has at least one production facility.</returns>
        private static bool HasProductionInfrastructure(AITurnContext context, Planet planet) =>
            context.Assessment.HasProductionInfrastructure(planet);

        /// <summary>
        /// Returns the strongest planetary fighter the faction can currently manufacture.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The fighter's combat strength, or zero when none is available.</returns>
        private static int GetStrongestAvailableStarfighterStrength(AITurnContext context)
        {
            return context
                .Faction.GetUnlockedTechnologies(ManufacturingType.Ship)
                .Select(technology => technology.GetReference())
                .OfType<Starfighter>()
                .Where(starfighter =>
                    IManufacturable.CanBeManufacturedBy(starfighter, context.Faction.InstanceID)
                )
                .Select(starfighter => starfighter.GetWeaponStrength())
                .DefaultIfEmpty()
                .Max();
        }

        /// <summary>
        /// Counts starfighters committed to defending one planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>The number of owned starfighters assigned to the planet.</returns>
        private static int GetOwnedStarfighterCount(AITurnContext context, Planet planet)
        {
            return context
                .Assessment.GetPlanetStarfighters(planet)
                .Count(starfighter =>
                    starfighter.GetOwnerInstanceID() == context.Faction.InstanceID
                );
        }

        /// <summary>
        /// Adds demands that establish missing battle fleets.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        private void AddFleetSeedDemand(AITurnContext context, List<AIDemand> demands)
        {
            int targetCount = GetTargetBattleFleetCount(context);
            int committedCount = context.Assessment.OwnedFleets.Count(IsCommittedBattleFleet);
            int deficit = targetCount - committedCount;
            Planet unguardedHeadquarters = FindUnguardedHeadquarters(context);
            if (deficit <= 0 && unguardedHeadquarters == null)
                return;

            Planet destination = unguardedHeadquarters ?? FindFleetAssemblyPlanet(context);
            if (destination == null)
                return;

            int quantityNeeded = Math.Max(1, deficit);

            demands.Add(
                new AIDemand(
                    AIDemand.CreateId(
                        context.Faction.InstanceID,
                        AIDemandKind.FleetSeedCapitalShip
                    ),
                    AIDemandKind.FleetSeedCapitalShip,
                    ManufacturingType.Ship,
                    BuildingType.None,
                    destination,
                    quantityNeeded,
                    GetDemandPressure(
                        context,
                        AIDemandKind.FleetSeedCapitalShip,
                        quantityNeeded,
                        Math.Max(1, targetCount),
                        context.Game.Config.AI.Infrastructure.FleetSeedCapitalShipDemandPercent
                    ),
                    capitalShipRole: AICapitalShipProductionRole.General
                )
            );
        }

        /// <summary>
        /// Adds demand for a dedicated colonization fleet when known unsettled planets remain.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        private void AddColonizationFleetSeedDemand(AITurnContext context, List<AIDemand> demands)
        {
            int targetCount = Math.Max(
                0,
                context.Game.Config.AI.FleetDeployment.ColonizationFleetTargetCount
            );
            int committedCount = context.Assessment.OwnedFleets.Count(fleet =>
                fleet.RoleType == FleetRoleType.Colonization
            );
            int deficit = targetCount - committedCount;
            if (context.Assessment.KnownUncolonizedPlanets.Count == 0 || deficit <= 0)
                return;

            Planet destination = FindFleetAssemblyPlanet(context);
            if (destination == null)
                return;

            demands.Add(
                new AIDemand(
                    AIDemand.CreateId(
                        context.Faction.InstanceID,
                        AIDemandKind.ColonizationFleetSeedCapitalShip
                    ),
                    AIDemandKind.ColonizationFleetSeedCapitalShip,
                    ManufacturingType.Ship,
                    BuildingType.None,
                    destination,
                    deficit,
                    context.Game.Config.AI.Infrastructure.ColonizationFleetDemandPercent,
                    capitalShipRole: AICapitalShipProductionRole.TroopTransport
                )
            );
        }

        /// <summary>
        /// Returns the desired battle-fleet count.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The desired fleet count.</returns>
        private int GetTargetBattleFleetCount(AITurnContext context)
        {
            GameConfig.AIFleetDeploymentConfig config = context.Game.Config.AI.FleetDeployment;
            int operationalPlanetCount = context.Assessment.OwnedPlanets.Count(planet =>
                planet.IsColonized && !planet.IsDestroyed
            );
            int scaledTarget = IntegerMath.DivideRoundedUp(
                operationalPlanetCount,
                config.PlanetsPerBattleFleet
            );
            return Math.Max(config.MinimumBattleFleetCount, scaledTarget);
        }

        /// <summary>
        /// Finds the highest-priority headquarters lacking a defense fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The unguarded headquarters, or null.</returns>
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

        /// <summary>
        /// Finds the preferred planet for assembling a new fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The assembly planet, or null.</returns>
        private Planet FindFleetAssemblyPlanet(AITurnContext context)
        {
            return context
                .Assessment.OwnedPlanets.Where(planet => planet.IsColonized && !planet.IsDestroyed)
                .OrderByDescending(context.Assessment.IsFactionHeadquarters)
                .ThenByDescending(planet =>
                    context.Assessment.GetPlanetProductionRate(planet, ManufacturingType.Ship)
                )
                .ThenByDescending(context.Assessment.GetPlanetValue)
                .ThenBy(planet => planet.InstanceID, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns whether a fleet counts toward the battle-fleet target.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <returns>True when the fleet is committed.</returns>
        private static bool IsCommittedBattleFleet(Fleet fleet)
        {
            return fleet?.RoleType == FleetRoleType.Battle
                && fleet
                    .GetChildren<CapitalShip>()
                    .Any(ship =>
                        ship.ManufacturingStatus
                            is ManufacturingStatus.Complete
                                or ManufacturingStatus.Building
                    );
        }

        /// <summary>
        /// Adds production-facility expansion demands.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        private void AddProductionFacilityDemands(
            AITurnContext context,
            List<AIDemand> demands,
            AIInfrastructurePlacementScorer placementScorer,
            AIFacilityAllocationPolicy facilityPolicy
        )
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            AddProductionFacilityDemand(
                context,
                demands,
                ManufacturingType.Ship,
                AIDemandKind.Shipyard,
                BuildingType.Shipyard,
                config.ShipyardDemandPercent,
                placementScorer,
                facilityPolicy
            );
            AddProductionFacilityDemand(
                context,
                demands,
                ManufacturingType.Building,
                AIDemandKind.ConstructionFacility,
                BuildingType.ConstructionFacility,
                config.ConstructionFacilityDemandPercent,
                placementScorer,
                facilityPolicy
            );
            AddProductionFacilityDemand(
                context,
                demands,
                ManufacturingType.Troop,
                AIDemandKind.TrainingFacility,
                BuildingType.TrainingFacility,
                config.TrainingFacilityDemandPercent,
                placementScorer,
                facilityPolicy
            );
        }

        /// <summary>
        /// Adds available production-facility upgrade demands.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        private void AddProductionFacilityUpgradeDemands(
            AITurnContext context,
            List<AIDemand> demands,
            AIFacilityAllocationPolicy facilityPolicy
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
                    BuildingType.ConstructionFacility,
                    facilityPolicy
                );
                AddProductionFacilityUpgradeDemand(
                    context,
                    demands,
                    planet,
                    BuildingType.Shipyard,
                    facilityPolicy
                );
                AddProductionFacilityUpgradeDemand(
                    context,
                    demands,
                    planet,
                    BuildingType.TrainingFacility,
                    facilityPolicy
                );
            }
        }

        /// <summary>
        /// Adds an upgrade demand for one manufacturing category.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        /// <param name="planet">The planet whose facilities are evaluated.</param>
        /// <param name="buildingType">The production-facility type to upgrade.</param>
        private void AddProductionFacilityUpgradeDemand(
            AITurnContext context,
            List<AIDemand> demands,
            Planet planet,
            BuildingType buildingType,
            AIFacilityAllocationPolicy facilityPolicy
        )
        {
            if (
                facilityPolicy.GetCap(planet, buildingType) <= 0
                || HasPendingFacility(context, planet, buildingType)
            )
                return;

            List<Building> activeFacilities = context
                .Assessment.GetPlanetBuildings(planet)
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
                    && IManufacturable.CanBeManufacturedBy(building, context.Faction.InstanceID)
                )
                .ToList();
            Building replacement = activeFacilities
                .Where(current =>
                    unlockedFacilities.Any(candidate => current.CanUpgradeTo(candidate))
                )
                .OrderByDescending(building => building.GetProcessRate())
                .ThenBy(building => building.ResearchOrder)
                .ThenBy(building => building.InstanceID, StringComparer.Ordinal)
                .FirstOrDefault();
            if (replacement == null)
                return;

            AIDemand demand = new AIDemand(
                AIDemand.CreateId(
                    context.Faction.InstanceID,
                    AIDemandKind.BuildingUpgrade,
                    buildingType,
                    planet.InstanceID,
                    replacement.InstanceID
                ),
                AIDemandKind.BuildingUpgrade,
                ManufacturingType.Building,
                buildingType,
                planet,
                1,
                GetProductionFacilityUpgradePressure(context, planet)
            );
            demand.BuildingToReplace = replacement;
            demands.Add(demand);
        }

        /// <summary>
        /// Returns the pressure for upgrading a production facility.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The upgrade destination.</param>
        /// <returns>The demand pressure.</returns>
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

        /// <summary>
        /// Adds expansion demand for one production-facility type.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        /// <param name="manufacturingType">The manufacturing category.</param>
        /// <param name="kind">The facility demand kind.</param>
        /// <param name="buildingType">The required facility type.</param>
        /// <param name="baseDemandPercent">The base demand pressure.</param>
        private void AddProductionFacilityDemand(
            AITurnContext context,
            List<AIDemand> demands,
            ManufacturingType manufacturingType,
            AIDemandKind kind,
            BuildingType buildingType,
            int baseDemandPercent,
            AIInfrastructurePlacementScorer placementScorer,
            AIFacilityAllocationPolicy facilityPolicy
        )
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            List<AIDemand> productionDemands = demands
                .Where(demand => demand.ManufacturingType == manufacturingType)
                .Where(demand => demand.Kind != kind)
                .OrderByDescending(demand => demand.Pressure)
                .ThenBy(demand => demand.Id, StringComparer.Ordinal)
                .ToList();
            if (productionDemands.Count == 0)
                return;

            int remainingTrainingFacilityCount = int.MaxValue;
            if (buildingType == BuildingType.TrainingFacility)
            {
                int demandCapacityTarget = IntegerMath.DivideRoundedUp(
                    productionDemands.Count,
                    Math.Max(1, config.TrainingDemandsPerFacility)
                );
                remainingTrainingFacilityCount = Math.Max(
                    0,
                    Math.Max(
                        GetDesiredProductionFacilityCount(context, buildingType),
                        demandCapacityTarget
                    )
                        - GetOwnedFacilityCount(context, buildingType)
                );
                if (remainingTrainingFacilityCount == 0)
                    return;
            }

            List<IGrouping<string, Planet>> sectors = context
                .Assessment.OwnedPlanets.Where(IsOwnedUsablePlanet)
                .GroupBy(context.Assessment.GetPlanetSystemId)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .ToList();
            int hubTarget =
                buildingType == BuildingType.Shipyard
                    ? context.Game.Config.AI.Infrastructure.ShipyardSectorHubTargetCount
                    : context.Game.Config.AI.Infrastructure.FacilitySectorHubTargetCount;
            double categoryBalancePressure = GetFacilityCategoryBalancePressure(
                sectors,
                buildingType,
                hubTarget,
                baseDemandPercent
            );
            bool hasIncompleteShipyardHub =
                buildingType == BuildingType.Shipyard
                && sectors.Any(sector =>
                    sector.Any(planet =>
                        facilityPolicy.IsPrimaryHub(planet, buildingType)
                        && GetAvailableFacilityExpansionEnergy(context, planet) > 0
                        && planet.GetTotalBuildingTypeCount(buildingType)
                            < facilityPolicy.GetPrimaryTarget(
                                planet,
                                buildingType,
                                hubTarget
                            )
                    )
                );

            foreach (IGrouping<string, Planet> sector in sectors)
            {
                if (remainingTrainingFacilityCount == 0)
                    break;

                List<Planet> sectorPlanets = sector
                    .Where(planet =>
                        facilityPolicy.GetCap(planet, buildingType) > 0
                        && GetAvailableFacilityExpansionEnergy(context, planet) > 0
                    )
                    .ToList();
                if (sectorPlanets.Count == 0)
                    continue;
                AIDemand sectorDemand = productionDemands
                    .Where(demand =>
                        context.Assessment.GetPlanetSystemId(GetDemandPlanet(context, demand))
                        == sector.Key
                    )
                    .DefaultIfEmpty(productionDemands[0])
                    .First();
                IReadOnlyList<Planet> rankedPlanets = placementScorer.RankDestinations(
                    sectorPlanets,
                    GetDemandPlanet(context, sectorDemand),
                    manufacturingType,
                    buildingType,
                    planet => GetAvailableFacilityExpansionEnergy(context, planet)
                );
                if (rankedPlanets.Count == 0)
                    continue;

                Planet hub = rankedPlanets.FirstOrDefault(planet =>
                    facilityPolicy.IsPrimaryHub(planet, buildingType)
                );
                if (hub == null)
                    continue;
                int primaryTarget = facilityPolicy.GetPrimaryTarget(
                    hub,
                    buildingType,
                    hubTarget
                );
                int hubCount = hub.GetTotalBuildingTypeCount(buildingType);
                if (hubCount < primaryTarget)
                {
                    int priorDemandCount = demands.Count;
                    AddSectorFacilityDemand(
                        context,
                        demands,
                        sectorDemand,
                        kind,
                        buildingType,
                        hub,
                        primaryTarget,
                        baseDemandPercent,
                        context.Game.Config.AI.Infrastructure.FacilitySectorCoveragePressureBonus
                            + context
                                .Game
                                .Config
                                .AI
                                .Infrastructure
                                .FacilitySectorPrimaryHubPressureBonus
                            + categoryBalancePressure
                    );
                    if (
                        buildingType == BuildingType.TrainingFacility
                        && demands.Count > priorDemandCount
                    )
                        remainingTrainingFacilityCount--;
                    continue;
                }

                if (hasIncompleteShipyardHub)
                    continue;

                int secondaryTarget = context
                    .Game
                    .Config
                    .AI
                    .Infrastructure
                    .FacilitySectorSecondaryTargetCount;
                Planet secondarySite = rankedPlanets.FirstOrDefault(planet =>
                    facilityPolicy.GetCap(planet, buildingType) == secondaryTarget
                    && planet.GetTotalBuildingTypeCount(buildingType) < secondaryTarget
                );

                int priorSecondaryDemandCount = demands.Count;
                AddSectorFacilityDemand(
                    context,
                    demands,
                    sectorDemand,
                    kind,
                    buildingType,
                    secondarySite,
                    secondaryTarget,
                    baseDemandPercent,
                    categoryBalancePressure
                );
                if (
                    buildingType == BuildingType.TrainingFacility
                    && demands.Count > priorSecondaryDemandCount
                )
                    remainingTrainingFacilityCount--;
            }
        }

        /// <summary>
        /// Returns pressure that keeps production-facility categories advancing at comparable
        /// rates while their sector hubs are established.
        /// </summary>
        /// <param name="sectors">Owned planets grouped by system.</param>
        /// <param name="buildingType">The production facility category.</param>
        /// <param name="hubTarget">The desired facility count at each primary site.</param>
        /// <param name="baseDemandPercent">The category's base demand pressure.</param>
        /// <returns>The category balance pressure.</returns>
        private static double GetFacilityCategoryBalancePressure(
            IReadOnlyCollection<IGrouping<string, Planet>> sectors,
            BuildingType buildingType,
            int hubTarget,
            int baseDemandPercent
        )
        {
            if (sectors.Count == 0 || hubTarget <= 0)
                return 0;

            int completedHubProgress = sectors.Sum(sector =>
                Math.Min(
                    hubTarget,
                    sector.Max(planet => planet.GetTotalBuildingTypeCount(buildingType))
                )
            );
            double targetHubProgress = sectors.Count * (double)hubTarget;
            double completion = completedHubProgress / targetHubProgress;
            return baseDemandPercent * (0.5 - completion);
        }

        /// <summary>
        /// Adds one production-facility demand toward a sector hub or established local cluster.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        /// <param name="primaryDemand">The production demand served by the facility.</param>
        /// <param name="kind">The facility demand kind.</param>
        /// <param name="buildingType">The required facility type.</param>
        /// <param name="target">The destination planet.</param>
        /// <param name="targetCount">The desired facility count at the destination.</param>
        /// <param name="baseDemandPercent">The base demand pressure.</param>
        /// <param name="strategicBonus">Additional pressure for the site's strategic role.</param>
        private void AddSectorFacilityDemand(
            AITurnContext context,
            List<AIDemand> demands,
            AIDemand primaryDemand,
            AIDemandKind kind,
            BuildingType buildingType,
            Planet target,
            int targetCount,
            int baseDemandPercent,
            double strategicBonus
        )
        {
            if (target == null || target.GetAvailableEnergy() <= 0 || targetCount <= 0)
                return;

            int currentCount = target.GetTotalBuildingTypeCount(buildingType);
            if (currentCount >= targetCount)
                return;

            double concentrationBonus = baseDemandPercent * currentCount / targetCount;

            demands.Add(
                new AIDemand(
                    AIDemand.CreateId(context.Faction.InstanceID, kind, target.InstanceID),
                    kind,
                    ManufacturingType.Building,
                    buildingType,
                    target,
                    1,
                    GetProductionFacilityPressure(
                        context,
                        kind,
                        currentCount,
                        targetCount,
                        baseDemandPercent,
                        0
                    )
                        + strategicBonus
                        + concentrationBonus,
                    primaryDemand.ProductTypeId,
                    primaryDemand.CapitalShipRole
                )
            );
        }

        /// <summary>
        /// Returns expansion pressure for a production facility.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="kind">The facility demand kind.</param>
        /// <param name="currentCount">The number of currently owned facilities.</param>
        /// <param name="desiredCount">The minimum strategic facility count.</param>
        /// <param name="baseDemandPercent">The base demand pressure.</param>
        /// <param name="investmentDeficit">The remaining construction-capacity deficit.</param>
        /// <returns>The adjusted pressure.</returns>
        private double GetProductionFacilityPressure(
            AITurnContext context,
            AIDemandKind kind,
            int currentCount,
            int desiredCount,
            int baseDemandPercent,
            double investmentDeficit
        )
        {
            int targetCount = Math.Max(currentCount + 1, desiredCount);
            int deficit = Math.Max(1, targetCount - currentCount);
            double pressure = baseDemandPercent + deficit * 100.0 / targetCount;
            if (kind == AIDemandKind.TrainingFacility)
                pressure += context
                    .Game
                    .Config
                    .AI
                    .Infrastructure
                    .TrainingFacilityBacklogPressureBonus;

            if (kind == AIDemandKind.ConstructionFacility)
                pressure +=
                    context.Game.Config.AI.Infrastructure.ProductionFacilityInvestmentPressureWeight
                    * investmentDeficit;

            return pressure;
        }

        /// <summary>
        /// Returns whether production throughput requires another facility.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The current production demands.</param>
        /// <param name="manufacturingType">The manufacturing category.</param>
        /// <param name="buildingType">The production facility type.</param>
        /// <param name="investmentDeficit">The remaining construction-capacity deficit.</param>
        /// <returns>True when another facility is needed.</returns>
        private bool NeedsProductionFacility(
            AITurnContext context,
            IReadOnlyCollection<AIDemand> demands,
            Planet target,
            ManufacturingType manufacturingType,
            BuildingType buildingType,
            double investmentDeficit
        )
        {
            int demandLaneCount = demands.Count(demand =>
                demand.ManufacturingType == manufacturingType && demand.QuantityNeeded > 0
            );
            if (demandLaneCount <= 0)
                return false;

            if (IsBelowProductionFacilityFloor(context, buildingType))
                return true;

            if (buildingType == BuildingType.ConstructionFacility && investmentDeficit > 0)
                return true;

            double throughput = context.Assessment.GetPlanetProductionRate(
                target,
                manufacturingType
            );
            if (throughput <= 0)
                return true;

            if (
                context.Assessment.GetPlanetQueuedProductionClearTicks(target, manufacturingType)
                <= 0
            )
                return false;

            int targetQueueTicks = buildingType switch
            {
                BuildingType.ConstructionFacility => context
                    .Game
                    .Config
                    .AI
                    .Infrastructure
                    .ConstructionFacilityTargetClearTicks,
                BuildingType.Shipyard => context
                    .Game
                    .Config
                    .AI
                    .Infrastructure
                    .ShipyardTargetClearTicks,
                BuildingType.TrainingFacility => context
                    .Game
                    .Config
                    .AI
                    .Infrastructure
                    .TrainingFacilityTargetClearTicks,
                _ => 0,
            };
            return context.Assessment.GetPlanetQueuedProductionClearTicks(target, manufacturingType)
                >= targetQueueTicks;
        }

        /// <summary>
        /// Returns additional construction facilities needed to deploy the remaining industrial
        /// budget within the configured investment horizon.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="currentCount">The projected construction-facility count.</param>
        /// <returns>The additional construction-facility count.</returns>
        private int GetConstructionCapacityInvestmentCount(AITurnContext context, int currentCount)
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            int allocatedMaintenance = IntegerMath.ScaleByPercent(
                context.Assessment.MaintenanceCapacity,
                config.ProductionFacilityMaintenanceAllocationPercent
            );
            int remainingMaintenance = Math.Max(
                0,
                allocatedMaintenance - context.Assessment.GetProductionFacilityMaintenance()
            );
            if (remainingMaintenance <= 0)
                return 0;

            int facilityTypeCount = 0;
            long totalConstructionCost = 0;
            long totalMaintenanceCost = 0;
            foreach (
                Technology technology in context.Faction.GetUnlockedTechnologies(
                    ManufacturingType.Building
                )
            )
            {
                if (
                    technology.GetReference() is not Building facility
                    || facility.GetBuildingType()
                        is not (
                            BuildingType.ConstructionFacility
                            or BuildingType.Shipyard
                            or BuildingType.TrainingFacility
                        )
                    || facility.ConstructionCost <= 0
                    || facility.MaintenanceCost <= 0
                )
                    continue;

                facilityTypeCount++;
                totalConstructionCost += facility.ConstructionCost;
                totalMaintenanceCost += facility.MaintenanceCost;
            }

            if (facilityTypeCount <= 0 || totalMaintenanceCost <= 0)
                return 0;

            double averageConstructionCost = totalConstructionCost / (double)facilityTypeCount;
            double averageMaintenanceCost = totalMaintenanceCost / (double)facilityTypeCount;
            double remainingFacilityCount = remainingMaintenance / averageMaintenanceCost;
            double requiredThroughput =
                remainingFacilityCount
                * averageConstructionCost
                / Math.Max(1, config.ProductionFacilityInvestmentHorizonTicks);
            if (requiredThroughput <= 0)
                return 0;

            double availableThroughput = context.Assessment.GetProductionThroughput(
                ManufacturingType.Building
            );
            if (availableThroughput >= requiredThroughput)
                return 0;

            int activeFacilityCount = context.Assessment.OwnedPlanets.Sum(planet =>
                context.Assessment.GetPlanetProductionFacilityCount(
                    planet,
                    ManufacturingType.Building
                )
            );
            double throughputPerFacility =
                activeFacilityCount > 0 ? availableThroughput / activeFacilityCount : 0;
            if (throughputPerFacility <= 0)
                return 1;

            int requiredFacilityCount = (int)
                Math.Ceiling(requiredThroughput / throughputPerFacility);
            return Math.Max(0, requiredFacilityCount - currentCount);
        }

        /// <summary>
        /// Returns whether projected capacity is below its strategic minimum.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="buildingType">The production facility type.</param>
        /// <returns>True when more facilities are required to meet the minimum.</returns>
        private bool IsBelowProductionFacilityFloor(
            AITurnContext context,
            BuildingType buildingType
        )
        {
            return GetOwnedFacilityCount(context, buildingType)
                < GetDesiredProductionFacilityCount(context, buildingType);
        }

        /// <summary>
        /// Returns the strategic minimum for a production-facility type.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="buildingType">The production facility type.</param>
        /// <returns>The minimum projected facility count.</returns>
        private int GetDesiredProductionFacilityCount(
            AITurnContext context,
            BuildingType buildingType
        )
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            int planetsPerFacility = buildingType switch
            {
                BuildingType.ConstructionFacility => config.PlanetsPerConstructionFacility,
                BuildingType.Shipyard => config.PlanetsPerShipyard,
                BuildingType.TrainingFacility => config.PlanetsPerTrainingFacility,
                _ => 0,
            };
            if (planetsPerFacility <= 0)
                return 0;

            int desiredCount = IntegerMath.DivideRoundedUp(
                context.Assessment.OwnedPlanets.Count,
                planetsPerFacility
            );
            if (buildingType == BuildingType.ConstructionFacility)
            {
                desiredCount = Math.Max(
                    desiredCount,
                    Math.Min(
                        context.Assessment.OwnedPlanets.Count,
                        config.MinimumConstructionFacilityLanes
                    )
                );
            }

            return desiredCount;
        }

        /// <summary>
        /// Returns whether a matching facility is already under construction.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="target">The prospective facility destination.</param>
        /// <param name="buildingType">The facility type.</param>
        /// <returns>True when construction is pending.</returns>
        private bool HasPendingFacility(
            AITurnContext context,
            Planet target,
            BuildingType buildingType
        )
        {
            return context
                .Assessment.GetPlanetBuildings(target)
                .Any(building =>
                    building.GetOwnerInstanceID() == context.Faction.InstanceID
                    && building.GetBuildingType() == buildingType
                    && (
                        building.GetManufacturingStatus() != ManufacturingStatus.Complete
                        || building.Movement != null
                    )
                );
        }

        /// <summary>
        /// Adds planetary garrison-regiment demands.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        private void AddPlanetaryGarrisonDemands(AITurnContext context, List<AIDemand> demands)
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
            List<AIDemand> demands,
            Planet planet
        )
        {
            int minimumTargetCount = GetTargetGarrisonRegimentReserveCount(context, planet);
            int currentCount = context
                .Assessment.GetPlanetRegiments(planet)
                .Count(regiment => regiment.GetOwnerInstanceID() == context.Faction.InstanceID);
            int deficit = minimumTargetCount - currentCount;
            if (deficit <= 0)
                return;

            demands.Add(
                new AIDemand(
                    AIDemand.CreateId(
                        context.Faction.InstanceID,
                        AIDemandKind.GarrisonRegimentReserve,
                        planet.InstanceID
                    ),
                    AIDemandKind.GarrisonRegimentReserve,
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
        private void AddFleetReinforcementDemands(AITurnContext context, List<AIDemand> demands)
        {
            foreach (Fleet fleet in GetPriorityReinforcementFleets(context))
            {
                AddFleetCapitalShipDemand(context, demands, fleet);
                AddFleetStarfighterDemand(context, demands, fleet);
                AddFleetRegimentDemand(context, demands, fleet);
            }
        }

        /// <summary>
        /// Returns fleets ordered for reinforcement planning.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The priority fleets.</returns>
        private IReadOnlyList<Fleet> GetPriorityReinforcementFleets(AITurnContext context)
        {
            List<Fleet> fleets = new List<Fleet>();
            AddPriorityFleet(fleets, GetPriorityDefenseFleet(context));

            IReadOnlyList<Fleet> attackFleets = GetPriorityAttackFleets(context);
            foreach (Fleet attackFleet in attackFleets)
                AddPriorityFleet(fleets, attackFleet);

            foreach (Fleet colonizationFleet in GetPriorityColonizationFleets(context))
                AddPriorityFleet(fleets, colonizationFleet);

            foreach (Fleet assemblyFleet in GetFleetAssemblyFleets(context))
                AddPriorityFleet(fleets, assemblyFleet);

            return fleets;
        }

        /// <summary>
        /// Adds a fleet to a priority list without duplication.
        /// </summary>
        /// <param name="fleets">The priority list.</param>
        /// <param name="fleet">The fleet to add.</param>
        private void AddPriorityFleet(List<Fleet> fleets, Fleet fleet)
        {
            if (fleet != null && fleets.All(candidate => candidate.InstanceID != fleet.InstanceID))
                fleets.Add(fleet);
        }

        /// <summary>
        /// Returns the defense fleet with the greatest reinforcement need.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The priority defense fleet, or null.</returns>
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

        /// <summary>
        /// Returns active attack fleets ordered by proximity to campaign readiness.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The ordered attack fleets that can receive reinforcements.</returns>
        private IReadOnlyList<Fleet> GetPriorityAttackFleets(AITurnContext context)
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
                    context.Assessment.GetFleetAttackReadinessGateCount(
                        candidate.Fleet,
                        candidate.Target
                    )
                )
                .ThenByDescending(candidate =>
                    context.Assessment.GetOwnedSystemPresenceRatio(
                        context.Assessment.GetPlanetSystemId(candidate.Target)
                    )
                )
                .ThenByDescending(candidate => candidate.Target?.IsHeadquarters == true)
                .ThenByDescending(candidate => context.Assessment.GetPlanetValue(candidate.Target))
                .ThenBy(candidate => candidate.Fleet.InstanceID, StringComparer.Ordinal)
                .Select(candidate => candidate.Fleet)
                .ToList();
        }

        /// <summary>
        /// Returns the primary colonization fleet for reinforcement.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The colonization fleet, or null.</returns>
        private IReadOnlyList<Fleet> GetPriorityColonizationFleets(AITurnContext context)
        {
            return context
                .Assessment.OwnedFleets.Where(fleet =>
                    fleet.RoleType == FleetRoleType.Colonization && CanReinforceFleet(fleet)
                )
                .OrderByDescending(fleet => fleet.GetCurrentRegimentCount())
                .ThenByDescending(fleet => fleet.GetRegimentCapacity())
                .ThenBy(fleet => fleet.InstanceID, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Returns stationary battle fleets that can be developed for future campaigns.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The eligible assembly fleets, weakest first.</returns>
        private IReadOnlyList<Fleet> GetFleetAssemblyFleets(AITurnContext context)
        {
            return context
                .Assessment.OwnedFleets.Where(fleet =>
                    fleet.RoleType == FleetRoleType.Battle
                    && CanReinforceFleet(fleet)
                    && fleet.Order == null
                    && context.Assessment.CanFleetDepartHeadquarters(fleet)
                )
                .OrderBy(context.Assessment.GetProjectedFleetCombatValue)
                .ThenBy(fleet => fleet.GetRegimentCapacity())
                .ThenBy(fleet => fleet.InstanceID, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Adds capital ship demand for a fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        /// <param name="fleet">The fleet to inspect.</param>
        private void AddFleetCapitalShipDemand(
            AITurnContext context,
            List<AIDemand> demands,
            Fleet fleet
        )
        {
            Planet targetPlanet = GetAttackTargetPlanet(context, fleet);
            bool isColonizationFleet = fleet.RoleType == FleetRoleType.Colonization;
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
                    ? context.Assessment.GetRequiredAttackCombatStrength(targetPlanet)
                : defenseTarget != null
                    ? context.Assessment.GetRequiredDefenseStrength(defenseTarget)
                : isColonizationFleet || isColonizationOrder ? projectedCombat
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
                targetPlanet == null || isColonizationFleet
                    ? 0
                    : context.Assessment.GetRequiredBombardmentStrength(targetPlanet);
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
            else if (!isColonizationFleet && NeedsInterdictionCapitalShip(context, fleet))
            {
                capitalShipRole = AICapitalShipProductionRole.Interdiction;
                deficit = 1;
                target = 1;
            }
            else
            {
                return;
            }

            demands.Add(
                CreateFleetDemand(
                    context,
                    AIDemandKind.FleetCapitalShip,
                    ManufacturingType.Ship,
                    fleet,
                    deficit,
                    target,
                    isColonizationFleet
                        ? context.Game.Config.AI.Infrastructure.ColonizationFleetDemandPercent
                        : context.Game.Config.AI.Infrastructure.FleetCapitalShipDemandPercent,
                    capitalShipRole
                )
            );
        }

        /// <summary>
        /// Returns whether a battle fleet should add an interdiction-capable capital ship.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">Fleet to inspect.</param>
        /// <returns>True when an unlocked gravity-well ship is needed.</returns>
        private static bool NeedsInterdictionCapitalShip(AITurnContext context, Fleet fleet)
        {
            bool supportsInterdiction =
                fleet.Order == null
                || fleet.Order.OrderType is FleetOrderType.Attack or FleetOrderType.Defend;
            if (
                !supportsInterdiction
                || fleet.GetChildren<CapitalShip>().Any(capitalShip => capitalShip.HasGravityWell)
            )
                return false;

            return context
                .Faction.GetUnlockedTechnologies(ManufacturingType.Ship)
                .Any(technology =>
                    technology.GetReference() is CapitalShip capitalShip
                    && IManufacturable.CanBeManufacturedBy(capitalShip, context.Faction.InstanceID)
                    && capitalShip.HasGravityWell
                    && !capitalShip.CanDestroyPlanets
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
            List<AIDemand> demands,
            Fleet fleet
        )
        {
            if (fleet.RoleType == FleetRoleType.Colonization)
                return;

            int targetCount = GetTargetStarfighterCount(context, fleet);
            int deficit = targetCount - fleet.GetCurrentStarfighterCount();
            if (deficit <= 0)
                return;

            demands.Add(
                CreateFleetDemand(
                    context,
                    AIDemandKind.FleetStarfighter,
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
            List<AIDemand> demands,
            Fleet fleet
        )
        {
            int targetCount = Math.Min(
                fleet.GetRegimentCapacity(),
                GetDesiredRegimentCount(context, fleet)
            );
            int deficit = targetCount - fleet.GetCurrentRegimentCount();
            if (deficit <= 0)
                return;

            demands.Add(
                CreateFleetDemand(
                    context,
                    AIDemandKind.FleetRegiment,
                    ManufacturingType.Troop,
                    fleet,
                    deficit,
                    targetCount,
                    fleet.RoleType == FleetRoleType.Colonization
                        ? context.Game.Config.AI.Infrastructure.ColonizationFleetDemandPercent
                        : context.Game.Config.AI.Infrastructure.FleetRegimentDemandPercent
                )
            );
        }

        /// <summary>
        /// Adds mine and refinery demands.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        private void AddResourceBalanceDemand(AITurnContext context, List<AIDemand> demands)
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            if (!NeedsEconomyExpansion(context))
                return;

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
                        AIDemandKind.Mine,
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
                        AIDemandKind.Refinery,
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
        /// Returns whether resource production is constraining manufacturing or maintenance.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when the faction has unmet material requests or insufficient headroom.</returns>
        private bool NeedsEconomyExpansion(AITurnContext context)
        {
            return context.Assessment.PendingRawMaterialRequestCount > 0
                || context.Assessment.PendingRefinedMaterialRequestCount > 0
                || GetProjectedRefinedMaterialPercent(context)
                    <= context.Game.Config.AI.Selection.RefinedMaterialEconomyWarningPercent
                || context.Assessment.ProjectedMaintenanceHeadroom
                    < context.Game.Config.AI.Selection.MinimumMaintenanceHeadroomAfterProduction;
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
                return Math.Min(
                    economyBatchSize,
                    Math.Min(plannedRefineries - plannedMines, rawResourceNodes - plannedMines)
                );

            if (plannedRefineries == plannedMines)
                return Math.Min(economyBatchSize, rawResourceNodes - plannedMines);

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

            return Math.Min(economyBatchSize, desiredRefineries - plannedRefineries);
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
            return Math.Max(config.EconomyDefaultBatchSize, Math.Max(0, economyLaneBudget));
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
        private AIDemand CreateBuildingDemand(
            AITurnContext context,
            AIDemandKind kind,
            BuildingType buildingType,
            Planet target,
            int deficit,
            int targetCount,
            int baseDemandPercent
        )
        {
            return new AIDemand(
                AIDemand.CreateId(context.Faction.InstanceID, kind, target.InstanceID),
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
        private AIDemand CreateFleetDemand(
            AITurnContext context,
            AIDemandKind kind,
            ManufacturingType manufacturingType,
            Fleet fleet,
            int deficit,
            int targetCount,
            int baseDemandPercent,
            AICapitalShipProductionRole capitalShipRole = AICapitalShipProductionRole.None
        )
        {
            return new AIDemand(
                AIDemand.CreateId(context.Faction.InstanceID, kind, fleet.InstanceID),
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

            return GetBuildingDestinationPlanets(context)
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

            List<Planet> preferredTargets = GetBuildingDestinationPlanets(context)
                .Where(planet => !excludedPlanetIds.Contains(planet.InstanceID))
                .OrderBy(planet => planet.GetTotalBuildingTypeCount(BuildingType.Refinery))
                .ThenByDescending(planet => planet.GetAvailableEnergy())
                .ThenBy(planet => planet.InstanceID)
                .Take(count)
                .ToList();

            if (preferredTargets.Count >= count)
                return preferredTargets;

            preferredTargets.AddRange(
                GetBuildingDestinationPlanets(context)
                    .Where(planet => excludedPlanetIds.Contains(planet.InstanceID))
                    .OrderBy(planet => planet.GetTotalBuildingTypeCount(BuildingType.Refinery))
                    .ThenByDescending(planet => planet.GetAvailableEnergy())
                    .ThenBy(planet => planet.InstanceID)
                    .Take(count - preferredTargets.Count)
            );

            return preferredTargets;
        }

        /// <summary>
        /// Returns eligible production-facility destinations in strategic-value order.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="primaryDemand">The production demand driving the expansion.</param>
        /// <param name="manufacturingType">The manufacturing category to expand.</param>
        /// <param name="buildingType">The production-facility type to expand.</param>
        /// <param name="placementScorer">The turn-scoped infrastructure placement scorer.</param>
        /// <param name="includePending">Whether planets with pending matching facilities remain eligible.</param>
        /// <returns>The ranked eligible planets.</returns>
        private IReadOnlyList<Planet> FindFacilityTargetPlanets(
            AITurnContext context,
            AIDemand primaryDemand,
            ManufacturingType manufacturingType,
            BuildingType buildingType,
            AIInfrastructurePlacementScorer placementScorer,
            bool includePending
        )
        {
            Planet demandPlanet = GetDemandPlanet(context, primaryDemand);
            List<Planet> candidates = GetBuildingDestinationPlanets(context)
                .Where(planet =>
                    includePending || !HasPendingFacility(context, planet, buildingType)
                )
                .ToList();
            return placementScorer.RankDestinations(
                candidates,
                demandPlanet,
                manufacturingType,
                buildingType,
                planet => GetAvailableFacilityExpansionEnergy(context, planet)
            );
        }

        /// <summary>
        /// Returns energy available for additional production facilities.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>The available energy.</returns>
        private int GetAvailableFacilityExpansionEnergy(AITurnContext context, Planet planet)
        {
            return Math.Max(
                0,
                planet.GetAvailableEnergy()
                    - context.Assessment.GetPlanetaryDefenseEnergyDeficit(planet)
            );
        }

        /// <summary>
        /// Resolves the live destination planet for a demand.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demand">The production demand.</param>
        /// <returns>The destination planet, or null.</returns>
        private Planet GetDemandPlanet(AITurnContext context, AIDemand demand)
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
            AIDemandKind kind,
            int deficit,
            int targetCount,
            int baseDemandPercent
        )
        {
            double pressure = GetBasePressure(baseDemandPercent, deficit, targetCount);

            if (kind is AIDemandKind.Mine or AIDemandKind.Refinery)
            {
                pressure += GetEconomyMaintenancePressure(context);
                pressure += GetEconomyRefinedMaterialPressure(context);
                return pressure;
            }

            return ClampPressure(pressure);
        }

        /// <summary>
        /// Returns extra economy pressure as uncommitted refined materials approach the reserve.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The refined-material economy pressure.</returns>
        private double GetEconomyRefinedMaterialPressure(AITurnContext context)
        {
            GameConfig.AISelectionConfig config = context.Game.Config.AI.Selection;
            int reservePercent = Math.Max(0, config.RefinedMaterialReservePercent);
            int warningPercent = Math.Max(
                reservePercent,
                config.RefinedMaterialEconomyWarningPercent
            );
            int projectedPercent = GetProjectedRefinedMaterialPercent(context);
            if (projectedPercent >= warningPercent)
                return 0;

            int pressureRange = Math.Max(1, warningPercent - reservePercent);
            double urgency = Math.Min(
                1,
                Math.Max(0, warningPercent - projectedPercent) / (double)pressureRange
            );
            return Math.Max(0, config.RefinedMaterialEconomyPressureWeight) * urgency;
        }

        /// <summary>
        /// Returns projected uncommitted refined materials as a percentage of supply.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The projected refined-material percentage.</returns>
        private int GetProjectedRefinedMaterialPercent(AITurnContext context)
        {
            long projectedStockpile = Math.Max(
                0,
                (long)context.Assessment.RefinedMaterialStockpile
                    - context.Assessment.NearTermRefinedMaterialCommitment
            );
            int supply = context.Assessment.RefinedMaterialSupply;
            if (supply <= 0)
                return projectedStockpile > 0 ? 100 : 0;

            return (int)Math.Min(100, projectedStockpile * 100 / supply);
        }

        /// <summary>
        /// Returns production pressure for a planet's defensive requirement.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The planet requiring defense.</param>
        /// <param name="baseDemandPercent">Base pressure for the demand.</param>
        /// <param name="deficit">Current defense deficit.</param>
        /// <param name="targetCount">Target defense count.</param>
        /// <param name="isInitialShield">Whether the demand establishes the first shield.</param>
        /// <returns>The defense pressure.</returns>
        private double GetPlanetaryDefensePressure(
            AITurnContext context,
            Planet planet,
            int baseDemandPercent,
            int deficit,
            int targetCount,
            bool isInitialShield = false
        )
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            double highestPlanetValue = context.Assessment.GetHighestOwnedPlanetValue();
            double pressure =
                baseDemandPercent
                + config.PlanetaryDefenseDeficitPressureWeight * deficit / Math.Max(1, targetCount);

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

            double boundedPressure = ClampPressure(pressure);
            return isInitialShield
                ? boundedPressure
                    + config.PlanetaryShieldInstabilityPressureWeight
                        * (
                            planet.GetOpposingPopularSupport(context.Faction.InstanceID) / 100.0
                            + context.Assessment.GetDefensiveSupportRisk(planet)
                        )
                : boundedPressure;
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
            AIDemandKind kind,
            Fleet fleet,
            int deficit,
            int targetCount,
            int baseDemandPercent
        )
        {
            double pressure = GetBasePressure(baseDemandPercent, deficit, targetCount);
            Planet targetPlanet = context.Assessment.GetAttackTargetPlanet(fleet);

            if (targetPlanet != null)
            {
                pressure += GetTargetValuePressure(context, targetPlanet);
                pressure += GetFleetReadinessPressure(context, kind, fleet, targetPlanet);
                pressure += GetFinalReadinessGatePressure(context, fleet, targetPlanet, deficit);
                if (kind is AIDemandKind.FleetCapitalShip or AIDemandKind.FleetRegiment)
                {
                    pressure += context
                        .Game
                        .Config
                        .AI
                        .Infrastructure
                        .AttackFleetReinforcementPressureBonus;
                }
            }

            if (kind == AIDemandKind.FleetStarfighter)
                pressure += GetStarfighterFillPressure(context, fleet, targetCount);

            return pressure;
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
            int deficitPercent = deficit * 100 / Math.Max(1, targetCount);
            return Math.Min(100, baseDemandPercent + deficitPercent);
        }

        /// <summary>
        /// Returns extra economy pressure from maintenance headroom.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The economy maintenance pressure.</returns>
        private double GetEconomyMaintenancePressure(AITurnContext context)
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            int headroom = context.Assessment.ProjectedMaintenanceHeadroom;
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
                / Math.Max(1, reserve);
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
            AIDemandKind kind,
            Fleet fleet,
            Planet targetPlanet
        )
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            int requiredCombat = context.Assessment.GetRequiredAttackCombatStrength(targetPlanet);
            int requiredRegiments = context.Assessment.GetProjectedRequiredAttackRegimentCount(
                fleet,
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
                AIDemandKind.FleetRegiment => config.FleetReadinessPressureWeight
                    * (combatReadiness + capacityReadiness)
                    / 2,
                AIDemandKind.FleetCapitalShip => config.FleetReadinessPressureWeight
                    * (regimentReadiness + capacityReadiness)
                    / 2,
                AIDemandKind.FleetStarfighter => config.FleetReadinessPressureWeight
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

            int requiredCombat = context.Assessment.GetRequiredAttackCombatStrength(targetPlanet);
            int requiredRegiments = context.Assessment.GetProjectedRequiredAttackRegimentCount(
                fleet,
                targetPlanet
            );
            bool combatReady =
                context.Assessment.GetProjectedFleetCombatValue(fleet) >= requiredCombat;
            bool capacityReady =
                context.Assessment.GetFleetRegimentCapacity(fleet) >= requiredRegiments;
            bool bombardmentReady =
                context.Assessment.GetProjectedFleetBombardmentStrength(fleet)
                >= context.Assessment.GetRequiredBombardmentStrength(targetPlanet);

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

            return Math.Max(0, Math.Min(1, value / target));
        }

        /// <summary>
        /// Clamps pressure to the standard demand-scoring range.
        /// </summary>
        /// <param name="pressure">Pressure to clamp.</param>
        /// <returns>The clamped pressure.</returns>
        private double ClampPressure(double pressure)
        {
            return Math.Max(0, Math.Min(100, pressure));
        }

        /// <summary>
        /// Returns whether a fleet can receive reinforcement demand.
        /// </summary>
        /// <param name="fleet">Fleet to inspect.</param>
        /// <returns>True if the fleet can receive reinforcement.</returns>
        private bool CanReinforceFleet(Fleet fleet)
        {
            return fleet?.RoleType is FleetRoleType.Battle or FleetRoleType.Colonization
                && fleet.Movement == null
                && (
                    HasPresentOrUnderConstructionCapitalShips(fleet)
                    || fleet.Order?.OrderType is FleetOrderType.Attack or FleetOrderType.Defend
                );
        }

        /// <summary>
        /// Resolves the defense target assigned to a fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <returns>The defense target, or null.</returns>
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
            return fleet?.GetChildren<CapitalShip>().Any(IsCommittedCapitalShip) == true;
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
            return Math.Min(
                capacity,
                IntegerMath.ScaleByPercentRoundedUp(
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

            if (fleet.RoleType == FleetRoleType.Colonization)
            {
                return Math.Max(
                    context.Game.Config.AI.FleetDeployment.ColonizationFleetMinimumRegimentCount,
                    context.Game.Config.AI.FleetDeployment.ColonizationFleetMaximumRegimentCount
                );
            }

            int capacity = fleet.GetRegimentCapacity();
            int fillTarget = IntegerMath.ScaleByPercentRoundedUp(
                capacity,
                context.Game.Config.AI.Infrastructure.AssaultRegimentLoadPercent
            );
            Planet targetPlanet = context.Assessment.GetAttackTargetPlanet(fleet);
            if (targetPlanet != null)
                fillTarget = Math.Max(
                    fillTarget,
                    context.Assessment.GetProjectedRequiredAttackRegimentCount(fleet, targetPlanet)
                );

            if (
                targetPlanet != null
                && context.Assessment.GetProjectedFleetRegimentAttackStrength(fleet)
                    < context.Assessment.GetProjectedRequiredAttackRegimentStrength(
                        fleet,
                        targetPlanet
                    )
            )
            {
                int currentCount = fleet.GetCurrentRegimentCount();
                int currentStrength = context.Assessment.GetProjectedFleetRegimentAttackStrength(
                    fleet
                );
                int requiredStrength =
                    context.Assessment.GetProjectedRequiredAttackRegimentStrength(
                        fleet,
                        targetPlanet
                    );
                int estimatedStrengthPerRegiment = Math.Max(
                    1,
                    currentCount > 0 ? currentStrength / currentCount : requiredStrength
                );
                int strengthDeficitCount = IntegerMath.DivideRoundedUp(
                    requiredStrength - currentStrength,
                    estimatedStrengthPerRegiment
                );
                fillTarget = Math.Max(fillTarget, currentCount + strengthDeficitCount);
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

            int captureFloor = context.Game.Config.Combat.PlanetaryAssault.CaptureGarrisonCount;
            if (!planet.IsHeadquarters && !context.Assessment.IsPlanetThreatened(planet))
            {
                captureFloor = IntegerMath.ScaleByPercent(
                    captureFloor,
                    context.Game.Config.AI.Garrison.InteriorCaptureFloorPercent
                );
            }

            return Math.Max(captureFloor, stabilityTarget);
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
    }
}
