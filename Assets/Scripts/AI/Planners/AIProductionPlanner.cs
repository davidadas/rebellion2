using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Planners.Demand;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.Util.Common;

namespace Rebellion.AI.Planners
{
    /// <summary>
    /// Builds production proposals from current demand.
    /// </summary>
    public sealed class AIProductionPlanner : IAIProposalPlanner
    {
        private const int _primaryLaserDivisor = 6;
        private const int _primaryWeaponWeight = 100;
        private const int _productionRateMetricScale = 1000;
        private const int _roleMetricScale = 10;
        private const int _weaponArcCount = 4;

        private readonly AIProductionDemandGenerator _demandGenerator =
            new AIProductionDemandGenerator();
        private readonly Dictionary<ManufacturingType, List<Technology>> _unlockedTechnologies =
            new Dictionary<ManufacturingType, List<Technology>>();
        private readonly Dictionary<
            (
                AIDemandKind Kind,
                BuildingType BuildingType,
                string DestinationId,
                string ProductTypeId,
                string ReplacementTypeId
            ),
            Technology
        > _selectedTechnologies =
            new Dictionary<
                (
                    AIDemandKind Kind,
                    BuildingType BuildingType,
                    string DestinationId,
                    string ProductTypeId,
                    string ReplacementTypeId
                ),
                Technology
            >();
        private readonly Dictionary<BuildingType, int> _committedFacilityMaintenance =
            new Dictionary<BuildingType, int>();
        private readonly Dictionary<
            (string DestinationId, ManufacturingType ManufacturingType, ProducerMode Mode),
            List<Planet>
        > _producerPlanets =
            new Dictionary<
                (string DestinationId, ManufacturingType ManufacturingType, ProducerMode Mode),
                List<Planet>
            >();
        private readonly Dictionary<
            (string PlanetId, ManufacturingType ManufacturingType),
            (double TargetWork, long QueuedWork)
        > _queueWork =
            new Dictionary<
                (string PlanetId, ManufacturingType ManufacturingType),
                (double TargetWork, long QueuedWork)
            >();
        private readonly Dictionary<
            (string FleetId, Type UnitType, string TypeId),
            int
        > _fleetUnitCounts = new Dictionary<(string FleetId, Type UnitType, string TypeId), int>();
        private readonly Dictionary<string, bool> _fleetHasIonStarfighters = new Dictionary<
            string,
            bool
        >(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> _fleetHasTorpedoStarfighters = new Dictionary<
            string,
            bool
        >(StringComparer.Ordinal);

        private int? _capitalShipMaintenanceBudget;

        /// <summary>
        /// Returns production proposals for the current AI turn.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Production proposals generated for this faction.</returns>
        public List<AIProposal> Plan(AITurnContext context)
        {
            ResetPlanningCache();
            List<AIDemand> demands = _demandGenerator.Generate(context);
            return GenerateProposals(context, demands);
        }

        /// <summary>
        /// Clears values indexed from the current game state before planning a new turn.
        /// </summary>
        private void ResetPlanningCache()
        {
            _unlockedTechnologies.Clear();
            _selectedTechnologies.Clear();
            _committedFacilityMaintenance.Clear();
            _producerPlanets.Clear();
            _queueWork.Clear();
            _fleetUnitCounts.Clear();
            _fleetHasIonStarfighters.Clear();
            _fleetHasTorpedoStarfighters.Clear();
            _capitalShipMaintenanceBudget = null;
        }

        /// <summary>
        /// Generates manufacture proposals for demand items.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">Demand items to satisfy.</param>
        /// <returns>Manufacture proposals generated for the demands.</returns>
        private List<AIProposal> GenerateProposals(AITurnContext context, List<AIDemand> demands)
        {
            List<AIProposal> proposals = new List<AIProposal>();
            if (context?.Faction == null || demands == null || demands.Count == 0)
                return proposals;

            foreach (AIDemand demand in demands)
            {
                AddManufactureProposal(context, demand, proposals);
            }

            return proposals;
        }

        /// <summary>
        /// Adds manufacture proposals for one demand item.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demand">Demand item to satisfy.</param>
        /// <param name="proposals">The proposal list to update.</param>
        private void AddManufactureProposal(
            AITurnContext context,
            AIDemand demand,
            List<AIProposal> proposals
        )
        {
            Technology product = GetUnlockedTechnology(context, demand);
            if (product == null)
                return;

            bool distributesDemand = IsDistributedProductionDemand(demand);
            int remainingQuantity = GetRequestedManufacturingCount(
                context,
                demand,
                product.GetReference()
            );
            if (distributesDemand)
            {
                remainingQuantity = Math.Min(
                    remainingQuantity,
                    GetFleetUnitDiversityLimit(context, demand, product.GetReference())
                );
            }
            if (remainingQuantity <= 0)
                return;

            List<Planet> producerPlanets = FindProducerPlanets(context, demand).ToList();
            if (producerPlanets.Count == 0)
                return;
            if (!distributesDemand)
            {
                if (IsFacilityExpansionDemand(demand))
                    AddProducerSpecificProposal(
                        context,
                        demand,
                        product,
                        remainingQuantity,
                        producerPlanets,
                        proposals
                    );
                else
                    AddEquivalentProducerProposal(
                        context,
                        demand,
                        product,
                        remainingQuantity,
                        producerPlanets,
                        proposals
                    );
                return;
            }

            foreach (Planet producerPlanet in producerPlanets)
            {
                AIDemand proposalDemand = GetProposalDemand(
                    context,
                    demand,
                    producerPlanet,
                    product,
                    remainingQuantity
                );
                if (proposalDemand == null)
                    continue;

                AIManufactureProposal proposal = new AIManufactureProposal(
                    proposalDemand,
                    producerPlanet,
                    product,
                    distributesDemand
                );

                proposals.Add(proposal);
                if (distributesDemand)
                {
                    remainingQuantity -= proposalDemand.QuantityNeeded;
                    if (remainingQuantity <= 0)
                        return;
                }
            }
        }

        /// <summary>
        /// Adds one proposal that can select from equivalent producer alternatives.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demand">Demand item to satisfy.</param>
        /// <param name="product">Technology selected for manufacture.</param>
        /// <param name="remainingQuantity">Quantity still required.</param>
        /// <param name="producerPlanets">Ranked producer alternatives.</param>
        /// <param name="proposals">The proposal list to update.</param>
        private void AddEquivalentProducerProposal(
            AITurnContext context,
            AIDemand demand,
            Technology product,
            int remainingQuantity,
            IReadOnlyList<Planet> producerPlanets,
            List<AIProposal> proposals
        )
        {
            if (producerPlanets.Count == 0)
                return;

            AIDemand proposalDemand = GetProposalDemand(
                context,
                demand,
                producerPlanets[0],
                product,
                remainingQuantity
            );
            if (proposalDemand == null)
                return;

            proposals.Add(
                new AIManufactureProposal(
                    proposalDemand,
                    producerPlanets,
                    product,
                    distributesDemand: false
                )
            );
        }

        /// <summary>
        /// Adds one proposal whose alternatives require producer-specific demand values.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demand">Demand item to satisfy.</param>
        /// <param name="product">Technology selected for manufacture.</param>
        /// <param name="remainingQuantity">Quantity still required.</param>
        /// <param name="producerPlanets">Ranked producer alternatives.</param>
        /// <param name="proposals">The proposal list to update.</param>
        private void AddProducerSpecificProposal(
            AITurnContext context,
            AIDemand demand,
            Technology product,
            int remainingQuantity,
            IReadOnlyList<Planet> producerPlanets,
            List<AIProposal> proposals
        )
        {
            List<AIManufactureOption> options = producerPlanets
                .Select(producerPlanet => new AIManufactureOption(
                    GetProposalDemand(context, demand, producerPlanet, product, remainingQuantity),
                    producerPlanet
                ))
                .Where(option => option.Demand != null)
                .ToList();
            if (options.Count == 0)
                return;

            proposals.Add(new AIManufactureProposal(options, product, distributesDemand: false));
        }

        /// <summary>
        /// Returns the unlocked technology that can satisfy a demand.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demand">Demand item to satisfy.</param>
        /// <returns>The selected technology, or null.</returns>
        private Technology GetUnlockedTechnology(AITurnContext context, AIDemand demand)
        {
            if (demand == null)
                return null;

            if (
                demand.Kind
                is AIDemandKind.FleetCapitalShip
                    or AIDemandKind.FleetSeedCapitalShip
                    or AIDemandKind.ColonizationFleetSeedCapitalShip
            )
                return GetUnlockedCapitalShipTechnology(context, demand);

            (
                AIDemandKind Kind,
                BuildingType BuildingType,
                string DestinationId,
                string ProductTypeId,
                string ReplacementTypeId
            ) key = (
                demand.Kind,
                demand.BuildingType,
                demand.Kind is AIDemandKind.FleetStarfighter or AIDemandKind.FleetRegiment
                    ? demand.Destination?.InstanceID
                    : null,
                demand.ProductTypeId,
                demand.BuildingToReplace?.GetTypeID()
            );
            if (_selectedTechnologies.TryGetValue(key, out Technology selectedTechnology))
                return selectedTechnology;

            selectedTechnology = demand.Kind switch
            {
                AIDemandKind.Colony
                or AIDemandKind.Mine
                or AIDemandKind.Refinery
                or AIDemandKind.ConstructionFacility
                or AIDemandKind.Shipyard
                or AIDemandKind.TrainingFacility
                or AIDemandKind.BuildingUpgrade
                or AIDemandKind.PlanetaryDefense => GetUnlockedBuildingTechnology(context, demand),
                AIDemandKind.FleetStarfighter
                or AIDemandKind.PlanetaryStarfighterReserve
                or AIDemandKind.FleetRegiment
                or AIDemandKind.GarrisonRegimentReserve
                or AIDemandKind.SpecialForces => GetUnlockedUnitTechnology(context, demand),
                _ => null,
            };
            _selectedTechnologies.Add(key, selectedTechnology);
            return selectedTechnology;
        }

        /// <summary>
        /// Returns the unlocked building technology for a building type.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demand">Building demand to satisfy.</param>
        /// <returns>The selected technology, or null.</returns>
        private Technology GetUnlockedBuildingTechnology(AITurnContext context, AIDemand demand)
        {
            if (context?.Faction == null || demand?.BuildingType == BuildingType.None)
                return null;

            int maintenanceBudget = GetBuildingMaintenanceBudget(context, demand);
            if (IsFacilityExpansionDemand(demand) && maintenanceBudget <= 0)
                return null;

            return GetUnlockedTechnologies(context, ManufacturingType.Building)
                .Where(technology =>
                    technology.GetReference() is Building building
                    && building.GetBuildingType() == demand.BuildingType
                    && IManufacturable.CanBeManufacturedBy(building, context.Faction.InstanceID)
                    && IsEligibleBuildingUpgrade(demand, building)
                    && GetBuildingMaintenanceCost(demand, building) <= maintenanceBudget
                )
                .OrderByDescending(technology =>
                    GetBuildingCapability((Building)technology.GetReference())
                )
                .ThenByDescending(technology => technology.GetResearchOrder())
                .ThenBy(technology => technology.GetReference().GetMaintenanceCost())
                .ThenBy(technology => technology.GetReference().GetConstructionCost())
                .ThenBy(technology => technology.GetReference().GetTypeID())
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns whether eligible building upgrade.
        /// </summary>
        /// <param name="demand">The production demand.</param>
        /// <param name="building">The building to evaluate.</param>
        /// <returns>True when the condition is satisfied.</returns>
        private static bool IsEligibleBuildingUpgrade(AIDemand demand, Building building)
        {
            return demand.Kind != AIDemandKind.BuildingUpgrade
                || demand.BuildingToReplace.CanUpgradeTo(building);
        }

        /// <summary>
        /// Returns building maintenance cost.
        /// </summary>
        /// <param name="demand">The production demand.</param>
        /// <param name="building">The building to evaluate.</param>
        /// <returns>The calculated value.</returns>
        private static int GetBuildingMaintenanceCost(AIDemand demand, Building building)
        {
            if (demand.Kind != AIDemandKind.BuildingUpgrade || demand.BuildingToReplace == null)
                return building.MaintenanceCost;

            return Math.Max(0, building.MaintenanceCost - demand.BuildingToReplace.MaintenanceCost);
        }

        /// <summary>
        /// Returns building maintenance budget.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demand">The production demand.</param>
        /// <returns>The calculated value.</returns>
        private int GetBuildingMaintenanceBudget(AITurnContext context, AIDemand demand)
        {
            if (IsFacilityExpansionDemand(demand))
                return GetFacilityMaintenanceBudget(context, demand);

            if (demand.UsesDefensiveReserve)
                return GetDefensiveMaintenanceBudget(context);

            return Math.Max(
                0,
                context.Assessment.ProjectedMaintenanceHeadroom
                    - context.Game.Config.AI.Selection.MaintenanceHeadroomHardFloor
            );
        }

        /// <summary>
        /// Returns facility maintenance budget.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demand">The production demand.</param>
        /// <returns>The calculated value.</returns>
        private int GetFacilityMaintenanceBudget(AITurnContext context, AIDemand demand)
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            int allocatedMaintenance = demand.Kind switch
            {
                AIDemandKind.Shipyard => IntegerMath.ScaleByPercent(
                    IntegerMath.ScaleByPercent(
                        context.Assessment.MaintenanceCapacity,
                        config.ShipyardMaintenanceAllocationPercent
                    ),
                    config.ShipyardMaintenanceAllocationScalePercent
                ),
                AIDemandKind.TrainingFacility => IntegerMath.ScaleByPercent(
                    IntegerMath.ScaleByPercent(
                        context.Assessment.MaintenanceCapacity,
                        config.TrainingFacilityMaintenanceAllocationPercent
                    ),
                    config.TrainingFacilityMaintenanceAllocationScalePercent
                ),
                AIDemandKind.ConstructionFacility => IntegerMath.ScaleByPercent(
                    context.Assessment.MaintenanceCapacity,
                    config.ConstructionFacilityMaintenanceAllocationPercent
                ),
                _ => 0,
            };
            int availableMaintenance =
                allocatedMaintenance - GetCommittedFacilityMaintenance(context, demand);
            if (
                availableMaintenance <= 0
                || context.Assessment.ProjectedMaintenanceHeadroom < availableMaintenance
            )
                return 0;

            int headroomBudget = Math.Max(
                0,
                context.Assessment.ProjectedMaintenanceHeadroom
                    - context.Game.Config.AI.Selection.MaintenanceHeadroomHardFloor
            );
            return Math.Min(availableMaintenance, headroomBudget);
        }

        /// <summary>
        /// Returns committed facility maintenance.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demand">The production demand.</param>
        /// <returns>The calculated value.</returns>
        private int GetCommittedFacilityMaintenance(AITurnContext context, AIDemand demand)
        {
            BuildingType buildingType = demand.Kind switch
            {
                AIDemandKind.Shipyard => BuildingType.Shipyard,
                AIDemandKind.TrainingFacility => BuildingType.TrainingFacility,
                AIDemandKind.ConstructionFacility => BuildingType.ConstructionFacility,
                _ => BuildingType.None,
            };

            if (_committedFacilityMaintenance.TryGetValue(buildingType, out int maintenance))
                return maintenance;

            maintenance = context
                .Assessment.OwnedPlanets.SelectMany(context.Assessment.GetPlanetBuildings)
                .Where(building =>
                    building.GetOwnerInstanceID() == context.Faction.InstanceID
                    && building.GetBuildingType() == buildingType
                )
                .Sum(building => building.MaintenanceCost);
            _committedFacilityMaintenance.Add(buildingType, maintenance);
            return maintenance;
        }

        /// <summary>
        /// Returns proposal demand.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demand">The production demand.</param>
        /// <param name="producerPlanet">The producing planet.</param>
        /// <param name="product">The manufacturable product.</param>
        /// <param name="remainingQuantity">The remaining requested quantity.</param>
        /// <returns>The selected value, or null when none is available.</returns>
        private AIDemand GetProposalDemand(
            AITurnContext context,
            AIDemand demand,
            Planet producerPlanet,
            Technology product,
            int remainingQuantity
        )
        {
            if (demand.Kind == AIDemandKind.BuildingUpgrade)
                return demand;

            if (IsDistributedProductionDemand(demand))
            {
                int distributedQuantity = GetDistributedBatchSize(
                    context,
                    producerPlanet,
                    product.GetReference(),
                    remainingQuantity
                );
                return distributedQuantity > 0
                    ? CreateProposalDemand(demand, distributedQuantity)
                    : null;
            }

            if (!IsFacilityExpansionDemand(demand) && !demand.UsesDefensiveReserve)
                return demand;

            int quantity;
            if (IsFacilityExpansionDemand(demand))
            {
                if (product.GetReference() is not Building building)
                    return null;

                quantity = GetFacilityBatchSize(context, demand, producerPlanet, building);
            }
            else
            {
                quantity = GetDefensiveBatchSize(context, demand, product.GetReference());
            }

            if (quantity <= 0)
                return null;

            return CreateProposalDemand(demand, quantity);
        }

        /// <summary>
        /// Creates proposal demand.
        /// </summary>
        /// <param name="demand">The production demand.</param>
        /// <param name="quantity">The requested quantity.</param>
        /// <returns>The selected value, or null when none is available.</returns>
        private static AIDemand CreateProposalDemand(AIDemand demand, int quantity)
        {
            AIDemand proposalDemand = new AIDemand(
                demand.Id,
                demand.Kind,
                demand.ManufacturingType,
                demand.BuildingType,
                demand.Destination,
                quantity,
                demand.Pressure,
                demand.ProductTypeId,
                demand.CapitalShipRole
            );
            proposalDemand.BuildingToReplace = demand.BuildingToReplace;
            return proposalDemand;
        }

        /// <summary>
        /// Returns requested manufacturing count.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demand">The production demand.</param>
        /// <param name="product">The manufacturable product.</param>
        /// <returns>The calculated value.</returns>
        private int GetRequestedManufacturingCount(
            AITurnContext context,
            AIDemand demand,
            IManufacturable product
        )
        {
            if (!IsDistributedProductionDemand(demand))
                return Math.Max(0, demand.QuantityNeeded);

            int requestedCount =
                demand.Kind == AIDemandKind.FleetCapitalShip
                    ? GetCapitalShipCount(context, demand, product as CapitalShip)
                    : demand.QuantityNeeded;

            if (demand.UsesDefensiveReserve)
                requestedCount = Math.Min(
                    requestedCount,
                    GetDefensiveBatchSize(context, demand, product)
                );

            return Math.Max(0, requestedCount);
        }

        /// <summary>
        /// Returns capital ship count.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demand">The production demand.</param>
        /// <param name="capitalShip">The capital ship to evaluate.</param>
        /// <returns>The calculated value.</returns>
        private int GetCapitalShipCount(
            AITurnContext context,
            AIDemand demand,
            CapitalShip capitalShip
        )
        {
            if (capitalShip == null)
                return 0;

            int contribution = demand.CapitalShipRole switch
            {
                AICapitalShipProductionRole.General =>
                    context.Assessment.GetProjectedCapitalShipCombatValue(capitalShip),
                AICapitalShipProductionRole.TroopTransport => capitalShip.RegimentCapacity,
                AICapitalShipProductionRole.Bombardment =>
                    context.Assessment.GetProjectedCapitalShipBombardmentStrength(
                        demand.DestinationFleet,
                        capitalShip
                    ),
                AICapitalShipProductionRole.Interdiction => 1,
                _ => 0,
            };
            if (contribution <= 0)
                return 0;

            int requestedCount = IntegerMath.DivideRoundedUp(demand.QuantityNeeded, contribution);
            if (capitalShip.MaintenanceCost <= 0)
                return requestedCount;

            return Math.Min(
                requestedCount,
                GetCapitalShipMaintenanceBudget(context) / capitalShip.MaintenanceCost
            );
        }

        /// <summary>
        /// Returns distributed batch size.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="producerPlanet">The producing planet.</param>
        /// <param name="product">The manufacturable product.</param>
        /// <param name="remainingQuantity">The remaining requested quantity.</param>
        /// <returns>The calculated value.</returns>
        private int GetDistributedBatchSize(
            AITurnContext context,
            Planet producerPlanet,
            IManufacturable product,
            int remainingQuantity
        )
        {
            int queueCapacity = GetQueueBatchCapacity(context, producerPlanet, product);
            return Math.Max(0, Math.Min(remainingQuantity, queueCapacity));
        }

        /// <summary>
        /// Returns queue batch capacity.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="producerPlanet">The producing planet.</param>
        /// <param name="product">The manufacturable product.</param>
        /// <returns>The calculated value.</returns>
        private int GetQueueBatchCapacity(
            AITurnContext context,
            Planet producerPlanet,
            IManufacturable product
        )
        {
            ManufacturingType manufacturingType = product.GetManufacturingType();
            (string PlanetId, ManufacturingType ManufacturingType) key = (
                producerPlanet.InstanceID,
                manufacturingType
            );
            if (!_queueWork.TryGetValue(key, out (double TargetWork, long QueuedWork) work))
            {
                work.TargetWork =
                    context.Assessment.GetPlanetProductionRate(producerPlanet, manufacturingType)
                    * context.Game.Config.AI.TickInterval
                    * context.Game.Config.AI.Infrastructure.ProductionQueueTargetPlanningIntervals;
                work.QueuedWork = producerPlanet
                    .GetManufacturingQueue()
                    .TryGetValue(manufacturingType, out List<IManufacturable> queue)
                    ? queue.Sum(item =>
                        (long)Math.Max(0, item.GetConstructionCost() - item.ManufacturingProgress)
                    )
                    : 0;
                _queueWork.Add(key, work);
            }

            double targetWork = work.TargetWork;
            long queuedWork = work.QueuedWork;
            long additionalWork = (long)Math.Ceiling(targetWork) - queuedWork;
            if (additionalWork <= 0)
                return 0;

            int constructionCost = product.GetConstructionCost();
            if (constructionCost <= 0)
                return int.MaxValue;

            long capacity = IntegerMath.DivideRoundedUp(additionalWork, constructionCost);
            return capacity > int.MaxValue ? int.MaxValue : (int)capacity;
        }

        /// <summary>
        /// Returns fleet unit diversity limit.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demand">The production demand.</param>
        /// <param name="product">The manufacturable product.</param>
        /// <returns>The calculated value.</returns>
        private int GetFleetUnitDiversityLimit(
            AITurnContext context,
            AIDemand demand,
            IManufacturable product
        )
        {
            if (demand.DestinationFleet == null)
                return int.MaxValue;

            GameConfig.AISelectionConfig config = context.Game.Config.AI.Selection;
            if (product is Starfighter starfighter)
            {
                return GetFleetUnitDiversityLimit(
                    context,
                    demand.DestinationFleet,
                    starfighter.GetTypeID(),
                    ManufacturingType.Ship,
                    config.PreferredStarfighterTypeCountPerFleet,
                    technology => technology.GetReference() as Starfighter
                );
            }

            if (product is Regiment regiment)
            {
                return GetFleetUnitDiversityLimit(
                    context,
                    demand.DestinationFleet,
                    regiment.GetTypeID(),
                    ManufacturingType.Troop,
                    config.PreferredRegimentTypeCountPerDestination,
                    technology => technology.GetReference() as Regiment
                );
            }

            return int.MaxValue;
        }

        /// <summary>
        /// Returns the remaining number of one unit type allowed before another available type
        /// should be selected for fleet diversity.
        /// </summary>
        /// <typeparam name="T">The manufacturable fleet-unit type.</typeparam>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The destination fleet.</param>
        /// <param name="selectedTypeId">The selected unit type identifier.</param>
        /// <param name="manufacturingType">The manufacturing category.</param>
        /// <param name="maximumDuplicateCount">The preferred duplicate limit.</param>
        /// <param name="getUnit">Resolves the unit represented by a technology.</param>
        /// <returns>The remaining allowed quantity, or no effective limit when diversity is unavailable.</returns>
        private int GetFleetUnitDiversityLimit<T>(
            AITurnContext context,
            Fleet fleet,
            string selectedTypeId,
            ManufacturingType manufacturingType,
            int maximumDuplicateCount,
            Func<Technology, T> getUnit
        )
            where T : class, IManufacturable
        {
            bool hasPreferredTechnology = GetUnlockedTechnologies(context, manufacturingType)
                .Select(getUnit)
                .Any(unit =>
                    unit != null
                    && CountFleetUnitsByType<T>(fleet, unit.GetTypeID()) < maximumDuplicateCount
                );
            if (!hasPreferredTechnology)
                return int.MaxValue;

            return Math.Max(
                0,
                maximumDuplicateCount - CountFleetUnitsByType<T>(fleet, selectedTypeId)
            );
        }

        /// <summary>
        /// Returns defensive batch size.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demand">The production demand.</param>
        /// <param name="product">The manufacturable product.</param>
        /// <returns>The calculated value.</returns>
        private int GetDefensiveBatchSize(
            AITurnContext context,
            AIDemand demand,
            IManufacturable product
        )
        {
            int maintenanceBudget = GetDefensiveMaintenanceBudget(context);
            int maintenanceLimit =
                product.GetMaintenanceCost() > 0
                    ? maintenanceBudget / product.GetMaintenanceCost()
                    : int.MaxValue;
            int destinationLimit =
                product is Building
                    ? demand.DestinationPlanet?.GetAvailableEnergy() ?? 0
                    : int.MaxValue;

            return Math.Max(
                0,
                Math.Min(demand.QuantityNeeded, Math.Min(maintenanceLimit, destinationLimit))
            );
        }

        /// <summary>
        /// Returns defensive maintenance budget.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The calculated value.</returns>
        private int GetDefensiveMaintenanceBudget(AITurnContext context)
        {
            return Math.Max(
                0,
                context.Assessment.ProjectedMaintenanceHeadroom
                    - GetDefensiveMaintenanceFloor(context)
            );
        }

        /// <summary>
        /// Returns defensive maintenance floor.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The calculated value.</returns>
        private int GetDefensiveMaintenanceFloor(AITurnContext context)
        {
            return Math.Max(
                context.Game.Config.AI.Selection.MaintenanceHeadroomHardFloor,
                Math.Max(
                    context.Game.Config.AI.Selection.MinimumMaintenanceHeadroomAfterProduction,
                    IntegerMath.ScaleByPercentRoundedUp(
                        context.Assessment.MaintenanceCapacity,
                        context
                            .Game
                            .Config
                            .AI
                            .Infrastructure
                            .PlanetaryDefenseMaintenanceReservePercent
                    )
                )
            );
        }

        /// <summary>
        /// Returns facility batch size.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demand">The production demand.</param>
        /// <param name="producerPlanet">The producing planet.</param>
        /// <param name="building">The building to evaluate.</param>
        /// <returns>The calculated value.</returns>
        private int GetFacilityBatchSize(
            AITurnContext context,
            AIDemand demand,
            Planet producerPlanet,
            Building building
        )
        {
            int maintenanceBudget = GetFacilityMaintenanceBudget(context, demand);
            int maintenanceLimit =
                building.MaintenanceCost > 0
                    ? maintenanceBudget / building.MaintenanceCost
                    : int.MaxValue;
            int facilityCount = context.Assessment.GetPlanetProductionFacilityCount(
                producerPlanet,
                ManufacturingType.Building
            );
            int laneReserve =
                demand.Kind == AIDemandKind.ConstructionFacility
                    ? 0
                    : Math.Max(
                        0,
                        context.Game.Config.AI.Infrastructure.FacilityConstructionLaneReserve
                    );
            int laneLimit =
                facilityCount > laneReserve ? facilityCount - laneReserve : facilityCount;
            int energyLimit = Math.Max(
                0,
                demand.DestinationPlanet.GetAvailableEnergy()
                    - context.Assessment.GetPlanetaryDefenseEnergyDeficit(demand.DestinationPlanet)
            );

            return Math.Max(0, Math.Min(maintenanceLimit, Math.Min(laneLimit, energyLimit)));
        }

        /// <summary>
        /// Returns whether facility expansion demand.
        /// </summary>
        /// <param name="demand">The production demand.</param>
        /// <returns>True when the condition is satisfied.</returns>
        private static bool IsFacilityExpansionDemand(AIDemand demand)
        {
            return demand?.Kind
                is AIDemandKind.ConstructionFacility
                    or AIDemandKind.Shipyard
                    or AIDemandKind.TrainingFacility;
        }

        /// <summary>
        /// Returns whether distributed production demand.
        /// </summary>
        /// <param name="demand">The production demand.</param>
        /// <returns>True when the condition is satisfied.</returns>
        private static bool IsDistributedProductionDemand(AIDemand demand)
        {
            return demand?.Kind
                is AIDemandKind.FleetCapitalShip
                    or AIDemandKind.FleetStarfighter
                    or AIDemandKind.PlanetaryStarfighterReserve
                    or AIDemandKind.FleetRegiment
                    or AIDemandKind.GarrisonRegimentReserve
                    or AIDemandKind.SpecialForces;
        }

        /// <summary>
        /// Returns building capability.
        /// </summary>
        /// <param name="building">The building to evaluate.</param>
        /// <returns>The calculated value.</returns>
        private static int GetBuildingCapability(Building building)
        {
            return building.GetBuildingType() switch
            {
                BuildingType.ConstructionFacility
                or BuildingType.Shipyard
                or BuildingType.TrainingFacility
                or BuildingType.Mine
                or BuildingType.Refinery => building.ProcessRate > 0
                    ? -building.ProcessRate
                    : int.MinValue,
                BuildingType.Weapon => building.WeaponPower,
                BuildingType.Defense => building.ShieldStrength,
                _ => int.MinValue,
            };
        }

        /// <summary>
        /// Returns the unlocked unit technology for a demand item.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demand">Demand item to satisfy.</param>
        /// <returns>The selected technology, or null.</returns>
        private Technology GetUnlockedUnitTechnology(AITurnContext context, AIDemand demand)
        {
            if (context?.Faction == null || demand == null)
                return null;

            return demand.Kind switch
            {
                AIDemandKind.FleetCapitalShip => GetUnlockedCapitalShipTechnology(context, demand),
                AIDemandKind.FleetSeedCapitalShip
                or AIDemandKind.ColonizationFleetSeedCapitalShip =>
                    GetUnlockedCapitalShipTechnology(context, demand),
                AIDemandKind.FleetStarfighter => GetUnlockedStarfighterTechnology(
                    context,
                    demand.DestinationFleet
                ),
                AIDemandKind.PlanetaryStarfighterReserve =>
                    GetUnlockedPlanetaryStarfighterTechnology(context),
                AIDemandKind.FleetRegiment => GetUnlockedRegimentTechnology(
                    context,
                    demand.DestinationFleet
                ),
                AIDemandKind.GarrisonRegimentReserve => GetUnlockedGarrisonRegimentTechnology(
                    context
                ),
                AIDemandKind.SpecialForces => GetUnlockedSpecialForcesTechnology(
                    context,
                    demand.ProductTypeId
                ),
                _ => null,
            };
        }

        /// <summary>
        /// Returns unlocked special forces technology.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="productTypeId">The product type id.</param>
        /// <returns>The selected value, or null when none is available.</returns>
        private Technology GetUnlockedSpecialForcesTechnology(
            AITurnContext context,
            string productTypeId
        )
        {
            if (string.IsNullOrEmpty(productTypeId))
                return null;

            return GetUnlockedTechnologies(context, ManufacturingType.Troop)
                .Where(technology =>
                    technology.GetReference() is SpecialForces specialForces
                    && specialForces.GetTypeID() == productTypeId
                )
                .OrderBy(technology => technology.GetResearchOrder())
                .ThenBy(technology => technology.GetReference().GetConstructionCost())
                .ThenBy(technology => technology.GetReference().GetTypeID())
                .FirstOrDefault();
        }

        /// <summary>
        /// Selects an unlocked capital ship technology for a fleet-production demand.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demand">The production demand.</param>
        /// <returns>The selected technology, or null when no eligible ship is affordable.</returns>
        private Technology GetUnlockedCapitalShipTechnology(AITurnContext context, AIDemand demand)
        {
            if (context?.Faction == null || demand == null)
                return null;

            int maintenanceBudget = GetCapitalShipMaintenanceBudget(context);
            bool prioritizeGeneralDelivery =
                demand.CapitalShipRole == AICapitalShipProductionRole.General
                && !HasCommittedCombatCapitalShip(demand.DestinationFleet);
            List<Technology> rankedTechnologies = new List<Technology>();

            foreach (
                Technology technology in GetUnlockedTechnologies(context, ManufacturingType.Ship)
            )
            {
                if (technology.GetReference() is not CapitalShip capitalShip)
                    continue;

                if (!IManufacturable.CanBeManufacturedBy(capitalShip, context.Faction.InstanceID))
                    continue;

                if (!CanFillCapitalShipRole(capitalShip, demand.CapitalShipRole))
                    continue;

                InsertCapitalShipTechnology(
                    context,
                    rankedTechnologies,
                    technology,
                    demand.CapitalShipRole,
                    prioritizeGeneralDelivery
                );
            }

            for (int index = rankedTechnologies.Count - 1; index >= 0; index--)
            {
                Technology technology = rankedTechnologies[index];
                if (technology.GetReference().GetMaintenanceCost() <= maintenanceBudget)
                    return technology;
            }

            return null;
        }

        /// <summary>
        /// Inserts a capital ship technology into the ascending role-metric ranking.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="rankedTechnologies">The ranked technologies.</param>
        /// <param name="candidate">The candidate.</param>
        /// <param name="role">The role.</param>
        /// <param name="prioritizeGeneralDelivery">
        /// Whether an unready fleet needs its first combat ship delivered quickly.
        /// </param>
        private static void InsertCapitalShipTechnology(
            AITurnContext context,
            List<Technology> rankedTechnologies,
            Technology candidate,
            AICapitalShipProductionRole role,
            bool prioritizeGeneralDelivery
        )
        {
            CapitalShip candidateShip = (CapitalShip)candidate.GetReference();
            long candidateMetric = GetCapitalShipRoleMetric(
                candidateShip,
                role,
                prioritizeGeneralDelivery
            );

            for (int index = 0; index < rankedTechnologies.Count; index++)
            {
                CapitalShip rankedShip = (CapitalShip)rankedTechnologies[index].GetReference();
                long rankedMetric = GetCapitalShipRoleMetric(
                    rankedShip,
                    role,
                    prioritizeGeneralDelivery
                );
                if (
                    candidateMetric < rankedMetric
                    || (
                        candidateMetric == rankedMetric
                        && ShouldInsertCapitalShipBeforeEqual(context)
                    )
                )
                {
                    rankedTechnologies.Insert(index, candidate);
                    return;
                }
            }

            rankedTechnologies.Add(candidate);
        }

        /// <summary>
        /// Resolves the configured random ordering between equally ranked capital ships.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when the new candidate should precede the existing candidate.</returns>
        private static bool ShouldInsertCapitalShipBeforeEqual(AITurnContext context)
        {
            GameConfig.AISelectionConfig config = context.Game.Config.AI.Selection;
            return context.Random.NextInt(0, config.CapitalShipTieRollRange)
                < config.CapitalShipTieInsertBeforeThreshold;
        }

        /// <summary>
        /// Gets the remaining maintenance budget available to new capital ships this turn.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The available capital-ship maintenance capacity.</returns>
        private int GetCapitalShipMaintenanceBudget(AITurnContext context)
        {
            if (_capitalShipMaintenanceBudget.HasValue)
                return _capitalShipMaintenanceBudget.Value;

            GameConfig.AISelectionConfig config = context.Game.Config.AI.Selection;
            int allocatedMaintenance = IntegerMath.ScaleByPercent(
                context.Assessment.MaintenanceCapacity,
                config.CapitalMaintenanceAllocationPercent
            );
            int targetCapitalMaintenance = IntegerMath.ScaleByPercent(
                allocatedMaintenance,
                config.CapitalMaintenanceSafetyPercent
            );
            int committedCapitalMaintenance = context
                .Faction.GetOwnedUnitsByType<CapitalShip>()
                .Where(IsCommittedCapitalShip)
                .Sum(capitalShip => capitalShip.MaintenanceCost);
            int budget = Math.Max(0, targetCapitalMaintenance - committedCapitalMaintenance);

            _capitalShipMaintenanceBudget =
                context.Assessment.ProjectedMaintenanceHeadroom < budget ? 0 : budget;
            return _capitalShipMaintenanceBudget.Value;
        }

        /// <summary>
        /// Returns whether a capital ship is complete or already under construction.
        /// </summary>
        /// <param name="capitalShip">The capital ship to evaluate.</param>
        /// <returns>True when production planning must account for the ship.</returns>
        private static bool IsCommittedCapitalShip(CapitalShip capitalShip)
        {
            return capitalShip?.ManufacturingStatus
                is ManufacturingStatus.Complete
                    or ManufacturingStatus.Building;
        }

        /// <summary>
        /// Returns whether a fleet has a completed or constructing combat capital ship.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <returns>True when the fleet has committed capital-ship combat capability.</returns>
        private static bool HasCommittedCombatCapitalShip(Fleet fleet)
        {
            return fleet
                    ?.GetChildren<CapitalShip>()
                    .Any(capitalShip =>
                        IsCommittedCapitalShip(capitalShip)
                        && GetMaximumPrimaryWeaponWeight(capitalShip) > 0
                    ) == true;
        }

        /// <summary>
        /// Returns whether a capital ship is eligible for a production role.
        /// </summary>
        /// <param name="capitalShip">The capital ship to evaluate.</param>
        /// <param name="role">The role.</param>
        /// <returns>True when the ship satisfies the role requirements.</returns>
        private static bool CanFillCapitalShipRole(
            CapitalShip capitalShip,
            AICapitalShipProductionRole role
        )
        {
            if (capitalShip.CanDestroyPlanets)
                return false;

            return role switch
            {
                AICapitalShipProductionRole.General => !capitalShip.HasGravityWell
                    && GetMaximumPrimaryWeaponWeight(capitalShip) > 0,
                AICapitalShipProductionRole.TroopTransport => capitalShip.RegimentCapacity > 0
                    && !capitalShip.HasGravityWell
                    && GetMaximumPrimaryWeaponWeight(capitalShip) == 0,
                AICapitalShipProductionRole.Bombardment => capitalShip.Bombardment > 0,
                AICapitalShipProductionRole.Interdiction => capitalShip.HasGravityWell,
                _ => false,
            };
        }

        /// <summary>
        /// Calculates a capital ship's production priority for a requested fleet role.
        /// </summary>
        /// <param name="capitalShip">The capital ship to evaluate.</param>
        /// <param name="role">The role.</param>
        /// <param name="prioritizeGeneralDelivery">
        /// Whether an unready fleet needs its first combat ship delivered quickly.
        /// </param>
        /// <returns>The ship's comparable role-priority metric.</returns>
        private static long GetCapitalShipRoleMetric(
            CapitalShip capitalShip,
            AICapitalShipProductionRole role,
            bool prioritizeGeneralDelivery
        )
        {
            int constructionCost = Math.Max(1, capitalShip.ConstructionCost);
            long capabilityMetric = role switch
            {
                AICapitalShipProductionRole.General => GetPrimaryWeaponMetric(capitalShip)
                    * _roleMetricScale
                    / Math.Max(1, capitalShip.MaintenanceCost),
                AICapitalShipProductionRole.TroopTransport => capitalShip.RegimentCapacity,
                AICapitalShipProductionRole.Bombardment => capitalShip.Bombardment,
                AICapitalShipProductionRole.Interdiction => capitalShip.ShieldRechargeRate,
                _ => 0,
            };
            bool prioritizeConstructionRate =
                role != AICapitalShipProductionRole.General || prioritizeGeneralDelivery;
            return prioritizeConstructionRate
                ? capabilityMetric * _productionRateMetricScale / constructionCost
                : capabilityMetric;
        }

        /// <summary>
        /// Returns primary weapon metric.
        /// </summary>
        /// <param name="capitalShip">The capital ship to evaluate.</param>
        /// <returns>The calculated value.</returns>
        private static long GetPrimaryWeaponMetric(CapitalShip capitalShip)
        {
            long maximumWeight = 0;
            int selectedWeaponCount = 0;

            for (int arc = 0; arc < _weaponArcCount; arc++)
            {
                int turbolasers = GetWeaponCount(capitalShip, PrimaryWeaponType.Turbolaser, arc);
                int ionCannons = GetWeaponCount(capitalShip, PrimaryWeaponType.IonCannon, arc);
                int laserCannons = GetWeaponCount(capitalShip, PrimaryWeaponType.LaserCannon, arc);
                long weight =
                    (long)_primaryWeaponWeight * turbolasers
                    + (long)_primaryWeaponWeight * ionCannons
                    + (long)_primaryWeaponWeight * laserCannons / _primaryLaserDivisor;
                if (weight <= maximumWeight)
                    continue;

                maximumWeight = weight;
                selectedWeaponCount = turbolasers + ionCannons + laserCannons;
            }

            if (selectedWeaponCount <= 0)
                return 0;

            return (long)capitalShip.WeaponRecharge * maximumWeight / selectedWeaponCount;
        }

        /// <summary>
        /// Returns maximum primary weapon weight.
        /// </summary>
        /// <param name="capitalShip">The capital ship to evaluate.</param>
        /// <returns>The calculated value.</returns>
        private static long GetMaximumPrimaryWeaponWeight(CapitalShip capitalShip)
        {
            long maximumWeight = 0;
            for (int arc = 0; arc < _weaponArcCount; arc++)
            {
                long weight =
                    (long)_primaryWeaponWeight
                        * GetWeaponCount(capitalShip, PrimaryWeaponType.Turbolaser, arc)
                    + (long)_primaryWeaponWeight
                        * GetWeaponCount(capitalShip, PrimaryWeaponType.IonCannon, arc)
                    + (long)_primaryWeaponWeight
                        * GetWeaponCount(capitalShip, PrimaryWeaponType.LaserCannon, arc)
                        / _primaryLaserDivisor;
                maximumWeight = Math.Max(maximumWeight, weight);
            }

            return maximumWeight;
        }

        /// <summary>
        /// Returns weapon count.
        /// </summary>
        /// <param name="capitalShip">The capital ship to evaluate.</param>
        /// <param name="weaponType">The weapon type.</param>
        /// <param name="arc">The arc.</param>
        /// <returns>The calculated value.</returns>
        private static int GetWeaponCount(
            CapitalShip capitalShip,
            PrimaryWeaponType weaponType,
            int arc
        )
        {
            if (
                capitalShip?.PrimaryWeapons == null
                || !capitalShip.PrimaryWeapons.TryGetValue(weaponType, out int[] values)
                || values == null
                || arc < 0
                || arc >= values.Length
            )
                return 0;

            return values[arc];
        }

        /// <summary>
        /// Returns the unlocked starfighter technology for a fleet demand.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">Fleet receiving the starfighter.</param>
        /// <returns>The selected technology, or null.</returns>
        private Technology GetUnlockedStarfighterTechnology(AITurnContext context, Fleet fleet)
        {
            GameConfig.AISelectionConfig config = context.Game.Config.AI.Selection;
            return GetUnlockedFleetTechnology<Starfighter>(
                context,
                fleet,
                ManufacturingType.Ship,
                config.PreferredStarfighterTypeCountPerFleet,
                starfighter => ScoreStarfighterTechnology(config, fleet, starfighter)
            );
        }

        /// <summary>
        /// Returns unlocked planetary starfighter technology.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The selected value, or null when none is available.</returns>
        private Technology GetUnlockedPlanetaryStarfighterTechnology(AITurnContext context)
        {
            int maintenanceBudget = GetDefensiveMaintenanceBudget(context);
            return GetUnlockedTechnologies(context, ManufacturingType.Ship)
                .Where(technology =>
                    technology.GetReference() is Starfighter starfighter
                    && IManufacturable.CanBeManufacturedBy(starfighter, context.Faction.InstanceID)
                    && starfighter.MaintenanceCost <= maintenanceBudget
                    && starfighter.GetWeaponStrength() > 0
                )
                .OrderByDescending(technology =>
                    GetPlanetaryStarfighterDefenseEfficiency((Starfighter)technology.GetReference())
                )
                .ThenByDescending(technology =>
                    ((Starfighter)technology.GetReference()).GetWeaponStrength()
                )
                .ThenByDescending(technology => technology.GetResearchOrder())
                .ThenBy(technology => technology.GetReference().GetMaintenanceCost())
                .ThenBy(technology => technology.GetReference().GetConstructionCost())
                .ThenBy(technology => technology.GetReference().GetTypeID())
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns planetary starfighter defense efficiency.
        /// </summary>
        /// <param name="starfighter">The starfighter.</param>
        /// <returns>The calculated value.</returns>
        private static double GetPlanetaryStarfighterDefenseEfficiency(Starfighter starfighter)
        {
            int strength = starfighter.GetWeaponStrength();
            return starfighter.MaintenanceCost > 0
                ? strength / (double)starfighter.MaintenanceCost
                : double.MaxValue;
        }

        /// <summary>
        /// Returns the unlocked regiment technology for a fleet demand.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">Fleet receiving the regiment.</param>
        /// <returns>The selected technology, or null.</returns>
        private Technology GetUnlockedRegimentTechnology(AITurnContext context, Fleet fleet)
        {
            GameConfig.AISelectionConfig config = context.Game.Config.AI.Selection;
            return GetUnlockedFleetTechnology<Regiment>(
                context,
                fleet,
                ManufacturingType.Troop,
                config.PreferredRegimentTypeCountPerDestination,
                regiment => ScoreRegimentTechnology(config, fleet, regiment)
            );
        }

        /// <summary>
        /// Selects an unlocked fleet-unit technology through shared diversity and tie-break rules.
        /// </summary>
        /// <typeparam name="T">The fleet-unit type referenced by eligible technologies.</typeparam>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet receiving the manufactured unit.</param>
        /// <param name="manufacturingType">The technology catalog to inspect.</param>
        /// <param name="maximumDuplicateCount">The preferred per-type duplicate limit.</param>
        /// <param name="getScore">Returns the unit-specific selection score.</param>
        /// <returns>The selected technology, or null when none is unlocked.</returns>
        private Technology GetUnlockedFleetTechnology<T>(
            AITurnContext context,
            Fleet fleet,
            ManufacturingType manufacturingType,
            int maximumDuplicateCount,
            Func<T, double> getScore
        )
            where T : class, IManufacturable
        {
            List<Technology> technologies = GetUnlockedTechnologies(context, manufacturingType)
                .Where(technology => technology.GetReference() is T)
                .ToList();
            List<Technology> preferredTechnologies = technologies
                .Where(technology =>
                    CountFleetUnitsByType<T>(fleet, technology.GetReference().GetTypeID())
                    < maximumDuplicateCount
                )
                .ToList();

            return (preferredTechnologies.Count > 0 ? preferredTechnologies : technologies)
                .OrderByDescending(technology => getScore((T)technology.GetReference()))
                .ThenBy(technology => technology.GetReference().GetConstructionCost())
                .ThenBy(technology => technology.GetReference().GetTypeID())
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns unlocked garrison regiment technology.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The selected value, or null when none is available.</returns>
        private Technology GetUnlockedGarrisonRegimentTechnology(AITurnContext context)
        {
            GameConfig.AISelectionConfig config = context.Game.Config.AI.Selection;
            int maintenanceBudget = GetDefensiveMaintenanceBudget(context);
            return GetUnlockedTechnologies(context, ManufacturingType.Troop)
                .Where(technology =>
                    technology.GetReference() is Regiment regiment
                    && regiment.MaintenanceCost <= maintenanceBudget
                )
                .OrderByDescending(technology =>
                    ScoreRegimentTechnology(config, null, (Regiment)technology.GetReference())
                )
                .ThenBy(technology => technology.GetReference().GetConstructionCost())
                .ThenBy(technology => technology.GetReference().GetTypeID())
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns the score for a starfighter technology.
        /// </summary>
        /// <param name="config">AI selection configuration.</param>
        /// <param name="fleet">Fleet receiving the starfighter.</param>
        /// <param name="starfighter">Starfighter to score.</param>
        /// <returns>The starfighter technology score.</returns>
        private double ScoreStarfighterTechnology(
            GameConfig.AISelectionConfig config,
            Fleet fleet,
            Starfighter starfighter
        )
        {
            double score =
                starfighter.LaserCannon * config.StarfighterEscortWeight
                + starfighter.IonCannon * config.StarfighterInterceptorWeight
                + starfighter.Torpedoes * config.StarfighterBomberWeight;

            if (starfighter.IonCannon > 0 && !FleetHasIonStarfighter(fleet))
                score += config.StarfighterMissingInterceptorBoost;

            if (starfighter.Torpedoes > 0 && !FleetHasTorpedoStarfighter(fleet))
                score += config.StarfighterMissingBomberBoost;

            score -=
                CountFleetUnitsByType<Starfighter>(fleet, starfighter.GetTypeID())
                * config.LocalDuplicatePenaltyPerSelection;

            return score;
        }

        /// <summary>
        /// Returns the score for a regiment technology.
        /// </summary>
        /// <param name="config">AI selection configuration.</param>
        /// <param name="fleet">Fleet receiving the regiment.</param>
        /// <param name="regiment">Regiment to score.</param>
        /// <returns>The regiment technology score.</returns>
        private double ScoreRegimentTechnology(
            GameConfig.AISelectionConfig config,
            Fleet fleet,
            Regiment regiment
        )
        {
            return regiment.AttackRating * config.RegimentAttackWeight
                + regiment.DefenseRating * config.RegimentDefenseWeight
                + regiment.BombardmentDefense * config.RegimentBombardmentDefenseWeight
                + config.RegimentFleetAttackBoost
                - regiment.MaintenanceCost * config.RegimentMaintenanceCostWeight
                - CountFleetUnitsByType<Regiment>(fleet, regiment.GetTypeID())
                    * config.LocalDuplicatePenaltyPerSelection;
        }

        /// <summary>
        /// Returns producer planets eligible for a demand item.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demand">Demand item to satisfy.</param>
        /// <returns>Eligible producer planets.</returns>
        private IEnumerable<Planet> FindProducerPlanets(AITurnContext context, AIDemand demand)
        {
            if (context?.Assessment == null || demand?.Destination == null)
                return Enumerable.Empty<Planet>();

            Planet destinationPlanet = GetDestinationPlanet(context, demand);
            ProducerMode mode =
                IsFacilityExpansionDemand(demand) ? ProducerMode.FacilityExpansion
                : IsDistributedProductionDemand(demand) ? ProducerMode.Distributed
                : ProducerMode.AvailableCapacity;
            (string DestinationId, ManufacturingType ManufacturingType, ProducerMode Mode) key = (
                destinationPlanet?.InstanceID,
                demand.ManufacturingType,
                mode
            );
            if (_producerPlanets.TryGetValue(key, out List<Planet> producers))
                return producers;

            producers = context
                .Assessment.OwnedPlanets.Where(planet =>
                    mode == ProducerMode.FacilityExpansion
                        ? CanQueueFacilityExpansion(context, planet)
                    : mode == ProducerMode.Distributed
                        ? HasProductionFacility(context, planet, demand.ManufacturingType)
                    : CanProduce(planet, demand.ManufacturingType)
                )
                .OrderBy(planet =>
                    destinationPlanet == null ? 0 : destinationPlanet.GetRawDistanceTo(planet)
                )
                .ThenByDescending(planet =>
                    context.Assessment.GetPlanetProductionRate(planet, demand.ManufacturingType)
                )
                .ThenBy(planet => planet.InstanceID)
                .ToList();
            _producerPlanets.Add(key, producers);
            return producers;
        }

        /// <summary>
        /// Returns whether queue facility expansion.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The planet to evaluate.</param>
        /// <returns>True when the condition is satisfied.</returns>
        private bool CanQueueFacilityExpansion(AITurnContext context, Planet planet)
        {
            return planet?.IsColonized == true
                && !planet.IsDestroyed
                && context.Assessment.GetPlanetProductionFacilityCount(
                    planet,
                    ManufacturingType.Building
                ) > 0;
        }

        /// <summary>
        /// Returns the destination planet for a demand item.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demand">Demand item to inspect.</param>
        /// <returns>The destination planet, or null.</returns>
        private Planet GetDestinationPlanet(AITurnContext context, AIDemand demand)
        {
            if (demand?.Destination is Planet planet)
                return planet;

            if (demand?.Destination is Fleet fleet)
                return context.Assessment.GetFleetPlanet(fleet);

            return null;
        }

        /// <summary>
        /// Returns whether a planet can produce a manufacturing type.
        /// </summary>
        /// <param name="planet">The planet to inspect.</param>
        /// <param name="manufacturingType">Manufacturing type to produce.</param>
        /// <returns>True if the planet can produce the type.</returns>
        private bool CanProduce(Planet planet, ManufacturingType manufacturingType)
        {
            if (planet == null)
                return false;

            return planet.IsColonized
                && !planet.IsDestroyed
                && planet.GetAvailableManufacturingCapacity(manufacturingType) > 0;
        }

        /// <summary>
        /// Returns whether production facility.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The planet to evaluate.</param>
        /// <param name="manufacturingType">The manufacturing category.</param>
        /// <returns>True when the condition is satisfied.</returns>
        private static bool HasProductionFacility(
            AITurnContext context,
            Planet planet,
            ManufacturingType manufacturingType
        )
        {
            return planet?.IsColonized == true
                && !planet.IsDestroyed
                && context.Assessment.GetPlanetProductionFacilityCount(planet, manufacturingType)
                    > 0;
        }

        /// <summary>
        /// Returns how many fleet units match a type id.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <param name="typeId">Unit type id to count.</param>
        /// <returns>The matching unit count.</returns>
        private int CountFleetUnitsByType<T>(Fleet fleet, string typeId)
            where T : class, IManufacturable
        {
            if (fleet == null || string.IsNullOrEmpty(typeId))
                return 0;

            (string FleetId, Type UnitType, string TypeId) key = (
                fleet.InstanceID,
                typeof(T),
                typeId
            );
            if (_fleetUnitCounts.TryGetValue(key, out int count))
                return count;

            if (typeof(T) == typeof(Starfighter))
                count = fleet
                    .GetStarfighters()
                    .Count(starfighter => starfighter.GetTypeID() == typeId);
            else if (typeof(T) == typeof(Regiment))
                count = fleet.GetRegiments().Count(regiment => regiment.GetTypeID() == typeId);
            else if (typeof(T) == typeof(CapitalShip))
                count = fleet
                    .GetChildren<CapitalShip>()
                    .Count(capitalShip => capitalShip.GetTypeID() == typeId);

            _fleetUnitCounts.Add(key, count);
            return count;
        }

        /// <summary>
        /// Returns whether a fleet already has an ion starfighter.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <returns>True if the fleet has an ion starfighter.</returns>
        private bool FleetHasIonStarfighter(Fleet fleet)
        {
            if (fleet == null)
                return false;

            if (_fleetHasIonStarfighters.TryGetValue(fleet.InstanceID, out bool hasIonStarfighter))
                return hasIonStarfighter;

            hasIonStarfighter = fleet
                .GetStarfighters()
                .Any(starfighter => starfighter.IonCannon > 0);
            _fleetHasIonStarfighters.Add(fleet.InstanceID, hasIonStarfighter);
            return hasIonStarfighter;
        }

        /// <summary>
        /// Returns whether a fleet already has a torpedo starfighter.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <returns>True if the fleet has a torpedo starfighter.</returns>
        private bool FleetHasTorpedoStarfighter(Fleet fleet)
        {
            if (fleet == null)
                return false;

            if (
                _fleetHasTorpedoStarfighters.TryGetValue(
                    fleet.InstanceID,
                    out bool hasTorpedoStarfighter
                )
            )
                return hasTorpedoStarfighter;

            hasTorpedoStarfighter = fleet
                .GetStarfighters()
                .Any(starfighter => starfighter.Torpedoes > 0);
            _fleetHasTorpedoStarfighters.Add(fleet.InstanceID, hasTorpedoStarfighter);
            return hasTorpedoStarfighter;
        }

        /// <summary>
        /// Gets the faction's unlocked technologies for a manufacturing category.
        /// </summary>
        /// <param name="context">Current AI turn context.</param>
        /// <param name="manufacturingType">Manufacturing category to retrieve.</param>
        /// <returns>Unlocked technologies in the requested category.</returns>
        private List<Technology> GetUnlockedTechnologies(
            AITurnContext context,
            ManufacturingType manufacturingType
        )
        {
            if (
                _unlockedTechnologies.TryGetValue(
                    manufacturingType,
                    out List<Technology> technologies
                )
            )
                return technologies;

            technologies = context.Faction.GetUnlockedTechnologies(manufacturingType).ToList();
            _unlockedTechnologies.Add(manufacturingType, technologies);
            return technologies;
        }

        /// <summary>
        /// Defines the production eligibility rule used to locate producer planets.
        /// </summary>
        private enum ProducerMode
        {
            /// <summary>
            /// Requires currently available manufacturing capacity.
            /// </summary>
            AvailableCapacity,

            /// <summary>
            /// Requires an appropriate manufacturing facility.
            /// </summary>
            Distributed,

            /// <summary>
            /// Requires a planet eligible for facility expansion.
            /// </summary>
            FacilityExpansion,
        }
    }
}
