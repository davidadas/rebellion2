using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Demands;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Mathematics;

namespace Rebellion.AI.Proposals
{
    /// <summary>
    /// Returns a fleet that cannot act at an unsafe location to friendly territory.
    /// </summary>
    public sealed class AIFleetEvacuationProposal : AIProposal
    {
        public Fleet Fleet { get; }

        public Planet CurrentPlanet { get; }

        public Planet DestinationPlanet { get; }

        internal override AIProposalPriority Priority => AIProposalPriority.Mandatory;

        /// <summary>
        /// Creates a proposal to evacuate a fleet from its current planet.
        /// </summary>
        /// <param name="fleet">Fleet to evacuate.</param>
        /// <param name="currentPlanet">Planet currently containing the fleet.</param>
        /// <param name="destinationPlanet">Friendly planet that should receive the fleet.</param>
        public AIFleetEvacuationProposal(
            Fleet fleet,
            Planet currentPlanet,
            Planet destinationPlanet
        )
        {
            Fleet = fleet;
            CurrentPlanet = currentPlanet;
            DestinationPlanet = destinationPlanet;
        }

        /// <summary>
        /// Returns a stable sort key for this evacuation.
        /// </summary>
        /// <returns>Stable evacuation key.</returns>
        public override string GetSortKey()
        {
            return $"fleet-evacuation:{Fleet?.InstanceID}:{CurrentPlanet?.InstanceID}:{DestinationPlanet?.InstanceID}";
        }

        /// <summary>
        /// Returns whether the fleet remains stationary at the recorded planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when evacuation remains valid.</returns>
        public override bool CanSelect(AITurnContext context)
        {
            return IsStillValid(context);
        }

        /// <summary>
        /// Returns whether the fleet remains stationary at the recorded planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when evacuation remains valid.</returns>
        public override bool CanExecute(AITurnContext context)
        {
            return IsStillValid(context);
        }

        /// <summary>
        /// Clears the fleet's stale assignment and evacuates it to friendly territory.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public override void Execute(AITurnContext context)
        {
            if (!CanExecute(context))
                return;

            if (AIFleetMovement.TryMove(context, Fleet, DestinationPlanet))
                Fleet.Order = null;
        }

        /// <summary>
        /// Returns whether a fleet must leave its current planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <param name="currentPlanet">The planet currently containing the fleet.</param>
        /// <returns>
        /// True when the stationary fleet cannot act in hostile territory or faces a stronger approach at an owned planet.
        /// </returns>
        internal static bool IsRequired(AITurnContext context, Fleet fleet, Planet currentPlanet)
        {
            if (
                context?.Faction == null
                || fleet?.GetOwnerInstanceID() != context.Faction.InstanceID
                || currentPlanet == null
                || fleet.Movement != null
                || fleet.IsInCombat
                || fleet.GetParentOfType<Planet>()?.InstanceID != currentPlanet.InstanceID
            )
                return false;

            string planetOwnerId = currentPlanet.GetOwnerInstanceID();
            if (string.IsNullOrEmpty(planetOwnerId))
                return false;

            if (planetOwnerId == context.Faction.InstanceID)
                return ShouldEvacuateOwnedPlanet(context, fleet, currentPlanet);

            return !CanAct(
                context,
                fleet,
                currentPlanet,
                context.Assessment.GetStrongestHostileFleetStrength(currentPlanet) > 0
            );
        }

        /// <summary>
        /// Returns whether a battle fleet should leave an owned planet before stronger hostile forces can trap it.
        /// </summary>
        /// <param name="context">The current faction turn.</param>
        /// <param name="fleet">The stationary fleet being considered for evacuation.</param>
        /// <param name="planet">The owned planet receiving hostile fleets.</param>
        /// <returns>True when an incoming force can prevent withdrawal and exceeds the combined ready local fleets and planetary starfighters.</returns>
        private static bool ShouldEvacuateOwnedPlanet(
            AITurnContext context,
            Fleet fleet,
            Planet planet
        )
        {
            if (
                fleet.RoleType != FleetRoleType.Battle
                || fleet.Order?.OrderType == FleetOrderType.Defend
                || context.Assessment.IsPriorityDefensePlanet(planet)
                || !context.StrategicPlan.CanFleetDepart(fleet)
            )
                return false;

            long incomingStrength = context.Assessment.GetKnownIncomingTrappingFleetStrength(
                planet
            );
            if (incomingStrength <= 0)
                return false;

            long defendingStrength = context
                .Assessment.GetFriendlyFleets(planet)
                .Where(friendly => friendly.Movement == null)
                .Sum(friendly =>
                    (long)context.Assessment.GetReadyFleetCombatValueAgainstCapitalShips(friendly)
                );
            defendingStrength += context
                .Assessment.GetPlanetStarfighters(planet)
                .Where(fighter =>
                    fighter.GetOwnerInstanceID() == context.Faction.InstanceID
                    && fighter.ManufacturingStatus == ManufacturingStatus.Complete
                    && fighter.Movement == null
                    && fighter.CurrentSquadronSize > 0
                )
                .Sum(fighter => (long)fighter.CalculateCombatValue(fighter.CurrentSquadronSize));
            return incomingStrength > defendingStrength;
        }

        /// <summary>
        /// Returns whether the proposal still matches live fleet and planet state.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when the fleet still requires evacuation.</returns>
        private bool IsStillValid(AITurnContext context)
        {
            return IsRequired(context, Fleet, CurrentPlanet)
                && AIFleetMovement.CanReturnTo(context, Fleet, DestinationPlanet)
                && AIFleetMovement.CanMove(context, Fleet, DestinationPlanet);
        }

        /// <summary>
        /// Returns whether the fleet has a viable immediate action at the hostile planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">The fleet to inspect.</param>
        /// <param name="hostilePlanet">The hostile planet currently containing the fleet.</param>
        /// <param name="hasHostileFleet">Whether a stationary hostile fleet is present.</param>
        /// <returns>True when the fleet can win orbit, bombard, or assault.</returns>
        private static bool CanAct(
            AITurnContext context,
            Fleet fleet,
            Planet hostilePlanet,
            bool hasHostileFleet
        )
        {
            if (hasHostileFleet)
            {
                int required = context.GetAttackDemand(hostilePlanet, fleet)?.OrbitalStrength ?? 0;
                return required > 0
                    && context.Assessment.GetProjectedFleetCombatValueAgainstCapitalShips(fleet)
                        >= required;
            }

            if (context.CanUseMilitaryBombardment(fleet, hostilePlanet))
                return true;

            AIAttackDemand demand = context.GetAttackDemand(hostilePlanet, fleet);
            if (demand?.IsAssaultBlockedByShields != false)
                return false;
            var groundRequirements = context.GetAttackRegimentRequirements(hostilePlanet, fleet);
            return context.Assessment.GetReadyFleetRegimentCount(fleet) >= groundRequirements.Count
                && context.Assessment.GetReadyFleetRegimentAttackStrength(fleet)
                    >= groundRequirements.Strength
                && context.Assessment.GetPlanetaryAssaultSuccessPercent(fleet, hostilePlanet)
                    >= context.Game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultSuccessPercent;
        }
    }

    /// <summary>
    /// Issues AI fleet relocation through the same owner-validated movement command used by
    /// player fleet orders.
    /// </summary>
    internal static class AIFleetMovement
    {
        /// <summary>
        /// Finds the nearest owned planet that can receive the fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">Fleet that needs a destination.</param>
        /// <param name="currentPlanet">Planet the fleet is leaving.</param>
        /// <returns>The nearest safe owned planet, or null when none is available.</returns>
        internal static Planet FindNearestSafeOwnedPlanet(
            AITurnContext context,
            Fleet fleet,
            Planet currentPlanet
        )
        {
            if (
                context?.Faction == null
                || context.Assessment == null
                || context.MovementQueries == null
                || fleet == null
                || fleet.Movement != null
                || currentPlanet == null
            )
                return null;

            Planet nearest = null;
            double nearestDistance = double.MaxValue;
            foreach (Planet candidate in context.Assessment.OwnedPlanets)
            {
                if (candidate == null || candidate.InstanceID == currentPlanet.InstanceID)
                    continue;

                double distance = currentPlanet.GetRawDistanceTo(candidate);
                if (
                    nearest != null
                    && (
                        distance > nearestDistance
                        || distance == nearestDistance
                            && string.Compare(
                                candidate.InstanceID,
                                nearest.InstanceID,
                                StringComparison.Ordinal
                            ) >= 0
                    )
                )
                    continue;

                if (!CanReturnTo(context, fleet, candidate))
                    continue;

                nearest = candidate;
                nearestDistance = distance;
            }

            return CanMove(context, fleet, nearest) ? nearest : null;
        }

        /// <summary>
        /// Checks whether an owned destination accepts a return and its known arrival threat is covered.
        /// </summary>
        /// <param name="context">The faction turn supplying movement and observed orbital forces.</param>
        /// <param name="fleet">The stationary fleet returning with its currently ready ships.</param>
        /// <param name="destination">The owned destination being considered.</param>
        /// <returns>True when normal movement is valid and the ready force meets the arrival requirement.</returns>
        internal static bool CanReturnTo(AITurnContext context, Fleet fleet, Planet destination)
        {
            if (
                context?.Faction == null
                || context.Assessment == null
                || context.MovementQueries == null
                || context.Game?.Config?.AI?.FleetDeployment == null
                || fleet == null
                || fleet.Movement != null
                || destination == null
                || destination.GetOwnerInstanceID() != context.Faction.InstanceID
                || !destination.IsColonized
                || destination.IsDestroyed
                || destination.IsBlockadedFor(context.Faction.InstanceID)
                || !destination.CanAcceptChild(fleet)
            )
                return false;

            IReadOnlyList<ISceneNode> selection = new ISceneNode[] { fleet };
            if (
                !context.MovementQueries.TryGetSelectionTransitTicks(
                    selection,
                    destination,
                    context.Faction.InstanceID,
                    out int arrivalTicks
                )
            )
                return false;

            int hostileStrength = context
                .Assessment.GetHostileOrbitalCapabilitiesAtArrival(destination, arrivalTicks)
                .Combat;
            if (hostileStrength <= 0)
                return true;

            int requiredStrength = IntegerMath.ScaleByPercent(
                hostileStrength,
                context.Game.Config.AI.FleetDeployment.AttackStrengthPercentOfStrongestHostileFleet
            );
            return context.Assessment.GetReadyFleetCombatValueAgainstCapitalShips(fleet)
                >= requiredStrength;
        }

        /// <summary>
        /// Returns whether the normal owner-controlled movement command accepts the fleet order.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">Fleet that would move.</param>
        /// <param name="destination">Requested destination planet.</param>
        /// <returns>True when the same validation used by player fleet orders accepts the move.</returns>
        internal static bool CanMove(AITurnContext context, Fleet fleet, Planet destination)
        {
            if (
                context?.Faction == null
                || context.MovementQueries == null
                || fleet == null
                || fleet.Movement != null
                || destination == null
            )
                return false;

            IReadOnlyList<ISceneNode> selection = new ISceneNode[] { fleet };
            return context.MovementQueries.TryGetSelectionTransitTicks(
                selection,
                destination,
                context.Faction.InstanceID,
                out _
            );
        }

        /// <summary>
        /// Moves a fleet through the normal owner-controlled movement command.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">Fleet receiving the order.</param>
        /// <param name="destination">Requested destination planet.</param>
        /// <returns>True when the movement order was accepted.</returns>
        internal static bool TryMove(AITurnContext context, Fleet fleet, Planet destination)
        {
            if (
                context?.Faction == null
                || context.Movement == null
                || fleet == null
                || fleet.Movement != null
                || destination == null
            )
                return false;

            return context.Movement.TryRequestMove(
                new ISceneNode[] { fleet },
                destination,
                context.Faction.InstanceID
            );
        }

        /// <summary>
        /// Moves a fleet to the nearest safe owned planet through the normal movement command.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">Fleet receiving the order.</param>
        /// <returns>True when a safe destination accepted the movement order.</returns>
        internal static bool TryMoveToNearestSafeOwnedPlanet(AITurnContext context, Fleet fleet)
        {
            Planet currentPlanet = fleet?.GetParentOfType<Planet>();
            Planet destination = FindNearestSafeOwnedPlanet(context, fleet, currentPlanet);
            return TryMove(context, fleet, destination);
        }
    }
}
