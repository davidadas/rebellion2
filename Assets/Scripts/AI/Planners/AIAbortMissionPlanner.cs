using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;

namespace Rebellion.AI.Planners
{
    /// <summary>
    /// Builds abort proposals for active missions that no longer satisfy AI safety policy.
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
                IReadOnlyList<IMissionParticipant> participants = mission.GetMainParticipants();
                if (!participants.OfType<Officer>().Any())
                    continue;

                Planet target = context.Assessment.GetKnownPlanet(mission.LocationInstanceID);
                if (target == null)
                {
                    proposals.Add(new AIAbortMissionProposal(mission));
                    continue;
                }

                MissionOdds odds = context.GetMissionOdds(mission, participants, target);
                if (
                    odds.PersonnelLossProbability
                        > context
                            .Game
                            .Config
                            .AI
                            .MissionPlanning
                            .MaximumOfficerMissionLossProbability
                    || !HasUsableHostileTargetIntelligence(context, mission, target)
                )
                    proposals.Add(new AIAbortMissionProposal(mission));
            }

            return proposals;
        }

        /// <summary>
        /// Returns whether a hostile active mission has current intelligence or an assigned
        /// decoy.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="mission">The active mission to inspect.</param>
        /// <param name="target">The faction-visible mission target.</param>
        /// <returns>True when target intelligence permits the mission to continue.</returns>
        private static bool HasUsableHostileTargetIntelligence(
            AITurnContext context,
            Mission mission,
            Planet target
        )
        {
            if (mission.GetDecoyParticipants().Count > 0)
                return true;

            string targetOwnerId = target.GetOwnerInstanceID();
            return string.IsNullOrEmpty(targetOwnerId)
                || targetOwnerId == context.Faction.InstanceID
                || context.Assessment.GetPlanetIntelAge(target) == 0;
        }
    }
}
