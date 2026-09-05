using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Planners.Demand;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

namespace Rebellion.AI.Proposals
{
    /// <summary>
    /// Pairs a producer with the producer-specific form of a production demand.
    /// </summary>
    internal readonly struct AIManufactureOption
    {
        internal AIDemand Demand { get; }

        internal Planet ProducerPlanet { get; }

        /// <summary>
        /// Creates a production option.
        /// </summary>
        /// <param name="demand">Demand adjusted for this producer.</param>
        /// <param name="producerPlanet">Planet capable of serving the demand.</param>
        public AIManufactureOption(AIDemand demand, Planet producerPlanet)
        {
            Demand = demand;
            ProducerPlanet = producerPlanet;
        }
    }

    /// <summary>
    /// Proposal to enqueue a manufacturable item.
    /// </summary>
    public sealed class AIManufactureProposal : AIProposal
    {
        public AIDemand Demand { get; private set; }

        public Planet ProducerPlanet { get; private set; }

        internal IReadOnlyList<AIManufactureOption> ProducerOptions { get; }

        internal IReadOnlyList<Planet> ProducerPlanets { get; }

        public Technology Product { get; }

        public ContainerNode Destination => Demand?.Destination;

        internal bool DistributesDemand { get; }

        internal bool UsesSharedProducerCapacity =>
            !IsFacilityExpansionDemand() && !DistributesDemand;

        /// <summary>
        /// Creates a manufacture proposal.
        /// </summary>
        /// <param name="demand">Production demand served by the proposal.</param>
        /// <param name="producerPlanet">Planet that will produce the item.</param>
        /// <param name="product">Technology to manufacture.</param>
        public AIManufactureProposal(AIDemand demand, Planet producerPlanet, Technology product)
            : this(demand, producerPlanet, product, false) { }

        /// <summary>
        /// Creates a manufacture proposal from one demand and producer.
        /// </summary>
        /// <param name="demand">Production demand served by the proposal.</param>
        /// <param name="producerPlanet">Planet that will produce the item.</param>
        /// <param name="product">Technology to manufacture.</param>
        /// <param name="distributesDemand">Whether the proposal may satisfy demand across producers.</param>
        internal AIManufactureProposal(
            AIDemand demand,
            Planet producerPlanet,
            Technology product,
            bool distributesDemand
        )
            : this(demand, new[] { producerPlanet }, product, distributesDemand) { }

        /// <summary>
        /// Creates a manufacture proposal from one demand and multiple producers.
        /// </summary>
        /// <param name="demand">Production demand served by the proposal.</param>
        /// <param name="producerPlanets">Planets eligible to produce the item.</param>
        /// <param name="product">Technology to manufacture.</param>
        /// <param name="distributesDemand">Whether the proposal may satisfy demand across producers.</param>
        internal AIManufactureProposal(
            AIDemand demand,
            IReadOnlyList<Planet> producerPlanets,
            Technology product,
            bool distributesDemand
        )
        {
            Demand = demand;
            ProducerPlanets = producerPlanets ?? System.Array.Empty<Planet>();
            ProducerOptions = System.Array.Empty<AIManufactureOption>();
            ProducerPlanet = ProducerPlanets.FirstOrDefault();
            Product = product;
            DistributesDemand = distributesDemand;
        }

        /// <summary>
        /// Creates a manufacture proposal from precomputed producer options.
        /// </summary>
        /// <param name="producerOptions">Eligible demand and producer combinations.</param>
        /// <param name="product">Technology to manufacture.</param>
        /// <param name="distributesDemand">Whether the proposal may satisfy demand across producers.</param>
        internal AIManufactureProposal(
            IReadOnlyList<AIManufactureOption> producerOptions,
            Technology product,
            bool distributesDemand
        )
        {
            ProducerOptions = producerOptions ?? System.Array.Empty<AIManufactureOption>();
            ProducerPlanets = System.Array.Empty<Planet>();
            SelectOption(ProducerOptions.FirstOrDefault());
            Product = product;
            DistributesDemand = distributesDemand;
        }

        /// <summary>
        /// Selects the producer option used when validating and executing this proposal.
        /// </summary>
        /// <param name="option">The producer option to use.</param>
        internal void SelectOption(AIManufactureOption option)
        {
            Demand = option.Demand;
            ProducerPlanet = option.ProducerPlanet;
        }

        /// <summary>
        /// Selects an equivalent producer while retaining the current demand.
        /// </summary>
        /// <param name="producerPlanet">The producer to use.</param>
        internal void SelectProducer(Planet producerPlanet)
        {
            ProducerPlanet = producerPlanet;
        }

        /// <summary>
        /// Returns claims that prevent incompatible production proposals.
        /// </summary>
        /// <returns>Claim keys for this proposal.</returns>
        public override IReadOnlyList<string> GetClaimKeys()
        {
            List<string> claimKeys = new List<string>();

            if (Demand != null && !DistributesDemand)
                claimKeys.Add(AIClaimKeys.ProductionDemand(Demand.Id));

            if (ProducerPlanet != null && !UsesSharedProducerCapacity)
                claimKeys.Add(GetProducerCapacityKey());

            if (Product?.GetReference() is Building && Destination is Planet destinationPlanet)
                claimKeys.Add(
                    AIClaimKeys.ProductionBuildingDestination(destinationPlanet.InstanceID)
                );

            if (Demand?.BuildingToReplace != null)
                claimKeys.Add(
                    AIClaimKeys.ProductionBuildingReplacement(Demand.BuildingToReplace.InstanceID)
                );

            if (Demand?.Kind == AIDemandKind.ConstructionFacility)
                claimKeys.Add(
                    AIClaimKeys.ProductionBuildingKind(BuildingType.ConstructionFacility)
                );

            if (Destination is Fleet destinationFleet && !DistributesDemand)
            {
                claimKeys.Add(
                    AIClaimKeys.FleetReinforcement(Demand?.Kind, destinationFleet.InstanceID)
                );
                if (Demand?.Kind == AIDemandKind.FleetCapitalShip)
                    claimKeys.Add(
                        AIClaimKeys.FleetCapitalReinforcement(destinationFleet.InstanceID)
                    );
            }

            if (
                Demand?.Kind
                is AIDemandKind.FleetSeedCapitalShip
                    or AIDemandKind.ColonizationFleetSeedCapitalShip
            )
                claimKeys.Add(AIClaimKeys.FleetCreation(Demand.Destination?.GetOwnerInstanceID()));

            return claimKeys;
        }

        /// <summary>
        /// Returns a stable sort key for manufacture proposals.
        /// </summary>
        /// <returns>A stable sort key.</returns>
        public override string GetSortKey()
        {
            if (
                Demand?.Kind
                is AIDemandKind.FleetSeedCapitalShip
                    or AIDemandKind.ColonizationFleetSeedCapitalShip
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

            if (Demand.Kind == AIDemandKind.BuildingUpgrade)
            {
                ExecuteBuildingUpgrade(context);
                return;
            }

            if (IsCountedManufacturingDemand())
            {
                if (
                    !context.Manufacturing.StartManufacturing(
                        ProducerPlanet,
                        Product.GetReference(),
                        Destination,
                        GetManufacturingCount(),
                        context.Faction.InstanceID
                    )
                )
                    LogEnqueueFailure();
                return;
            }

            IManufacturable manufacturable = Product.GetReferenceCopy();
            if (manufacturable is not ISceneNode sceneNode)
                return;

            sceneNode.OwnerInstanceID = context.Faction.InstanceID;

            if (
                Demand.Kind
                    is AIDemandKind.FleetSeedCapitalShip
                        or AIDemandKind.ColonizationFleetSeedCapitalShip
                && manufacturable is CapitalShip capitalShip
                && Destination is Planet fleetPlanet
            )
            {
                if (!EnqueueFleetSeed(context, capitalShip, fleetPlanet))
                    LogEnqueueFailure();
                return;
            }

            if (Destination is Planet planet)
            {
                if (!EnqueueAtPlanet(context, planet, manufacturable))
                    LogEnqueueFailure();
                return;
            }

            if (
                Destination is Fleet fleet
                && !context.Manufacturing.Enqueue(ProducerPlanet, manufacturable, fleet)
            )
                LogEnqueueFailure();
        }

        /// <summary>
        /// Returns the maintenance cost of the proposed product.
        /// </summary>
        /// <returns>The maintenance cost.</returns>
        public int GetMaintenanceCost()
        {
            int maintenanceCost = Product?.GetReference()?.GetMaintenanceCost() ?? 0;
            if (Demand?.Kind == AIDemandKind.BuildingUpgrade && Demand.BuildingToReplace != null)
            {
                maintenanceCost = Math.Max(
                    0,
                    maintenanceCost - Demand.BuildingToReplace.MaintenanceCost
                );
            }

            long totalMaintenanceCost = (long)maintenanceCost * GetManufacturingCount();
            return totalMaintenanceCost > int.MaxValue ? int.MaxValue : (int)totalMaintenanceCost;
        }

        /// <summary>
        /// Returns the maintenance headroom required after manufacture.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The required maintenance headroom.</returns>
        public int GetMinimumMaintenanceHeadroom(AITurnContext context)
        {
            int hardFloor = context.Game.Config.AI.Selection.MaintenanceHeadroomHardFloor;
            if (Demand?.UsesDefensiveReserve != true)
                return hardFloor;

            int percentageFloor = IntegerMath.ScaleByPercentRoundedUp(
                context.Assessment.MaintenanceCapacity,
                context.Game.Config.AI.Infrastructure.PlanetaryDefenseMaintenanceReservePercent
            );
            return Math.Max(
                hardFloor,
                Math.Max(
                    context.Game.Config.AI.Selection.MinimumMaintenanceHeadroomAfterProduction,
                    percentageFloor
                )
            );
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
                Demand.Kind != AIDemandKind.BuildingUpgrade
                && IsCountedManufacturingDemand()
                && validateOrderAcceptance
                && !context.Manufacturing.CanAcceptManufacturingOrder(
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
                AIDemandKind.Colony or AIDemandKind.Mine or AIDemandKind.Refinery =>
                    CanManufactureBuilding(context),
                AIDemandKind.ConstructionFacility
                or AIDemandKind.Shipyard
                or AIDemandKind.TrainingFacility
                or AIDemandKind.BuildingUpgrade
                or AIDemandKind.PlanetaryDefense => CanManufactureBuilding(context),
                AIDemandKind.FleetCapitalShip => CanManufactureCapitalShip(context),
                AIDemandKind.FleetStarfighter => CanManufactureStarfighter(context),
                AIDemandKind.PlanetaryStarfighterReserve => CanManufacturePlanetStarfighter(
                    context
                ),
                AIDemandKind.FleetRegiment => CanManufactureRegiment(context),
                AIDemandKind.GarrisonRegimentReserve => CanManufacturePlanetRegiment(context),
                AIDemandKind.SpecialForces => CanManufactureSpecialForces(context),
                AIDemandKind.FleetSeedCapitalShip
                or AIDemandKind.ColonizationFleetSeedCapitalShip => CanManufactureFleetSeed(
                    context
                ),
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
                Demand.Kind == AIDemandKind.ColonizationFleetSeedCapitalShip
                    ? FleetRoleType.Colonization
                    : FleetRoleType.Battle;
            Fleet fleet = context.Faction.CreateFleet(roleType: roleType);
            context.Game.AttachNode(fleet, destinationPlanet);

            if (context.Manufacturing.Enqueue(ProducerPlanet, capitalShip, fleet))
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

            if (Demand.Kind == AIDemandKind.BuildingUpgrade)
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
        /// Replaces the completed facility and restores it if manufacturing cannot start.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        private void ExecuteBuildingUpgrade(AITurnContext context)
        {
            Building replacement = Demand.BuildingToReplace;
            Planet destinationPlanet = Destination as Planet;
            context.Game.DetachNode(replacement);

            bool started = false;
            try
            {
                started = context.Manufacturing.StartManufacturing(
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
            return context.Manufacturing.Enqueue(ProducerPlanet, manufacturable, destinationPlanet);
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
                is AIDemandKind.ConstructionFacility
                    or AIDemandKind.Shipyard
                    or AIDemandKind.TrainingFacility;
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
                return AIClaimKeys.BuildingManufacturingLane(ProducerPlanet.InstanceID);

            return AIClaimKeys.ManufacturingLane(
                Demand?.ManufacturingType,
                ProducerPlanet.InstanceID
            );
        }

        /// <summary>
        /// Returns whether maintenance can support this proposal.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True if maintenance headroom is sufficient.</returns>
        private bool HasMaintenanceHeadroom(AITurnContext context)
        {
            int maintenanceCost = GetMaintenanceCost();
            if (maintenanceCost <= 0)
                return true;

            int minimumHeadroom = GetMinimumMaintenanceHeadroom(context);
            return context.Faction.ProjectedMaintenanceHeadroom - maintenanceCost
                >= minimumHeadroom;
        }
    }
}
