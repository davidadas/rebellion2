using System.Collections.Generic;
using Rebellion.AI.Director;
using Rebellion.Game.Missions;

namespace Rebellion.AI.Proposals
{
    /// <summary>
    /// Proposal to abort an active mission.
    /// </summary>
    public sealed class AIAbortMissionProposal : AIProposal
    {
        internal override AIProposalPriority Priority => AIProposalPriority.Mandatory;

        public Mission Mission { get; }

        /// <summary>
        /// Creates a proposal for the supplied active mission.
        /// </summary>
        /// <param name="mission">The mission to abort.</param>
        public AIAbortMissionProposal(Mission mission)
        {
            Mission = mission;
        }

        /// <summary>
        /// Returns the claim that prevents another action from modifying the mission.
        /// </summary>
        /// <returns>The mission claim, or no claims when the mission is unavailable.</returns>
        public override IReadOnlyList<string> GetClaimKeys()
        {
            return Mission == null
                ? new List<string>()
                : new List<string> { AIClaimKeys.Mission(Mission.InstanceID) };
        }

        /// <summary>
        /// Returns a stable sort key for the mission-abort proposal.
        /// </summary>
        /// <returns>A stable sort key.</returns>
        public override string GetSortKey()
        {
            return $"mission-abort:{Mission?.InstanceID}";
        }

        /// <summary>
        /// Returns whether the mission-abort proposal may be selected.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when the mission remains eligible to be aborted.</returns>
        public override bool CanSelect(AITurnContext context)
        {
            return IsStillValid(context);
        }

        /// <summary>
        /// Returns whether the mission-abort proposal may execute.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when the mission can still be aborted.</returns>
        public override bool CanExecute(AITurnContext context)
        {
            return IsStillValid(context);
        }

        /// <summary>
        /// Aborts the active mission when it remains valid.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public override void Execute(AITurnContext context)
        {
            if (CanExecute(context))
                context.Missions.AbortMission(Mission.InstanceID);
        }

        /// <summary>
        /// Returns whether the mission remains active and owned by the current faction.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when the mission can still be aborted.</returns>
        private bool IsStillValid(AITurnContext context)
        {
            return context?.Game != null
                && context.Missions != null
                && Mission != null
                && Mission.GetOwnerInstanceID() == context.Faction?.InstanceID
                && context.Game.GetSceneNodeByInstanceID<Mission>(Mission.InstanceID) == Mission;
        }
    }
}
