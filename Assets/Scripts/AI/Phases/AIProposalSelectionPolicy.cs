using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Planners;
using Rebellion.AI.Proposals;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.AI.Phases
{
    /// <summary>
    /// Tracks claims, production capacity, and economic reservations while proposals are selected.
    /// </summary>
    internal sealed class AIProposalSelectionPolicy
    {
        private const string _mixedProductType = "*";

        // Selection State.
        private readonly HashSet<string> _claimedKeys = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _reservedProducerCapacity = new Dictionary<
            string,
            int
        >(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _availableProducerCapacity = new Dictionary<
            string,
            int
        >(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _selectedProducerProducts = new Dictionary<
            string,
            string
        >(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _currentProducerProducts = new Dictionary<
            string,
            string
        >(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _reservedDestinationEnergy = new Dictionary<
            string,
            int
        >(StringComparer.Ordinal);
        private readonly HashSet<string> _drainingProducerStreams = new HashSet<string>(
            StringComparer.Ordinal
        );
        private int _selectedMaintenanceCost;

        /// <summary>
        /// Selects a valid proposal option and reserves the resources it consumes.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal being considered.</param>
        /// <param name="selectedProposal">The exact proposal selected for execution.</param>
        /// <returns>True when the proposal is valid and its resources were reserved.</returns>
        internal bool TrySelect(
            AITurnContext context,
            AIProposal proposal,
            out AIProposal selectedProposal
        )
        {
            selectedProposal = proposal;
            if (
                proposal is AIManufactureProposal manufactureProposal
                && !TryResolveManufactureProposal(
                    context,
                    manufactureProposal,
                    out selectedProposal
                )
            )
                return false;

            return TrySelectCore(context, selectedProposal);
        }

        /// <summary>
        /// Selects a proposal option and reserves resources without restoring on failure.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal being considered.</param>
        /// <returns>True when the proposal is valid and its resources were reserved.</returns>
        private bool TrySelectCore(AITurnContext context, AIProposal proposal)
        {
            if (!CanSelect(context, proposal))
                return false;

            if (WouldExceedMaintenanceHeadroom(context, proposal))
                return false;

            IReadOnlyList<string> claimKeys = proposal.GetClaimKeys() ?? Array.Empty<string>();
            foreach (string claimKey in claimKeys)
                _claimedKeys.Add(claimKey);

            ReserveProducerCapacity(proposal);
            ReserveDestinationEnergy(proposal);
            int maintenanceCost = GetMaintenanceCost(proposal);
            _selectedMaintenanceCost += maintenanceCost;
            return true;
        }

        /// <summary>
        /// Selects the first viable producer option and validates its proposal claims.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal to inspect.</param>
        /// <param name="selectedProposal">The exact producer option selected for validation.</param>
        /// <returns>True when the proposal has a selectable option.</returns>
        private bool TryResolveManufactureProposal(
            AITurnContext context,
            AIManufactureProposal proposal,
            out AIProposal selectedProposal
        )
        {
            int equivalentProducerCount = proposal.ManufacturingCount;
            foreach (Planet producerPlanet in proposal.ProducerPlanets)
            {
                AIManufactureProposal candidate = proposal.ResolveOption(
                    proposal.Demand,
                    producerPlanet
                );
                if (equivalentProducerCount < candidate.ManufacturingCount)
                    candidate = candidate.WithManufacturingCount(equivalentProducerCount);
                if (TrySelectManufacturePrefix(context, candidate, out selectedProposal))
                    return true;

                equivalentProducerCount = (
                    (AIManufactureProposal)selectedProposal
                ).ManufacturingCount;
            }

            foreach (AIManufactureOption option in proposal.ProducerOptions)
            {
                AIManufactureProposal candidate = proposal.ResolveOption(
                    option.Demand,
                    option.ProducerPlanet
                );
                if (TrySelectManufacturePrefix(context, candidate, out selectedProposal))
                    return true;
            }

            selectedProposal = proposal;
            return false;
        }

        /// <summary>
        /// Selects the largest affordable prefix of a counted manufacturing proposal.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The manufacturing proposal to adjust.</param>
        /// <param name="selectedProposal">The exact affordable proposal prefix.</param>
        /// <returns>True when at least one item can be selected.</returns>
        private bool TrySelectManufacturePrefix(
            AITurnContext context,
            AIManufactureProposal proposal,
            out AIProposal selectedProposal
        )
        {
            int requestedCount = proposal.GetManufacturingCount();
            if (requestedCount <= 1)
            {
                selectedProposal = proposal;
                return CanSelectManufactureProposal(context, proposal);
            }

            int maximumCount = GetAvailableManufacturingCount(context, proposal, requestedCount);
            if (maximumCount <= 0)
            {
                selectedProposal = proposal;
                return false;
            }

            AIManufactureProposal accepted = proposal.WithManufacturingCount(maximumCount);
            selectedProposal = accepted;
            return CanSelectManufactureProposal(context, accepted);
        }

        /// <summary>
        /// Returns the count allowed by remaining producer capacity and maintenance headroom.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal being considered.</param>
        /// <param name="requestedCount">The proposal's requested count.</param>
        /// <returns>The maximum count worth validating against domain constraints.</returns>
        private int GetAvailableManufacturingCount(
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
                if (!_availableProducerCapacity.TryGetValue(capacityKey, out int availableCapacity))
                {
                    availableCapacity = proposal.ProducerPlanet.GetAvailableManufacturingCapacity(
                        proposal.Demand.ManufacturingType
                    );
                    _availableProducerCapacity[capacityKey] = availableCapacity;
                }

                availableCount = Math.Min(
                    availableCount,
                    Math.Max(0, availableCapacity - reservedCapacity)
                );
            }

            availableCount = Math.Min(availableCount, GetAvailableDestinationEnergy(proposal));

            int unitMaintenanceCost = proposal.GetUnitMaintenanceCost();
            if (unitMaintenanceCost <= 0)
                return availableCount;

            int minimumHeadroom = proposal.GetMinimumMaintenanceHeadroom(context);
            int availableMaintenance =
                context.Assessment.ProjectedMaintenanceHeadroom
                - _selectedMaintenanceCost
                - minimumHeadroom;
            return Math.Min(
                availableCount,
                Math.Max(0, availableMaintenance / unitMaintenanceCost)
            );
        }

        /// <summary>
        /// Returns whether a manufacturing proposal is valid and has producer capacity.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The manufacturing proposal to inspect.</param>
        /// <returns>True when the selected producer option is available.</returns>
        private bool CanSelectManufactureProposal(
            AITurnContext context,
            AIManufactureProposal proposal
        )
        {
            return CanSelect(context, proposal)
                && HasProducerCapacity(proposal)
                && GetAvailableDestinationEnergy(proposal) >= proposal.GetManufacturingCount()
                && ContinuesProductionStream(proposal);
        }

        /// <summary>
        /// Returns whether the proposal preserves the producer's current product type.
        /// </summary>
        /// <param name="proposal">The manufacturing proposal to inspect.</param>
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
            if (string.IsNullOrEmpty(currentTypeId))
                return true;

            if (currentTypeId == proposedTypeId)
                return true;

            _drainingProducerStreams.Add(capacityKey);
            return false;
        }

        /// <summary>
        /// Returns the indexed product type currently queued by a producer stream.
        /// </summary>
        /// <param name="proposal">A proposal targeting the producer stream.</param>
        /// <param name="capacityKey">The stream's stable capacity key.</param>
        /// <returns>The queued type, an empty value for an idle stream, or a mixed-type marker.</returns>
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
        /// Returns whether a proposal is executable without conflicting claims.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal to inspect.</param>
        /// <returns>True when the proposal can be selected.</returns>
        private bool CanSelect(AITurnContext context, AIProposal proposal)
        {
            IReadOnlyList<string> proposalClaims =
                proposal?.GetClaimKeys() ?? Array.Empty<string>();
            return !proposalClaims.Any(_claimedKeys.Contains)
                && proposal?.CanSelect(context) == true;
        }

        /// <summary>
        /// Returns whether the selected producer has capacity remaining this turn.
        /// </summary>
        /// <param name="proposal">The manufacturing proposal to inspect.</param>
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
            if (!_availableProducerCapacity.TryGetValue(capacityKey, out int availableCapacity))
            {
                availableCapacity = proposal.ProducerPlanet.GetAvailableManufacturingCapacity(
                    proposal.Demand.ManufacturingType
                );
                _availableProducerCapacity[capacityKey] = availableCapacity;
            }

            return reservedCapacity < availableCapacity;
        }

        /// <summary>
        /// Reserves the producer capacity consumed by a selected proposal.
        /// </summary>
        /// <param name="proposal">The selected proposal.</param>
        private void ReserveProducerCapacity(AIProposal proposal)
        {
            if (proposal is not AIManufactureProposal manufactureProposal)
                return;

            string capacityKey = manufactureProposal.GetProducerCapacityKey();
            _selectedProducerProducts[capacityKey] = manufactureProposal
                .Product.GetReference()
                .GetTypeID();
            if (!manufactureProposal.UsesSharedProducerCapacity)
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
                (long)reservedCapacity + Math.Max(1, manufactureProposal.GetManufacturingCount());
            _reservedProducerCapacity[capacityKey] =
                updatedCapacity > int.MaxValue ? int.MaxValue : (int)updatedCapacity;
        }

        /// <summary>
        /// Returns destination energy remaining after previously selected building proposals.
        /// </summary>
        /// <param name="proposal">The manufacturing proposal to inspect.</param>
        /// <returns>Available destination energy, or an unbounded value for non-building work.</returns>
        private int GetAvailableDestinationEnergy(AIManufactureProposal proposal)
        {
            if (
                proposal?.Product?.GetReference() is not Building
                || proposal.Demand?.Kind == AIDemandKind.BuildingUpgrade
                || proposal.Destination is not Planet destination
            )
            {
                return int.MaxValue;
            }

            int reserved = _reservedDestinationEnergy.TryGetValue(
                destination.InstanceID,
                out int reservedEnergy
            )
                ? reservedEnergy
                : 0;
            return Math.Max(0, destination.GetAvailableEnergy() - reserved);
        }

        /// <summary>
        /// Reserves destination energy consumed by a selected building proposal.
        /// </summary>
        /// <param name="proposal">The selected proposal.</param>
        private void ReserveDestinationEnergy(AIProposal proposal)
        {
            if (
                proposal is not AIManufactureProposal manufactureProposal
                || manufactureProposal.Product?.GetReference() is not Building
                || manufactureProposal.Demand?.Kind == AIDemandKind.BuildingUpgrade
                || manufactureProposal.Destination is not Planet destination
            )
            {
                return;
            }

            int reserved = _reservedDestinationEnergy.TryGetValue(
                destination.InstanceID,
                out int reservedEnergy
            )
                ? reservedEnergy
                : 0;
            _reservedDestinationEnergy[destination.InstanceID] =
                reserved + manufactureProposal.GetManufacturingCount();
        }

        /// <summary>
        /// Returns whether a proposal would exceed the configured maintenance reserve.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal to inspect.</param>
        /// <returns>True when selecting the proposal would exceed the reserve.</returns>
        private bool WouldExceedMaintenanceHeadroom(AITurnContext context, AIProposal proposal)
        {
            if (
                proposal is AIManufactureProposal recoveryProposal
                && recoveryProposal.Demand?.RestoresMaintenanceCapacity == true
            )
            {
                return false;
            }

            int maintenanceCost = GetMaintenanceCost(proposal);
            if (maintenanceCost <= 0)
                return false;

            int minimumHeadroom = proposal is AIManufactureProposal manufactureProposal
                ? manufactureProposal.GetMinimumMaintenanceHeadroom(context)
                : context.Game.Config.AI.Selection.MaintenanceHeadroomReserve;
            int projectedHeadroom =
                context.Assessment.ProjectedMaintenanceHeadroom
                - _selectedMaintenanceCost
                - maintenanceCost;

            return projectedHeadroom < minimumHeadroom;
        }

        /// <summary>
        /// Returns the maintenance cost reserved by a proposal.
        /// </summary>
        /// <param name="proposal">The proposal to inspect.</param>
        /// <returns>The proposal's maintenance cost.</returns>
        private static int GetMaintenanceCost(AIProposal proposal)
        {
            return proposal is AIManufactureProposal manufactureProposal
                ? manufactureProposal.GetMaintenanceCost()
                : 0;
        }
    }
}
