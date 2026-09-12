using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.AI.Scoring;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.AI.Planners
{
    /// <summary>
    /// Selects the highest-value attack candidate for an idle fleet.
    /// </summary>
    internal sealed class AIFleetAttackCandidateSelector
    {
        // Candidate Scoring.
        private readonly AIFleetProposalScorer _scorer = new AIFleetProposalScorer();

        /// <summary>
        /// Returns the strongest attack proposal from the eligible fleets and targets.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleets">The fleets available for a new attack order.</param>
        /// <param name="targets">Eligible attack targets.</param>
        /// <returns>The highest-scoring proposal, or null when no target has value.</returns>
        internal AIFleetAttackProposal Select(
            AITurnContext context,
            IEnumerable<Fleet> fleets,
            IEnumerable<Planet> targets
        )
        {
            AIFleetAttackProposal selected = null;
            IReadOnlyList<Fleet> availableFleets = fleets.ToList();
            IEnumerable<(Planet Target, double UpperBound)> orderedTargets = targets
                .Select(target =>
                    (
                        Target: target,
                        UpperBound: _scorer.GetNewAttackScoreUpperBound(context, target)
                    )
                )
                .OrderByDescending(candidate => candidate.UpperBound)
                .ThenBy(candidate => candidate.Target.InstanceID, StringComparer.Ordinal);
            foreach ((Planet Target, double UpperBound) candidate in orderedTargets)
            {
                if (selected != null && candidate.UpperBound < selected.Score)
                    break;

                foreach (Fleet fleet in availableFleets)
                {
                    Planet currentPlanet = context.Assessment.GetFleetPlanet(fleet);
                    AIFleetAttackProposal proposal = new AIFleetAttackProposal(
                        fleet,
                        FleetOrderType.Attack,
                        currentPlanet == candidate.Target
                            ? FleetOrderStatus.Ready
                            : FleetOrderStatus.Staging,
                        candidate.Target
                    );
                    proposal.SetScore(_scorer.Score(context, proposal));
                    if (proposal.Score <= 0 || !IsPreferred(proposal, selected))
                        continue;

                    selected = proposal;
                }
            }

            return selected;
        }

        /// <summary>
        /// Returns whether a candidate outranks the currently selected proposal.
        /// </summary>
        /// <param name="candidate">The candidate proposal.</param>
        /// <param name="selected">The currently selected proposal.</param>
        /// <returns>True when the candidate should replace the selection.</returns>
        private static bool IsPreferred(
            AIFleetAttackProposal candidate,
            AIFleetAttackProposal selected
        )
        {
            if (selected == null || candidate.Score != selected.Score)
                return selected == null || candidate.Score > selected.Score;

            return string.Compare(
                    candidate.GetSortKey(),
                    selected.GetSortKey(),
                    StringComparison.Ordinal
                ) < 0;
        }
    }
}
