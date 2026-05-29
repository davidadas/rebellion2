using System.Collections.Generic;
using Rebellion.AI.Director;

namespace Rebellion.AI.Proposals
{
    /// <summary>
    /// Base type for an AI proposal that can be scored, selected, and executed.
    /// </summary>
    public abstract class AIProposal
    {
        /// <summary>
        /// Score assigned by the scoring phase.
        /// </summary>
        public double Score { get; private set; }

        /// <summary>
        /// Whether the proposal has been scored.
        /// </summary>
        public bool HasScore { get; private set; }

        /// <summary>
        /// Sets the score used by proposal selection.
        /// </summary>
        /// <param name="score">The proposal score.</param>
        public void SetScore(double score)
        {
            Score = score;
            HasScore = true;
        }

        /// <summary>
        /// Returns ownership keys that prevent incompatible proposals from both being selected.
        /// </summary>
        /// <returns>Claim keys for this proposal.</returns>
        public abstract IReadOnlyList<string> GetClaimKeys();

        /// <summary>
        /// Returns a stable key used to sort otherwise equivalent proposals.
        /// </summary>
        /// <returns>A stable sort key.</returns>
        public abstract string GetSortKey();

        /// <summary>
        /// Returns whether this proposal may be selected.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True if this proposal may be selected.</returns>
        public abstract bool CanSelect(AITurnContext context);

        /// <summary>
        /// Returns whether this proposal may execute against the current game state.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True if this proposal may execute.</returns>
        public abstract bool CanExecute(AITurnContext context);

        /// <summary>
        /// Applies this proposal to the game state.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public abstract void Execute(AITurnContext context);
    }
}
