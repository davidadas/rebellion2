using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.Game.Galaxy;

namespace Rebellion.AI.Phases
{
    /// <summary>
    /// Tracks claims, production capacity, and economic reservations while proposals are selected.
    /// </summary>
    internal sealed class AIProposalSelectionPolicy
    {
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
        private int _selectedMaintenanceCost;

        /// <summary>
        /// Selects a valid proposal option and reserves the resources it consumes.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal being considered.</param>
        /// <returns>True when the proposal is valid and its resources were reserved.</returns>
        internal bool TrySelect(AITurnContext context, AIProposal proposal)
        {
            AIManufactureProposal manufactureProposal = proposal as AIManufactureProposal;
            AIManufactureOption priorOption =
                manufactureProposal != null
                    ? new AIManufactureOption(
                        manufactureProposal.Demand,
                        manufactureProposal.ProducerPlanet
                    )
                    : default;

            if (TrySelectCore(context, proposal))
                return true;

            manufactureProposal?.SelectOption(priorOption);
            return false;
        }

        /// <summary>
        /// Selects a proposal option and reserves resources without restoring on failure.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal being considered.</param>
        /// <returns>True when the proposal is valid and its resources were reserved.</returns>
        private bool TrySelectCore(AITurnContext context, AIProposal proposal)
        {
            if (!TrySelectOption(context, proposal))
                return false;

            if (IsBlockedByRefinedMaterialReserve(context, proposal))
                return false;

            if (WouldExceedMaintenanceHeadroom(context, proposal))
                return false;

            IReadOnlyList<string> claimKeys = proposal.GetClaimKeys() ?? Array.Empty<string>();
            foreach (string claimKey in claimKeys)
                _claimedKeys.Add(claimKey);

            ReserveProducerCapacity(proposal);
            _selectedMaintenanceCost += GetMaintenanceCost(proposal);
            return true;
        }

        /// <summary>
        /// Selects the first viable producer option and validates its proposal claims.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal to inspect.</param>
        /// <returns>True when the proposal has a selectable option.</returns>
        private bool TrySelectOption(AITurnContext context, AIProposal proposal)
        {
            if (proposal is not AIManufactureProposal manufactureProposal)
                return CanSelect(context, proposal);

            foreach (Planet producerPlanet in manufactureProposal.ProducerPlanets)
            {
                manufactureProposal.SelectProducer(producerPlanet);
                if (CanSelectManufactureProposal(context, manufactureProposal))
                    return true;
            }

            foreach (AIManufactureOption option in manufactureProposal.ProducerOptions)
            {
                manufactureProposal.SelectOption(option);
                if (CanSelectManufactureProposal(context, manufactureProposal))
                    return true;
            }

            return false;
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
            return CanSelect(context, proposal) && HasProducerCapacity(proposal);
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
        /// Returns whether the refined-material reserve blocks discretionary production.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal to inspect.</param>
        /// <returns>True when the configured reserve must be preserved.</returns>
        private static bool IsBlockedByRefinedMaterialReserve(
            AITurnContext context,
            AIProposal proposal
        )
        {
            if (
                proposal is not AIManufactureProposal manufactureProposal
                || manufactureProposal.Demand?.CanUseRefinedMaterialReserve != false
            )
                return false;

            int reservePercent = context.Game.Config.AI.Selection.RefinedMaterialReservePercent;
            long reserve = (long)context.Assessment.RefinedMaterialSupply * reservePercent / 100;
            return context.Assessment.RefinedMaterialStockpile < reserve;
        }

        /// <summary>
        /// Returns whether a proposal would exceed the configured maintenance reserve.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal to inspect.</param>
        /// <returns>True when selecting the proposal would exceed the reserve.</returns>
        private bool WouldExceedMaintenanceHeadroom(AITurnContext context, AIProposal proposal)
        {
            int maintenanceCost = GetMaintenanceCost(proposal);
            if (maintenanceCost <= 0)
                return false;

            int minimumHeadroom = proposal is AIManufactureProposal manufactureProposal
                ? manufactureProposal.GetMinimumMaintenanceHeadroom(context)
                : context.Game.Config.AI.Selection.MaintenanceHeadroomHardFloor;
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
