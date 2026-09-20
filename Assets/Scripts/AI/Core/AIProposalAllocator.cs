using System;
using System.Collections.Generic;
using System.Linq;

namespace Rebellion.AI.Core
{
    /// <summary>
    /// Tracks mutually exclusive proposal claims while proposals are selected.
    /// </summary>
    internal sealed class AIProposalAllocator
    {
        private readonly HashSet<string> _claimedKeys = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// Returns whether a proposal is executable without conflicting claims.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal to inspect.</param>
        /// <returns>True when the proposal can be selected.</returns>
        internal bool CanSelect(AITurnContext context, AIProposal proposal)
        {
            IReadOnlyList<string> proposalClaims =
                proposal?.GetClaimKeys() ?? Array.Empty<string>();
            return !proposalClaims.Any(_claimedKeys.Contains)
                && proposal?.CanSelect(context) == true;
        }

        /// <summary>
        /// Selects an exact proposal and reserves its mutually exclusive claims.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The proposal being considered.</param>
        /// <returns>True when the proposal is valid and its claims were reserved.</returns>
        internal bool TrySelect(AITurnContext context, AIProposal proposal)
        {
            if (!CanSelect(context, proposal))
                return false;

            Reserve(proposal);
            return true;
        }

        /// <summary>
        /// Reserves the mutually exclusive claims of an already validated proposal.
        /// </summary>
        /// <param name="proposal">The validated proposal.</param>
        internal void Reserve(AIProposal proposal)
        {
            IReadOnlyList<string> claimKeys = proposal.GetClaimKeys() ?? Array.Empty<string>();
            foreach (string claimKey in claimKeys)
                _claimedKeys.Add(claimKey);
        }
    }
}
