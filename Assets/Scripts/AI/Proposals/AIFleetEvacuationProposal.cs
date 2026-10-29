using System;
using System.Collections.Generic;
using Rebellion.AI.Demands;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

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
        /// True when the stationary fleet cannot act at hostile territory.
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
                return false;

            return !CanAct(
                context,
                fleet,
                currentPlanet,
                context.Assessment.GetStrongestHostileFleetStrength(currentPlanet) > 0
            );
        }

        /// <summary>
        /// Returns whether the proposal still matches live fleet and planet state.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when the fleet still requires evacuation.</returns>
        private bool IsStillValid(AITurnContext context)
        {
            return IsRequired(context, Fleet, CurrentPlanet)
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

            if (
                context.Assessment.GetFleetBombardmentStrength(fleet)
                    > context.Assessment.GetBombardmentShieldStrength(hostilePlanet)
                && context.Assessment.HasBombardmentTargets(hostilePlanet)
            )
                return true;

            AIAttackDemand demand = context.GetAttackDemand(hostilePlanet, fleet);
            if (demand?.IsAssaultBlockedByShields != false)
                return false;
            bool canBombardDefenders =
                context.Assessment.GetDefendingRegimentCount(hostilePlanet) > 0
                && context.Assessment.GetFleetBombardmentStrength(fleet)
                    > context.Assessment.GetBombardmentShieldStrength(hostilePlanet);
            int requiredRegiments = canBombardDefenders
                ? demand.OccupationRegimentCount
                : demand.RegimentCount;
            int requiredStrength = canBombardDefenders ? 0 : demand.RegimentStrength;
            return context.Assessment.GetReadyFleetRegimentCount(fleet) >= requiredRegiments
                && context.Assessment.GetReadyFleetRegimentAttackStrength(fleet) >= requiredStrength
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
                || fleet == null
                || fleet.Movement != null
                || currentPlanet == null
            )
                return null;

            Planet nearest = null;
            double nearestDistance = double.MaxValue;
            foreach (Planet candidate in context.Assessment.OwnedPlanets)
            {
                if (
                    candidate == null
                    || candidate.InstanceID == currentPlanet.InstanceID
                    || !candidate.IsColonized
                    || candidate.IsDestroyed
                    || candidate.IsBlockadedFor(context.Faction.InstanceID)
                    || !candidate.CanAcceptChild(fleet)
                )
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

                nearest = candidate;
                nearestDistance = distance;
            }

            return CanMove(context, fleet, nearest) ? nearest : null;
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
