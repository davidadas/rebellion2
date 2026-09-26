using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Demands;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.AI.Scorers
{
    /// <summary>
    /// Scores production proposals.
    /// </summary>
    public sealed class AIProductionProposalScorer : IAIProposalScorer
    {
        /// <summary>
        /// Returns whether this scorer can score the proposal.
        /// </summary>
        /// <param name="proposal">The proposal to check.</param>
        /// <returns>True if the proposal is a production proposal.</returns>
        public bool CanScore(AIProposal proposal)
        {
            return proposal is AIManufactureProposal or AIFacilityRemovalProposal;
        }

        /// <summary>
        /// Returns the production proposal score.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal to score.</param>
        /// <returns>The production proposal score.</returns>
        public double Score(AITurnContext context, AIProposal proposal)
        {
            return proposal switch
            {
                AIFacilityRemovalProposal => 0,
                AIManufactureProposal manufactureProposal => ScoreManufactureProposal(
                    context,
                    manufactureProposal
                ),
                _ => 0,
            };
        }

        /// <summary>
        /// Returns the score for one manufacture proposal.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal to score.</param>
        /// <returns>The manufacture proposal score.</returns>
        private double ScoreManufactureProposal(
            AITurnContext context,
            AIManufactureProposal proposal
        )
        {
            double demandPressure = GetDemandPressure(context, proposal?.Demand);
            int maintenanceCost = proposal?.GetUnitMaintenanceCost() ?? 0;
            if (context?.Game == null || context.Faction == null || proposal == null)
                return AIUtility.Fulfillment(
                    demandPressure,
                    new GameConfig.AISelectionConfig().DemandUtility
                );

            GameConfig.AISelectionConfig config = context.Game.Config.AI.Selection;
            GameConfig.AIProductionUtilityConfig utility = config.ProductionUtility;
            AIUtilityScore score = new AIUtilityScore();
            score.Add(
                AIUtility.Fulfillment(demandPressure, config.DemandUtility),
                config.DemandUtility
            );
            double colonyFoundationInput = GetColonyFoundationInput(context, proposal);
            if (proposal.Demand.BuildingType == BuildingType.ConstructionFacility)
                score.Add(colonyFoundationInput, utility.ColonyFoundation);
            score.AddCost(
                AIUtility.Fulfillment(GetTravelCost(context, proposal), utility.TravelCost),
                utility.TravelCost
            );

            int projectedHeadroom =
                context.Assessment.ProjectedMaintenanceHeadroom - maintenanceCost;
            if (
                maintenanceCost > 0
                && proposal.Demand.RestoresMaintenanceCapacity == false
                && projectedHeadroom < proposal.GetMinimumMaintenanceHeadroom(context)
            )
                return 0;

            int headroomDeficit = config.MaintenanceHeadroomReserve - projectedHeadroom;

            score.AddCost(
                AIUtility.Fulfillment(
                    System.Math.Max(0, headroomDeficit),
                    config.MaintenanceHeadroomReserve
                ),
                utility.HeadroomRisk
            );
            score.AddCost(projectedHeadroom < 0 ? 1 : 0, utility.Shortfall);

            return score.RankValue;
        }

        /// <summary>
        /// Calculates production urgency from a demand's measured deficit and current turn facts.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demand">The production demand to evaluate.</param>
        /// <returns>The demand urgency used by production utility.</returns>
        internal static double GetDemandPressure(AITurnContext context, AIProductionDemand demand)
        {
            if (demand == null)
                return 0;
            if (context?.Game == null || context.Faction == null || context.Assessment == null)
                return 0;
            if (demand.TargetCount <= 0 && demand.BaseDemandPercent <= 0)
                return 0;

            return demand.Kind switch
            {
                AIProductionDemandKind.Colony => demand.BaseDemandPercent,
                AIProductionDemandKind.Mine or AIProductionDemandKind.Refinery =>
                    GetEconomyPressure(context, demand),
                AIProductionDemandKind.PlanetaryDefense
                or AIProductionDemandKind.PlanetaryStarfighterReserve
                or AIProductionDemandKind.GarrisonRegimentReserve => GetDefensePressure(
                    context,
                    demand
                ),
                AIProductionDemandKind.FleetSeedCapitalShip =>
                    AIFleetProductionAllocationScorer.ScoreDeficit(
                        context,
                        demand.BaseDemandPercent,
                        demand.DeficitCount,
                        demand.TargetCount
                    ),
                AIProductionDemandKind.ColonizationFleetSeedCapitalShip => demand.BaseDemandPercent,
                AIProductionDemandKind.FleetCapitalShip
                or AIProductionDemandKind.FleetStarfighter
                or AIProductionDemandKind.FleetRegiment => GetFleetPressure(context, demand),
                AIProductionDemandKind.SpecialForces => Math.Min(
                    100,
                    demand.BaseDemandPercent
                        + AIUtility.EvaluateDiscretePressure(
                            demand.DeficitCount / (double)Math.Max(1, demand.TargetCount),
                            context.Game.Config.AI.Infrastructure.DemandUtility.Deficit
                        )
                ),
                AIProductionDemandKind.BuildingUpgrade => GetUpgradePressure(context, demand),
                AIProductionDemandKind.ConstructionFacility
                or AIProductionDemandKind.Shipyard
                or AIProductionDemandKind.TrainingFacility => GetFacilityPressure(context, demand),
                _ => 0,
            };
        }

        /// <summary>
        /// Calculates urgency for expanding one production-facility category.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demand">The facility-capacity demand.</param>
        /// <returns>The facility expansion urgency.</returns>
        private static double GetFacilityPressure(AITurnContext context, AIProductionDemand demand)
        {
            Planet destination = demand.DestinationPlanet;
            if (destination == null || demand.TargetCount <= 0)
                return 0;

            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            string systemId = context.Assessment.GetPlanetSystemId(destination);
            List<IGrouping<string, Planet>> sectors = context
                .Assessment.OwnedPlanets.Where(planet =>
                    planet?.IsColonized == true && !planet.IsDestroyed
                    || (
                        demand.BuildingType == BuildingType.ConstructionFacility
                        && planet?.IsDestroyed == false
                        && planet.GetParentOfType<PlanetSector>()?.SectorType
                            == PlanetSectorType.OuterRim
                    )
                )
                .GroupBy(context.Assessment.GetPlanetSystemId)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .ToList();
            IGrouping<string, Planet> sector = sectors.FirstOrDefault(group =>
                group.Key == systemId
            );
            if (sector == null)
                return 0;

            double placementUtility = GetFacilityPlacementUtility(context, demand);
            int currentCount = destination.GetTotalBuildingTypeCount(demand.BuildingType);
            int deficit = Math.Max(1, demand.TargetCount - currentCount);
            double investmentDeficit = deficit / (double)demand.TargetCount;
            double pressure =
                demand.BaseDemandPercent
                + AIUtility.EvaluatePressure(
                    deficit / (double)demand.TargetCount,
                    config.DemandUtility.Deficit
                );
            if (demand.Kind == AIProductionDemandKind.TrainingFacility)
                pressure += AIUtility.EvaluatePressure(1, config.DemandUtility.TrainingBacklog);
            if (demand.Kind == AIProductionDemandKind.ConstructionFacility)
                pressure += AIUtility.EvaluatePressure(
                    investmentDeficit,
                    config.DemandUtility.FacilityInvestment
                );

            double colonyFoundation = GetColonyFoundationInput(sector, demand.BuildingType);
            if (colonyFoundation <= 0)
                pressure += GetPortfolioPressure(context, demand.Kind);
            int hubTarget =
                demand.BuildingType == BuildingType.Shipyard
                    ? config.ShipyardSectorHubTargetCount
                    : config.FacilitySectorHubTargetCount;
            int sectorFacilityCount = sector.Sum(planet =>
                planet.GetTotalBuildingTypeCount(demand.BuildingType)
            );
            pressure += AIUtility.EvaluatePressure(
                sectorFacilityCount == 0 ? 1 : 0,
                config.DemandUtility.SectorCoverage
            );
            pressure += AIUtility.EvaluatePressure(
                sectorFacilityCount > 0 ? 1 : 0,
                config.DemandUtility.PrimaryHub
            );
            pressure += AIUtility.EvaluatePressure(
                colonyFoundation,
                config.DemandUtility.ColonyFoundation
            );
            pressure += GetFacilityCategoryBalancePressure(
                sectors,
                demand.BuildingType,
                hubTarget,
                config.DemandUtility.FacilityBalance
            );
            pressure += demand.BaseDemandPercent * placementUtility;
            pressure += demand.BaseDemandPercent * currentCount / demand.TargetCount;
            return pressure;
        }

        /// <summary>
        /// Calculates the normalized placement utility for a facility demand destination.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demand">The facility-capacity demand.</param>
        /// <returns>The destination's placement utility.</returns>
        private static double GetFacilityPlacementUtility(
            AITurnContext context,
            AIProductionDemand demand
        )
        {
            Planet destination = demand?.DestinationPlanet;
            if (destination == null)
                return 0;
            string systemId = context.Assessment.GetPlanetSystemId(destination);
            List<Planet> candidates = GetFacilityCandidates(context, demand);
            return new AIInfrastructurePlacementScorer(context)
                .ScoreDestinations(
                    candidates,
                    demand.ReferencePlanet,
                    GetFacilityOutputManufacturingType(demand.BuildingType),
                    demand.BuildingType,
                    planet => planet?.GetAvailableEnergy() ?? 0
                )
                .Where(candidate => candidate.Planet == destination)
                .Select(candidate => candidate.Score)
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns the production domain supplied by a facility type.
        /// </summary>
        /// <param name="buildingType">The production-facility type.</param>
        /// <returns>The facility's output manufacturing domain.</returns>
        private static ManufacturingType GetFacilityOutputManufacturingType(
            BuildingType buildingType
        )
        {
            return buildingType switch
            {
                BuildingType.Shipyard => ManufacturingType.Ship,
                BuildingType.TrainingFacility => ManufacturingType.Troop,
                _ => ManufacturingType.Building,
            };
        }

        /// <summary>
        /// Returns destinations normalized together for facility placement.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demand">The facility-capacity demand.</param>
        /// <returns>The eligible destinations in assessment order.</returns>
        private static List<Planet> GetFacilityCandidates(
            AITurnContext context,
            AIProductionDemand demand
        )
        {
            Planet destination = demand?.DestinationPlanet;
            if (destination == null)
                return new List<Planet>();
            string systemId = context.Assessment.GetPlanetSystemId(destination);
            return context
                .Assessment.OwnedPlanets.Where(planet =>
                    context.Assessment.GetPlanetSystemId(planet) == systemId
                    && (
                        planet?.IsColonized == true && !planet.IsDestroyed
                        || (
                            demand.BuildingType == BuildingType.ConstructionFacility
                            && planet?.IsDestroyed == false
                            && planet.GetParentOfType<PlanetSector>()?.SectorType
                                == PlanetSectorType.OuterRim
                        )
                    )
                    && planet.GetAvailableEnergy() > 0
                )
                .ToList();
        }

        /// <summary>
        /// Returns the normalized need for founding construction capacity in an Outer Rim sector.
        /// </summary>
        /// <param name="sector">Owned planets in one sector.</param>
        /// <param name="buildingType">The facility category being evaluated.</param>
        /// <returns>One before the first construction facility and zero otherwise.</returns>
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
                return 0;
            int count = planets.Sum(planet =>
                planet.GetTotalBuildingTypeCount(BuildingType.ConstructionFacility)
            );
            return 1 - AIUtility.Fulfillment(count, 1);
        }

        /// <summary>
        /// Calculates pressure favoring the least-developed facility category.
        /// </summary>
        /// <param name="sectors">Owned planets grouped by system.</param>
        /// <param name="buildingType">The facility category.</param>
        /// <param name="hubTarget">The desired facility count per established hub.</param>
        /// <param name="consideration">The configured category-balance consideration.</param>
        /// <returns>The centered category-balance pressure.</returns>
        private static double GetFacilityCategoryBalancePressure(
            IReadOnlyCollection<IGrouping<string, Planet>> sectors,
            BuildingType buildingType,
            int hubTarget,
            GameConfig.AIConsiderationConfig consideration
        )
        {
            if (sectors.Count == 0 || hubTarget <= 0)
                return 0;
            int progress = sectors.Sum(sector =>
                Math.Min(
                    hubTarget,
                    sector.Max(planet => planet.GetTotalBuildingTypeCount(buildingType))
                )
            );
            double target = sectors.Count * (double)hubTarget;
            return AIUtility.EvaluateCenteredPressure(1 - progress / target, consideration);
        }

        /// <summary>
        /// Calculates urgency for a production-facility upgrade.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demand">The upgrade demand.</param>
        /// <returns>The bounded upgrade urgency.</returns>
        private static double GetUpgradePressure(AITurnContext context, AIProductionDemand demand)
        {
            GameConfig.AIProductionDemandUtilityConfig utility = context
                .Game
                .Config
                .AI
                .Infrastructure
                .DemandUtility;
            Planet planet = demand.DestinationPlanet;
            double pressure = demand.BaseDemandPercent;
            double highestPlanetValue = context.Assessment.GetHighestOwnedPlanetValue();
            if (highestPlanetValue > 0)
                pressure += AIUtility.EvaluatePressure(
                    context.Assessment.GetPlanetValue(planet) / highestPlanetValue,
                    utility.UpgradeValue
                );
            pressure += AIUtility.EvaluatePressure(
                context.Assessment.IsFactionHeadquarters(planet) ? 1 : 0,
                utility.UpgradeHeadquarters
            );
            return Math.Max(0, Math.Min(100, pressure));
        }

        /// <summary>
        /// Calculates urgency for a planetary defensive reserve.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demand">The defensive demand.</param>
        /// <returns>The defensive urgency.</returns>
        private static double GetDefensePressure(AITurnContext context, AIProductionDemand demand)
        {
            if (demand.Id?.Contains(":idle-shipyard:", StringComparison.Ordinal) == true)
                return demand.BaseDemandPercent;

            GameConfig.AIProductionDemandUtilityConfig utility = context
                .Game
                .Config
                .AI
                .Infrastructure
                .DemandUtility;
            Planet planet = demand.DestinationPlanet;
            double pressure =
                demand.BaseDemandPercent
                + AIUtility.EvaluateDiscretePressure(
                    demand.DeficitCount / (double)Math.Max(1, demand.TargetCount),
                    utility.DefenseDeficit
                );
            double highestValue = context.Assessment.GetHighestOwnedPlanetValue();
            if (highestValue > 0)
                pressure += AIUtility.EvaluatePressure(
                    context.Assessment.GetPlanetValue(planet) / highestValue,
                    utility.DefenseValue
                );
            pressure += AIUtility.EvaluatePressure(
                context.Assessment.IsFactionHeadquarters(planet) ? 1 : 0,
                utility.DefenseHeadquarters
            );
            pressure += AIUtility.EvaluatePressure(
                context.Assessment.GetPlanetDefenseThreatStrength(planet) > 0 ? 1 : 0,
                utility.DefenseThreat
            );
            if (demand.Kind == AIProductionDemandKind.PlanetaryDefense)
                pressure += GetPortfolioPressure(context, AIProductionDemandKind.PlanetaryDefense);

            double boundedPressure = Math.Max(0, Math.Min(100, pressure));
            if (!demand.EstablishesInitialShield)
                return boundedPressure;
            return boundedPressure
                + AIUtility.EvaluatePressure(
                    planet.GetOpposingPopularSupport(context.Faction.InstanceID) / 100.0,
                    utility.ShieldSupport
                )
                + AIUtility.EvaluatePressure(
                    AIUtility.Fulfillment(
                        context.Assessment.GetDefensiveSupportRisk(planet),
                        utility.ShieldSectorRisk
                    ),
                    utility.ShieldSectorRisk
                );
        }

        /// <summary>
        /// Calculates urgency for restoring economy capacity.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demand">The economy demand.</param>
        /// <returns>The economy urgency.</returns>
        private static double GetEconomyPressure(AITurnContext context, AIProductionDemand demand)
        {
            double pressure = Math.Min(
                100,
                demand.BaseDemandPercent
                    + AIUtility.EvaluateDiscretePressure(
                        1 / (double)Math.Max(1, demand.TargetCount),
                        context.Game.Config.AI.Infrastructure.DemandUtility.Deficit
                    )
            );
            return pressure + GetMaintenancePressure(context) + GetRefinedMaterialPressure(context);
        }

        /// <summary>
        /// Calculates economy urgency from projected maintenance headroom.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The maintenance urgency.</returns>
        private static double GetMaintenancePressure(AITurnContext context)
        {
            GameConfig.AIProductionDemandUtilityConfig utility = context
                .Game
                .Config
                .AI
                .Infrastructure
                .DemandUtility;
            int headroom = context.Assessment.ProjectedEconomyMaintenanceHeadroom;
            int floor = context.Game.Config.AI.Selection.MaintenanceHeadroomReserve;
            int target = Math.Max(
                floor,
                context.Game.Config.AI.Selection.MaintenanceHeadroomTarget
            );
            if (headroom < floor)
                return AIUtility.EvaluatePressure(1, utility.MaintenanceShortfall);
            return headroom >= target
                ? 0
                : AIUtility.EvaluateDiscretePressure(
                    (target - headroom) / (double)Math.Max(1, target - floor),
                    utility.MaintenanceReserve
                );
        }

        /// <summary>
        /// Calculates economy urgency from projected refined-material reserves.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The refined-material urgency.</returns>
        private static double GetRefinedMaterialPressure(AITurnContext context)
        {
            GameConfig.AISelectionConfig config = context.Game.Config.AI.Selection;
            int reservePercent = Math.Max(0, config.RefinedMaterialReservePercent);
            int warningPercent = Math.Max(
                reservePercent,
                config.RefinedMaterialEconomyWarningPercent
            );
            long projectedStockpile = Math.Max(
                0,
                (long)context.Assessment.RefinedMaterialStockpile
                    - context.Assessment.NearTermRefinedMaterialCommitment
            );
            int supply = context.Assessment.RefinedMaterialSupply;
            int projectedPercent =
                supply <= 0
                    ? projectedStockpile > 0
                        ? 100
                        : 0
                    : (int)Math.Min(100, projectedStockpile * 100 / supply);
            if (projectedPercent >= warningPercent)
                return 0;
            double urgency = Math.Min(
                1,
                Math.Max(0, warningPercent - projectedPercent)
                    / (double)Math.Max(1, warningPercent - reservePercent)
            );
            return AIUtility.EvaluatePressure(
                urgency,
                context.Game.Config.AI.Infrastructure.DemandUtility.ResourceShortage
            );
        }

        /// <summary>
        /// Calculates urgency for reinforcing a fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demand">The fleet demand.</param>
        /// <returns>The reinforcement urgency.</returns>
        private static double GetFleetPressure(AITurnContext context, AIProductionDemand demand)
        {
            Fleet fleet = demand.DestinationFleet;
            double pressure = AIFleetProductionAllocationScorer.ScoreDeficit(
                context,
                demand.BaseDemandPercent,
                demand.DeficitCount,
                demand.TargetCount
            );
            Planet targetPlanet = context.Assessment.GetAttackTargetPlanet(fleet);
            if (targetPlanet != null)
            {
                double highestValue = context.Assessment.GetHighestEnemyPlanetValue();
                if (highestValue > 0)
                    pressure += AIUtility.EvaluatePressure(
                        context.Assessment.GetPlanetValue(targetPlanet) / highestValue,
                        context.Game.Config.AI.Infrastructure.DemandUtility.FleetTargetValue
                    );
                pressure += GetFleetReadinessPressure(context, demand.Kind, fleet, targetPlanet);
                pressure += GetFinalReadinessPressure(
                    context,
                    fleet,
                    targetPlanet,
                    demand.DeficitCount
                );
                if (
                    demand.Kind
                    is AIProductionDemandKind.FleetCapitalShip
                        or AIProductionDemandKind.FleetRegiment
                )
                    pressure += AIUtility.EvaluatePressure(
                        1,
                        context.Game.Config.AI.Infrastructure.DemandUtility.AttackReinforcement
                    );
            }
            if (demand.Kind == AIProductionDemandKind.FleetStarfighter)
            {
                int loadedCount = context.Assessment.GetFleetLoadedStarfighterCount(fleet);
                pressure += AIUtility.EvaluateDiscretePressure(
                    (demand.TargetCount - loadedCount) / (double)demand.TargetCount,
                    context.Game.Config.AI.Infrastructure.DemandUtility.StarfighterFill
                );
            }
            return pressure;
        }

        /// <summary>
        /// Calculates reinforcement urgency from complementary fleet readiness.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="kind">The reinforced capability.</param>
        /// <param name="fleet">The fleet being reinforced.</param>
        /// <param name="targetPlanet">The fleet's attack target.</param>
        /// <returns>The readiness urgency.</returns>
        private static double GetFleetReadinessPressure(
            AITurnContext context,
            AIProductionDemandKind kind,
            Fleet fleet,
            Planet targetPlanet
        )
        {
            GameConfig.AIConsiderationConfig readiness = context
                .Game
                .Config
                .AI
                .Infrastructure
                .DemandUtility
                .FleetReadiness;
            AIAttackDemand attackDemand = context.GetAttackDemand(targetPlanet);
            int requiredCombat = attackDemand?.CombatStrength ?? 0;
            int requiredRegiments = GetProjectedRegimentCount(context, fleet, targetPlanet);
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
                AIProductionDemandKind.FleetRegiment => AIUtility.EvaluatePressure(
                    (combatReadiness + capacityReadiness) / 2,
                    readiness
                ),
                AIProductionDemandKind.FleetCapitalShip => AIUtility.EvaluatePressure(
                    (regimentReadiness + capacityReadiness) / 2,
                    readiness
                ),
                AIProductionDemandKind.FleetStarfighter => AIUtility.EvaluatePressure(
                    (combatReadiness + regimentReadiness + capacityReadiness) / 3,
                    readiness
                ),
                _ => 0,
            };
        }

        /// <summary>
        /// Calculates urgency when one final reinforcement closes the attack-readiness gate.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet being reinforced.</param>
        /// <param name="targetPlanet">The fleet's attack target.</param>
        /// <param name="deficit">The remaining unit deficit.</param>
        /// <returns>The final-readiness urgency.</returns>
        private static double GetFinalReadinessPressure(
            AITurnContext context,
            Fleet fleet,
            Planet targetPlanet,
            int deficit
        )
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            if (deficit > config.FleetFinalReadinessGateUnitCount)
                return 0;
            bool combatReady =
                context.Assessment.GetProjectedFleetCombatValue(fleet)
                >= (context.GetAttackDemand(targetPlanet)?.CombatStrength ?? 0);
            int requiredRegiments = GetProjectedRegimentCount(context, fleet, targetPlanet);
            bool capacityReady =
                context.Assessment.GetFleetRegimentCapacity(fleet) >= requiredRegiments;
            bool bombardmentReady =
                context.Assessment.GetProjectedFleetBombardmentStrength(fleet)
                >= (context.GetAttackDemand(targetPlanet)?.BombardmentStrength ?? 0);
            if (!combatReady || !capacityReady || !bombardmentReady)
                return 0;
            return AIUtility.EvaluateDiscretePressure(
                (config.FleetFinalReadinessGateUnitCount - deficit + 1)
                    / (double)config.FleetFinalReadinessGateUnitCount,
                config.DemandUtility.FinalReadiness
            );
        }

        /// <summary>
        /// Returns a bounded fulfillment ratio.
        /// </summary>
        /// <param name="value">Current value.</param>
        /// <param name="target">Target value.</param>
        /// <returns>The bounded fulfillment ratio.</returns>
        private static double GetFulfillmentRatio(double value, double target)
        {
            return target <= 0 ? 1 : Math.Max(0, Math.Min(1, value / target));
        }

        /// <summary>
        /// Returns the regiment count required after projected bombardment.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The reinforced fleet.</param>
        /// <param name="targetPlanet">The attack target.</param>
        /// <returns>The projected regiment requirement.</returns>
        private static int GetProjectedRegimentCount(
            AITurnContext context,
            Fleet fleet,
            Planet targetPlanet
        )
        {
            AIAttackDemand demand = context.GetAttackDemand(targetPlanet);
            if (demand == null)
                return 0;
            bool canBombardDefenders =
                fleet != null
                && context.Assessment.GetDefendingRegimentCount(targetPlanet) > 0
                && context.Assessment.GetProjectedFleetBombardmentStrength(fleet)
                    > context.Assessment.GetBombardmentShieldResistance(targetPlanet);
            return canBombardDefenders ? demand.OccupationRegimentCount : demand.RegimentCount;
        }

        /// <summary>
        /// Calculates signed pressure from the current strategic-facility portfolio.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="kind">The facility category being evaluated.</param>
        /// <returns>The signed portfolio pressure.</returns>
        private static double GetPortfolioPressure(
            AITurnContext context,
            AIProductionDemandKind kind
        )
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
            int total = constructionFacilities + shipyards + trainingFacilities + staticDefenses;
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            (int count, int targetPercent) = kind switch
            {
                AIProductionDemandKind.ConstructionFacility => (
                    constructionFacilities,
                    config.ConstructionFacilityPortfolioPercent
                ),
                AIProductionDemandKind.Shipyard => (shipyards, config.ShipyardPortfolioPercent),
                AIProductionDemandKind.TrainingFacility => (
                    trainingFacilities,
                    config.TrainingFacilityPortfolioPercent
                ),
                AIProductionDemandKind.PlanetaryDefense => (
                    staticDefenses,
                    config.StaticDefensePortfolioPercent
                ),
                _ => (0, 0),
            };
            if (total <= 0 || targetPercent <= 0)
                return 0;
            double deviation = (targetPercent - count * 100.0 / total) / targetPercent;
            double normalized = AIUtility.Fulfillment(Math.Abs(deviation), 1);
            GameConfig.AIConsiderationConfig consideration = config.DemandUtility.FacilityPortfolio;
            return deviation >= 0
                ? AIUtility.EvaluatePressure(normalized, consideration)
                : -AIUtility.EvaluatePressure(normalized, consideration);
        }

        /// <summary>
        /// Returns the normalized value of using a proposal to found Outer Rim construction.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The production proposal to inspect.</param>
        /// <returns>One before the first construction yard reaches an Outer Rim planet; otherwise zero.</returns>
        private static double GetColonyFoundationInput(
            AITurnContext context,
            AIManufactureProposal proposal
        )
        {
            Planet destination = proposal?.Demand?.DestinationPlanet;
            if (
                destination?.GetParentOfType<PlanetSector>()?.SectorType
                != PlanetSectorType.OuterRim
            )
            {
                return 0;
            }

            return
                context.Assessment.GetPlanetProductionFacilityCount(
                    destination,
                    ManufacturingType.Building
                ) == 0
                ? 1
                : 0;
        }

        /// <summary>
        /// Returns the travel penalty for fleet reinforcement production.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal to inspect.</param>
        /// <returns>The travel penalty.</returns>
        private double GetTravelCost(AITurnContext context, AIManufactureProposal proposal)
        {
            if (proposal?.Demand?.Destination is not Fleet destinationFleet)
                return 0;

            Planet producerPlanet = proposal.ProducerPlanet;
            Planet destinationPlanet = context.Assessment.GetFleetPlanet(destinationFleet);
            if (producerPlanet == null || destinationPlanet == null)
                return 0;

            double distanceScale = context.Game.Config.Movement.DistanceScale;
            if (distanceScale <= 0)
                return 0;

            return producerPlanet.GetRawDistanceTo(destinationPlanet) / distanceScale;
        }
    }
}
