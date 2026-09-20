using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Core;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Util.Common;

namespace Rebellion.AI.Production
{
    /// <summary>
    /// Defines production-capacity requirements shared by construction and retirement.
    /// </summary>
    internal sealed class AIProductionCapacityRequirements
    {
        internal readonly struct FacilityPortfolio
        {
            internal int ConstructionFacilities { get; }
            internal int Shipyards { get; }
            internal int TrainingFacilities { get; }
            internal int StaticDefenses { get; }
            internal int Total =>
                ConstructionFacilities + Shipyards + TrainingFacilities + StaticDefenses;

            /// <summary>
            /// Creates a strategic-facility portfolio snapshot.
            /// </summary>
            /// <param name="constructionFacilities">Construction-facility count.</param>
            /// <param name="shipyards">Shipyard count.</param>
            /// <param name="trainingFacilities">Training-facility count.</param>
            /// <param name="staticDefenses">Static-defense count.</param>
            internal FacilityPortfolio(
                int constructionFacilities,
                int shipyards,
                int trainingFacilities,
                int staticDefenses
            )
            {
                ConstructionFacilities = constructionFacilities;
                Shipyards = shipyards;
                TrainingFacilities = trainingFacilities;
                StaticDefenses = staticDefenses;
            }
        }

        /// <summary>
        /// Captures the projected strategic-facility mix once for a production turn.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Counts for productive and static-defense facilities.</returns>
        internal FacilityPortfolio BuildPortfolio(AITurnContext context)
        {
            int constructionFacilities = 0;
            int shipyards = 0;
            int trainingFacilities = 0;
            int staticDefenses = 0;
            foreach (Planet planet in context.Assessment.OwnedPlanets)
            {
                foreach (Building building in context.Assessment.GetPlanetBuildings(planet))
                {
                    if (building.GetOwnerInstanceID() != context.Faction.InstanceID)
                        continue;
                    switch (building.GetBuildingType())
                    {
                        case BuildingType.ConstructionFacility:
                            constructionFacilities++;
                            break;
                        case BuildingType.Shipyard:
                            shipyards++;
                            break;
                        case BuildingType.TrainingFacility:
                            trainingFacilities++;
                            break;
                        case BuildingType.Defense:
                        case BuildingType.Weapon:
                            staticDefenses++;
                            break;
                    }
                }
            }
            return new FacilityPortfolio(
                constructionFacilities,
                shipyards,
                trainingFacilities,
                staticDefenses
            );
        }

        /// <summary>
        /// Returns the strategic facility quantity required by the faction's current planet count.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="buildingType">The production-facility category.</param>
        /// <returns>The required faction-wide facility count.</returns>
        internal int GetDesiredFacilityCount(AITurnContext context, BuildingType buildingType)
        {
            if (context?.Assessment == null || context.Game?.Config?.AI?.Infrastructure == null)
                return 0;

            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            int planetsPerFacility = GetPlanetsPerFacility(config, buildingType);
            if (planetsPerFacility <= 0)
                return 0;

            int desiredCount = IntegerMath.DivideRoundedUp(
                context.Assessment.OwnedPlanets.Count,
                planetsPerFacility
            );
            return buildingType == BuildingType.ConstructionFacility
                ? Math.Max(
                    desiredCount,
                    Math.Min(
                        context.Assessment.OwnedPlanets.Count,
                        config.MinimumConstructionFacilityLanes
                    )
                )
                : desiredCount;
        }

        /// <summary>
        /// Returns the configured number of planets supported by one facility category.
        /// </summary>
        /// <param name="config">Infrastructure configuration.</param>
        /// <param name="buildingType">The production-facility category.</param>
        /// <returns>The planets-per-facility ratio, or zero for non-production facilities.</returns>
        private static int GetPlanetsPerFacility(
            GameConfig.AIInfrastructureConfig config,
            BuildingType buildingType
        )
        {
            return buildingType switch
            {
                BuildingType.ConstructionFacility => config.PlanetsPerConstructionFacility,
                BuildingType.Shipyard => config.PlanetsPerShipyard,
                BuildingType.TrainingFacility => config.PlanetsPerTrainingFacility,
                _ => 0,
            };
        }

        /// <summary>
        /// Adds production-facility expansion demands.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        /// <param name="placementScorer">The turn-scoped infrastructure placement scorer.</param>
        /// <param name="facilityPortfolio">The turn-scoped facility portfolio.</param>
        internal void AddProductionFacilityRequirements(
            AITurnContext context,
            List<AIProductionRequirement> demands,
            AIInfrastructurePlacementScorer placementScorer,
            FacilityPortfolio facilityPortfolio
        )
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            AddProductionFacilityDemand(
                context,
                demands,
                ManufacturingType.Ship,
                AIProductionRequirementKind.Shipyard,
                BuildingType.Shipyard,
                config.ShipyardDemandPercent,
                placementScorer,
                facilityPortfolio
            );
            AddProductionFacilityDemand(
                context,
                demands,
                ManufacturingType.Building,
                AIProductionRequirementKind.ConstructionFacility,
                BuildingType.ConstructionFacility,
                config.ConstructionFacilityDemandPercent,
                placementScorer,
                facilityPortfolio
            );
            AddProductionFacilityDemand(
                context,
                demands,
                ManufacturingType.Troop,
                AIProductionRequirementKind.TrainingFacility,
                BuildingType.TrainingFacility,
                config.TrainingFacilityDemandPercent,
                placementScorer,
                facilityPortfolio
            );
        }

        /// <summary>
        /// Adds available production-facility upgrade demands.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        internal void AddProductionFacilityUpgradeRequirements(
            AITurnContext context,
            List<AIProductionRequirement> demands
        )
        {
            foreach (
                Planet planet in context
                    .Assessment.OwnedPlanets.Where(planet =>
                        planet?.IsColonized == true && !planet.IsDestroyed
                    )
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

        /// <summary>
        /// Adds an upgrade demand for one manufacturing category.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">The demand list to update.</param>
        /// <param name="planet">The planet whose facilities are evaluated.</param>
        /// <param name="buildingType">The production-facility type to upgrade.</param>
        private void AddProductionFacilityUpgradeDemand(
            AITurnContext context,
            List<AIProductionRequirement> demands,
            Planet planet,
            BuildingType buildingType
        )
        {
            if (HasPendingFacility(context, planet, buildingType))
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

            AIProductionRequirement demand = new AIProductionRequirement(
                AIProductionRequirement.CreateId(
                    context.Faction.InstanceID,
                    AIProductionRequirementKind.BuildingUpgrade,
                    buildingType,
                    planet.InstanceID,
                    replacement.InstanceID
                ),
                AIProductionRequirementKind.BuildingUpgrade,
                ManufacturingType.Building,
                buildingType,
                planet,
                1,
                GetProductionFacilityUpgradePressure(context, planet),
                buildingToReplace: replacement
            );
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
            GameConfig.AIProductionDemandUtilityConfig utility = config.DemandUtility;
            double pressure = config.ProductionFacilityUpgradeDemandPercent;
            double highestPlanetValue = context.Assessment.GetHighestOwnedPlanetValue();
            if (highestPlanetValue > 0)
            {
                pressure += AIUtility.EvaluatePressure(
                    context.Assessment.GetPlanetValue(planet) / highestPlanetValue,
                    utility.UpgradeValue
                );
            }

            pressure += AIUtility.EvaluatePressure(
                context.Assessment.IsFactionHeadquarters(planet) ? 1 : 0,
                utility.UpgradeHeadquarters
            );

            return Math.Max(0, Math.Min(100, pressure));
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
        /// <param name="placementScorer">The turn-scoped infrastructure placement scorer.</param>
        /// <param name="facilityPortfolio">The turn-scoped facility portfolio.</param>
        private void AddProductionFacilityDemand(
            AITurnContext context,
            List<AIProductionRequirement> demands,
            ManufacturingType manufacturingType,
            AIProductionRequirementKind kind,
            BuildingType buildingType,
            int baseDemandPercent,
            AIInfrastructurePlacementScorer placementScorer,
            FacilityPortfolio facilityPortfolio
        )
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            List<AIProductionRequirement> productionDemands = demands
                .Where(demand => demand.ManufacturingType == manufacturingType)
                .Where(demand => demand.Kind != kind)
                .OrderByDescending(demand => demand.Pressure)
                .ThenBy(demand => demand.Id, StringComparer.Ordinal)
                .ToList();
            int desiredFacilityCount = GetDesiredFacilityCount(context, buildingType);
            if (buildingType == BuildingType.TrainingFacility)
            {
                int demandCapacityTarget = IntegerMath.DivideRoundedUp(
                    productionDemands.Count,
                    Math.Max(1, config.TrainingDemandsPerFacility)
                );
                desiredFacilityCount = Math.Max(desiredFacilityCount, demandCapacityTarget);
            }
            int hubTarget =
                buildingType == BuildingType.Shipyard
                    ? context.Game.Config.AI.Infrastructure.ShipyardSectorHubTargetCount
                    : context.Game.Config.AI.Infrastructure.FacilitySectorHubTargetCount;
            List<IGrouping<string, Planet>> sectors = context
                .Assessment.OwnedPlanets.Where(planet =>
                    planet?.IsColonized == true && !planet.IsDestroyed
                    || (
                        buildingType == BuildingType.ConstructionFacility
                        && planet?.IsDestroyed == false
                        && planet.GetParentOfType<PlanetSector>()?.SectorType
                            == PlanetSectorType.OuterRim
                    )
                )
                .GroupBy(context.Assessment.GetPlanetSystemId)
                .OrderByDescending(sector =>
                    GetSectorFacilityDeficit(sector, buildingType, hubTarget)
                )
                .ThenByDescending(sector => GetColonyFoundationInput(sector, buildingType))
                .ThenBy(group => group.Key, StringComparer.Ordinal)
                .ToList();
            int remainingFacilityCount = Math.Max(
                0,
                desiredFacilityCount - GetOwnedFacilityCount(context, buildingType)
            );
            if (buildingType == BuildingType.ConstructionFacility)
                remainingFacilityCount = Math.Max(
                    remainingFacilityCount,
                    CountUnseededOuterRimSectors(sectors)
                );
            if (remainingFacilityCount == 0)
                return;

            double categoryBalancePressure = GetFacilityCategoryBalancePressure(
                sectors,
                buildingType,
                hubTarget,
                config.DemandUtility.FacilityBalance
            );
            foreach (IGrouping<string, Planet> sector in sectors)
            {
                if (remainingFacilityCount == 0)
                    break;

                List<Planet> sectorPlanets = sector
                    .Where(planet => GetAvailableFacilityExpansionEnergy(planet) > 0)
                    .ToList();
                if (sectorPlanets.Count == 0)
                    continue;
                AIProductionRequirement sectorDemand =
                    productionDemands.FirstOrDefault(demand =>
                        context.Assessment.GetPlanetSystemId(GetDemandPlanet(context, demand))
                        == sector.Key
                    ) ?? productionDemands.FirstOrDefault();
                Planet demandPlanet = GetDemandPlanet(context, sectorDemand) ?? sector.First();
                IReadOnlyList<(Planet Planet, double Score)> rankedDestinations =
                    placementScorer.ScoreDestinations(
                        sectorPlanets,
                        demandPlanet,
                        manufacturingType,
                        buildingType,
                        GetAvailableFacilityExpansionEnergy
                    );
                if (rankedDestinations.Count == 0)
                    continue;

                double colonyFoundationInput = GetColonyFoundationInput(sector, buildingType);
                int sectorFacilityCount = sector.Sum(planet =>
                    planet.GetTotalBuildingTypeCount(buildingType)
                );
                int targetCount = sectorFacilityCount > 0 ? hubTarget : 1;
                int sectorDeficit = GetSectorFacilityDeficit(sector, buildingType, hubTarget);
                if (colonyFoundationInput > 0)
                    sectorDeficit = Math.Max(1, sectorDeficit);
                int requestedQuantity = Math.Min(remainingFacilityCount, sectorDeficit);
                if (requestedQuantity <= 0)
                    continue;

                FacilityPortfolio pressurePortfolio =
                    colonyFoundationInput > 0 ? default : facilityPortfolio;
                double strategicBonus =
                    AIUtility.EvaluatePressure(
                        sectorFacilityCount == 0 ? 1 : 0,
                        config.DemandUtility.SectorCoverage
                    )
                    + AIUtility.EvaluatePressure(
                        sectorFacilityCount > 0 ? 1 : 0,
                        config.DemandUtility.PrimaryHub
                    )
                    + AIUtility.EvaluatePressure(
                        colonyFoundationInput,
                        config.DemandUtility.ColonyFoundation
                    )
                    + categoryBalancePressure;
                string demandId = AIProductionRequirement.CreateId(
                    context.Faction.InstanceID,
                    kind,
                    sector.Key,
                    "capacity"
                );
                int alternativeCount = Math.Max(1, config.FacilityPlanetsPerSector);
                bool addedAlternative = false;
                foreach (
                    (Planet destination, double placementUtility) in rankedDestinations.Take(
                        alternativeCount
                    )
                )
                {
                    addedAlternative |= AddSectorFacilityDemand(
                        context,
                        demands,
                        sectorDemand,
                        kind,
                        buildingType,
                        destination,
                        targetCount,
                        baseDemandPercent,
                        strategicBonus,
                        pressurePortfolio,
                        requestedQuantity,
                        demandId,
                        placementUtility
                    );
                }
                if (addedAlternative)
                    remainingFacilityCount -= requestedQuantity;
            }
        }

        /// <summary>
        /// Returns the amount required to seed or complete the strongest facility cluster in a sector.
        /// </summary>
        /// <param name="sector">The planets in one system.</param>
        /// <param name="buildingType">The facility category.</param>
        /// <param name="hubTarget">The desired concentrated facility count.</param>
        /// <returns>The outstanding sector facility quantity.</returns>
        private static int GetSectorFacilityDeficit(
            IEnumerable<Planet> sector,
            BuildingType buildingType,
            int hubTarget
        )
        {
            List<Planet> planets = sector.ToList();
            if (planets.Count == 0)
                return 0;

            int strongestCluster = planets.Max(planet =>
                planet.GetTotalBuildingTypeCount(buildingType)
            );
            return strongestCluster == 0 ? 1 : Math.Max(0, hubTarget - strongestCluster);
        }

        /// <summary>
        /// Counts Outer Rim systems that still lack construction capacity.
        /// </summary>
        /// <param name="sectors">Owned planets grouped by system.</param>
        /// <returns>The number of unseeded Outer Rim systems.</returns>
        private static int CountUnseededOuterRimSectors(
            IEnumerable<IGrouping<string, Planet>> sectors
        )
        {
            return sectors.Count(sector =>
                sector.FirstOrDefault()?.GetParentOfType<PlanetSector>()?.SectorType
                    == PlanetSectorType.OuterRim
                && sector.Sum(planet =>
                    planet.GetTotalBuildingTypeCount(BuildingType.ConstructionFacility)
                ) == 0
            );
        }

        /// <summary>
        /// Returns the normalized need for founding construction capacity in an Outer Rim sector.
        /// </summary>
        /// <param name="sector">Owned planets in one sector.</param>
        /// <param name="buildingType">The facility category being considered.</param>
        /// <returns>One before the first construction yard and zero otherwise.</returns>
        private static double GetColonyFoundationInput(
            IEnumerable<Planet> sector,
            BuildingType buildingType
        )
        {
            if (buildingType != BuildingType.ConstructionFacility)
                return 0;

            List<Planet> planets = sector.ToList();
            if (
                planets.Count == 0
                || planets[0].GetParentOfType<PlanetSector>()?.SectorType
                    != PlanetSectorType.OuterRim
            )
            {
                return 0;
            }

            int constructionFacilityCount = planets.Sum(planet =>
                planet.GetTotalBuildingTypeCount(BuildingType.ConstructionFacility)
            );
            return 1 - AIUtility.Fulfillment(constructionFacilityCount, 1);
        }

        /// <summary>
        /// Returns pressure that keeps production-facility categories advancing at comparable
        /// rates while their sector hubs are established.
        /// </summary>
        /// <param name="sectors">Owned planets grouped by system.</param>
        /// <param name="buildingType">The production facility category.</param>
        /// <param name="hubTarget">The desired facility count at each primary site.</param>
        /// <param name="consideration">The category-balance utility consideration.</param>
        /// <returns>The category balance pressure.</returns>
        private static double GetFacilityCategoryBalancePressure(
            IReadOnlyCollection<IGrouping<string, Planet>> sectors,
            BuildingType buildingType,
            int hubTarget,
            GameConfig.AIConsiderationConfig consideration
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
            return AIUtility.EvaluateCenteredPressure(1 - completion, consideration);
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
        /// <param name="facilityPortfolio">The faction's current strategic-facility mix.</param>
        /// <param name="maximumQuantity">The remaining faction-wide capacity deficit.</param>
        /// <param name="demandId">The shared identifier for alternative destinations.</param>
        /// <param name="placementUtility">The normalized utility of this destination.</param>
        /// <returns>True when an alternative demand was added.</returns>
        private bool AddSectorFacilityDemand(
            AITurnContext context,
            List<AIProductionRequirement> demands,
            AIProductionRequirement primaryDemand,
            AIProductionRequirementKind kind,
            BuildingType buildingType,
            Planet target,
            int targetCount,
            int baseDemandPercent,
            double strategicBonus,
            FacilityPortfolio facilityPortfolio,
            int maximumQuantity,
            string demandId,
            double placementUtility
        )
        {
            if (target == null || target.GetAvailableEnergy() <= 0 || targetCount <= 0)
                return false;

            int currentCount = target.GetTotalBuildingTypeCount(buildingType);
            if (currentCount >= targetCount)
                return false;

            int quantity = Math.Min(
                Math.Min(targetCount - currentCount, target.GetAvailableEnergy()),
                maximumQuantity
            );
            if (quantity <= 0)
                return false;

            double concentrationBonus = baseDemandPercent * currentCount / targetCount;
            double investmentDeficit = (double)(targetCount - currentCount) / targetCount;

            demands.Add(
                new AIProductionRequirement(
                    demandId,
                    kind,
                    ManufacturingType.Building,
                    buildingType,
                    target,
                    quantity,
                    GetProductionFacilityPressure(
                        context,
                        kind,
                        currentCount,
                        targetCount,
                        baseDemandPercent,
                        investmentDeficit,
                        facilityPortfolio
                    )
                        + strategicBonus
                        + baseDemandPercent * placementUtility
                        + concentrationBonus,
                    primaryDemand?.ProductTypeId,
                    primaryDemand?.CapitalShipRole ?? AICapitalShipProductionRole.None
                )
            );
            return true;
        }

        /// <summary>
        /// Returns pressure based on a facility category's target portfolio share.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="kind">The facility requirement category.</param>
        /// <param name="portfolio">The current facility portfolio.</param>
        /// <returns>Signed portfolio pressure.</returns>
        internal static double GetPortfolioPressure(
            AITurnContext context,
            AIProductionRequirementKind kind,
            FacilityPortfolio portfolio
        )
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            (int count, int targetPercent) = kind switch
            {
                AIProductionRequirementKind.ConstructionFacility => (
                    portfolio.ConstructionFacilities,
                    config.ConstructionFacilityPortfolioPercent
                ),
                AIProductionRequirementKind.Shipyard => (
                    portfolio.Shipyards,
                    config.ShipyardPortfolioPercent
                ),
                AIProductionRequirementKind.TrainingFacility => (
                    portfolio.TrainingFacilities,
                    config.TrainingFacilityPortfolioPercent
                ),
                AIProductionRequirementKind.PlanetaryDefense => (
                    portfolio.StaticDefenses,
                    config.StaticDefensePortfolioPercent
                ),
                _ => (0, 0),
            };
            if (portfolio.Total <= 0 || targetPercent <= 0)
                return 0;
            double deviation = (targetPercent - count * 100.0 / portfolio.Total) / targetPercent;
            double normalized = AIUtility.Fulfillment(Math.Abs(deviation), 1);
            GameConfig.AIConsiderationConfig consideration = config.DemandUtility.FacilityPortfolio;
            return deviation >= 0
                ? AIUtility.EvaluatePressure(normalized, consideration)
                : -AIUtility.EvaluatePressure(normalized, consideration);
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
        /// <param name="facilityPortfolio">The turn-scoped facility portfolio.</param>
        /// <returns>The adjusted pressure.</returns>
        private double GetProductionFacilityPressure(
            AITurnContext context,
            AIProductionRequirementKind kind,
            int currentCount,
            int desiredCount,
            int baseDemandPercent,
            double investmentDeficit,
            FacilityPortfolio facilityPortfolio
        )
        {
            int targetCount = Math.Max(currentCount + 1, desiredCount);
            int deficit = Math.Max(1, targetCount - currentCount);
            double pressure =
                baseDemandPercent
                + AIUtility.EvaluatePressure(
                    deficit / (double)targetCount,
                    context.Game.Config.AI.Infrastructure.DemandUtility.Deficit
                );
            if (kind == AIProductionRequirementKind.TrainingFacility)
                pressure += AIUtility.EvaluatePressure(
                    1,
                    context.Game.Config.AI.Infrastructure.DemandUtility.TrainingBacklog
                );

            if (kind == AIProductionRequirementKind.ConstructionFacility)
                pressure += AIUtility.EvaluatePressure(
                    investmentDeficit,
                    context.Game.Config.AI.Infrastructure.DemandUtility.FacilityInvestment
                );

            pressure += GetPortfolioPressure(context, kind, facilityPortfolio);

            return pressure;
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
        /// Returns energy available for another production facility.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>The available energy.</returns>
        private static int GetAvailableFacilityExpansionEnergy(Planet planet)
        {
            return planet?.GetAvailableEnergy() ?? 0;
        }

        /// <summary>
        /// Resolves the destination planet for a production requirement.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="requirement">The production requirement.</param>
        /// <returns>The destination planet, or null.</returns>
        private static Planet GetDemandPlanet(
            AITurnContext context,
            AIProductionRequirement requirement
        )
        {
            return requirement?.DestinationPlanet
                ?? context.Assessment.GetFleetPlanet(requirement?.DestinationFleet);
        }

        /// <summary>
        /// Returns the current and queued count for one facility type.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="buildingType">The facility type.</param>
        /// <returns>The owned facility count.</returns>
        private static int GetOwnedFacilityCount(AITurnContext context, BuildingType buildingType)
        {
            return context.Assessment.OwnedPlanets.Sum(planet =>
                planet.GetTotalBuildingTypeCount(buildingType)
            );
        }
    }
}
