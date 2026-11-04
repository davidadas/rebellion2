using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Proposals;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.Util.Logging;

namespace Rebellion.AI.Planners
{
    /// <summary>
    /// Plans nearby favorable fleet engagements without abandoning defensive assignments.
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
            using var performanceMeasurement = PerformanceLogger.Measure(
                nameof(Plan),
                context?.Faction?.PeekInstanceID(),
                subjectType: typeof(AIOrbitalEngagementPlanner)
            );
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
            using var performanceMeasurement = PerformanceLogger.Measure(
                nameof(AddExistingEngagements),
                context?.Faction?.PeekInstanceID(),
                subjectType: typeof(AIOrbitalEngagementPlanner)
            );
            foreach (Fleet fleet in context.Assessment.EngagementOrderedFleets)
            {
                Planet target = context.Assessment.GetKnownPlanet(fleet.Order.TargetPlanetId);
                Planet origin = context.Assessment.GetKnownPlanet(fleet.Order.OriginPlanetId);
                if (
                    target != null
                    && (
                        fleet.Order.Status == FleetOrderStatus.Returning
                        || fleet.Movement != null
                        || fleet.IsInCombat
                        || context.HasCurrentHeadquartersAttackIntel(fleet, target)
                    )
                )
                    proposals.Add(new AIOrbitalEngagementProposal(fleet, target, origin));
                else
                    proposals.Add(new AIClearFleetOrderProposal(fleet, fleet.Order));
            }
        }

        /// <summary>
        /// Returns whether an idle or assembling fleet can receive an engagement order.
        /// </summary>
        /// <param name="fleet">Fleet to inspect.</param>
        /// <returns>True when the fleet is available for orbital combat.</returns>
        private static bool CanAssign(Fleet fleet)
        {
            return fleet?.RoleType == FleetRoleType.Battle
                && (
                    fleet.Order == null
                    || fleet.Order.OrderType == FleetOrderType.Attack
                        && fleet.Order.Status
                            is FleetOrderStatus.Building
                                or FleetOrderStatus.Staging
                )
                && fleet.Movement == null
                && !fleet.IsInCombat
                && fleet.HasOperationalCapitalShips();
        }

        /// <summary>
        /// Returns favorable hostile-fleet targets.
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
            using var performanceMeasurement = PerformanceLogger.Measure(
                nameof(GetTargets),
                context?.Faction?.PeekInstanceID(),
                subjectType: typeof(AIOrbitalEngagementPlanner)
            );
            if (origin == null || !context.StrategicPlan.CanFleetDepart(fleet))
                return Enumerable.Empty<Planet>();

            return context.Assessment.FactionViewPlanets.Where(target =>
                target.InstanceID != origin.InstanceID
                && context.HasCurrentHeadquartersAttackIntel(fleet, target)
                && context.GetAttackArrivalTicks(fleet, target)
                    <= context.Game.Config.AI.FleetDeployment.LocalAttackMaximumTravelTicks
                && context.Assessment.GetStrongestHostileFleetStrength(target) > 0
                && context.CanWinOrbitalCombat(fleet, target)
            );
        }
    }
}
