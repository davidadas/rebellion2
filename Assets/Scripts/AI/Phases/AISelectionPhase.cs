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

            AIProposalSelectionPolicy selectionPolicy = new AIProposalSelectionPolicy();
            float minimumSelectableScore = GetMinimumSelectableScore(context);
            foreach (AIProposal proposal in GetSortedProposals(context.Proposals))
            {
                if (
                    !proposal.HasScore
                    || proposal.Priority == AIProposalPriority.Optional
                        && proposal.Score <= minimumSelectableScore
                )
                    continue;

                if (!selectionPolicy.TrySelect(context, proposal))
                    continue;

                selectedProposals.Add(proposal);
            }

            return selectedProposals;
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
        private static IEnumerable<AIProposal> GetSortedProposals(IEnumerable<AIProposal> proposals)
        {
            return proposals
                .Where(proposal => proposal != null)
                .OrderByDescending(proposal => proposal.Priority)
                .ThenByDescending(proposal => proposal.Score)
                .ThenBy(proposal => proposal.GetType().Name, StringComparer.Ordinal)
                .ThenBy(proposal => proposal.GetSortKey(), StringComparer.Ordinal);
        }
    }
}
