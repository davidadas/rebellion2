using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.Game;

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
            int selectedMaintenanceCost = 0;
            float minimumSelectableScore = GetMinimumSelectableScore(context);
            foreach (AIProposal proposal in GetSortedProposals(context.Proposals))
            {
                if (!proposal.HasScore || proposal.Score <= minimumSelectableScore)
                    continue;

                if (proposal?.CanSelect(context) != true)
                    continue;

                IReadOnlyList<string> claimKeys = proposal.GetClaimKeys() ?? Array.Empty<string>();
                if (HasClaimConflict(claimedKeys, claimKeys))
                    continue;

                if (WouldExceedMaintenanceHeadroom(context, proposal, selectedMaintenanceCost))
                    continue;

                ClaimKeys(claimedKeys, claimKeys);
                selectedProposals.Add(proposal);
                selectedMaintenanceCost += GetMaintenanceCost(proposal);
            }

            return selectedProposals;
        }

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

            int minimumHeadroom = context.Game.Config.AI.Selection.MaintenanceHeadroomHardFloor;
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
