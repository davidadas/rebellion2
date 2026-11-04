using System.Collections.Generic;
using Rebellion.AI.Proposals;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Util.Logging;

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
            using var performanceMeasurement = PerformanceLogger.Measure(
                nameof(Plan),
                context?.Faction?.PeekInstanceID(),
                subjectType: typeof(AIAbortMissionPlanner)
            );
            List<AIProposal> proposals = new List<AIProposal>();
            if (context?.Assessment == null || context.Missions == null)
                return proposals;

            foreach (Mission mission in context.Assessment.ActiveMissions)
            {
                Planet target = context.Assessment.GetKnownPlanet(mission.LocationInstanceID);
                if (target == null && mission.ConfigKey != ReconnaissanceMission.MissionTypeID)
                    proposals.Add(new AIAbortMissionProposal(mission));
            }

            return proposals;
        }
    }
}
