using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.AI.Proposals
{
    /// <summary>
    /// Redirects unfinished capital ships away from an operationally empty fleet beneath an
    /// opposing fleet at an owned planet.
    /// </summary>
    public sealed class AIFleetReinforcementRedirectionProposal : AIProposal
    {
        public Fleet Fleet { get; }

        public Planet CurrentPlanet { get; }

        public Planet DestinationPlanet { get; }

        internal override AIProposalPriority Priority => AIProposalPriority.Mandatory;

        /// <summary>
        /// Creates a proposal to change the delivery destination of unfinished capital ships.
        /// </summary>
        /// <param name="fleet">Fleet currently designated to receive the ships.</param>
        /// <param name="currentPlanet">Planet containing the receiving fleet.</param>
        /// <param name="destinationPlanet">Safe owned planet that should receive the ships.</param>
        public AIFleetReinforcementRedirectionProposal(
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
        /// Returns a stable sort key for reinforcement redirection.
        /// </summary>
        /// <returns>The stable proposal key.</returns>
        public override string GetSortKey()
        {
            return $"fleet-reinforcement-redirection:{Fleet?.InstanceID}:{CurrentPlanet?.InstanceID}:{DestinationPlanet?.InstanceID}";
        }

        /// <summary>
        /// Returns whether the proposal remains eligible for selection.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when the unfinished ships can be redirected through normal movement.</returns>
        public override bool CanSelect(AITurnContext context)
        {
            return IsStillValid(context) && CanRequestMove(context, GetRedirectableShips(context));
        }

        /// <summary>
        /// Returns whether the proposal remains eligible for execution.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when the unfinished ships can still be redirected.</returns>
        public override bool CanExecute(AITurnContext context)
        {
            return IsStillValid(context) && context.Movement != null;
        }

        /// <summary>
        /// Changes the unfinished ships' delivery destination through the owner-controlled movement
        /// command.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public override void Execute(AITurnContext context)
        {
            if (!CanExecute(context))
                return;

            IReadOnlyList<ISceneNode> ships = GetRedirectableShips(context);
            if (ships.Count == 0)
                return;

            context.Movement.TryRequestMove(ships, DestinationPlanet, context.Faction.InstanceID);
        }

        /// <summary>
        /// Finds the nearest owned planet with no known fleet threat.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="currentPlanet">Planet the unfinished ships would otherwise reach.</param>
        /// <returns>The nearest safe destination, or null when none is available.</returns>
        internal static Planet FindDestination(AITurnContext context, Planet currentPlanet)
        {
            if (context?.Faction == null || currentPlanet == null)
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
                    || context.Assessment.GetPlanetDefenseThreatStrength(candidate) > 0
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

            return nearest;
        }

        /// <summary>
        /// Returns whether a fleet has unfinished capital ships that must be redirected from its
        /// blockaded owned planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">Fleet designated to receive the ships.</param>
        /// <param name="currentPlanet">Planet containing the fleet.</param>
        /// <returns>True when normal manufacturing-destination reassignment is required.</returns>
        internal static bool IsRequired(AITurnContext context, Fleet fleet, Planet currentPlanet)
        {
            return context?.Faction != null
                && fleet?.GetOwnerInstanceID() == context.Faction.InstanceID
                && currentPlanet != null
                && fleet.Movement == null
                && !fleet.IsInCombat
                && fleet.GetParentOfType<Planet>()?.InstanceID == currentPlanet.InstanceID
                && context.Assessment.IsOwnedPlanet(currentPlanet)
                && !context.StrategicPlan.CanReinforce(fleet)
                && HasRedirectableShips(context, fleet);
        }

        /// <summary>
        /// Returns whether the recorded fleet still requires reinforcement redirection.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>
        /// True when the fleet remains operationally empty beneath an opposing stationary fleet.
        /// </returns>
        private bool IsStillValid(AITurnContext context)
        {
            return IsRequired(context, Fleet, CurrentPlanet)
                && DestinationPlanet != null
                && context.Assessment.IsOwnedPlanet(DestinationPlanet)
                && context.Assessment.GetPlanetDefenseThreatStrength(DestinationPlanet) <= 0;
        }

        /// <summary>
        /// Returns whether a fleet contains an unfinished owned capital ship that can have its
        /// delivery destination changed.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="fleet">Fleet designated to receive the ship.</param>
        /// <returns>True when at least one capital ship can be redirected.</returns>
        private static bool HasRedirectableShips(AITurnContext context, Fleet fleet)
        {
            return context?.Faction != null
                && fleet
                    ?.GetChildren<CapitalShip>()
                    .Any(ship =>
                        ship.GetOwnerInstanceID() == context.Faction.InstanceID
                        && ship.ManufacturingStatus == ManufacturingStatus.Building
                        && ship.Movement == null
                    ) == true;
        }

        /// <summary>
        /// Returns unfinished owned capital ships whose delivery destination remains this fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The redirectable capital-ship selection.</returns>
        private IReadOnlyList<ISceneNode> GetRedirectableShips(AITurnContext context)
        {
            if (context?.Faction == null || Fleet == null)
                return Array.Empty<ISceneNode>();

            return Fleet
                .GetChildren<CapitalShip>()
                .Where(ship =>
                    ship.GetOwnerInstanceID() == context.Faction.InstanceID
                    && ship.ManufacturingStatus == ManufacturingStatus.Building
                    && ship.Movement == null
                )
                .Cast<ISceneNode>()
                .ToList();
        }

        /// <summary>
        /// Returns whether the same validation used by a player movement order accepts the ships.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="ships">Unfinished ships to redirect.</param>
        /// <returns>True when the complete redirection is valid.</returns>
        private bool CanRequestMove(AITurnContext context, IReadOnlyList<ISceneNode> ships)
        {
            return ships?.Count > 0
                && context.MovementQueries?.TryGetSelectionTransitTicks(
                    ships,
                    DestinationPlanet,
                    context.Faction.InstanceID,
                    out _
                ) == true;
        }
    }
}
