using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Research;
using Rebellion.Game.Units;

namespace Rebellion.AI.Planners
{
    /// <summary>
    /// Builds production proposals from current demand.
    /// </summary>
    public sealed class AIProductionPlanner : IAIProposalPlanner
    {
        private const int _percentageScale = 100;
        private const int _primaryLaserDivisor = 6;
        private const int _primaryWeaponWeight = 100;
        private const int _roleMetricScale = 10;
        private const int _weaponArcCount = 4;

        private readonly AIProductionDemandGenerator _demandGenerator =
            new AIProductionDemandGenerator();
        private readonly Dictionary<ManufacturingType, List<Technology>> _unlockedTechnologies =
            new Dictionary<ManufacturingType, List<Technology>>();
        private readonly Dictionary<
            (
                AIProductionDemandKind Kind,
                BuildingType BuildingType,
                string DestinationId,
                string ProductTypeId,
                string ReplacementTypeId
            ),
            Technology
        > _selectedTechnologies =
            new Dictionary<
                (
                    AIProductionDemandKind Kind,
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
            List<AIProductionDemand> demands = _demandGenerator.Generate(context);
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
        private List<AIProposal> GenerateProposals(
            AITurnContext context,
            List<AIProductionDemand> demands
        )
        {
            List<AIProposal> proposals = new List<AIProposal>();
            if (context?.Faction == null || demands == null || demands.Count == 0)
                return proposals;

            foreach (AIProductionDemand demand in demands)
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
            AIProductionDemand demand,
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
                remainingQuantity = System.Math.Min(
                    remainingQuantity,
                    GetFleetUnitDiversityLimit(context, demand, product.GetReference())
                );
            }
            if (remainingQuantity <= 0)
                return;

            List<Planet> producerPlanets = FindProducerPlanets(context, demand).ToList();
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
                AIProductionDemand proposalDemand = GetProposalDemand(
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
            AIProductionDemand demand,
            Technology product,
            int remainingQuantity,
            IReadOnlyList<Planet> producerPlanets,
            List<AIProposal> proposals
        )
        {
            if (producerPlanets.Count == 0)
                return;

            AIProductionDemand proposalDemand = GetProposalDemand(
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
            AIProductionDemand demand,
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
        private Technology GetUnlockedTechnology(AITurnContext context, AIProductionDemand demand)
        {
            if (demand == null)
                return null;

            if (
                demand.Kind
                is AIProductionDemandKind.FleetCapitalShip
                    or AIProductionDemandKind.FleetSeedCapitalShip
            )
                return GetUnlockedCapitalShipTechnology(context, demand);

            (
                AIProductionDemandKind Kind,
                BuildingType BuildingType,
                string DestinationId,
                string ProductTypeId,
                string ReplacementTypeId
            ) key = (
                demand.Kind,
                demand.BuildingType,
                demand.Kind
                    is AIProductionDemandKind.FleetStarfighter
                        or AIProductionDemandKind.FleetRegiment
                    ? demand.Destination?.InstanceID
                    : null,
                demand.ProductTypeId,
                demand.ReplacementBuilding?.GetTypeID()
            );
            if (_selectedTechnologies.TryGetValue(key, out Technology selectedTechnology))
                return selectedTechnology;

            selectedTechnology = demand.Kind switch
            {
                AIProductionDemandKind.Colony
                or AIProductionDemandKind.Mine
                or AIProductionDemandKind.Refinery
                or AIProductionDemandKind.ConstructionFacility
                or AIProductionDemandKind.Shipyard
                or AIProductionDemandKind.TrainingFacility
                or AIProductionDemandKind.BuildingUpgrade
                or AIProductionDemandKind.PlanetaryDefense => GetUnlockedBuildingTechnology(
                    context,
                    demand
                ),
                AIProductionDemandKind.FleetStarfighter
                or AIProductionDemandKind.PlanetaryStarfighterReserve
                or AIProductionDemandKind.FleetRegiment
                or AIProductionDemandKind.GarrisonRegimentReserve
                or AIProductionDemandKind.SpecialForces => GetUnlockedUnitTechnology(
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
            AIProductionDemand demand
        )
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

        private static bool IsEligibleBuildingUpgrade(AIProductionDemand demand, Building building)
        {
            return demand.Kind != AIProductionDemandKind.BuildingUpgrade
                || demand.ReplacementBuilding.CanUpgradeTo(building);
        }

        private static int GetBuildingMaintenanceCost(AIProductionDemand demand, Building building)
        {
            if (
                demand.Kind != AIProductionDemandKind.BuildingUpgrade
                || demand.ReplacementBuilding == null
            )
                return building.MaintenanceCost;

            return System.Math.Max(
                0,
                building.MaintenanceCost - demand.ReplacementBuilding.MaintenanceCost
            );
        }

        private int GetBuildingMaintenanceBudget(AITurnContext context, AIProductionDemand demand)
        {
            if (IsFacilityExpansionDemand(demand))
                return GetFacilityMaintenanceBudget(context, demand);

            if (demand.UsesDefensiveReserve)
                return GetDefensiveMaintenanceBudget(context);

            return System.Math.Max(
                0,
                context.Assessment.ProjectedMaintenanceHeadroom
                    - context.Game.Config.AI.Selection.MaintenanceHeadroomHardFloor
            );
        }

        private int GetFacilityMaintenanceBudget(AITurnContext context, AIProductionDemand demand)
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            int allocatedMaintenance = demand.Kind switch
            {
                AIProductionDemandKind.Shipyard => ScaleByPercent(
                    ScaleByPercent(
                        context.Assessment.MaintenanceCapacity,
                        config.ShipyardMaintenanceAllocationPercent
                    ),
                    config.ShipyardMaintenanceAllocationScalePercent
                ),
                AIProductionDemandKind.TrainingFacility => ScaleByPercent(
                    ScaleByPercent(
                        context.Assessment.MaintenanceCapacity,
                        config.TrainingFacilityMaintenanceAllocationPercent
                    ),
                    config.TrainingFacilityMaintenanceAllocationScalePercent
                ),
                AIProductionDemandKind.ConstructionFacility => ScaleByPercent(
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

            int headroomBudget = System.Math.Max(
                0,
                context.Assessment.ProjectedMaintenanceHeadroom
                    - context.Game.Config.AI.Selection.MaintenanceHeadroomHardFloor
            );
            return System.Math.Min(availableMaintenance, headroomBudget);
        }

        private int GetCommittedFacilityMaintenance(
            AITurnContext context,
            AIProductionDemand demand
        )
        {
            BuildingType buildingType = demand.Kind switch
            {
                AIProductionDemandKind.Shipyard => BuildingType.Shipyard,
                AIProductionDemandKind.TrainingFacility => BuildingType.TrainingFacility,
                AIProductionDemandKind.ConstructionFacility => BuildingType.ConstructionFacility,
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

        private AIProductionDemand GetProposalDemand(
            AITurnContext context,
            AIProductionDemand demand,
            Planet producerPlanet,
            Technology product,
            int remainingQuantity
        )
        {
            if (demand.Kind == AIProductionDemandKind.BuildingUpgrade)
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

        private static AIProductionDemand CreateProposalDemand(
            AIProductionDemand demand,
            int quantity
        )
        {
            AIProductionDemand proposalDemand = new AIProductionDemand(
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
            proposalDemand.ReplacementBuilding = demand.ReplacementBuilding;
            return proposalDemand;
        }

        private int GetRequestedManufacturingCount(
            AITurnContext context,
            AIProductionDemand demand,
            IManufacturable product
        )
        {
            if (!IsDistributedProductionDemand(demand))
                return System.Math.Max(0, demand.QuantityNeeded);

            int requestedCount =
                demand.Kind == AIProductionDemandKind.FleetCapitalShip
                    ? GetCapitalShipCount(context, demand, product as CapitalShip)
                    : demand.QuantityNeeded;

            if (demand.UsesDefensiveReserve)
                requestedCount = System.Math.Min(
                    requestedCount,
                    GetDefensiveBatchSize(context, demand, product)
                );

            return System.Math.Max(0, requestedCount);
        }

        private int GetCapitalShipCount(
            AITurnContext context,
            AIProductionDemand demand,
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

            int requestedCount = DivideRoundedUp(demand.QuantityNeeded, contribution);
            if (capitalShip.MaintenanceCost <= 0)
                return requestedCount;

            return System.Math.Min(
                requestedCount,
                GetCapitalShipMaintenanceBudget(context) / capitalShip.MaintenanceCost
            );
        }

        private int GetDistributedBatchSize(
            AITurnContext context,
            Planet producerPlanet,
            IManufacturable product,
            int remainingQuantity
        )
        {
            int queueCapacity = GetQueueBatchCapacity(context, producerPlanet, product);
            return System.Math.Max(0, System.Math.Min(remainingQuantity, queueCapacity));
        }

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
                        (long)
                            System.Math.Max(
                                0,
                                item.GetConstructionCost() - item.ManufacturingProgress
                            )
                    )
                    : 0;
                _queueWork.Add(key, work);
            }

            double targetWork = work.TargetWork;
            long queuedWork = work.QueuedWork;
            long additionalWork = (long)System.Math.Ceiling(targetWork) - queuedWork;
            if (additionalWork <= 0)
                return 0;

            int constructionCost = product.GetConstructionCost();
            if (constructionCost <= 0)
                return int.MaxValue;

            long capacity = (additionalWork + constructionCost - 1) / constructionCost;
            return capacity > int.MaxValue ? int.MaxValue : (int)capacity;
        }

        private int GetFleetUnitDiversityLimit(
            AITurnContext context,
            AIProductionDemand demand,
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

            return System.Math.Max(
                0,
                maximumDuplicateCount - CountFleetUnitsByType<T>(fleet, selectedTypeId)
            );
        }

        private int GetDefensiveBatchSize(
            AITurnContext context,
            AIProductionDemand demand,
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

            return System.Math.Max(
                0,
                System.Math.Min(
                    demand.QuantityNeeded,
                    System.Math.Min(maintenanceLimit, destinationLimit)
                )
            );
        }

        private int GetDefensiveMaintenanceBudget(AITurnContext context)
        {
            return System.Math.Max(
                0,
                context.Assessment.ProjectedMaintenanceHeadroom
                    - GetDefensiveMaintenanceFloor(context)
            );
        }

        private int GetDefensiveMaintenanceFloor(AITurnContext context)
        {
            return System.Math.Max(
                context.Game.Config.AI.Selection.MaintenanceHeadroomHardFloor,
                System.Math.Max(
                    context.Game.Config.AI.Selection.MinimumMaintenanceHeadroomAfterProduction,
                    ScaleByPercentRoundedUp(
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

        private static int ScaleByPercentRoundedUp(int value, int percent)
        {
            return (int)(((long)value * percent + _percentageScale - 1) / _percentageScale);
        }

        private int GetFacilityBatchSize(
            AITurnContext context,
            AIProductionDemand demand,
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
                demand.Kind == AIProductionDemandKind.ConstructionFacility
                    ? 0
                    : System.Math.Max(
                        0,
                        context.Game.Config.AI.Infrastructure.FacilityConstructionLaneReserve
                    );
            int laneLimit =
                facilityCount > laneReserve ? facilityCount - laneReserve : facilityCount;
            int energyLimit = System.Math.Max(
                0,
                demand.DestinationPlanet.GetAvailableEnergy()
                    - context.Assessment.GetPlanetaryDefenseEnergyDeficit(demand.DestinationPlanet)
            );

            return System.Math.Max(
                0,
                System.Math.Min(maintenanceLimit, System.Math.Min(laneLimit, energyLimit))
            );
        }

        private static bool IsFacilityExpansionDemand(AIProductionDemand demand)
        {
            return demand?.Kind
                is AIProductionDemandKind.ConstructionFacility
                    or AIProductionDemandKind.Shipyard
                    or AIProductionDemandKind.TrainingFacility;
        }

        private static bool IsDistributedProductionDemand(AIProductionDemand demand)
        {
            return demand?.Kind
                is AIProductionDemandKind.FleetCapitalShip
                    or AIProductionDemandKind.FleetStarfighter
                    or AIProductionDemandKind.PlanetaryStarfighterReserve
                    or AIProductionDemandKind.FleetRegiment
                    or AIProductionDemandKind.GarrisonRegimentReserve
                    or AIProductionDemandKind.SpecialForces;
        }

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
        private Technology GetUnlockedUnitTechnology(
            AITurnContext context,
            AIProductionDemand demand
        )
        {
            if (context?.Faction == null || demand == null)
                return null;

            return demand.Kind switch
            {
                AIProductionDemandKind.FleetCapitalShip => GetUnlockedCapitalShipTechnology(
                    context,
                    demand
                ),
                AIProductionDemandKind.FleetSeedCapitalShip => GetUnlockedCapitalShipTechnology(
                    context,
                    demand
                ),
                AIProductionDemandKind.FleetStarfighter => GetUnlockedStarfighterTechnology(
                    context,
                    demand.DestinationFleet
                ),
                AIProductionDemandKind.PlanetaryStarfighterReserve =>
                    GetUnlockedPlanetaryStarfighterTechnology(context),
                AIProductionDemandKind.FleetRegiment => GetUnlockedRegimentTechnology(
                    context,
                    demand.DestinationFleet
                ),
                AIProductionDemandKind.GarrisonRegimentReserve =>
                    GetUnlockedGarrisonRegimentTechnology(context),
                AIProductionDemandKind.SpecialForces => GetUnlockedSpecialForcesTechnology(
                    context,
                    demand.ProductTypeId
                ),
                _ => null,
            };
        }

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
                .FirstOrDefault();
        }

        private Technology GetUnlockedCapitalShipTechnology(
            AITurnContext context,
            AIProductionDemand demand
        )
        {
            if (context?.Faction == null || demand == null)
                return null;

            int maintenanceBudget = GetCapitalShipMaintenanceBudget(context);
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
                    demand.CapitalShipRole
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

        private static void InsertCapitalShipTechnology(
            AITurnContext context,
            List<Technology> rankedTechnologies,
            Technology candidate,
            AICapitalShipProductionRole role
        )
        {
            CapitalShip candidateShip = (CapitalShip)candidate.GetReference();
            long candidateMetric = GetCapitalShipRoleMetric(candidateShip, role);

            for (int index = 0; index < rankedTechnologies.Count; index++)
            {
                CapitalShip rankedShip = (CapitalShip)rankedTechnologies[index].GetReference();
                long rankedMetric = GetCapitalShipRoleMetric(rankedShip, role);
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

        private static bool ShouldInsertCapitalShipBeforeEqual(AITurnContext context)
        {
            GameConfig.AISelectionConfig config = context.Game.Config.AI.Selection;
            return context.Random.NextInt(0, config.CapitalShipTieRollRange)
                < config.CapitalShipTieInsertBeforeThreshold;
        }

        private int GetCapitalShipMaintenanceBudget(AITurnContext context)
        {
            if (_capitalShipMaintenanceBudget.HasValue)
                return _capitalShipMaintenanceBudget.Value;

            GameConfig.AISelectionConfig config = context.Game.Config.AI.Selection;
            int allocatedMaintenance = ScaleByPercent(
                context.Assessment.MaintenanceCapacity,
                config.CapitalMaintenanceAllocationPercent
            );
            int targetCapitalMaintenance = ScaleByPercent(
                allocatedMaintenance,
                config.CapitalMaintenanceSafetyPercent
            );
            int committedCapitalMaintenance = context
                .Faction.GetOwnedUnitsByType<CapitalShip>()
                .Where(IsCommittedCapitalShip)
                .Sum(capitalShip => capitalShip.MaintenanceCost);
            int budget = System.Math.Max(0, targetCapitalMaintenance - committedCapitalMaintenance);

            _capitalShipMaintenanceBudget =
                context.Assessment.ProjectedMaintenanceHeadroom < budget ? 0 : budget;
            return _capitalShipMaintenanceBudget.Value;
        }

        private static bool IsCommittedCapitalShip(CapitalShip capitalShip)
        {
            return capitalShip?.ManufacturingStatus
                is ManufacturingStatus.Complete
                    or ManufacturingStatus.Building;
        }

        private static int ScaleByPercent(int value, int percent)
        {
            return (int)((long)value * percent / _percentageScale);
        }

        private static int DivideRoundedUp(int value, int divisor)
        {
            if (value <= 0 || divisor <= 0)
                return 0;

            return (int)(((long)value + divisor - 1) / divisor);
        }

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

        private static long GetCapitalShipRoleMetric(
            CapitalShip capitalShip,
            AICapitalShipProductionRole role
        )
        {
            return role switch
            {
                AICapitalShipProductionRole.General => GetPrimaryWeaponMetric(capitalShip)
                    * _roleMetricScale
                    / System.Math.Max(1, capitalShip.MaintenanceCost),
                AICapitalShipProductionRole.TroopTransport => capitalShip.RegimentCapacity,
                AICapitalShipProductionRole.Bombardment => capitalShip.Bombardment > 0 ? 1 : 0,
                AICapitalShipProductionRole.Interdiction => capitalShip.ShieldRechargeRate,
                _ => 0,
            };
        }

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
                maximumWeight = System.Math.Max(maximumWeight, weight);
            }

            return maximumWeight;
        }

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
        private IEnumerable<Planet> FindProducerPlanets(
            AITurnContext context,
            AIProductionDemand demand
        )
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
        private Planet GetDestinationPlanet(AITurnContext context, AIProductionDemand demand)
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
            /// <summary>Requires currently available manufacturing capacity.</summary>
            AvailableCapacity,

            /// <summary>Requires an appropriate manufacturing facility.</summary>
            Distributed,

            /// <summary>Requires a planet eligible for facility expansion.</summary>
            FacilityExpansion,
        }
    }
}
