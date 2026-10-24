using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Rebellion.AI.Demands;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Simulation;
using Rebellion.Util.Logging;
using Rebellion.Util.Mathematics;

namespace Rebellion.AI.Proposals
{
    /// <summary>
    /// Proposal to enqueue a manufacturable item.
    /// </summary>
    public sealed class AIManufactureProposal : AIProposal
    {
        private readonly IManufacturable _replacementOrder;
        public AIProductionDemand Demand { get; }

        public Planet ProducerPlanet { get; }

        internal IReadOnlyList<AIManufactureProposal> ProducerAlternatives { get; }

        internal bool CarriesReducedCountAcrossAlternatives { get; }

        public Technology Product { get; }

        /// <summary>
        /// Gets whether this action keeps an existing queued order running.
        /// </summary>
        internal bool IsContinuation => Demand?.ExistingOrder != null;

        /// <summary>
        /// Gets the number of manufactured items represented by this proposal.
        /// </summary>
        public int ManufacturingCount => GetManufacturingCount();

        public ContainerNode Destination => Demand?.Destination;

        /// <summary>
        /// Gets the current order that this action continues or replaces.
        /// </summary>
        internal IManufacturable CurrentOrder
        {
            get
            {
                IManufacturable order = Demand?.ExistingOrder ?? _replacementOrder;
                return IsOrderStillQueued(order) ? order : null;
            }
        }

        /// <summary>
        /// Gets maintenance released by replacing an unfinished order.
        /// </summary>
        internal int ReplacedMaintenanceCost =>
            ContinuesExistingOrder ? 0 : CurrentOrder?.MaintenanceCost ?? 0;

        /// <summary>
        /// Gets whether the unfinished order matches this requested product and destination.
        /// </summary>
        internal bool ContinuesExistingOrder =>
            CurrentOrder is ISceneNode node
            && CurrentOrder.GetTypeID() == Product?.GetReference()?.GetTypeID()
            && ReferenceEquals(node.GetParent(), Destination);

        /// <summary>
        /// Gets destination space released by the producer's replaceable unfinished building.
        /// </summary>
        internal int ReleasedDestinationSpace =>
            CurrentOrder is Building building && ReferenceEquals(building.GetParent(), Destination)
                ? 1
                : 0;

        /// <summary>
        /// Gets usable producer capacity, including a lane occupied by a replaceable order.
        /// </summary>
        internal int AvailableProducerCapacity =>
            CurrentOrder != null
                ? ProducerPlanet.GetProductionFacilityCount(Demand.ManufacturingType)
                : ProducerPlanet.GetAvailableManufacturingCapacity(Demand.ManufacturingType);

        internal bool DistributesDemand { get; }

        internal bool UsesSharedProducerCapacity =>
            !IsContinuation
            && !IsFacilityExpansionDemand()
            && Product?.GetReference() is not CapitalShip
            && !DistributesDemand
            && Demand?.IsProductionFiller != true;

        internal bool IsProductionFacilityExpansion => IsFacilityExpansionDemand();

        internal override AIProposalPriority Priority =>
            Demand?.IsEmergencyDefense == true ? AIProposalPriority.Mandatory
            : Demand?.Kind == AIProductionDemandKind.IdleSpecialForces ? AIProposalPriority.Filler
            : Demand?.Kind == AIProductionDemandKind.ColonizationFleetSeedCapitalShip
            || Demand?.DestinationFleet?.RoleType == FleetRoleType.Colonization
                ? AIProposalPriority.Mandatory
            : Demand?.Kind == AIProductionDemandKind.PlanetaryDefense
                ? AIProposalPriority.DeferredPlanetaryDefense
            : AIProposalPriority.Optional;

        /// <summary>
        /// Creates a manufacture proposal from one demand and producer.
        /// </summary>
        /// <param name="demand">Production demand served by the proposal.</param>
        /// <param name="producerPlanet">Planet that will produce the item.</param>
        /// <param name="product">Technology to manufacture.</param>
        /// <param name="distributesDemand">Whether the proposal may satisfy demand across producers.</param>
        /// <param name="replacedOrder">The unfinished order evaluated for replacement, if any.</param>
        internal AIManufactureProposal(
            AIProductionDemand demand,
            Planet producerPlanet,
            Technology product,
            bool distributesDemand,
            IManufacturable replacedOrder = null
        )
        {
            Demand = demand;
            ProducerPlanet = producerPlanet;
            Product = product;
            DistributesDemand = distributesDemand;
            if (
                replacedOrder != null
                && demand?.ExistingOrder == null
                && demand?.IsProductionFiller == false
                && replacedOrder.GetTypeID() != product?.GetReference()?.GetTypeID()
            )
                _replacementOrder = replacedOrder;
            ProducerAlternatives = Array.Empty<AIManufactureProposal>();
        }

        /// <summary>
        /// Creates a manufacture proposal from exact ranked alternatives.
        /// </summary>
        /// <param name="candidates">Exact eligible manufacturing actions in preference order.</param>
        /// <param name="carriesReducedCountAcrossAlternatives">Whether each producer retains earlier quantity reductions.</param>
        internal AIManufactureProposal(
            IReadOnlyList<AIManufactureProposal> candidates,
            bool carriesReducedCountAcrossAlternatives = false
        )
        {
            AIManufactureProposal first = candidates?.FirstOrDefault();
            Demand = first?.Demand;
            ProducerPlanet = first?.ProducerPlanet;
            Product = first?.Product;
            DistributesDemand = first?.DistributesDemand == true;
            _replacementOrder = first?._replacementOrder;
            ProducerAlternatives =
                candidates?.Skip(1).ToArray() ?? Array.Empty<AIManufactureProposal>();
            CarriesReducedCountAcrossAlternatives = carriesReducedCountAcrossAlternatives;
        }

        /// <summary>
        /// Returns a stable sort key for manufacture proposals.
        /// </summary>
        /// <returns>A stable sort key.</returns>
        public override string GetSortKey()
        {
            if (
                Demand?.Kind
                is AIProductionDemandKind.FleetSeedCapitalShip
                    or AIProductionDemandKind.ColonizationFleetSeedCapitalShip
            )
            {
                return string.Join(
                    ":",
                    "fleet-seed",
                    GetProducerDistanceSortKey(),
                    ProducerPlanet?.InstanceID,
                    Destination?.InstanceID,
                    Product?.GetReference()?.GetTypeID()
                );
            }

            if (Destination is Fleet destinationFleet)
            {
                return string.Join(
                    ":",
                    "fleet-reinforcement",
                    Demand?.Kind,
                    GetProducerDistanceSortKey(),
                    ProducerPlanet?.InstanceID,
                    destinationFleet.InstanceID,
                    Product?.GetReference()?.GetTypeID()
                );
            }

            return string.Join(
                ":",
                "manufacture-building",
                Demand?.Kind,
                GetProducerDistanceSortKey(),
                ProducerPlanet?.InstanceID,
                Destination?.InstanceID,
                Product?.GetReference()?.GetTypeID()
            );
        }

        /// <summary>
        /// Returns whether this proposal may be selected.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True if this proposal may be selected.</returns>
        public override bool CanSelect(AITurnContext context)
        {
            return IsStillValid(context, validateOrderAcceptance: true);
        }

        /// <summary>
        /// Returns whether this proposal may execute against the current game state.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True if this proposal may execute.</returns>
        public override bool CanExecute(AITurnContext context)
        {
            return IsStillValid(context, validateOrderAcceptance: false)
                && (IsContinuation || HasMaintenanceHeadroom(context));
        }

        /// <summary>
        /// Enqueues the product at the producer planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public override void Execute(AITurnContext context)
        {
            if (!CanExecute(context))
                return;

            int maintenanceCredit = ReplacedMaintenanceCost;
            IManufacturable replacedOrder = CurrentOrder;
            if (ContinuesExistingOrder)
            {
                return;
            }
            if (
                replacedOrder != null
                && !context.Manufacturing.CancelManufacturing(
                    replacedOrder,
                    context.Faction.InstanceID
                )
            )
                return;
            if (replacedOrder != null)
            {
                context.RecordManufacturingMutation(0, maintenanceCredit);
            }

            if (Demand.Kind == AIProductionDemandKind.BuildingUpgrade)
            {
                ExecuteBuildingUpgrade(context);
                return;
            }

            if (IsCountedManufacturingDemand())
            {
                bool started = context.Manufacturing.StartManufacturing(
                    ProducerPlanet,
                    Product.GetReferenceCopy(),
                    Destination,
                    GetManufacturingCount(),
                    context.Faction.InstanceID,
                    context.AvailableProjectedMaintenanceHeadroom,
                    GetMinimumMaintenanceHeadroom(context),
                    out int addedMaintenance,
                    out int releasedMaintenance
                );
                context.RecordManufacturingMutation(addedMaintenance, releasedMaintenance);
                if (!started)
                    LogEnqueueFailure();
                return;
            }

            IManufacturable manufacturable = Product.GetReferenceCopy();
            if (manufacturable is not ISceneNode sceneNode)
                return;

            sceneNode.OwnerInstanceID = context.Faction.InstanceID;

            if (
                Demand.Kind
                    is AIProductionDemandKind.FleetSeedCapitalShip
                        or AIProductionDemandKind.ColonizationFleetSeedCapitalShip
                && manufacturable is CapitalShip capitalShip
                && Destination is Planet fleetPlanet
            )
            {
                if (!EnqueueFleetSeed(context, capitalShip, fleetPlanet))
                    LogEnqueueFailure();
                return;
            }

            if (Destination is Planet or Fleet && !Enqueue(context, manufacturable, Destination))
                LogEnqueueFailure();
        }

        /// <summary>
        /// Returns the maintenance cost of the proposed product.
        /// </summary>
        /// <returns>The maintenance cost.</returns>
        public int GetMaintenanceCost()
        {
            if (ContinuesExistingOrder)
                return 0;
            long totalMaintenanceCost = (long)GetUnitMaintenanceCost() * GetManufacturingCount();
            return totalMaintenanceCost > int.MaxValue ? int.MaxValue : (int)totalMaintenanceCost;
        }

        /// <summary>
        /// Returns the maintenance cost of one manufactured item.
        /// </summary>
        /// <returns>The per-item maintenance cost.</returns>
        internal int GetUnitMaintenanceCost()
        {
            if (ContinuesExistingOrder)
                return 0;
            int maintenanceCost = Product?.GetReference()?.GetMaintenanceCost() ?? 0;
            if (
                Demand?.Kind != AIProductionDemandKind.BuildingUpgrade
                || Demand.BuildingToReplace == null
            )
                return maintenanceCost;

            return Math.Max(0, maintenanceCost - Demand.BuildingToReplace.MaintenanceCost);
        }

        /// <summary>
        /// Copies a counted manufacturing proposal with an affordable prefix.
        /// </summary>
        /// <param name="count">The accepted manufacturing count.</param>
        /// <returns>The proposal representing the accepted prefix.</returns>
        internal AIManufactureProposal WithManufacturingCount(int count)
        {
            if (
                !IsCountedManufacturingDemand()
                || Demand == null
                || count == GetManufacturingCount()
            )
                return this;

            AIManufactureProposal resolved = new AIManufactureProposal(
                Demand.WithQuantity(Math.Max(0, Math.Min(count, Demand.QuantityNeeded))),
                ProducerPlanet,
                Product,
                DistributesDemand,
                _replacementOrder
            );
            if (HasScore)
                resolved.SetScore(Score);
            return resolved;
        }

        /// <summary>
        /// Returns the maintenance headroom required after manufacture.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The required maintenance headroom.</returns>
        public int GetMinimumMaintenanceHeadroom(AITurnContext context)
        {
            if (Demand?.RestoresMaintenanceCapacity == true)
                return 0;

            int reserve = context.Game.Config.AI.Selection.MaintenanceHeadroomReserve;
            if (Demand?.UsesDefensiveReserve != true)
                return reserve;

            int percentageFloor = IntegerMath.ScaleByPercentRoundedUp(
                context.Assessment.MaintenanceCapacity,
                context.Game.Config.AI.Infrastructure.PlanetaryDefenseMaintenanceReservePercent
            );
            return Math.Max(reserve, percentageFloor);
        }

        /// <summary>
        /// Returns a stable distance key for producer sorting.
        /// </summary>
        /// <returns>The producer distance sort key.</returns>
        private string GetProducerDistanceSortKey()
        {
            Planet destinationPlanet =
                Destination as Planet ?? Destination?.GetParentOfType<Planet>();
            if (destinationPlanet == null || ProducerPlanet == null)
                return string.Empty;

            return destinationPlanet
                .GetRawDistanceTo(ProducerPlanet)
                .ToString("0000000000.000", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Returns whether the manufacture proposal still has valid inputs.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="validateOrderAcceptance">Whether to validate structural order acceptance.</param>
        /// <returns>True if the proposal is still valid.</returns>
        private bool IsStillValid(AITurnContext context, bool validateOrderAcceptance)
        {
            if (
                context?.Faction == null
                || context.Manufacturing == null
                || Demand == null
                || Destination == null
                || Product?.GetReference() == null
            )
                return false;

            if (!IsOwnedBy(context, ProducerPlanet))
                return false;

            if (!ProducerPlanet.IsColonized || ProducerPlanet.IsDestroyed)
                return false;

            bool hasQueue = ProducerPlanet
                .GetManufacturingQueue()
                .TryGetValue(Demand.ManufacturingType, out List<IManufacturable> queue);
            IManufacturable currentOrder = CurrentOrder;
            if (_replacementOrder != null && currentOrder == null && hasQueue && queue.Count > 0)
                return false;
            if (
                !validateOrderAcceptance
                && !IsContinuation
                && currentOrder == null
                && hasQueue
                && queue.Any(item => item.GetTypeID() != Product.GetReference().GetTypeID())
            )
                return false;

            if (IsContinuation)
                return currentOrder is ISceneNode current
                    && current.GetOwnerInstanceID() == context.Faction.InstanceID
                    && ReferenceEquals(current.GetParent(), Destination)
                    && current.GetTypeID() == Product.GetReference().GetTypeID();

            if (
                Demand.IsProductionFiller
                && ManufacturingQueries.GetManufacturingOrder(
                    ProducerPlanet,
                    Demand.ManufacturingType
                ) != null
            )
                return false;

            if (
                Destination is Fleet destinationFleet
                && !context.StrategicPlan.CanReinforce(destinationFleet)
            )
                return false;

            if (IsFacilityExpansionDemand())
            {
                if (
                    Demand.QuantityNeeded <= 0
                    || ProducerPlanet.GetProductionFacilityCount(ManufacturingType.Building) <= 0
                )
                    return false;
            }
            else if (DistributesDemand)
            {
                if (
                    Demand.QuantityNeeded <= 0
                    || ProducerPlanet.GetProductionFacilityCount(Demand.ManufacturingType) <= 0
                )
                    return false;
            }
            else if (AvailableProducerCapacity <= 0)
                return false;

            if (Product.GetReference().GetManufacturingType() != Demand.ManufacturingType)
                return false;

            if (
                Demand.Kind != AIProductionDemandKind.BuildingUpgrade
                && IsCountedManufacturingDemand()
                && validateOrderAcceptance
                && !ManufacturingQueries.CanAcceptManufacturingOrder(
                    ProducerPlanet,
                    Product.GetReference(),
                    Destination,
                    GetManufacturingCount(),
                    context.Faction.InstanceID,
                    CurrentOrder
                )
            )
                return false;

            return Demand.Kind switch
            {
                AIProductionDemandKind.Colony
                or AIProductionDemandKind.Mine
                or AIProductionDemandKind.Refinery => CanManufactureBuilding(context),
                AIProductionDemandKind.ConstructionFacility
                or AIProductionDemandKind.Shipyard
                or AIProductionDemandKind.TrainingFacility
                or AIProductionDemandKind.BuildingUpgrade
                or AIProductionDemandKind.PlanetaryDefense => CanManufactureBuilding(context),
                AIProductionDemandKind.FleetCapitalShip => CanManufactureCapitalShip(context),
                AIProductionDemandKind.FleetStarfighter => CanManufactureStarfighter(context),
                AIProductionDemandKind.PlanetaryStarfighterReserve =>
                    CanManufacturePlanetStarfighter(context),
                AIProductionDemandKind.FleetRegiment => CanManufactureRegiment(context),
                AIProductionDemandKind.GarrisonRegimentReserve => CanManufacturePlanetRegiment(
                    context
                ),
                AIProductionDemandKind.SpecialForces or AIProductionDemandKind.IdleSpecialForces =>
                    CanManufactureSpecialForces(context),
                AIProductionDemandKind.FleetSeedCapitalShip
                or AIProductionDemandKind.ColonizationFleetSeedCapitalShip =>
                    CanManufactureFleetSeed(context),
                _ => false,
            };
        }

        /// <summary>
        /// Checks the identity, ownership and active queue membership of the evaluated order.
        /// </summary>
        /// <param name="order">The order recorded when the proposal was formed.</param>
        /// <returns>True while the same eligible order remains queued by its producer.</returns>
        private bool IsOrderStillQueued(IManufacturable order)
        {
            if (
                order is not ISceneNode node
                || ProducerPlanet == null
                || Demand == null
                || order.ManufacturingStatus != ManufacturingStatus.Building
                || order.IsManufacturingComplete()
                || order.ProducerPlanetID != ProducerPlanet.InstanceID
                || node.GetOwnerInstanceID() != ProducerPlanet.GetOwnerInstanceID()
                || !ProducerPlanet
                    .GetManufacturingQueue()
                    .TryGetValue(Demand.ManufacturingType, out var queue)
                || !IsContinuation && queue.Count != 1
            )
                return false;
            foreach (IManufacturable queued in queue)
                if (ReferenceEquals(queued, order))
                    return true;
            return false;
        }

        /// <summary>
        /// Creates a fleet and queues its first capital ship.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="capitalShip">The capital ship to evaluate.</param>
        /// <param name="destinationPlanet">The destination planet.</param>
        /// <returns>True when the ship was queued in the new fleet.</returns>
        private bool EnqueueFleetSeed(
            AITurnContext context,
            CapitalShip capitalShip,
            Planet destinationPlanet
        )
        {
            FleetRoleType roleType =
                Demand.Kind == AIProductionDemandKind.ColonizationFleetSeedCapitalShip
                    ? FleetRoleType.Colonization
                    : FleetRoleType.Battle;
            Fleet fleet = context.Faction.CreateFleet(roleType: roleType);
            context.Game.AttachNode(fleet, destinationPlanet);

            if (Enqueue(context, capitalShip, fleet))
                return true;

            context.Game.DetachNode(fleet);
            return false;
        }

        /// <summary>
        /// Returns whether a fleet seed can be manufactured at its destination.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when the destination and capital-ship template remain valid.</returns>
        private bool CanManufactureFleetSeed(AITurnContext context)
        {
            if (Destination is not Planet destinationPlanet)
                return false;

            if (
                destinationPlanet.GetOwnerInstanceID() != context.Faction.InstanceID
                || !destinationPlanet.IsColonized
                || destinationPlanet.IsDestroyed
            )
                return false;

            if (
                Product.GetReference() is not CapitalShip capitalShip
                || !IManufacturable.CanBeManufacturedBy(capitalShip, context.Faction.InstanceID)
            )
                return false;

            return true;
        }

        /// <summary>
        /// Returns whether the building product can be manufactured to the destination.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True if the building can be manufactured.</returns>
        private bool CanManufactureBuilding(AITurnContext context)
        {
            if (Destination is not Planet destinationPlanet)
                return false;

            if (Product.GetReference() is not Building building)
                return false;

            if (destinationPlanet.GetOwnerInstanceID() != context.Faction.InstanceID)
                return false;

            if (destinationPlanet.IsDestroyed)
                return false;

            if (Demand.Kind == AIProductionDemandKind.BuildingUpgrade)
                return CanReplaceProductionFacility(context, destinationPlanet, building);

            if (
                Math.Min(
                    destinationPlanet.GetAvailableEnergy() + ReleasedDestinationSpace,
                    context.StrategicPlan.GetAvailableConstructionSpace(
                        destinationPlanet,
                        building.BuildingType,
                        Demand.IsEmergencyDefense
                    )
                ) < GetManufacturingCount()
            )
                return false;

            if (building.GetBuildingType() != Demand.BuildingType)
                return false;

            if (
                Demand.BuildingType == BuildingType.Mine
                && destinationPlanet.GetUnminedResourceNodeCount() <= 0
            )
                return false;

            return IManufacturable.CanBeManufacturedBy(building, context.Faction.InstanceID);
        }

        /// <summary>
        /// Returns whether a completed production facility can be replaced by this building.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="destinationPlanet">The destination planet.</param>
        /// <param name="building">The building to evaluate.</param>
        /// <returns>True when the replacement is a valid upgrade and leaves production capacity.</returns>
        private bool CanReplaceProductionFacility(
            AITurnContext context,
            Planet destinationPlanet,
            Building building
        )
        {
            Building replacement = Demand.BuildingToReplace;
            if (
                replacement == null
                || context.Game.GetSceneNodeByInstanceID<Building>(replacement.InstanceID)
                    != replacement
                || replacement.GetParent() != destinationPlanet
                || replacement.GetOwnerInstanceID() != context.Faction.InstanceID
                || replacement.GetManufacturingStatus() != ManufacturingStatus.Complete
                || replacement.Movement != null
                || !replacement.CanUpgradeTo(building)
                || !IManufacturable.CanBeManufacturedBy(building, context.Faction.InstanceID)
                || destinationPlanet.GetEnergyUsed() > destinationPlanet.GetEnergyCapacity()
            )
                return false;

            int activeFacilityCount = destinationPlanet
                .GetAllBuildings()
                .Count(candidate =>
                    candidate.GetOwnerInstanceID() == context.Faction.InstanceID
                    && candidate.GetBuildingType() == replacement.GetBuildingType()
                    && candidate.GetManufacturingStatus() == ManufacturingStatus.Complete
                    && candidate.Movement == null
                    && candidate.GetProcessRate() > 0
                );
            return activeFacilityCount
                > context
                    .Game
                    .Config
                    .AI
                    .Infrastructure
                    .ProductionFacilityUpgradeMinimumRemainingCount;
        }

        /// <summary>
        /// Replaces the selected production facility with its planned upgrade.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when the replacement order was queued.</returns>
        private bool ExecuteBuildingUpgrade(AITurnContext context)
        {
            bool started = context.Manufacturing.StartBuildingUpgrade(
                ProducerPlanet,
                Product.GetReference() as Building,
                Destination as Planet,
                Demand.BuildingToReplace,
                context.Faction.InstanceID,
                context.AvailableProjectedMaintenanceHeadroom,
                GetMinimumMaintenanceHeadroom(context),
                out int addedMaintenance,
                out int releasedMaintenance
            );
            context.RecordManufacturingMutation(addedMaintenance, releasedMaintenance);

            if (!started)
                LogEnqueueFailure();
            return started;
        }

        /// <summary>
        /// Queues one item and records its exact maintenance change in the AI turn budget.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="manufacturable">The item to queue.</param>
        /// <param name="destination">The destination that receives the completed item.</param>
        /// <returns>True when the item was queued.</returns>
        private bool Enqueue(
            AITurnContext context,
            IManufacturable manufacturable,
            ISceneNode destination
        )
        {
            bool enqueued = context.Manufacturing.Enqueue(
                ProducerPlanet,
                manufacturable,
                destination,
                context.Faction.InstanceID,
                context.AvailableProjectedMaintenanceHeadroom,
                GetMinimumMaintenanceHeadroom(context),
                out int addedMaintenance
            );
            context.RecordManufacturingMutation(addedMaintenance, 0);
            return enqueued;
        }

        /// <summary>
        /// Logs a failed production enqueue with its product and producer identifiers.
        /// </summary>
        private void LogEnqueueFailure()
        {
            GameLogger.Warning(
                $"AI production enqueue failed for {Product?.GetReference()?.GetTypeID()} at {ProducerPlanet?.InstanceID}."
            );
        }

        /// <summary>
        /// Returns the number of units represented by the proposal.
        /// </summary>
        /// <returns>The manufacturing count.</returns>
        internal int GetManufacturingCount()
        {
            if (Demand?.IsProductionFiller == true || ContinuesExistingOrder)
                return 1;
            return IsCountedManufacturingDemand() ? Demand?.QuantityNeeded ?? 0 : 1;
        }

        /// <summary>
        /// Returns whether the proposal quantity represents more than one unit.
        /// </summary>
        /// <returns>True when manufacturing should use the demand quantity.</returns>
        private bool IsCountedManufacturingDemand()
        {
            return IsFacilityExpansionDemand()
                || Demand?.UsesDefensiveReserve == true
                || DistributesDemand;
        }

        /// <summary>
        /// Returns whether this proposal expands a production facility category.
        /// </summary>
        /// <returns>True for construction-facility, shipyard, and training-facility demand.</returns>
        private bool IsFacilityExpansionDemand()
        {
            return Demand?.Kind
                is AIProductionDemandKind.ConstructionFacility
                    or AIProductionDemandKind.Shipyard
                    or AIProductionDemandKind.TrainingFacility;
        }

        /// <summary>
        /// Returns whether a starfighter can be manufactured into a fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True if the starfighter can be manufactured.</returns>
        private bool CanManufactureStarfighter(AITurnContext context)
        {
            return Destination is Fleet destinationFleet
                && destinationFleet.GetOwnerInstanceID() == context.Faction.InstanceID
                && Product.GetReference() is Starfighter
                && destinationFleet.FindShipForStarfighter() != null;
        }

        /// <summary>
        /// Returns whether a starfighter can be manufactured at the destination planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when the destination and starfighter template remain valid.</returns>
        private bool CanManufacturePlanetStarfighter(AITurnContext context)
        {
            return Destination is Planet destinationPlanet
                && destinationPlanet.GetOwnerInstanceID() == context.Faction.InstanceID
                && destinationPlanet.IsColonized
                && !destinationPlanet.IsDestroyed
                && Product.GetReference() is Starfighter starfighter
                && IManufacturable.CanBeManufacturedBy(starfighter, context.Faction.InstanceID);
        }

        /// <summary>
        /// Returns whether a capital ship can be manufactured into a fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True if the capital ship can be manufactured.</returns>
        private bool CanManufactureCapitalShip(AITurnContext context)
        {
            return Destination is Fleet destinationFleet
                && destinationFleet.GetOwnerInstanceID() == context.Faction.InstanceID
                && Product.GetReference() is CapitalShip capitalShip
                && IManufacturable.CanBeManufacturedBy(capitalShip, context.Faction.InstanceID);
        }

        /// <summary>
        /// Returns whether a regiment can be manufactured into a fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True if the regiment can be manufactured.</returns>
        private bool CanManufactureRegiment(AITurnContext context)
        {
            return Destination is Fleet destinationFleet
                && destinationFleet.GetOwnerInstanceID() == context.Faction.InstanceID
                && Product.GetReference() is Regiment
                && destinationFleet.FindShipForRegiment() != null;
        }

        /// <summary>
        /// Returns whether a regiment can be manufactured to a planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True if the regiment can be manufactured.</returns>
        private bool CanManufacturePlanetRegiment(AITurnContext context)
        {
            return Destination is Planet destinationPlanet
                && destinationPlanet.GetOwnerInstanceID() == context.Faction.InstanceID
                && !destinationPlanet.IsDestroyed
                && Product.GetReference() is Regiment;
        }

        /// <summary>
        /// Returns whether the requested special-forces type can be manufactured at the planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when the destination and requested template remain valid.</returns>
        private bool CanManufactureSpecialForces(AITurnContext context)
        {
            return Destination is Planet destinationPlanet
                && destinationPlanet.GetOwnerInstanceID() == context.Faction.InstanceID
                && destinationPlanet.IsColonized
                && !destinationPlanet.IsDestroyed
                && Product.GetReference() is SpecialForces specialForces
                && specialForces.GetTypeID() == Demand.ProductTypeId
                && IManufacturable.CanBeManufacturedBy(specialForces, context.Faction.InstanceID);
        }

        /// <summary>
        /// Returns the shared producer-capacity claim key.
        /// </summary>
        /// <returns>The capacity key.</returns>
        internal string GetProducerCapacityKey()
        {
            if (Demand?.ManufacturingType == ManufacturingType.Building)
                return $"production:building:{ProducerPlanet.InstanceID}";

            return $"production:{Demand?.ManufacturingType}:{ProducerPlanet.InstanceID}";
        }

        /// <summary>
        /// Returns whether maintenance can support this proposal.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True if maintenance headroom is sufficient.</returns>
        private bool HasMaintenanceHeadroom(AITurnContext context)
        {
            if (Demand?.RestoresMaintenanceCapacity == true)
                return true;

            int maintenanceCost = GetMaintenanceCost();
            if (maintenanceCost <= 0)
                return true;

            int minimumHeadroom = GetMinimumMaintenanceHeadroom(context);
            return (long)context.AvailableProjectedMaintenanceHeadroom
                    - maintenanceCost
                    + ReplacedMaintenanceCost
                >= minimumHeadroom;
        }
    }
}
