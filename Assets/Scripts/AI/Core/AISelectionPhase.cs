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

            AIProposalAllocator allocation = new AIProposalAllocator();
            AIProductionSelector productionSelector = new AIProductionSelector(allocation);
            float minimumSelectableScore = GetMinimumSelectableScore(context);
            foreach (AIProposal proposal in GetSortedProposals(context))
            {
                if (
                    !proposal.HasScore
                    || proposal.Priority != AIProposalPriority.Mandatory
                        && proposal.Score <= minimumSelectableScore
                )
                    continue;

                AIProposal selectedProposal = proposal;
                if (proposal is AIManufactureProposal manufactureProposal)
                {
                    if (
                        !productionSelector.TryResolve(
                            context,
                            manufactureProposal,
                            out AIManufactureProposal selectedManufactureProposal
                        )
                    )
                        continue;
                    selectedProposal = selectedManufactureProposal;
                }

                if (!allocation.TrySelect(context, selectedProposal))
                    continue;

                if (proposal is AIManufactureProposal)
                    productionSelector.ScoreResolved(context, proposal, selectedProposal);
                selectedProposals.Add(selectedProposal);
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
        /// Returns proposals ordered by strategic value with seed-faithful random tie resolution.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Sorted proposals.</returns>
        private static IEnumerable<AIProposal> GetSortedProposals(AITurnContext context)
        {
            List<AIProposal> proposals = context
                .Proposals.Where(proposal => proposal != null)
                .OrderByDescending(proposal => proposal.Priority)
                .ThenByDescending(proposal => proposal.Score)
                .ToList();
            int groupStart = 0;
            while (groupStart < proposals.Count)
            {
                int groupEnd = groupStart + 1;
                while (
                    groupEnd < proposals.Count
                    && proposals[groupEnd].Priority == proposals[groupStart].Priority
                    && proposals[groupEnd].Score == proposals[groupStart].Score
                )
                {
                    groupEnd++;
                }

                ShuffleRange(proposals, groupStart, groupEnd, context.Random);
                groupStart = groupEnd;
            }

            return proposals;
        }

        /// <summary>
        /// Randomizes one tied proposal range using the persisted game random stream.
        /// </summary>
        /// <param name="proposals">The proposal list to update.</param>
        /// <param name="start">The inclusive tied-range start.</param>
        /// <param name="end">The exclusive tied-range end.</param>
        /// <param name="random">The persisted random provider.</param>
        private static void ShuffleRange(
            IList<AIProposal> proposals,
            int start,
            int end,
            Rebellion.Util.Common.IRandomNumberProvider random
        )
        {
            if (random == null)
                return;

            for (int index = end - 1; index > start; index--)
            {
                int otherIndex = random.NextInt(start, index + 1);
                (proposals[index], proposals[otherIndex]) = (
                    proposals[otherIndex],
                    proposals[index]
                );
            }
        }
    }
}
