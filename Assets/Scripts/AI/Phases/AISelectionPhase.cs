using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Missions;

namespace Rebellion.AI.Phases
{
    /// <summary>
    /// Selects non-conflicting proposals for execution.
    /// </summary>
    public sealed class AISelectionPhase : IAITurnPhase
    {
        /// <summary>
        /// Selects proposals and stores the result on the turn context.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public void Execute(AITurnContext context)
        {
            context?.SetSelectedProposals(Select(context));
        }

        /// <summary>
        /// Returns selected proposals after score ordering and claim checks.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The selected proposals.</returns>
        public List<AIProposal> Select(AITurnContext context)
        {
            List<AIProposal> selectedProposals = new List<AIProposal>();
            if (context?.Proposals == null)
                return selectedProposals;

            HashSet<string> claimedKeys = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, int> reservedProducerCapacity = new Dictionary<string, int>(
                StringComparer.Ordinal
            );
            int remainingHostileMissionCapacity = GetRemainingHostileMissionCapacity(context);
            int selectedMaintenanceCost = 0;
            float minimumSelectableScore = GetMinimumSelectableScore(context);
            foreach (AIProposal proposal in GetSortedProposals(context.Proposals))
            {
                if (!proposal.HasScore || proposal.Score <= minimumSelectableScore)
                    continue;

                if (proposal?.CanSelect(context) != true)
                    continue;

                if (
                    proposal is AIMissionProposal { IsHostileMission: true }
                    && remainingHostileMissionCapacity <= 0
                )
                    continue;

                IReadOnlyList<string> claimKeys = proposal.GetClaimKeys() ?? Array.Empty<string>();
                if (HasClaimConflict(claimedKeys, claimKeys))
                    continue;

                if (!HasProducerCapacity(proposal, reservedProducerCapacity))
                    continue;

                if (WouldExceedMaintenanceHeadroom(context, proposal, selectedMaintenanceCost))
                    continue;

                ClaimKeys(claimedKeys, claimKeys);
                ReserveProducerCapacity(proposal, reservedProducerCapacity);
                selectedProposals.Add(proposal);
                if (proposal is AIMissionProposal { IsHostileMission: true })
                    remainingHostileMissionCapacity--;
                selectedMaintenanceCost += GetMaintenanceCost(proposal);
            }

            return selectedProposals;
        }

        private static int GetRemainingHostileMissionCapacity(AITurnContext context)
        {
            if (context?.Assessment == null)
                return int.MaxValue;

            int maximumConcurrentMissions =
                context.Game?.Config?.AI?.MissionPlanning?.MaximumConcurrentHostileMissions
                ?? new GameConfig.AIMissionPlanningConfig().MaximumConcurrentHostileMissions;
            int activeMissionCount = context.Assessment.ActiveMissions.Count(mission =>
                mission.ConfigKey == MissionTypeIDs.Sabotage
                || mission.ConfigKey == MissionTypeIDs.Abduction
                || mission.ConfigKey == MissionTypeIDs.Assassination
                || mission.ConfigKey == MissionTypeIDs.InciteUprising
            );
            return System.Math.Max(0, maximumConcurrentMissions - activeMissionCount);
        }

        /// <summary>
        /// Returns the minimum score required for proposal selection.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The minimum selectable score.</returns>
        private static float GetMinimumSelectableScore(AITurnContext context)
        {
            return context.Game?.Config?.AI?.Selection?.MinimumSelectableScore
                ?? new GameConfig.AISelectionConfig().MinimumSelectableScore;
        }

        /// <summary>
        /// Returns proposals in deterministic selection order.
        /// </summary>
        /// <param name="proposals">The proposals to sort.</param>
        /// <returns>Sorted proposals.</returns>
        private IEnumerable<AIProposal> GetSortedProposals(IEnumerable<AIProposal> proposals)
        {
            return proposals
                .Where(proposal => proposal != null)
                .OrderByDescending(proposal => proposal.Score)
                .ThenBy(proposal => proposal.GetType().Name, StringComparer.Ordinal)
                .ThenBy(proposal => proposal.GetSortKey(), StringComparer.Ordinal);
        }

        /// <summary>
        /// Returns whether any proposal claim is already held.
        /// </summary>
        /// <param name="claimedKeys">Claims already selected this turn.</param>
        /// <param name="claimKeys">Claims requested by a proposal.</param>
        /// <returns>True if any requested claim is already held.</returns>
        private bool HasClaimConflict(HashSet<string> claimedKeys, IEnumerable<string> claimKeys)
        {
            return claimKeys.Any(claimedKeys.Contains);
        }

        /// <summary>
        /// Adds proposal claims to the selected claim set.
        /// </summary>
        /// <param name="claimedKeys">Claims already selected this turn.</param>
        /// <param name="claimKeys">Claims requested by a proposal.</param>
        private void ClaimKeys(HashSet<string> claimedKeys, IEnumerable<string> claimKeys)
        {
            foreach (string claimKey in claimKeys)
                claimedKeys.Add(claimKey);
        }

        private static bool HasProducerCapacity(
            AIProposal proposal,
            IReadOnlyDictionary<string, int> reservedProducerCapacity
        )
        {
            if (proposal is not AIManufactureProposal manufactureProposal)
                return true;

            string capacityKey = manufactureProposal.GetProducerCapacityKey();
            if (!manufactureProposal.UsesSharedProducerCapacity)
                return !reservedProducerCapacity.ContainsKey(capacityKey);

            int reservedCapacity = reservedProducerCapacity.TryGetValue(
                capacityKey,
                out int reserved
            )
                ? reserved
                : 0;
            int availableCapacity =
                manufactureProposal.ProducerPlanet.GetAvailableManufacturingCapacity(
                    manufactureProposal.Demand.ManufacturingType
                );
            return reservedCapacity < availableCapacity;
        }

        private static void ReserveProducerCapacity(
            AIProposal proposal,
            IDictionary<string, int> reservedProducerCapacity
        )
        {
            if (proposal is not AIManufactureProposal manufactureProposal)
                return;

            string capacityKey = manufactureProposal.GetProducerCapacityKey();
            if (!manufactureProposal.UsesSharedProducerCapacity)
            {
                reservedProducerCapacity[capacityKey] = int.MaxValue;
                return;
            }

            int reservedCapacity = reservedProducerCapacity.TryGetValue(
                capacityKey,
                out int reserved
            )
                ? reserved
                : 0;
            long updatedCapacity =
                (long)reservedCapacity
                + System.Math.Max(1, manufactureProposal.GetManufacturingCount());
            reservedProducerCapacity[capacityKey] =
                updatedCapacity > int.MaxValue ? int.MaxValue : (int)updatedCapacity;
        }

        /// <summary>
        /// Returns whether selecting a proposal would exceed maintenance reserve limits.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal to inspect.</param>
        /// <param name="selectedMaintenanceCost">Maintenance already selected this turn.</param>
        /// <returns>True if the proposal would exceed the reserve limit.</returns>
        private bool WouldExceedMaintenanceHeadroom(
            AITurnContext context,
            AIProposal proposal,
            int selectedMaintenanceCost
        )
        {
            int maintenanceCost = GetMaintenanceCost(proposal);
            if (maintenanceCost <= 0)
                return false;

            int minimumHeadroom = proposal is AIManufactureProposal manufactureProposal
                ? manufactureProposal.GetMinimumMaintenanceHeadroom(context)
                : context.Game.Config.AI.Selection.MaintenanceHeadroomHardFloor;
            int projectedHeadroom =
                context.Faction.ProjectedMaintenanceHeadroom
                - selectedMaintenanceCost
                - maintenanceCost;

            return projectedHeadroom < minimumHeadroom;
        }

        /// <summary>
        /// Returns the maintenance cost added by a proposal.
        /// </summary>
        /// <param name="proposal">The proposal to inspect.</param>
        /// <returns>The proposal maintenance cost.</returns>
        private int GetMaintenanceCost(AIProposal proposal)
        {
            return proposal is AIManufactureProposal manufactureProposal
                ? manufactureProposal.GetMaintenanceCost()
                : 0;
        }
    }
}
