using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Planners;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

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
                claimKeys.Add($"fleet:reinforcement:{Demand?.Kind}:{destinationFleet.InstanceID}");

            return claimKeys;
        }

        /// <summary>
        /// Returns a stable sort key for manufacture proposals.
        /// </summary>
        /// <returns>A stable sort key.</returns>
        public override string GetSortKey()
        {
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

            if (Destination is Planet planet)
            {
                if (
                    manufacturable is Building building
                    && !EnsureBuildingDestinationCanAccept(context, planet, building)
                )
                    return;

                context.Manufacturing.Enqueue(ProducerPlanet, manufacturable, planet);
                return;
            }

            if (Destination is Fleet fleet)
                context.Manufacturing.Enqueue(ProducerPlanet, manufacturable, fleet);
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
                or AIProductionDemandKind.TrainingFacility => CanManufactureBuilding(context),
                AIProductionDemandKind.FleetCapitalShip => CanManufactureCapitalShip(context),
                AIProductionDemandKind.FleetStarfighter => CanManufactureStarfighter(context),
                AIProductionDemandKind.FleetRegiment => CanManufactureRegiment(context),
                AIProductionDemandKind.LocalStarfighterReserve => CanManufacturePlanetStarfighter(
                    context
                ),
                AIProductionDemandKind.GarrisonRegimentReserve => CanManufacturePlanetRegiment(
                    context
                ),
                _ => false,
            };
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

            if (
                destinationPlanet.GetAvailableEnergy() <= 0
                && !CanReplaceBuildingForIncomingBuilding(context, destinationPlanet, building)
            )
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

        /// <summary>
        /// Ensures a building destination has room for the incoming building.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="destinationPlanet">Planet receiving the building.</param>
        /// <param name="building">Building being manufactured.</param>
        /// <returns>True if the destination can accept the building.</returns>
        private bool EnsureBuildingDestinationCanAccept(
            AITurnContext context,
            Planet destinationPlanet,
            Building building
        )
        {
            if (destinationPlanet.GetAvailableEnergy() > 0)
                return true;

            Building replacement = GetReplacementBuildingForIncomingBuilding(
                context,
                destinationPlanet,
                building
            );
            if (replacement == null)
                return false;

            context.Game.DetachNode(replacement);
            return destinationPlanet.GetAvailableEnergy() > 0;
        }

        /// <summary>
        /// Returns whether a destination can replace a building for the incoming building.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="destinationPlanet">Planet receiving the building.</param>
        /// <param name="incomingBuilding">Building being manufactured.</param>
        /// <returns>True if a replacement building exists.</returns>
        private bool CanReplaceBuildingForIncomingBuilding(
            AITurnContext context,
            Planet destinationPlanet,
            Building incomingBuilding
        )
        {
            return GetReplacementBuildingForIncomingBuilding(
                    context,
                    destinationPlanet,
                    incomingBuilding
                ) != null;
        }

        /// <summary>
        /// Returns the building that can be replaced for an incoming building.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="destinationPlanet">Planet receiving the building.</param>
        /// <param name="incomingBuilding">Building being manufactured.</param>
        /// <returns>The replacement building, or null.</returns>
        private Building GetReplacementBuildingForIncomingBuilding(
            AITurnContext context,
            Planet destinationPlanet,
            Building incomingBuilding
        )
        {
            if (!CanReplaceFacilityForIncomingBuilding(incomingBuilding))
                return null;

            return destinationPlanet
                .GetAllBuildings()
                .Where(building =>
                    CanReplaceBuilding(context, destinationPlanet, incomingBuilding, building)
                )
                .OrderByDescending(building =>
                    GetExcessFacilityCount(context, building.GetBuildingType())
                )
                .ThenByDescending(building => building.MaintenanceCost)
                .ThenBy(building => building.InstanceID)
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns whether the incoming building may replace another facility.
        /// </summary>
        /// <param name="building">The incoming building.</param>
        /// <returns>True if this building type can trigger replacement.</returns>
        private bool CanReplaceFacilityForIncomingBuilding(Building building)
        {
            return building.GetBuildingType()
                is BuildingType.Mine
                    or BuildingType.Refinery
                    or BuildingType.ConstructionFacility
                    or BuildingType.Shipyard
                    or BuildingType.TrainingFacility;
        }

        /// <summary>
        /// Returns whether an existing building can be replaced.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="destinationPlanet">Planet holding the existing building.</param>
        /// <param name="incomingBuilding">Building being manufactured.</param>
        /// <param name="building">Existing building to inspect.</param>
        /// <returns>True if the existing building can be replaced.</returns>
        private bool CanReplaceBuilding(
            AITurnContext context,
            Planet destinationPlanet,
            Building incomingBuilding,
            Building building
        )
        {
            if (building == null)
                return false;

            if (building.GetOwnerInstanceID() != context.Faction.InstanceID)
                return false;

            if (building.GetManufacturingStatus() != ManufacturingStatus.Complete)
                return false;

            if (building.Movement != null)
                return false;

            if (building.GetBuildingType() == incomingBuilding.GetBuildingType())
                return false;

            return building.GetBuildingType() switch
            {
                BuildingType.Shipyard
                or BuildingType.TrainingFacility
                or BuildingType.ConstructionFacility => CanReplaceExcessFacility(
                    context,
                    destinationPlanet,
                    building.GetBuildingType()
                ),
                _ => false,
            };
        }

        /// <summary>
        /// Returns whether a completed facility is excess for replacement.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="destinationPlanet">Planet holding the facility.</param>
        /// <param name="buildingType">Facility type to inspect.</param>
        /// <returns>True if the facility type is excess.</returns>
        private bool CanReplaceExcessFacility(
            AITurnContext context,
            Planet destinationPlanet,
            BuildingType buildingType
        )
        {
            if (destinationPlanet.GetBuildingTypeCount(buildingType) <= 1)
                return false;

            return GetExcessFacilityCount(context, buildingType) > 0;
        }

        /// <summary>
        /// Returns the global excess count for a facility type.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="buildingType">Facility type to inspect.</param>
        /// <returns>The excess facility count.</returns>
        private int GetExcessFacilityCount(AITurnContext context, BuildingType buildingType)
        {
            int currentCount = context.Assessment.OwnedPlanets.Sum(planet =>
                planet.GetBuildingTypeCount(buildingType)
            );
            int desiredCount = GetDesiredFacilityCount(context, buildingType);
            return System.Math.Max(0, currentCount - desiredCount);
        }

        /// <summary>
        /// Returns the desired count for a facility type.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="buildingType">Facility type to inspect.</param>
        /// <returns>The desired facility count.</returns>
        private int GetDesiredFacilityCount(AITurnContext context, BuildingType buildingType)
        {
            GameConfig.AIInfrastructureConfig config = context.Game.Config.AI.Infrastructure;
            return buildingType switch
            {
                BuildingType.Shipyard => CeilingDivide(
                    context.Assessment.OwnedPlanets.Count,
                    config.PlanetsPerShipyard
                ),
                BuildingType.TrainingFacility => CeilingDivide(
                    context.Assessment.OwnedPlanets.Count,
                    config.PlanetsPerTrainingFacility
                ),
                BuildingType.ConstructionFacility => System.Math.Max(
                    CeilingDivide(
                        context.Assessment.OwnedPlanets.Count,
                        config.PlanetsPerConstructionFacility
                    ),
                    System.Math.Min(
                        context.Assessment.OwnedPlanets.Count,
                        config.MinimumConstructionFacilityLanes
                    )
                ),
                _ => 0,
            };
        }

        /// <summary>
        /// Divides two integers and rounds up.
        /// </summary>
        /// <param name="value">Value to divide.</param>
        /// <param name="divisor">Divisor to use.</param>
        /// <returns>The rounded-up quotient.</returns>
        private int CeilingDivide(int value, int divisor)
        {
            if (divisor <= 0)
                return value;

            return (value + divisor - 1) / divisor;
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
