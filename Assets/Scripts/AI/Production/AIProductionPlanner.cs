using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
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
    /// Builds production proposals from current demand.
    /// </summary>
    public sealed class AIProductionPlanner : IAIProposalPlanner
    {
        private readonly AIProductionRequirements _demandGenerator = new AIProductionRequirements();
        private readonly Dictionary<ManufacturingType, List<Technology>> _unlockedTechnologies =
            new Dictionary<ManufacturingType, List<Technology>>();
        private readonly Dictionary<
            (
                AIProductionRequirementKind Kind,
                BuildingType BuildingType,
                string DestinationId,
                string ProductTypeId,
                string ReplacementTypeId
            ),
            Technology
        > _selectedTechnologies =
            new Dictionary<
                (
                    AIProductionRequirementKind Kind,
                    BuildingType BuildingType,
                    string DestinationId,
                    string ProductTypeId,
                    string ReplacementTypeId
                ),
                Technology
            >();
        private readonly Dictionary<
            (
                string DestinationId,
                ManufacturingType ManufacturingType,
                ProducerMode Mode,
                AIProductionRequirementKind DemandKind,
                string ProductTypeId,
                int Quantity
            ),
            List<Planet>
        > _producerPlanets =
            new Dictionary<
                (
                    string DestinationId,
                    ManufacturingType ManufacturingType,
                    ProducerMode Mode,
                    AIProductionRequirementKind DemandKind,
                    string ProductTypeId,
                    int Quantity
                ),
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
        private readonly Dictionary<
            (
                string ProducerPlanetId,
                string DestinationFleetId,
                string ProductTypeId,
                int Quantity
            ),
            int
        > _reinforcementArrivalTicks = new();

        /// <summary>
        /// Returns production proposals for the current AI turn.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Production proposals generated for this faction.</returns>
        public List<AIProposal> Plan(AITurnContext context)
        {
            ResetPlanningCache();
            List<AIProductionRequirement> demands = _demandGenerator.BuildRequirements(context);
            return GenerateProposals(context, demands);
        }

        /// <summary>
        /// Clears values indexed from the current game state before planning a new turn.
        /// </summary>
        private void ResetPlanningCache()
        {
            _unlockedTechnologies.Clear();
            _selectedTechnologies.Clear();
            _producerPlanets.Clear();
            _queueWork.Clear();
            _fleetUnitCounts.Clear();
            _fleetHasIonStarfighters.Clear();
            _fleetHasTorpedoStarfighters.Clear();
            _reinforcementArrivalTicks.Clear();
        }

        /// <summary>
        /// Generates manufacture proposals for demand items.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demands">Requirement items to satisfy.</param>
        /// <returns>Manufacture proposals generated for the demands.</returns>
        private List<AIProposal> GenerateProposals(
            AITurnContext context,
            List<AIProductionRequirement> demands
        )
        {
            List<AIProposal> proposals = new List<AIProposal>();
            if (context?.Faction == null || demands == null || demands.Count == 0)
                return proposals;

            foreach (AIProductionRequirement demand in demands)
            {
                AddManufactureProposal(context, demand, proposals);
            }

            return proposals;
        }

        /// <summary>
        /// Adds manufacture proposals for one demand item.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demand">Requirement item to satisfy.</param>
        /// <param name="proposals">The proposal list to update.</param>
        private void AddManufactureProposal(
            AITurnContext context,
            AIProductionRequirement demand,
            List<AIProposal> proposals
        )
        {
            Technology product = GetUnlockedTechnology(context, demand);
            if (product == null)
                return;

            bool distributesRequirement = IsDistributedProductionRequirement(demand);
            int remainingQuantity = GetRequestedManufacturingCount(
                context,
                demand,
                product.GetReference()
            );
            if (distributesRequirement)
            {
                remainingQuantity = Math.Min(
                    remainingQuantity,
                    GetFleetUnitDiversityLimit(context, demand, product.GetReference())
                );
            }
            if (remainingQuantity <= 0)
                return;

            List<Planet> producerPlanets = FindProducerPlanets(
                    context,
                    demand,
                    product.GetReference(),
                    remainingQuantity
                )
                .ToList();
            if (producerPlanets.Count == 0)
                return;
            if (!distributesRequirement)
            {
                if (IsFacilityExpansionRequirement(demand))
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
                AIProductionRequirement proposalDemand = GetProposalDemand(
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
                    distributesRequirement
                );

                proposals.Add(proposal);
                if (distributesRequirement)
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
        /// <param name="demand">Requirement item to satisfy.</param>
        /// <param name="product">Technology selected for manufacture.</param>
        /// <param name="remainingQuantity">Quantity still required.</param>
        /// <param name="producerPlanets">Ranked producer alternatives.</param>
        /// <param name="proposals">The proposal list to update.</param>
        private void AddEquivalentProducerProposal(
            AITurnContext context,
            AIProductionRequirement demand,
            Technology product,
            int remainingQuantity,
            IReadOnlyList<Planet> producerPlanets,
            List<AIProposal> proposals
        )
        {
            if (producerPlanets.Count == 0)
                return;

            AIProductionRequirement proposalDemand = GetProposalDemand(
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
                    distributesRequirement: false
                )
            );
        }

        /// <summary>
        /// Adds one proposal whose alternatives require producer-specific demand values.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demand">Requirement item to satisfy.</param>
        /// <param name="product">Technology selected for manufacture.</param>
        /// <param name="remainingQuantity">Quantity still required.</param>
        /// <param name="producerPlanets">Ranked producer alternatives.</param>
        /// <param name="proposals">The proposal list to update.</param>
        private void AddProducerSpecificProposal(
            AITurnContext context,
            AIProductionRequirement demand,
            Technology product,
            int remainingQuantity,
            IReadOnlyList<Planet> producerPlanets,
            List<AIProposal> proposals
        )
        {
            List<AIManufactureProposal> candidates = producerPlanets
                .Select(producerPlanet =>
                {
                    AIProductionRequirement candidateDemand = GetProposalDemand(
                        context,
                        demand,
                        producerPlanet,
                        product,
                        remainingQuantity
                    );
                    return candidateDemand == null
                        ? null
                        : new AIManufactureProposal(
                            candidateDemand,
                            producerPlanet,
                            product,
                            distributesRequirement: false
                        );
                })
                .Where(candidate => candidate != null)
                .ToList();
            if (candidates.Count == 0)
                return;

            proposals.Add(new AIManufactureProposal(candidates));
        }

        /// <summary>
        /// Returns the unlocked technology that can satisfy a demand.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demand">Requirement item to satisfy.</param>
        /// <returns>The selected technology, or null.</returns>
        private Technology GetUnlockedTechnology(
            AITurnContext context,
            AIProductionRequirement demand
        )
        {
            if (demand == null)
                return null;

            if (
                demand.Kind
                is AIProductionRequirementKind.FleetCapitalShip
                    or AIProductionRequirementKind.FleetSeedCapitalShip
                    or AIProductionRequirementKind.ColonizationFleetSeedCapitalShip
            )
                return GetUnlockedCapitalShipTechnology(context, demand);

            (
                AIProductionRequirementKind Kind,
                BuildingType BuildingType,
                string DestinationId,
                string ProductTypeId,
                string ReplacementTypeId
            ) key = (
                demand.Kind,
                demand.BuildingType,
                demand.Kind
                    is AIProductionRequirementKind.FleetStarfighter
                        or AIProductionRequirementKind.FleetRegiment
                    ? demand.Destination?.InstanceID
                    : null,
                demand.ProductTypeId,
                demand.BuildingToReplace?.GetTypeID()
            );
            if (_selectedTechnologies.TryGetValue(key, out Technology selectedTechnology))
                return selectedTechnology;

            selectedTechnology = demand.Kind switch
            {
                AIProductionRequirementKind.Colony
                or AIProductionRequirementKind.Mine
                or AIProductionRequirementKind.Refinery
                or AIProductionRequirementKind.ConstructionFacility
                or AIProductionRequirementKind.Shipyard
                or AIProductionRequirementKind.TrainingFacility
                or AIProductionRequirementKind.BuildingUpgrade
                or AIProductionRequirementKind.PlanetaryDefense => GetUnlockedBuildingTechnology(
                    context,
                    demand
                ),
                AIProductionRequirementKind.FleetStarfighter
                or AIProductionRequirementKind.PlanetaryStarfighterReserve
                or AIProductionRequirementKind.FleetRegiment
                or AIProductionRequirementKind.GarrisonRegimentReserve
                or AIProductionRequirementKind.SpecialForces => GetUnlockedUnitTechnology(
                    context,
                    demand
                ),
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
        private Technology GetUnlockedBuildingTechnology(
            AITurnContext context,
            AIProductionRequirement demand
        )
        {
            if (context?.Faction == null || demand?.BuildingType == BuildingType.None)
                return null;

            GameConfig.AISelectionConfig selectionConfig = context.Game.Config.AI.Selection;
            int maintenanceBudget = GetBuildingMaintenanceBudget(context, demand);
            if (IsFacilityExpansionRequirement(demand) && maintenanceBudget <= 0)
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
                    ScoreBuildingTechnology(selectionConfig, (Building)technology.GetReference())
                )
                .ThenByDescending(technology => technology.GetResearchOrder())
                .ThenBy(technology => technology.GetReference().GetMaintenanceCost())
                .ThenBy(technology => technology.GetReference().GetConstructionCost())
                .ThenBy(technology => technology.GetReference().GetTypeID())
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns whether a building can satisfy an ordinary or upgrade demand.
        /// </summary>
        /// <param name="demand">The production demand.</param>
        /// <param name="building">The building to evaluate.</param>
        /// <returns>True when no replacement is required or the building is its declared upgrade.</returns>
        private static bool IsEligibleBuildingUpgrade(
            AIProductionRequirement demand,
            Building building
        )
        {
            return demand.Kind != AIProductionRequirementKind.BuildingUpgrade
                || demand.BuildingToReplace.CanUpgradeTo(building);
        }

        /// <summary>
        /// Returns the additional maintenance incurred by constructing or upgrading a building.
        /// </summary>
        /// <param name="demand">The production demand.</param>
        /// <param name="building">The building to evaluate.</param>
        /// <returns>The net maintenance cost.</returns>
        private static int GetBuildingMaintenanceCost(
            AIProductionRequirement demand,
            Building building
        )
        {
            if (
                demand.Kind != AIProductionRequirementKind.BuildingUpgrade
                || demand.BuildingToReplace == null
            )
                return building.MaintenanceCost;

            return Math.Max(0, building.MaintenanceCost - demand.BuildingToReplace.MaintenanceCost);
        }

        /// <summary>
        /// Returns the maintenance budget available to a building demand.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demand">The production demand.</param>
        /// <returns>The available maintenance budget.</returns>
        private int GetBuildingMaintenanceBudget(
            AITurnContext context,
            AIProductionRequirement demand
        )
        {
            if (IsFacilityExpansionRequirement(demand))
                return GetFacilityMaintenanceBudget(context);

            if (demand.UsesDefensiveReserve)
                return GetDefensiveMaintenanceBudget(context);

            return Math.Max(
                0,
                context.Assessment.ProjectedMaintenanceHeadroom
                    - context.Game.Config.AI.Selection.MaintenanceHeadroomReserve
            );
        }

        /// <summary>
        /// Returns the maintenance budget allocated to production-facility expansion.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The remaining facility maintenance budget.</returns>
        private int GetFacilityMaintenanceBudget(AITurnContext context)
        {
            return Math.Max(
                0,
                context.Assessment.ProjectedMaintenanceHeadroom
                    - context.Game.Config.AI.Selection.MaintenanceHeadroomReserve
            );
        }

        /// <summary>
        /// Creates the producer-specific demand represented by one proposal.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demand">The production demand.</param>
        /// <param name="producerPlanet">The producing planet.</param>
        /// <param name="product">The manufacturable product.</param>
        /// <param name="remainingQuantity">The remaining requested quantity.</param>
        /// <returns>The adjusted demand, or null when this producer cannot accept a batch.</returns>
        private AIProductionRequirement GetProposalDemand(
            AITurnContext context,
            AIProductionRequirement demand,
            Planet producerPlanet,
            Technology product,
            int remainingQuantity
        )
        {
            if (demand.Kind == AIProductionRequirementKind.BuildingUpgrade)
                return demand;

            if (IsDistributedProductionRequirement(demand))
            {
                int distributedQuantity = GetDistributedBatchSize(
                    context,
                    producerPlanet,
                    product.GetReference(),
                    remainingQuantity
                );
                return distributedQuantity > 0
                    ? CreateProposalRequirement(demand, distributedQuantity)
                    : null;
            }

            if (!IsFacilityExpansionRequirement(demand) && !demand.UsesDefensiveReserve)
                return demand;

            int quantity;
            if (IsFacilityExpansionRequirement(demand))
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

            return CreateProposalRequirement(demand, quantity);
        }

        /// <summary>
        /// Copies a demand with a producer-specific quantity.
        /// </summary>
        /// <param name="demand">The production demand.</param>
        /// <param name="quantity">The requested quantity.</param>
        /// <returns>The copied demand.</returns>
        private static AIProductionRequirement CreateProposalRequirement(
            AIProductionRequirement demand,
            int quantity
        )
        {
            return demand.WithQuantity(quantity);
        }

        /// <summary>
        /// Returns the requested batch size for a demand and product.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demand">The production demand.</param>
        /// <param name="product">The manufacturable product.</param>
        /// <returns>The requested unit count.</returns>
        private int GetRequestedManufacturingCount(
            AITurnContext context,
            AIProductionRequirement demand,
            IManufacturable product
        )
        {
            if (demand.Kind == AIProductionRequirementKind.PlanetaryStarfighterReserve)
            {
                return Math.Min(1, Math.Max(0, demand.QuantityNeeded));
            }

            if (!IsDistributedProductionRequirement(demand))
                return Math.Max(0, demand.QuantityNeeded);

            int requestedCount =
                demand.Kind == AIProductionRequirementKind.FleetCapitalShip
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
            AIProductionRequirement demand,
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

            return IntegerMath.DivideRoundedUp(demand.QuantityNeeded, contribution);
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
            AIProductionRequirement demand,
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
            AIProductionRequirement demand,
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
                context.Game.Config.AI.Selection.MaintenanceHeadroomReserve,
                IntegerMath.ScaleByPercentRoundedUp(
                    context.Assessment.MaintenanceCapacity,
                    context.Game.Config.AI.Infrastructure.PlanetaryDefenseMaintenanceReservePercent
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
            AIProductionRequirement demand,
            Planet producerPlanet,
            Building building
        )
        {
            int maintenanceBudget = GetFacilityMaintenanceBudget(context);
            int maintenanceLimit =
                building.MaintenanceCost > 0
                    ? maintenanceBudget / building.MaintenanceCost
                    : int.MaxValue;
            int facilityCount = context.Assessment.GetPlanetProductionFacilityCount(
                producerPlanet,
                ManufacturingType.Building
            );
            int laneReserve =
                demand.Kind == AIProductionRequirementKind.ConstructionFacility
                    ? 0
                    : Math.Max(
                        0,
                        context.Game.Config.AI.Infrastructure.FacilityConstructionLaneReserve
                    );
            int laneLimit =
                facilityCount > laneReserve ? facilityCount - laneReserve : facilityCount;
            int queueLimit = GetQueueBatchCapacity(context, producerPlanet, building);
            int energyLimit = demand.DestinationPlanet.GetAvailableEnergy();

            return Math.Max(
                0,
                Math.Min(
                    demand.QuantityNeeded,
                    Math.Min(
                        maintenanceLimit,
                        Math.Min(laneLimit, Math.Min(queueLimit, energyLimit))
                    )
                )
            );
        }

        /// <summary>
        /// Returns whether a demand expands a production facility category.
        /// </summary>
        /// <param name="demand">The production demand.</param>
        /// <returns>True for construction-facility, shipyard, and training-facility demand.</returns>
        private static bool IsFacilityExpansionRequirement(AIProductionRequirement demand)
        {
            return demand?.Kind
                is AIProductionRequirementKind.ConstructionFacility
                    or AIProductionRequirementKind.Shipyard
                    or AIProductionRequirementKind.TrainingFacility;
        }

        /// <summary>
        /// Returns whether a demand can be divided among multiple producers.
        /// </summary>
        /// <param name="demand">The production demand.</param>
        /// <returns>True when separate producers may manufacture portions of the demand.</returns>
        private static bool IsDistributedProductionRequirement(AIProductionRequirement demand)
        {
            return demand?.Kind
                is AIProductionRequirementKind.FleetCapitalShip
                    or AIProductionRequirementKind.FleetStarfighter
                    or AIProductionRequirementKind.PlanetaryStarfighterReserve
                    or AIProductionRequirementKind.FleetRegiment
                    or AIProductionRequirementKind.SpecialForces;
        }

        /// <summary>
        /// Returns the comparable production or defensive capability of a building.
        /// </summary>
        /// <param name="config">AI selection configuration.</param>
        /// <param name="building">The building to evaluate.</param>
        /// <returns>The building capability used for technology selection.</returns>
        private static double ScoreBuildingTechnology(
            GameConfig.AISelectionConfig config,
            Building building
        )
        {
            double capability = building.GetBuildingType() switch
            {
                BuildingType.ConstructionFacility
                or BuildingType.Shipyard
                or BuildingType.TrainingFacility
                or BuildingType.Mine
                or BuildingType.Refinery => building.ProcessRate > 0
                    ? 1.0 / building.ProcessRate
                    : 0,
                BuildingType.Weapon => building.WeaponPower,
                BuildingType.Defense => building.ShieldStrength,
                _ => 0,
            };
            return AIUtility.Evaluate(
                AIUtility.Fulfillment(capability, AIUtilityDomain.ProductionCapability),
                config.TechnologyUtility.Building.Capability
            );
        }

        /// <summary>
        /// Returns the unlocked unit technology for a demand item.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demand">Requirement item to satisfy.</param>
        /// <returns>The selected technology, or null.</returns>
        private Technology GetUnlockedUnitTechnology(
            AITurnContext context,
            AIProductionRequirement demand
        )
        {
            if (context?.Faction == null || demand == null)
                return null;

            return demand.Kind switch
            {
                AIProductionRequirementKind.FleetCapitalShip => GetUnlockedCapitalShipTechnology(
                    context,
                    demand
                ),
                AIProductionRequirementKind.FleetSeedCapitalShip
                or AIProductionRequirementKind.ColonizationFleetSeedCapitalShip =>
                    GetUnlockedCapitalShipTechnology(context, demand),
                AIProductionRequirementKind.FleetStarfighter => GetUnlockedStarfighterTechnology(
                    context,
                    demand.DestinationFleet
                ),
                AIProductionRequirementKind.PlanetaryStarfighterReserve =>
                    GetUnlockedPlanetaryStarfighterTechnology(context),
                AIProductionRequirementKind.FleetRegiment => GetUnlockedRegimentTechnology(
                    context,
                    demand.DestinationFleet
                ),
                AIProductionRequirementKind.GarrisonRegimentReserve =>
                    GetUnlockedGarrisonRegimentTechnology(context),
                AIProductionRequirementKind.SpecialForces => GetUnlockedSpecialForcesTechnology(
                    context,
                    demand.ProductTypeId
                ),
                _ => null,
            };
        }

        /// <summary>
        /// Returns the unlocked special-forces technology matching a requested type.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="productTypeId">The product type id.</param>
        /// <returns>The matching technology, or null when it is unavailable.</returns>
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
        private Technology GetUnlockedCapitalShipTechnology(
            AITurnContext context,
            AIProductionRequirement demand
        )
        {
            if (context?.Faction == null || demand == null)
                return null;

            bool needsStarfighterCapacity =
                demand.CapitalShipRole == AICapitalShipProductionRole.General
                && demand.DestinationFleet?.GetStarfighterCapacity() <= 0;
            List<Technology> eligibleTechnologies = new List<Technology>();
            List<Technology> carrierTechnologies = new List<Technology>();

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

                eligibleTechnologies.Add(technology);
                if (needsStarfighterCapacity && capitalShip.StarfighterCapacity > 0)
                    carrierTechnologies.Add(technology);
            }

            if (eligibleTechnologies.Count == 0)
                return null;

            if (carrierTechnologies.Count > 0)
                eligibleTechnologies = carrierTechnologies;

            eligibleTechnologies.Sort(
                (left, right) =>
                    string.CompareOrdinal(
                        left.GetReference().GetTypeID(),
                        right.GetReference().GetTypeID()
                    )
            );
            return eligibleTechnologies[context.Random.NextInt(0, eligibleTechnologies.Count)];
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
                    && GetMaximumPrimaryWeaponStrength(capitalShip) > 0,
                AICapitalShipProductionRole.TroopTransport => capitalShip.RegimentCapacity > 0
                    && !capitalShip.HasGravityWell
                    && GetMaximumPrimaryWeaponStrength(capitalShip) == 0,
                AICapitalShipProductionRole.Bombardment => capitalShip.Bombardment > 0,
                AICapitalShipProductionRole.Interdiction => capitalShip.HasGravityWell,
                _ => false,
            };
        }

        /// <summary>
        /// Returns the largest primary-weapon count on any firing arc.
        /// </summary>
        /// <param name="capitalShip">The capital ship to evaluate.</param>
        /// <returns>The maximum weapon count.</returns>
        private static int GetMaximumPrimaryWeaponStrength(CapitalShip capitalShip)
        {
            int maximumStrength = 0;
            foreach (PrimaryWeaponArc weaponArc in CapitalShip.PrimaryWeaponArcs)
            {
                int strength =
                    GetWeaponCount(capitalShip, PrimaryWeaponType.Turbolaser, weaponArc)
                    + GetWeaponCount(capitalShip, PrimaryWeaponType.IonCannon, weaponArc)
                    + GetWeaponCount(capitalShip, PrimaryWeaponType.LaserCannon, weaponArc);
                maximumStrength = Math.Max(maximumStrength, strength);
            }

            return maximumStrength;
        }

        /// <summary>
        /// Returns the weapon count for a type and firing arc.
        /// </summary>
        /// <param name="capitalShip">The capital ship to evaluate.</param>
        /// <param name="weaponType">The weapon type.</param>
        /// <param name="weaponArc">The weapon arc.</param>
        /// <returns>The weapon count, or zero when no value is defined.</returns>
        private static int GetWeaponCount(
            CapitalShip capitalShip,
            PrimaryWeaponType weaponType,
            PrimaryWeaponArc weaponArc
        )
        {
            int weaponArcIndex = (int)weaponArc;
            if (
                capitalShip?.PrimaryWeapons == null
                || !capitalShip.PrimaryWeapons.TryGetValue(weaponType, out int[] values)
                || values == null
                || weaponArcIndex >= values.Length
            )
                return 0;

            return values[weaponArcIndex];
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
        /// Returns the most efficient unlocked starfighter for planetary defense.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The selected technology, or null when none is affordable.</returns>
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
                    ScorePlanetaryStarfighter(
                        context.Game.Config.AI.Selection,
                        (Starfighter)technology.GetReference()
                    )
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
        /// Scores a starfighter's defensive strength per maintenance point.
        /// </summary>
        /// <param name="config">AI selection configuration.</param>
        /// <param name="starfighter">The starfighter.</param>
        /// <returns>The planetary-defense utility score.</returns>
        private static double ScorePlanetaryStarfighter(
            GameConfig.AISelectionConfig config,
            Starfighter starfighter
        )
        {
            int strength = starfighter.GetWeaponStrength();
            double efficiency =
                starfighter.MaintenanceCost > 0
                    ? strength / (double)starfighter.MaintenanceCost
                    : 100;
            return AIUtility.Evaluate(
                AIUtility.Fulfillment(efficiency, AIUtilityDomain.Percent),
                config.TechnologyUtility.Starfighter.PlanetDefenseEfficiency
            );
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
        /// Returns the strongest affordable unlocked regiment for planetary defense.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The selected technology, or null when none is affordable.</returns>
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
            GameConfig.AIStarfighterSelectionUtilityConfig utility = config
                .TechnologyUtility
                .Starfighter;
            AIUtilityScore score = new AIUtilityScore();
            score.Add(
                AIUtility.Fulfillment(
                    starfighter.LaserCannon,
                    AIUtilityDomain.StarfighterWeaponRating
                ),
                utility.Laser
            );
            score.Add(
                AIUtility.Fulfillment(
                    starfighter.IonCannon,
                    AIUtilityDomain.StarfighterWeaponRating
                ),
                utility.Ion
            );
            score.Add(
                AIUtility.Fulfillment(
                    starfighter.Torpedoes,
                    AIUtilityDomain.StarfighterWeaponRating
                ),
                utility.Torpedo
            );
            score.Add(
                starfighter.IonCannon > 0 && !FleetHasIonStarfighter(fleet) ? 1 : 0,
                utility.MissingIon
            );
            score.Add(
                starfighter.Torpedoes > 0 && !FleetHasTorpedoStarfighter(fleet) ? 1 : 0,
                utility.MissingTorpedo
            );
            score.AddCost(
                AIUtility.Fulfillment(
                    CountFleetUnitsByType<Starfighter>(fleet, starfighter.GetTypeID()),
                    AIUtilityDomain.DuplicateFleetUnitCount
                ),
                config.TechnologyUtility.DuplicateCost
            );
            return score.Value;
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
            GameConfig.AIRegimentSelectionUtilityConfig utility = config.TechnologyUtility.Regiment;
            AIUtilityScore score = new AIUtilityScore();
            score.Add(
                AIUtility.Fulfillment(regiment.AttackRating, AIUtilityDomain.RegimentRating),
                utility.Attack
            );
            score.Add(
                AIUtility.Fulfillment(regiment.DefenseRating, AIUtilityDomain.RegimentRating),
                utility.Defense
            );
            score.Add(
                AIUtility.Fulfillment(regiment.BombardmentDefense, AIUtilityDomain.RegimentRating),
                utility.BombardmentDefense
            );
            score.Add(1, utility.Base);
            score.AddCost(
                AIUtility.Fulfillment(regiment.MaintenanceCost, AIUtilityDomain.RegimentRating),
                utility.MaintenanceCost
            );
            score.AddCost(
                AIUtility.Fulfillment(
                    CountFleetUnitsByType<Regiment>(fleet, regiment.GetTypeID()),
                    AIUtilityDomain.DuplicateFleetUnitCount
                ),
                config.TechnologyUtility.DuplicateCost
            );
            return score.Value;
        }

        /// <summary>
        /// Returns producer planets eligible for a demand item.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demand">Requirement item to satisfy.</param>
        /// <param name="product">The product to manufacture.</param>
        /// <param name="quantity">The quantity required by the demand.</param>
        /// <returns>Eligible producer planets in fulfillment order.</returns>
        private IEnumerable<Planet> FindProducerPlanets(
            AITurnContext context,
            AIProductionRequirement demand,
            IManufacturable product,
            int quantity
        )
        {
            if (context?.Assessment == null || demand?.Destination == null)
                return Enumerable.Empty<Planet>();

            Planet destinationPlanet = GetDestinationPlanet(context, demand);
            ProducerMode mode =
                IsFacilityExpansionRequirement(demand) ? ProducerMode.FacilityExpansion
                : IsDistributedProductionRequirement(demand) ? ProducerMode.Distributed
                : ProducerMode.AvailableCapacity;
            (
                string DestinationId,
                ManufacturingType ManufacturingType,
                ProducerMode Mode,
                AIProductionRequirementKind DemandKind,
                string ProductTypeId,
                int Quantity
            ) key = (
                destinationPlanet?.InstanceID,
                demand.ManufacturingType,
                mode,
                demand.Kind,
                product?.GetTypeID(),
                quantity
            );
            if (_producerPlanets.TryGetValue(key, out List<Planet> producers))
                return producers;

            IEnumerable<Planet> eligibleProducers = context.Assessment.OwnedPlanets.Where(planet =>
                mode == ProducerMode.FacilityExpansion ? CanQueueFacilityExpansion(context, planet)
                : mode == ProducerMode.Distributed
                    ? HasProductionFacility(context, planet, demand.ManufacturingType)
                : CanProduce(planet, demand.ManufacturingType)
            );
            eligibleProducers = eligibleProducers.Where(producer =>
                CanAllocateProducerToDemand(context, producer, demand)
            );
            if (mode == ProducerMode.FacilityExpansion && destinationPlanet != null)
            {
                string destinationSystemId = context.Assessment.GetPlanetSystemId(
                    destinationPlanet
                );
                List<Planet> localProducers = eligibleProducers
                    .Where(planet =>
                        context.Assessment.GetPlanetSystemId(planet) == destinationSystemId
                    )
                    .ToList();
                if (localProducers.Count > 0)
                    eligibleProducers = localProducers;
            }
            producers =
                mode == ProducerMode.FacilityExpansion
                    ? eligibleProducers
                        .OrderBy(planet =>
                            context.Assessment.GetProductionBacklogTicks(
                                planet,
                                ManufacturingType.Building
                            )
                        )
                        .ThenByDescending(planet =>
                            context.Assessment.GetPlanetProductionRate(
                                planet,
                                ManufacturingType.Building
                            )
                        )
                        .ThenBy(planet =>
                            destinationPlanet == null
                                ? 0
                                : destinationPlanet.GetRawDistanceTo(planet)
                        )
                        .ThenBy(planet => planet.InstanceID)
                        .ToList()
                    : eligibleProducers
                        .OrderBy(planet =>
                            GetProducerFulfillmentTicks(
                                context,
                                demand,
                                product,
                                quantity,
                                planet,
                                destinationPlanet
                            )
                        )
                        .ThenByDescending(planet =>
                            context.Assessment.GetPlanetProductionRate(
                                planet,
                                demand.ManufacturingType
                            )
                        )
                        .ThenBy(planet => planet.InstanceID)
                        .ToList();
            _producerPlanets.Add(key, producers);
            return producers;
        }

        /// <summary>
        /// Returns the fulfillment time used to order eligible producers.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="demand">The demand being fulfilled.</param>
        /// <param name="product">The product to manufacture.</param>
        /// <param name="quantity">The quantity required by the demand.</param>
        /// <param name="producer">The candidate producer.</param>
        /// <param name="destinationPlanet">The destination planet, when available.</param>
        /// <returns>Estimated arrival ticks for fleet reinforcements, otherwise raw distance.</returns>
        private double GetProducerFulfillmentTicks(
            AITurnContext context,
            AIProductionRequirement demand,
            IManufacturable product,
            int quantity,
            Planet producer,
            Planet destinationPlanet
        )
        {
            if (demand.DestinationFleet != null && product is IMovable movable)
            {
                return GetReinforcementArrivalTicks(
                    context,
                    producer,
                    demand.DestinationFleet,
                    product,
                    movable,
                    quantity
                );
            }

            return destinationPlanet == null ? 0 : destinationPlanet.GetRawDistanceTo(producer);
        }

        /// <summary>
        /// Estimates when a manufactured reinforcement will reach its destination fleet.
        /// </summary>
        /// <param name="context">The active AI turn.</param>
        /// <param name="producer">The planet manufacturing the reinforcement.</param>
        /// <param name="destinationFleet">The fleet receiving the reinforcement.</param>
        /// <param name="product">The manufactured product.</param>
        /// <param name="movable">The product movement characteristics.</param>
        /// <param name="quantity">The appended product quantity.</param>
        /// <returns>Production plus transit ticks, or <see cref="int.MaxValue"/> when unavailable.</returns>
        private int GetReinforcementArrivalTicks(
            AITurnContext context,
            Planet producer,
            Fleet destinationFleet,
            IManufacturable product,
            IMovable movable,
            int quantity
        )
        {
            if (producer == null || destinationFleet == null || product == null || movable == null)
                return int.MaxValue;

            (
                string ProducerPlanetId,
                string DestinationFleetId,
                string ProductTypeId,
                int Quantity
            ) key = (
                producer.InstanceID,
                destinationFleet.InstanceID,
                product.GetTypeID(),
                quantity
            );
            if (_reinforcementArrivalTicks.TryGetValue(key, out int cachedTicks))
                return cachedTicks;

            int manufacturingTicks =
                ManufacturingSystem.EstimateAppendedCompletionTicks(producer, product, quantity)
                ?? int.MaxValue;
            if (manufacturingTicks == int.MaxValue || context.Movement == null)
                return CacheReinforcementArrival(key, int.MaxValue);

            if (
                !context.Movement.TryEstimateManufacturedTransitTicks(
                    movable,
                    producer,
                    destinationFleet,
                    out int transitTicks
                )
            )
            {
                return CacheReinforcementArrival(key, int.MaxValue);
            }

            long arrivalTicks = (long)manufacturingTicks + transitTicks;
            return CacheReinforcementArrival(
                key,
                arrivalTicks >= int.MaxValue ? int.MaxValue : (int)arrivalTicks
            );
        }

        /// <summary>
        /// Stores and returns one reinforcement-arrival estimate.
        /// </summary>
        /// <param name="key">The producer, fleet, product, and quantity identity.</param>
        /// <param name="ticks">The estimated arrival duration.</param>
        /// <returns>The supplied duration.</returns>
        private int CacheReinforcementArrival(
            (
                string ProducerPlanetId,
                string DestinationFleetId,
                string ProductTypeId,
                int Quantity
            ) key,
            int ticks
        )
        {
            _reinforcementArrivalTicks[key] = ticks;
            return ticks;
        }

        /// <summary>
        /// Returns whether a producer may serve the requested demand.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="producer">The prospective producing planet.</param>
        /// <param name="demand">The demand seeking production capacity.</param>
        /// <returns>True when the producer may serve the demand.</returns>
        private static bool CanAllocateProducerToDemand(
            AITurnContext context,
            Planet producer,
            AIProductionRequirement demand
        )
        {
            return CanUseShipProducerForDemand(context, producer, demand);
        }

        /// <summary>
        /// Returns whether a ship-producing planet is dedicated to the requested strategic role.
        /// Single-shipyard planets defend planets with starfighters, while larger shipyard groups
        /// manufacture fleet units.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="producer">The prospective producing planet.</param>
        /// <param name="demand">The demand seeking production capacity.</param>
        /// <returns>True when the producer may manufacture the requested demand.</returns>
        private static bool CanUseShipProducerForDemand(
            AITurnContext context,
            Planet producer,
            AIProductionRequirement demand
        )
        {
            if (demand?.ManufacturingType != ManufacturingType.Ship)
                return true;

            int shipyardCount = context.Assessment.GetPlanetProductionFacilityCount(
                producer,
                ManufacturingType.Ship
            );
            int fleetProductionMinimum = Math.Max(
                1,
                context.Game.Config.AI.Infrastructure.FleetProductionMinimumShipyardCount
            );
            return demand.Kind == AIProductionRequirementKind.PlanetaryStarfighterReserve
                ? shipyardCount == 1
                : shipyardCount >= fleetProductionMinimum;
        }

        /// <summary>
        /// Returns whether a planet can queue additional production facilities.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The planet to evaluate.</param>
        /// <returns>True when the planet is operational and has construction capacity.</returns>
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
        /// <param name="demand">Requirement item to inspect.</param>
        /// <returns>The destination planet, or null.</returns>
        private Planet GetDestinationPlanet(AITurnContext context, AIProductionRequirement demand)
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
        /// Returns whether a planet has an operational facility for a manufacturing category.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The planet to evaluate.</param>
        /// <param name="manufacturingType">The manufacturing category.</param>
        /// <returns>True when the planet can distribute this production category.</returns>
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
        /// <typeparam name="T">The manufacturable fleet-unit type to count.</typeparam>
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
            AvailableCapacity,

            Distributed,

            FacilityExpansion,
        }
    }
}
