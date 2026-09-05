using System.Collections.Generic;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;

namespace Rebellion.AI.Planners
{
    /// <summary>
    /// Builds abort proposals for active missions whose known target no longer exists.
    /// </summary>
    public sealed class AIAbortMissionPlanner : IAIProposalPlanner
    {
        /// <summary>
        /// Returns proposals for active missions that should be aborted.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Mission-abort proposals.</returns>
        public List<AIProposal> Plan(AITurnContext context)
        {
            List<AIProposal> proposals = new List<AIProposal>();
            if (context?.Assessment == null || context.Missions == null)
                return proposals;

            foreach (Mission mission in context.Assessment.ActiveMissions)
            {
                Planet target = context.Assessment.GetKnownPlanet(mission.LocationInstanceID);
                if (target == null && mission.ConfigKey != MissionTypeIDs.Reconnaissance)
                    proposals.Add(new AIAbortMissionProposal(mission));
            }

            return proposals;
        }
    }
}
