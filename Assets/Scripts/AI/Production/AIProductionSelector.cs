using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.AI.Scoring;

namespace Rebellion.AI.Phases
{
    /// <summary>
    /// Resolves one globally ranked production decision to an exact executable action.
    /// </summary>
    internal sealed class AIProductionSelector
    {
        private static readonly AIProductionProposalScorer _scorer = new();
        private readonly AIProposalAllocator _allocation;

        /// <summary>
        /// Creates a production selector backed by the current turn's allocation ledger.
        /// </summary>
        /// <param name="allocation">Shared feasibility and reservation state.</param>
        internal AIProductionSelector(AIProposalAllocator allocation)
        {
            _allocation = allocation;
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
                return _allocation.CanSelectManufactureProposal(context, proposal);
            }

            int maximumCount = _allocation.GetAvailableManufacturingCount(
                context,
                proposal,
                requestedCount
            );
            if (maximumCount <= 0)
            {
                selectedProposal = proposal;
                return false;
            }

            selectedProposal = proposal.WithManufacturingCount(maximumCount);
            return _allocation.CanSelectManufactureProposal(context, selectedProposal);
        }
    }
}
