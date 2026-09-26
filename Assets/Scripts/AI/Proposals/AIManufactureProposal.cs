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
        public AIProductionDemand Demand { get; }

        public Planet ProducerPlanet { get; }

        internal IReadOnlyList<AIManufactureProposal> ProducerAlternatives { get; }

        internal bool CarriesReducedCountAcrossAlternatives { get; }

        public Technology Product { get; }

        /// <summary>
        /// Gets the number of manufactured items represented by this proposal.
        /// </summary>
        public int ManufacturingCount => GetManufacturingCount();

        public ContainerNode Destination => Demand?.Destination;

        internal bool DistributesDemand { get; }

        internal bool UsesSharedProducerCapacity =>
            !IsFacilityExpansionDemand() && !DistributesDemand;

        internal bool IsProductionFacilityExpansion => IsFacilityExpansionDemand();

        internal override AIProposalPriority Priority =>
            Demand?.Kind == AIProductionDemandKind.ColonizationFleetSeedCapitalShip
            || Demand?.DestinationFleet?.RoleType == FleetRoleType.Colonization
                ? AIProposalPriority.Mandatory
            : Demand?.Kind == AIProductionDemandKind.PlanetaryDefense
                ? AIProposalPriority.DeferredPlanetaryDefense
            : AIProposalPriority.Optional;

        /// <summary>
        /// Creates a manufacture proposal.
        /// </summary>
        /// <param name="demand">Production demand served by the proposal.</param>
        /// <param name="producerPlanet">Planet that will produce the item.</param>
        /// <param name="product">Technology to manufacture.</param>
        public AIManufactureProposal(
            AIProductionDemand demand,
            Planet producerPlanet,
            Technology product
        )
            : this(demand, producerPlanet, product, false) { }

        /// <summary>
        /// Creates a manufacture proposal from one demand and producer.
        /// </summary>
        /// <param name="demand">Production demand served by the proposal.</param>
        /// <param name="producerPlanet">Planet that will produce the item.</param>
        /// <param name="product">Technology to manufacture.</param>
        /// <param name="distributesDemand">Whether the proposal may satisfy demand across producers.</param>
        internal AIManufactureProposal(
            AIProductionDemand demand,
            Planet producerPlanet,
            Technology product,
            bool distributesDemand
        )
        {
            Demand = demand;
            ProducerPlanet = producerPlanet;
            Product = product;
            DistributesDemand = distributesDemand;
            ProducerAlternatives = Array.Empty<AIManufactureProposal>();
        }

        /// <summary>
        /// Creates a manufacture proposal from one demand and multiple producers.
        /// </summary>
        /// <param name="demand">Production demand served by the proposal.</param>
        /// <param name="producerPlanets">Planets eligible to produce the item.</param>
        /// <param name="product">Technology to manufacture.</param>
        /// <param name="distributesDemand">Whether the proposal may satisfy demand across producers.</param>
        internal AIManufactureProposal(
            AIProductionDemand demand,
            IReadOnlyList<Planet> producerPlanets,
            Technology product,
            bool distributesDemand
        )
        {
            Demand = demand;
            ProducerPlanet = producerPlanets?.FirstOrDefault();
            Product = product;
            DistributesDemand = distributesDemand;
            ProducerAlternatives =
                producerPlanets
                    ?.Skip(1)
                    .Select(producerPlanet => new AIManufactureProposal(
                        demand,
                        producerPlanet,
                        product,
                        distributesDemand
                    ))
                    .ToList()
                ?? new List<AIManufactureProposal>();
            CarriesReducedCountAcrossAlternatives = true;
        }

        /// <summary>
        /// Creates a manufacture proposal from exact ranked alternatives.
        /// </summary>
        /// <param name="candidates">Exact eligible manufacturing actions in preference order.</param>
        internal AIManufactureProposal(IReadOnlyList<AIManufactureProposal> candidates)
        {
            AIManufactureProposal first = candidates?.FirstOrDefault();
            Demand = first?.Demand;
            ProducerPlanet = first?.ProducerPlanet;
            Product = first?.Product;
            DistributesDemand = first?.DistributesDemand == true;
            ProducerAlternatives =
                candidates?.Skip(1).ToList() ?? new List<AIManufactureProposal>();
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
                && HasMaintenanceHeadroom(context);
        }

        /// <summary>
        /// Enqueues the product at the producer planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public override void Execute(AITurnContext context)
        {
            if (!CanExecute(context))
                return;

            if (Demand.Kind == AIProductionDemandKind.BuildingUpgrade)
            {
                if (ExecuteBuildingUpgrade(context))
                    CommitMaintenance(context);
                return;
            }

            if (IsCountedManufacturingDemand())
            {
                bool started = context.Manufacturing.StartPrevalidatedManufacturing(
                    ProducerPlanet,
                    Product.GetReference(),
                    Destination,
                    GetManufacturingCount(),
                    context.Faction.InstanceID
                );
                if (!started)
                    LogEnqueueFailure();
                else
                    CommitMaintenance(context);
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
                else
                    CommitMaintenance(context);
                return;
            }

            if (Destination is Planet planet)
            {
                if (!EnqueueAtPlanet(context, planet, manufacturable))
                    LogEnqueueFailure();
                else
                    CommitMaintenance(context);
                return;
            }

            if (Destination is Fleet fleet)
            {
                if (!context.Manufacturing.Enqueue(ProducerPlanet, manufacturable, fleet, true))
                    LogEnqueueFailure();
                else
                    CommitMaintenance(context);
            }
        }

        /// <summary>
        /// Returns the maintenance cost of the proposed product.
        /// </summary>
        /// <returns>The maintenance cost.</returns>
        public int GetMaintenanceCost()
        {
            long totalMaintenanceCost = (long)GetUnitMaintenanceCost() * GetManufacturingCount();
            return totalMaintenanceCost > int.MaxValue ? int.MaxValue : (int)totalMaintenanceCost;
        }

        /// <summary>
        /// Returns the maintenance cost of one manufactured item.
        /// </summary>
        /// <returns>The per-item maintenance cost.</returns>
        internal int GetUnitMaintenanceCost()
        {
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
            if (!IsCountedManufacturingDemand() || Demand == null)
                return this;

            AIManufactureProposal resolved = new AIManufactureProposal(
                Demand.WithQuantity(Math.Max(0, Math.Min(count, Demand.QuantityNeeded))),
                ProducerPlanet,
                Product,
                DistributesDemand
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

            if (Destination is Fleet destinationFleet && destinationFleet.Movement != null)
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
            else if (
                ProducerPlanet.GetAvailableManufacturingCapacity(Demand.ManufacturingType) <= 0
            )
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
                    context.Faction.InstanceID
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
                AIProductionDemandKind.SpecialForces => CanManufactureSpecialForces(context),
                AIProductionDemandKind.FleetSeedCapitalShip
                or AIProductionDemandKind.ColonizationFleetSeedCapitalShip =>
                    CanManufactureFleetSeed(context),
                _ => false,
            };
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

            if (context.Manufacturing.Enqueue(ProducerPlanet, capitalShip, fleet, true))
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

            if (destinationPlanet.GetAvailableEnergy() < GetManufacturingCount())
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
            Building replacement = Demand.BuildingToReplace;
            Planet destinationPlanet = Destination as Planet;
            context.Game.DetachNode(replacement);

            bool started = false;
            try
            {
                started = context.Manufacturing.StartPrevalidatedManufacturing(
                    ProducerPlanet,
                    Product.GetReference(),
                    destinationPlanet,
                    1,
                    context.Faction.InstanceID
                );
            }
            finally
            {
                if (!started && replacement.GetParent() == null)
                    context.Game.AttachNode(replacement, destinationPlanet);
            }

            if (!started)
                LogEnqueueFailure();
            return started;
        }

        /// <summary>
        /// Queues a manufactured item for delivery to a planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="destinationPlanet">The destination planet.</param>
        /// <param name="manufacturable">The manufacturable.</param>
        /// <returns>True when the item was queued.</returns>
        private bool EnqueueAtPlanet(
            AITurnContext context,
            Planet destinationPlanet,
            IManufacturable manufacturable
        )
        {
            return context.Manufacturing.Enqueue(
                ProducerPlanet,
                manufacturable,
                destinationPlanet,
                true
            );
        }

        /// <summary>
        /// Commits this successfully queued order to the turn-scoped maintenance budget.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        private void CommitMaintenance(AITurnContext context)
        {
            if (Demand?.RestoresMaintenanceCapacity != true)
                context.CommitManufacturingMaintenance(GetMaintenanceCost());
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
            return context.AvailableProjectedMaintenanceHeadroom - maintenanceCost
                >= minimumHeadroom;
        }
    }
}
