using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.AI.Planners
{
    /// <summary>
    /// Plans fleet responses for threatened headquarters and owned planets.
    /// </summary>
    internal sealed class AIFleetDefensePlanner
    {
        /// <summary>
        /// Returns fleet-defense proposals for the current AI turn.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Fleet-defense proposals generated for the faction.</returns>
        internal List<AIProposal> Plan(AITurnContext context)
        {
            List<AIProposal> proposals = new List<AIProposal>();
            if (context?.Game == null || context.Faction == null)
                return proposals;

            Fleet headquartersDefense = AddHeadquartersDefenseProposal(context, proposals);
            AddPlanetDefenseProposals(context, proposals, headquartersDefense);
            return proposals;
        }

        /// <summary>
        /// Adds a headquarters defense proposal when current commitments are insufficient.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposals">The proposal collection to update.</param>
        /// <returns>The assigned fleet, or null when no assignment is needed or available.</returns>
        private static Fleet AddHeadquartersDefenseProposal(
            AITurnContext context,
            ICollection<AIProposal> proposals
        )
        {
            Planet headquarters = context.Assessment.OwnedPlanets.FirstOrDefault(
                context.Assessment.IsFactionHeadquarters
            );
            if (headquarters == null)
                return null;

            int requiredDefense = context.Assessment.GetRequiredHeadquartersDefenseStrength(
                headquarters
            );
            int committedDefense = context.Assessment.GetCommittedHeadquartersDefenseStrength(
                headquarters
            );
            if (
                committedDefense >= requiredDefense
                || HasHeadquartersDefenseOrder(context, headquarters)
            )
                return null;

            Fleet fleet = FindHeadquartersDefenseFleet(context, headquarters, requiredDefense);
            if (fleet != null)
                proposals.Add(new AIFleetDefenseProposal(fleet, headquarters));

            return fleet;
        }

        /// <summary>
        /// Adds defense proposals for threatened non-headquarters planets.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposals">The proposal collection to update.</param>
        /// <param name="headquartersDefense">The fleet already reserved for headquarters defense.</param>
        private static void AddPlanetDefenseProposals(
            AITurnContext context,
            ICollection<AIProposal> proposals,
            Fleet headquartersDefense
        )
        {
            HashSet<Fleet> assignedFleets = new HashSet<Fleet>();
            if (headquartersDefense != null)
                assignedFleets.Add(headquartersDefense);

            foreach (
                Planet targetPlanet in context
                    .Assessment.OwnedPlanets.Where(planet =>
                        !context.Assessment.IsFactionHeadquarters(planet)
                        && context.Assessment.GetRequiredPlanetDefenseStrength(planet) > 0
                        && !HasDefenseOrder(context, planet)
                    )
                    .OrderByDescending(context.Assessment.GetPlanetValue)
                    .ThenByDescending(context.Assessment.GetRequiredPlanetDefenseStrength)
                    .ThenBy(planet => planet.InstanceID)
            )
            {
                Fleet fleet = FindPlanetDefenseFleet(context, targetPlanet, assignedFleets);
                if (fleet == null)
                    continue;

                assignedFleets.Add(fleet);
                proposals.Add(new AIFleetDefenseProposal(fleet, targetPlanet));
            }
        }

        /// <summary>
        /// Finds the least costly available fleet capable of defending a planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="targetPlanet">The threatened planet.</param>
        /// <param name="assignedFleets">Fleets already assigned during this planning pass.</param>
        /// <returns>The selected fleet, or null when none can defend the planet.</returns>
        private static Fleet FindPlanetDefenseFleet(
            AITurnContext context,
            Planet targetPlanet,
            ISet<Fleet> assignedFleets
        )
        {
            return context
                .Assessment.OwnedFleets.Where(fleet =>
                    !assignedFleets.Contains(fleet)
                    && CanAssignPlanetDefense(context, fleet)
                    && context.Assessment.CanDefendPlanet(fleet, targetPlanet)
                )
                .OrderBy(fleet => GetFleetDistance(context, fleet, targetPlanet))
                .ThenBy(context.Assessment.GetFleetCombatValue)
                .ThenBy(fleet => fleet.InstanceID)
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns whether a fleet can be reassigned to defend an ordinary planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <returns>True when the fleet is available for defense.</returns>
        private static bool CanAssignPlanetDefense(AITurnContext context, Fleet fleet)
        {
            if (
                fleet?.RoleType != FleetRoleType.Battle
                || fleet.Movement != null
                || fleet.IsInCombat
                || !fleet.HasOperationalCapitalShips()
                || !context.Assessment.CanFleetDepartHeadquarters(fleet)
            )
                return false;

            return fleet.Order == null
                || fleet.Order.OrderType != FleetOrderType.Defend
                    && fleet.Order.Status == FleetOrderStatus.Staging;
        }

        /// <summary>
        /// Finds the best available fleet for headquarters defense.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="headquarters">The headquarters planet.</param>
        /// <param name="requiredDefense">The required defense strength.</param>
        /// <returns>The selected fleet, or null when none is available.</returns>
        private static Fleet FindHeadquartersDefenseFleet(
            AITurnContext context,
            Planet headquarters,
            int requiredDefense
        )
        {
            List<Fleet> candidates = context
                .Assessment.OwnedFleets.Where(CanAssignHeadquartersDefense)
                .ToList();
            Fleet sufficientFleet = candidates
                .Where(fleet => context.Assessment.GetFleetCombatValue(fleet) >= requiredDefense)
                .OrderBy(fleet => GetFleetDistance(context, fleet, headquarters))
                .ThenBy(context.Assessment.GetFleetCombatValue)
                .ThenBy(fleet => fleet.InstanceID)
                .FirstOrDefault();
            if (sufficientFleet != null)
                return sufficientFleet;

            return candidates
                .OrderByDescending(context.Assessment.GetFleetCombatValue)
                .ThenBy(fleet => GetFleetDistance(context, fleet, headquarters))
                .ThenBy(fleet => fleet.InstanceID)
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns whether a fleet can be assigned to headquarters defense.
        /// </summary>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <returns>True when the fleet is idle and combat-capable.</returns>
        private static bool CanAssignHeadquartersDefense(Fleet fleet)
        {
            return fleet?.RoleType == FleetRoleType.Battle
                && fleet.Order == null
                && fleet.Movement == null
                && !fleet.IsInCombat
                && fleet.HasOperationalCapitalShips();
        }

        /// <summary>
        /// Returns whether a fleet is already ordered to defend headquarters.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="headquarters">The headquarters planet.</param>
        /// <returns>True when a matching defense order exists.</returns>
        private static bool HasHeadquartersDefenseOrder(AITurnContext context, Planet headquarters)
        {
            return context.Assessment.OwnedFleets.Any(fleet =>
                fleet.Order?.OrderType == FleetOrderType.Defend
                && fleet.Order.TargetPlanetId == headquarters.InstanceID
            );
        }

        /// <summary>
        /// Returns the direct distance from a fleet's current planet to a destination.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet to locate.</param>
        /// <param name="destination">The destination planet.</param>
        /// <returns>The distance, or the maximum value when the fleet has no planet.</returns>
        private static double GetFleetDistance(
            AITurnContext context,
            Fleet fleet,
            Planet destination
        )
        {
            return context.Assessment.GetFleetPlanet(fleet)?.GetRawDistanceTo(destination)
                ?? double.MaxValue;
        }

        /// <summary>
        /// Returns whether a fleet is already ordered to defend a planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="targetPlanet">The planet to inspect.</param>
        /// <returns>True when a matching defense order exists.</returns>
        private static bool HasDefenseOrder(AITurnContext context, Planet targetPlanet)
        {
            return context.Assessment.OwnedFleets.Any(fleet =>
                fleet.Order?.OrderType == FleetOrderType.Defend
                && fleet.Order.TargetPlanetId == targetPlanet.InstanceID
            );
        }
    }
}
