using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.AI.Planners
{
    /// <summary>
    /// Plans favorable fleet engagements using faction-visible orbital intelligence.
    /// </summary>
    public sealed class AIOrbitalEngagementPlanner : IAIProposalPlanner
    {
        /// <summary>
        /// Returns proposals for favorable orbital engagements.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Orbital-engagement proposals.</returns>
        public List<AIProposal> Plan(AITurnContext context)
        {
            List<AIProposal> proposals = new List<AIProposal>();
            if (context?.Game == null || context.Faction == null)
                return proposals;

            AddExistingEngagements(context, proposals);
            foreach (Fleet fleet in context.Assessment.OwnedFleets.Where(CanAssign))
            {
                Planet origin = context.Assessment.GetFleetPlanet(fleet);
                foreach (Planet target in GetTargets(context, fleet, origin))
                    proposals.Add(new AIOrbitalEngagementProposal(fleet, target, origin));
            }

            return proposals;
        }

        /// <summary>
        /// Adds proposals that advance active engagement orders.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposals">Proposal collection to update.</param>
        private static void AddExistingEngagements(
            AITurnContext context,
            ICollection<AIProposal> proposals
        )
        {
            foreach (Fleet fleet in context.Assessment.EngagementOrderedFleets)
            {
                Planet target = context.Assessment.GetKnownPlanet(fleet.Order.TargetPlanetId);
                Planet origin = context.Assessment.GetKnownPlanet(fleet.Order.OriginPlanetId);
                if (target != null)
                    proposals.Add(new AIOrbitalEngagementProposal(fleet, target, origin));
                else
                    proposals.Add(new AIClearFleetOrderProposal(fleet, fleet.Order));
            }
        }

        /// <summary>
        /// Returns whether an idle fleet can receive an engagement order.
        /// </summary>
        /// <param name="fleet">Fleet to inspect.</param>
        /// <returns>True when the fleet is available for orbital combat.</returns>
        private static bool CanAssign(Fleet fleet)
        {
            return fleet?.RoleType == FleetRoleType.Battle
                && fleet.Order == null
                && fleet.Movement == null
                && !fleet.IsInCombat
                && fleet.HasOperationalCapitalShips();
        }

        /// <summary>
        /// Returns favorable hostile-fleet targets ordered by proximity.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">Fleet being assigned.</param>
        /// <param name="origin">Fleet's current planet.</param>
        /// <returns>Known engagement targets.</returns>
        private static IEnumerable<Planet> GetTargets(
            AITurnContext context,
            Fleet fleet,
            Planet origin
        )
        {
            if (origin == null || !context.Assessment.CanFleetDepartHeadquarters(fleet))
                return Enumerable.Empty<Planet>();

            return context
                .Assessment.EnemyPlanets.Where(target =>
                    target.InstanceID != origin.InstanceID
                    && context.Assessment.GetStrongestHostileFleetStrength(target) > 0
                    && context.Assessment.CanWinOrbitalCombat(fleet, target)
                )
                .OrderBy(target => origin.GetRawDistanceTo(target))
                .ThenByDescending(context.Assessment.GetPlanetValue)
                .ThenBy(target => target.InstanceID);
        }
    }
}
