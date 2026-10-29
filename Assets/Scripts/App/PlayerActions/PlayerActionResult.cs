using Rebellion.Game.Messages;
using Rebellion.Game.Results;

namespace Rebellion.App.PlayerActions
{
    /// <summary>
    /// Reports completed execution separately from request delivery or queuing.
    /// </summary>
    public sealed class PlayerActionResult
    {
        public bool Accepted { get; }
        public bool OutcomeKnown { get; }
        public string RejectionReason { get; }
        public string CreatedEntityID { get; }
        public CombatReport CombatReport { get; }
        public SpaceCombatResult SpaceCombatResult { get; }

        /// <summary>
        /// Captures a completed request's outcome and optional domain presentation result.
        /// </summary>
        /// <param name="accepted">Whether execution accepted the request.</param>
        /// <param name="rejectionReason">The reason execution rejected the request.</param>
        /// <param name="createdEntityID">The identity of an entity created by the request.</param>
        /// <param name="combatReport">The actor-visible planetary combat report.</param>
        /// <param name="spaceCombatResult">The completed pending space battle.</param>
        /// <param name="outcomeKnown">Whether the execution outcome was confirmed before the connection ended.</param>
        public PlayerActionResult(
            bool accepted,
            string rejectionReason = null,
            string createdEntityID = null,
            CombatReport combatReport = null,
            SpaceCombatResult spaceCombatResult = null,
            bool outcomeKnown = true
        )
        {
            Accepted = outcomeKnown && accepted;
            OutcomeKnown = outcomeKnown;
            RejectionReason = rejectionReason;
            CreatedEntityID = createdEntityID;
            CombatReport = combatReport;
            SpaceCombatResult = spaceCombatResult;
        }
    }
}
