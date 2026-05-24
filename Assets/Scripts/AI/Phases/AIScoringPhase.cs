using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.AI.Scoring;

namespace Rebellion.AI.Phases
{
    /// <summary>
    /// Applies proposal scores before selection.
    /// </summary>
    public sealed class AIScoringPhase : IAITurnPhase
    {
        private readonly List<IAIProposalScorer> _proposalScorers;

        /// <summary>
        /// Creates a scoring phase.
        /// </summary>
        public AIScoringPhase()
            : this(
                new IAIProposalScorer[]
                {
                    new AIMissionProposalScorer(),
                    new AIFleetProposalScorer(),
                    new AIProductionProposalScorer(),
                }
            ) { }

        /// <summary>
        /// Creates a scoring phase with the provided scorers.
        /// </summary>
        /// <param name="proposalScorers">Scorers used to score proposals.</param>
        private AIScoringPhase(IEnumerable<IAIProposalScorer> proposalScorers)
        {
            _proposalScorers =
                proposalScorers?.Where(proposalScorer => proposalScorer != null).ToList()
                ?? new List<IAIProposalScorer>();
        }

        /// <summary>
        /// Scores proposals stored on the turn context.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public void Execute(AITurnContext context)
        {
            if (context?.Proposals == null)
                return;

            foreach (AIProposal proposal in context.Proposals)
                ScoreProposal(context, proposal);
        }

        /// <summary>
        /// Scores one proposal with the first matching scorer.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal to score.</param>
        private void ScoreProposal(AITurnContext context, AIProposal proposal)
        {
            if (proposal == null)
                return;

            foreach (IAIProposalScorer proposalScorer in _proposalScorers)
            {
                if (!proposalScorer.CanScore(proposal))
                    continue;

                proposal.SetScore(proposalScorer.Score(context, proposal));
                return;
            }

            throw new System.InvalidOperationException(
                $"No AI proposal scorer is registered for {proposal.GetType().Name}."
            );
        }
    }
}
