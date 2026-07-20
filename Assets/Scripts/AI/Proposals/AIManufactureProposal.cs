using System.Collections.Generic;
using System.Globalization;
using Rebellion.AI.Director;
using Rebellion.AI.Planners;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

namespace Rebellion.AI.Proposals
{
    /// <summary>
    /// Proposal to enqueue a manufacturable item.
    /// </summary>
    public sealed class AIManufactureProposal : AIProposal
    {
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
        {
            Demand = demand;
            ProducerPlanet = producerPlanet;
            Product = product;
        }

        public AIProductionDemand Demand { get; }

        public Planet ProducerPlanet { get; }

        public Technology Product { get; }

        public ContainerNode Destination => Demand?.Destination;

        /// <summary>
        /// Returns claims that prevent incompatible production proposals.
        /// </summary>
        /// <returns>Claim keys for this proposal.</returns>
        public override IReadOnlyList<string> GetClaimKeys()
        {
            List<string> claimKeys = new List<string>();

            if (Demand != null)
                claimKeys.Add($"production:demand:{Demand.Id}");

            if (ProducerPlanet != null)
                claimKeys.Add(GetProducerClaimKey());

            if (Product?.GetReference() is Building && Destination is Planet destinationPlanet)
                claimKeys.Add($"production:building-destination:{destinationPlanet.InstanceID}");

            if (Demand?.Kind == AIProductionDemandKind.ConstructionFacility)
                claimKeys.Add("production:building-kind:ConstructionFacility");

            if (Destination is Fleet destinationFleet)
            {
                claimKeys.Add($"fleet:reinforcement:{Demand?.Kind}:{destinationFleet.InstanceID}");
                if (Demand?.Kind == AIProductionDemandKind.FleetCapitalShip)
                    claimKeys.Add($"fleet:capital-reinforcement:{destinationFleet.InstanceID}");
            }

            if (Demand?.Kind == AIProductionDemandKind.FleetSeedCapitalShip)
                claimKeys.Add($"fleet:create:{Demand.Destination?.GetOwnerInstanceID()}");

            return claimKeys;
        }

        /// <summary>
        /// Returns a stable sort key for manufacture proposals.
        /// </summary>
        /// <returns>A stable sort key.</returns>
        public override string GetSortKey()
        {
            if (Demand?.Kind == AIProductionDemandKind.FleetSeedCapitalShip)
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
            return IsStillValid(context);
        }

        /// <summary>
        /// Returns whether this proposal may execute against the current game state.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True if this proposal may execute.</returns>
        public override bool CanExecute(AITurnContext context)
        {
            return IsStillValid(context) && HasMaintenanceHeadroom(context);
        }

        /// <summary>
        /// Enqueues the product at the producer planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public override void Execute(AITurnContext context)
        {
            if (!CanExecute(context))
                return;

            IManufacturable manufacturable = Product.GetReferenceCopy();
            if (manufacturable is not ISceneNode sceneNode)
                return;

            sceneNode.OwnerInstanceID = context.Faction.InstanceID;

            if (
                Demand.Kind == AIProductionDemandKind.FleetSeedCapitalShip
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
            return Product?.GetReference()?.GetMaintenanceCost() ?? 0;
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
        /// <returns>True if the proposal is still valid.</returns>
        private bool IsStillValid(AITurnContext context)
        {
            if (
                context?.Faction == null
                || context.Manufacturing == null
                || Demand == null
                || ProducerPlanet == null
                || Destination == null
                || Product?.GetReference() == null
            )
                return false;

            if (ProducerPlanet.GetOwnerInstanceID() != context.Faction.InstanceID)
                return false;

            if (!ProducerPlanet.IsColonized || ProducerPlanet.IsDestroyed)
                return false;

            if (ProducerPlanet.GetAvailableManufacturingCapacity(Demand.ManufacturingType) <= 0)
                return false;

            if (Product.GetReference().GetManufacturingType() != Demand.ManufacturingType)
                return false;

            return Demand.Kind switch
            {
                AIProductionDemandKind.Mine or AIProductionDemandKind.Refinery =>
                    CanManufactureBuilding(context),
                AIProductionDemandKind.ConstructionFacility
                or AIProductionDemandKind.Shipyard
                or AIProductionDemandKind.TrainingFacility
                or AIProductionDemandKind.HeadquartersDefense => CanManufactureBuilding(context),
                AIProductionDemandKind.FleetCapitalShip => CanManufactureCapitalShip(context),
                AIProductionDemandKind.FleetStarfighter => CanManufactureStarfighter(context),
                AIProductionDemandKind.FleetRegiment => CanManufactureRegiment(context),
                AIProductionDemandKind.LocalStarfighterReserve => CanManufacturePlanetStarfighter(
                    context
                ),
                AIProductionDemandKind.GarrisonRegimentReserve => CanManufacturePlanetRegiment(
                    context
                ),
                AIProductionDemandKind.SpecialForces => CanManufactureSpecialForces(context),
                AIProductionDemandKind.FleetSeedCapitalShip => CanManufactureFleetSeed(context),
                _ => false,
            };
        }

        private bool EnqueueFleetSeed(
            AITurnContext context,
            CapitalShip capitalShip,
            Planet destinationPlanet
        )
        {
            Fleet fleet = context.Faction.CreateFleet(roleType: FleetRoleType.Battle);
            context.Game.AttachNode(fleet, destinationPlanet);

            if (context.Manufacturing.Enqueue(ProducerPlanet, capitalShip, fleet))
                return true;

            context.Game.DetachNode(fleet);
            return false;
        }

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
                || !capitalShip.HasAllowedOwnerInstanceID(context.Faction.InstanceID)
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

            if (destinationPlanet.GetAvailableEnergy() <= 0)
                return false;

            if (building.GetBuildingType() != Demand.BuildingType)
                return false;

            if (
                Demand.BuildingType == BuildingType.Mine
                && destinationPlanet.GetUnminedResourceNodeCount() <= 0
            )
                return false;

            return building.HasAllowedOwnerInstanceID(context.Faction.InstanceID);
        }

        private bool EnqueueAtPlanet(
            AITurnContext context,
            Planet destinationPlanet,
            IManufacturable manufacturable
        )
        {
            return context.Manufacturing.Enqueue(ProducerPlanet, manufacturable, destinationPlanet);
        }

        private void LogEnqueueFailure()
        {
            GameLogger.Warning(
                $"AI production enqueue failed for {Product?.GetReference()?.GetTypeID()} at {ProducerPlanet?.InstanceID}."
            );
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
        /// Returns whether a capital ship can be manufactured into a fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True if the capital ship can be manufactured.</returns>
        private bool CanManufactureCapitalShip(AITurnContext context)
        {
            return Destination is Fleet destinationFleet
                && destinationFleet.GetOwnerInstanceID() == context.Faction.InstanceID
                && Product.GetReference() is CapitalShip capitalShip
                && capitalShip.HasAllowedOwnerInstanceID(context.Faction.InstanceID);
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
        /// Returns whether a starfighter can be manufactured to a planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True if the starfighter can be manufactured.</returns>
        private bool CanManufacturePlanetStarfighter(AITurnContext context)
        {
            return Destination is Planet destinationPlanet
                && destinationPlanet.GetOwnerInstanceID() == context.Faction.InstanceID
                && !destinationPlanet.IsDestroyed
                && Product.GetReference() is Starfighter;
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

        private bool CanManufactureSpecialForces(AITurnContext context)
        {
            return Destination is Planet destinationPlanet
                && destinationPlanet.GetOwnerInstanceID() == context.Faction.InstanceID
                && destinationPlanet.IsColonized
                && !destinationPlanet.IsDestroyed
                && Product.GetReference() is SpecialForces specialForces
                && specialForces.GetTypeID() == Demand.ProductTypeId
                && specialForces.HasAllowedOwnerInstanceID(context.Faction.InstanceID);
        }

        /// <summary>
        /// Returns the claim key for the producer lane.
        /// </summary>
        /// <returns>The producer claim key.</returns>
        private string GetProducerClaimKey()
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
            int maintenanceCost = GetMaintenanceCost();
            if (maintenanceCost <= 0)
                return true;

            int minimumHeadroom = context.Game.Config.AI.Selection.MaintenanceHeadroomHardFloor;
            return context.Faction.ProjectedMaintenanceHeadroom - maintenanceCost
                >= minimumHeadroom;
        }
    }
}
