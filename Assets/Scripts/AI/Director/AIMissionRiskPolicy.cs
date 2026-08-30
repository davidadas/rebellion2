using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;

namespace Rebellion.AI.Director
{
    /// <summary>
    /// Determines whether committing officers to a mission is acceptably safe from the faction's
    /// current point of view.
    /// </summary>
    internal static class AIMissionRiskPolicy
    {
        /// <summary>
        /// Returns whether the mission's projected officer-loss risk is within configured limits.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="mission">The mission being evaluated.</param>
        /// <param name="observedPlanet">The faction-visible mission target.</param>
        /// <returns>True when the mission may safely commit or retain its officers.</returns>
        internal static bool AllowsMission(
            AITurnContext context,
            Mission mission,
            Planet observedPlanet
        )
        {
            if (context?.Faction == null || context.Missions == null || mission == null)
                return false;

            if (!mission.GetMainParticipants().OfType<Officer>().Any())
                return true;

            if (observedPlanet == null)
                return false;

            MissionOdds odds = context.GetMissionOdds(
                mission,
                mission.GetMainParticipants(),
                observedPlanet
            );
            return AllowsMission(context, mission, observedPlanet, odds);
        }

        /// <summary>
        /// Returns whether precomputed mission odds keep officer-loss risk within configured
        /// limits.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="mission">The mission being evaluated.</param>
        /// <param name="observedPlanet">The faction-visible mission target.</param>
        /// <param name="odds">The mission odds already calculated for this proposal.</param>
        /// <returns>True when the mission may safely commit its officers.</returns>
        internal static bool AllowsMission(
            AITurnContext context,
            Mission mission,
            Planet observedPlanet,
            MissionOdds odds
        )
        {
            if (
                context?.Faction == null
                || mission == null
                || observedPlanet == null
                || odds == null
            )
                return false;

            if (!mission.GetMainParticipants().OfType<Officer>().Any())
                return true;

            if (
                odds.PersonnelLossProbability
                > context.Game.Config.AI.MissionPlanning.MaximumOfficerMissionLossProbability
            )
                return false;

            if (mission.GetDecoyParticipants().Count > 0)
                return true;

            string targetOwnerId = observedPlanet.GetOwnerInstanceID();
            return string.IsNullOrEmpty(targetOwnerId)
                || targetOwnerId == context.Faction.InstanceID
                || context.Assessment.GetPlanetIntelAge(observedPlanet) == 0;
        }
    }
}
