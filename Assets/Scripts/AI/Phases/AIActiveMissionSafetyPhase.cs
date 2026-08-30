using System.Linq;
using Rebellion.AI.Director;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;

namespace Rebellion.AI.Phases
{
    /// <summary>
    /// Stops active officer missions when new intelligence makes their projected loss risk unsafe.
    /// </summary>
    public sealed class AIActiveMissionSafetyPhase : IAITurnPhase
    {
        /// <summary>
        /// Re-evaluates active missions before the faction plans new work.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public void Execute(AITurnContext context)
        {
            if (context?.Missions == null || context.Assessment == null)
                return;

            foreach (Mission mission in context.Assessment.ActiveMissions.ToList())
            {
                Planet target = context.Assessment.GetKnownPlanet(mission.LocationInstanceID);
                if (AIMissionRiskPolicy.AllowsMission(context, mission, target))
                    continue;

                context.Missions.AbortMission(mission.InstanceID);
            }
        }
    }
}
