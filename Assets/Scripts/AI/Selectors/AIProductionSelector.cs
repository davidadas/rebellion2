using System;
using System.Collections.Generic;
using Rebellion.AI.Demands;
using Rebellion.AI.Proposals;
using Rebellion.AI.Scorers;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.AI.Selectors
{
    /// <summary>
    /// Resolves one globally ranked production decision to an exact executable action.
    /// </summary>
    internal sealed class AIProductionSelector
    {
        private const string _mixedProductType = "*";
        private static readonly AIProductionProposalScorer _scorer = new();
        private readonly AISelectionState _selectionState;
        private readonly Dictionary<string, int> _reservedProducerCapacity = new(
            StringComparer.Ordinal
        );
        private readonly Dictionary<string, int> _availableProducerCapacity = new(
            StringComparer.Ordinal
        );
        private readonly Dictionary<string, string> _selectedProducerProducts = new(
            StringComparer.Ordinal
        );
        private readonly Dictionary<string, string> _currentProducerProducts = new(
            StringComparer.Ordinal
        );
        private readonly Dictionary<string, int> _reservedDestinationEnergy = new(
            StringComparer.Ordinal
        );
        private readonly HashSet<string> _drainingProducerStreams = new(StringComparer.Ordinal);
        private int _selectedMaintenanceCost;

        /// <summary>
        /// Creates a production selector backed by the current turn's allocation ledger.
        /// </summary>
        /// <param name="selectionState">Shared typed reservation state.</param>
        internal AIProductionSelector(AISelectionState selectionState)
        {
            _selectionState = selectionState;
        }

        /// <summary>
        /// Attempts to select a facility-removal proposal.
        /// </summary>
        /// <param name="context">Current AI turn context.</param>
        /// <param name="proposal">Facility-removal proposal.</param>
        /// <returns>True when the proposal is valid and nonconflicting.</returns>
        internal bool TrySelect(AITurnContext context, AIFacilityRemovalProposal proposal)
        {
            List<AISelectionReservation> reservations = GetReservations(proposal);
            if (!_selectionState.CanReserve(reservations) || proposal?.CanSelect(context) != true)
                return false;

            _selectionState.Reserve(reservations);
            return true;
        }

        /// <summary>
        /// Resolves the first viable producer and quantity without changing global ordering.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The globally ranked production decision.</param>
        /// <param name="selectedProposal">The exact executable manufacturing action.</param>
        /// <returns>True when the production decision has a selectable exact action.</returns>
        internal bool TryResolve(
            AITurnContext context,
            AIManufactureProposal proposal,
            out AIManufactureProposal selectedProposal
        )
        {
            int equivalentProducerCount = proposal.ManufacturingCount;
            if (TryResolveQuantity(context, proposal, out selectedProposal))
                return true;

            if (proposal.CarriesReducedCountAcrossAlternatives)
                equivalentProducerCount = selectedProposal.ManufacturingCount;

            foreach (AIManufactureProposal alternative in proposal.ProducerAlternatives)
            {
                AIManufactureProposal candidate = alternative;
                if (
                    proposal.CarriesReducedCountAcrossAlternatives
                    && equivalentProducerCount < candidate.ManufacturingCount
                )
                    candidate = alternative.WithManufacturingCount(equivalentProducerCount);
                if (TryResolveQuantity(context, candidate, out selectedProposal))
                    return true;

                if (proposal.CarriesReducedCountAcrossAlternatives)
                    equivalentProducerCount = selectedProposal.ManufacturingCount;
            }

            selectedProposal = proposal;
            return false;
        }

        /// <summary>
        /// Scores a resolved fallback after shared resources have been reserved.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The globally ranked production decision.</param>
        /// <param name="selectedProposal">The selected exact action.</param>
        internal void ScoreResolved(
            AITurnContext context,
            AIProposal proposal,
            AIProposal selectedProposal
        )
        {
            if (
                selectedProposal is AIManufactureProposal selectedManufactureProposal
                && !ReferenceEquals(selectedProposal, proposal)
            )
                selectedManufactureProposal.SetScore(
                    _scorer.Score(context, selectedManufactureProposal)
                );
        }

        /// <summary>
        /// Reserves production resources consumed by an accepted manufacturing action.
        /// </summary>
        /// <param name="proposal">The accepted manufacturing action.</param>
        internal void Reserve(AIManufactureProposal proposal)
        {
            _selectionState.Reserve(GetReservations(proposal));
            ReserveProducerCapacity(proposal);
            ReserveDestinationEnergy(proposal);
            _selectedMaintenanceCost += proposal.GetMaintenanceCost();
        }

        /// <summary>
        /// Returns whether an exact manufacturing action fits its maintenance reserve.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The exact manufacturing action.</param>
        /// <returns>True when the action can reserve its maintenance cost.</returns>
        internal bool CanReserve(AITurnContext context, AIManufactureProposal proposal)
        {
            return _selectionState.CanReserve(GetReservations(proposal))
                && !WouldExceedMaintenanceHeadroom(context, proposal);
        }

        /// <summary>
        /// Resolves the largest currently available quantity for one exact producer.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The exact-producer manufacturing option.</param>
        /// <param name="selectedProposal">The exact available quantity.</param>
        /// <returns>True when at least one item is selectable.</returns>
        private bool TryResolveQuantity(
            AITurnContext context,
            AIManufactureProposal proposal,
            out AIManufactureProposal selectedProposal
        )
        {
            int requestedCount = proposal.GetManufacturingCount();
            if (requestedCount <= 1)
            {
                selectedProposal = proposal;
                return CanSelect(context, proposal);
            }

            int maximumCount = GetAvailableCount(context, proposal, requestedCount);
            if (maximumCount <= 0)
            {
                selectedProposal = proposal;
                return false;
            }

            selectedProposal = proposal.WithManufacturingCount(maximumCount);
            return CanSelect(context, selectedProposal);
        }

        /// <summary>
        /// Returns whether a manufacturing action is currently feasible.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The manufacturing action to inspect.</param>
        /// <returns>True when its claims and production resources are available.</returns>
        private bool CanSelect(AITurnContext context, AIManufactureProposal proposal)
        {
            return proposal?.CanSelect(context) == true
                && _selectionState.CanReserve(GetReservations(proposal))
                && HasProducerCapacity(proposal)
                && GetAvailableDestinationEnergy(proposal) >= proposal.GetManufacturingCount()
                && ContinuesProductionStream(proposal);
        }

        /// <summary>
        /// Returns the quantity allowed by production capacity, energy, and maintenance headroom.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The manufacturing action to inspect.</param>
        /// <param name="requestedCount">The requested manufacturing count.</param>
        /// <returns>The currently feasible count.</returns>
        private int GetAvailableCount(
            AITurnContext context,
            AIManufactureProposal proposal,
            int requestedCount
        )
        {
            int availableCount = requestedCount;
            if (proposal.UsesSharedProducerCapacity)
            {
                string capacityKey = proposal.GetProducerCapacityKey();
                int reservedCapacity = _reservedProducerCapacity.TryGetValue(
                    capacityKey,
                    out int reserved
                )
                    ? reserved
                    : 0;
                if (!_availableProducerCapacity.TryGetValue(capacityKey, out int capacity))
                {
                    capacity = proposal.ProducerPlanet.GetAvailableManufacturingCapacity(
                        proposal.Demand.ManufacturingType
                    );
                    _availableProducerCapacity[capacityKey] = capacity;
                }

                availableCount = Math.Min(availableCount, Math.Max(0, capacity - reservedCapacity));
            }

            availableCount = Math.Min(availableCount, GetAvailableDestinationEnergy(proposal));
            int unitMaintenanceCost = proposal.GetUnitMaintenanceCost();
            if (unitMaintenanceCost <= 0)
                return availableCount;

            int availableMaintenance =
                context.Assessment.ProjectedMaintenanceHeadroom
                - _selectedMaintenanceCost
                - proposal.GetMinimumMaintenanceHeadroom(context);
            return Math.Min(
                availableCount,
                Math.Max(0, availableMaintenance / unitMaintenanceCost)
            );
        }

        /// <summary>
        /// Returns whether the selected producer has capacity remaining this turn.
        /// </summary>
        /// <param name="proposal">The manufacturing action to inspect.</param>
        /// <returns>True when the producer has unreserved capacity.</returns>
        private bool HasProducerCapacity(AIManufactureProposal proposal)
        {
            string capacityKey = proposal.GetProducerCapacityKey();
            if (!proposal.UsesSharedProducerCapacity)
                return !_reservedProducerCapacity.ContainsKey(capacityKey);

            int reservedCapacity = _reservedProducerCapacity.TryGetValue(
                capacityKey,
                out int reserved
            )
                ? reserved
                : 0;
            if (!_availableProducerCapacity.TryGetValue(capacityKey, out int capacity))
            {
                capacity = proposal.ProducerPlanet.GetAvailableManufacturingCapacity(
                    proposal.Demand.ManufacturingType
                );
                _availableProducerCapacity[capacityKey] = capacity;
            }

            return reservedCapacity < capacity;
        }

        /// <summary>
        /// Returns whether a proposal preserves the producer's current product stream.
        /// </summary>
        /// <param name="proposal">The manufacturing action to inspect.</param>
        /// <returns>True when the stream is idle or already produces the proposed item.</returns>
        private bool ContinuesProductionStream(AIManufactureProposal proposal)
        {
            string capacityKey = proposal.GetProducerCapacityKey();
            string proposedTypeId = proposal.Product?.GetReference()?.GetTypeID();
            if (string.IsNullOrEmpty(proposedTypeId))
                return false;

            if (_selectedProducerProducts.TryGetValue(capacityKey, out string selectedTypeId))
                return selectedTypeId == proposedTypeId;
            if (_drainingProducerStreams.Contains(capacityKey))
                return false;

            string currentTypeId = GetCurrentProductTypeId(proposal, capacityKey);
            if (string.IsNullOrEmpty(currentTypeId) || currentTypeId == proposedTypeId)
                return true;

            _drainingProducerStreams.Add(capacityKey);
            return false;
        }

        /// <summary>
        /// Returns the product type currently queued by a producer stream.
        /// </summary>
        /// <param name="proposal">A proposal targeting the producer stream.</param>
        /// <param name="capacityKey">The producer stream key.</param>
        /// <returns>The queued type, an empty value, or the mixed-type marker.</returns>
        private string GetCurrentProductTypeId(AIManufactureProposal proposal, string capacityKey)
        {
            if (_currentProducerProducts.TryGetValue(capacityKey, out string currentTypeId))
                return currentTypeId;

            currentTypeId = string.Empty;
            if (
                proposal
                    .ProducerPlanet.GetManufacturingQueue()
                    .TryGetValue(proposal.Demand.ManufacturingType, out List<IManufacturable> queue)
                && queue != null
            )
            {
                foreach (IManufacturable item in queue)
                {
                    string itemTypeId = item?.GetTypeID();
                    if (string.IsNullOrEmpty(currentTypeId))
                    {
                        currentTypeId = itemTypeId ?? string.Empty;
                        continue;
                    }

                    if (currentTypeId != itemTypeId)
                    {
                        currentTypeId = _mixedProductType;
                        break;
                    }
                }
            }

            _currentProducerProducts.Add(capacityKey, currentTypeId);
            return currentTypeId;
        }

        /// <summary>
        /// Reserves the producer capacity consumed by an accepted action.
        /// </summary>
        /// <param name="proposal">The accepted manufacturing action.</param>
        private void ReserveProducerCapacity(AIManufactureProposal proposal)
        {
            string capacityKey = proposal.GetProducerCapacityKey();
            _selectedProducerProducts[capacityKey] = proposal.Product.GetReference().GetTypeID();
            if (!proposal.UsesSharedProducerCapacity)
            {
                _reservedProducerCapacity[capacityKey] = int.MaxValue;
                return;
            }

            int reservedCapacity = _reservedProducerCapacity.TryGetValue(
                capacityKey,
                out int reserved
            )
                ? reserved
                : 0;
            long updatedCapacity =
                (long)reservedCapacity + Math.Max(1, proposal.GetManufacturingCount());
            _reservedProducerCapacity[capacityKey] =
                updatedCapacity > int.MaxValue ? int.MaxValue : (int)updatedCapacity;
        }

        /// <summary>
        /// Returns destination energy remaining after accepted building actions.
        /// </summary>
        /// <param name="proposal">The manufacturing action to inspect.</param>
        /// <returns>Remaining energy, or an unbounded value for non-building work.</returns>
        private int GetAvailableDestinationEnergy(AIManufactureProposal proposal)
        {
            if (
                proposal?.Product?.GetReference() is not Building
                || proposal.Demand?.Kind == AIProductionDemandKind.BuildingUpgrade
                || proposal.Destination is not Planet destination
            )
                return int.MaxValue;

            int reserved = _reservedDestinationEnergy.TryGetValue(
                destination.InstanceID,
                out int reservedEnergy
            )
                ? reservedEnergy
                : 0;
            return Math.Max(0, destination.GetAvailableEnergy() - reserved);
        }

        /// <summary>
        /// Reserves destination energy consumed by an accepted building action.
        /// </summary>
        /// <param name="proposal">The accepted manufacturing action.</param>
        private void ReserveDestinationEnergy(AIManufactureProposal proposal)
        {
            if (
                proposal.Product?.GetReference() is not Building
                || proposal.Demand?.Kind == AIProductionDemandKind.BuildingUpgrade
                || proposal.Destination is not Planet destination
            )
                return;

            int reserved = _reservedDestinationEnergy.TryGetValue(
                destination.InstanceID,
                out int reservedEnergy
            )
                ? reservedEnergy
                : 0;
            _reservedDestinationEnergy[destination.InstanceID] =
                reserved + proposal.GetManufacturingCount();
        }

        /// <summary>
        /// Returns whether an action would exceed its maintenance reserve.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The manufacturing action to inspect.</param>
        /// <returns>True when the action exceeds its required reserve.</returns>
        private bool WouldExceedMaintenanceHeadroom(
            AITurnContext context,
            AIManufactureProposal proposal
        )
        {
            if (proposal.Demand?.RestoresMaintenanceCapacity == true)
                return false;

            int maintenanceCost = proposal.GetMaintenanceCost();
            if (maintenanceCost <= 0)
                return false;

            int projectedHeadroom =
                context.Assessment.ProjectedMaintenanceHeadroom
                - _selectedMaintenanceCost
                - maintenanceCost;
            return projectedHeadroom < proposal.GetMinimumMaintenanceHeadroom(context);
        }

        /// <summary>
        /// Returns typed reservations for a production proposal.
        /// </summary>
        /// <param name="proposal">Production proposal to inspect.</param>
        /// <returns>Reservations required by the proposal.</returns>
        private static List<AISelectionReservation> GetReservations(AIProposal proposal)
        {
            List<AISelectionReservation> reservations = new();
            if (proposal is AIFacilityRemovalProposal removal)
            {
                Add(
                    reservations,
                    AISelectionReservationKind.FacilityAllocation,
                    removal.Planet?.InstanceID,
                    removal.BuildingType.ToString()
                );
                return reservations;
            }

            if (proposal is not AIManufactureProposal manufacture)
                return reservations;

            if (manufacture.Demand != null && !manufacture.DistributesDemand)
                Add(
                    reservations,
                    AISelectionReservationKind.ProductionDemand,
                    manufacture.Demand.Id
                );
            if (manufacture.ProducerPlanet != null && !manufacture.UsesSharedProducerCapacity)
                Add(
                    reservations,
                    AISelectionReservationKind.ManufacturingLane,
                    manufacture.GetProducerCapacityKey()
                );
            if (
                manufacture.Product?.GetReference() is Building building
                && manufacture.Destination is Planet destinationPlanet
                && building.GetBuildingType()
                    is BuildingType.Shipyard
                        or BuildingType.ConstructionFacility
            )
                Add(
                    reservations,
                    AISelectionReservationKind.FacilityAllocation,
                    destinationPlanet.InstanceID,
                    building.GetBuildingType().ToString()
                );
            Add(
                reservations,
                AISelectionReservationKind.ProductionBuildingReplacement,
                manufacture.Demand?.BuildingToReplace?.InstanceID
            );
            if (manufacture.Destination is Fleet destinationFleet && !manufacture.DistributesDemand)
            {
                Add(
                    reservations,
                    AISelectionReservationKind.FleetReinforcement,
                    destinationFleet.InstanceID,
                    manufacture.Demand?.Kind.ToString()
                );
                if (manufacture.Demand?.Kind == AIProductionDemandKind.FleetCapitalShip)
                    Add(
                        reservations,
                        AISelectionReservationKind.FleetCapitalReinforcement,
                        destinationFleet.InstanceID
                    );
            }
            if (
                manufacture.Demand?.Kind
                is AIProductionDemandKind.FleetSeedCapitalShip
                    or AIProductionDemandKind.ColonizationFleetSeedCapitalShip
            )
                Add(
                    reservations,
                    AISelectionReservationKind.FleetCreation,
                    manufacture.Destination?.GetOwnerInstanceID()
                );
            return reservations;
        }

        /// <summary>
        /// Adds a reservation only when its primary identity exists.
        /// </summary>
        /// <param name="reservations">Reservation list to update.</param>
        /// <param name="kind">Reservation category.</param>
        /// <param name="primaryId">Primary resource identity.</param>
        /// <param name="secondaryId">Optional secondary identity.</param>
        private static void Add(
            List<AISelectionReservation> reservations,
            AISelectionReservationKind kind,
            string primaryId,
            string secondaryId = null
        )
        {
            if (!string.IsNullOrEmpty(primaryId))
                reservations.Add(new AISelectionReservation(kind, primaryId, secondaryId));
        }
    }
}
