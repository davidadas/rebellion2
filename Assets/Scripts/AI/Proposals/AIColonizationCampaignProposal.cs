using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.AI.Proposals
{
    /// <summary>
    /// Assigns one sector to a colonization fleet and reveals it before selecting a colony.
    /// </summary>
    public sealed class AIColonizationCampaignProposal : AIProposal
    {
        public Fleet Fleet { get; }

        public string SystemId { get; }

        public IReadOnlyList<Planet> UnexploredPlanets { get; }

        public Planet ColonyTarget { get; }

        public Planet EntryPlanet { get; }

        /// <summary>
        /// Creates a proposal to campaign the supplied planets or claim the selected colony.
        /// </summary>
        /// <param name="fleet">The colonization fleet.</param>
        /// <param name="systemId">The sector being revealed.</param>
        /// <param name="unexploredPlanets">Planets that still require observation.</param>
        /// <param name="colonyTarget">The best revealed colony, when revealing is complete.</param>
        public AIColonizationCampaignProposal(
            Fleet fleet,
            string systemId,
            IReadOnlyList<Planet> unexploredPlanets,
            Planet colonyTarget = null
        )
        {
            Fleet = fleet;
            SystemId = systemId ?? string.Empty;
            UnexploredPlanets = unexploredPlanets ?? Array.Empty<Planet>();
            ColonyTarget = colonyTarget;
            EntryPlanet = FindNearestPlanet(fleet?.GetParentOfType<Planet>(), UnexploredPlanets);
        }

        /// <summary>
        /// Returns claims that prevent competing orders, movement, and sector campaign assignments.
        /// </summary>
        /// <returns>Claim keys for this proposal.</returns>
        public override IReadOnlyList<string> GetClaimKeys()
        {
            if (Fleet == null)
                return Array.Empty<string>();

            List<string> claims = new List<string>
            {
                AIClaimKeys.FleetOrder(Fleet.InstanceID),
                AIClaimKeys.FleetColonization(Fleet.InstanceID),
                AIClaimKeys.SystemColonization(SystemId),
            };
            if (Fleet.Order == null)
                claims.Add(AIClaimKeys.NewColonizationOrder(Fleet.GetOwnerInstanceID()));
            if (UnexploredPlanets.Count > 0)
                claims.Add(AIClaimKeys.FleetMovement(Fleet.InstanceID));
            if (ColonyTarget != null)
                claims.Add(AIClaimKeys.PlanetColonization(ColonyTarget.InstanceID));
            return claims;
        }

        /// <summary>
        /// Returns the deterministic proposal key.
        /// </summary>
        /// <returns>The fleet, sector, and resulting colony identifiers.</returns>
        public override string GetSortKey() =>
            $"fleet-colonization:{Fleet?.InstanceID}:{SystemId}:{ColonyTarget?.InstanceID}";

        /// <summary>
        /// Returns whether this campaign proposal may be selected.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when the fleet and campaign objective remain valid.</returns>
        public override bool CanSelect(AITurnContext context) => IsStillValid(context);

        /// <summary>
        /// Returns whether this campaign proposal may execute.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when the fleet and campaign objective remain valid.</returns>
        public override bool CanExecute(AITurnContext context) => IsStillValid(context);

        /// <summary>
        /// Starts the campaign route or assigns its highest-capacity revealed colony.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public override void Execute(AITurnContext context)
        {
            if (!CanExecute(context))
                return;

            if (UnexploredPlanets.Count == 0)
            {
                if (ColonyTarget == null)
                {
                    Fleet.Order = null;
                    return;
                }

                Fleet.Order = new FleetOrder
                {
                    OrderType = FleetOrderType.Colonize,
                    Status =
                        Fleet.GetParentOfType<Planet>()?.InstanceID == ColonyTarget.InstanceID
                            ? FleetOrderStatus.Ready
                            : FleetOrderStatus.Staging,
                    TargetPlanetId = ColonyTarget.InstanceID,
                    TargetSystemId = SystemId,
                };
                return;
            }

            if (EntryPlanet == null)
                return;

            bool accepted =
                context.Movement?.TrySetFleetWaypointRoute(
                    new ISceneNode[] { Fleet },
                    new[] { EntryPlanet.InstanceID },
                    context.Faction.InstanceID
                ) == true;
            if (!accepted)
                return;

            Fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Colonize,
                Status = FleetOrderStatus.Readying,
                TargetSystemId = SystemId,
            };
        }

        /// <summary>
        /// Returns whether the proposal still matches the live fleet and visible system state.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when the proposal remains valid.</returns>
        private bool IsStillValid(AITurnContext context)
        {
            if (
                !IsOwnedBy(context, Fleet)
                || Fleet.RoleType != FleetRoleType.Colonization
                || string.IsNullOrEmpty(SystemId)
                || Fleet.Movement != null
                || Fleet.IsInCombat
                || Fleet.HasWaypoints()
            )
                return false;

            FleetOrder order = Fleet.Order;
            if (
                order != null
                && (
                    order.OrderType != FleetOrderType.Colonize
                    || !string.IsNullOrEmpty(order.TargetPlanetId)
                    || order.TargetSystemId != SystemId
                )
            )
                return false;

            if (UnexploredPlanets.Count > 0)
                return UnexploredPlanets.All(planet => planet?.IsUnexploredView == true);

            return ColonyTarget == null
                || !ColonyTarget.IsUnexploredView
                    && !ColonyTarget.IsColonized
                    && !ColonyTarget.IsDestroyed
                    && string.IsNullOrEmpty(ColonyTarget.GetOwnerInstanceID());
        }

        /// <summary>
        /// Finds the nearest candidate planet using its identifier as the deterministic tie-break.
        /// </summary>
        /// <param name="origin">The route origin, or null.</param>
        /// <param name="candidates">Candidate destinations.</param>
        /// <returns>The nearest candidate, or null when none exist.</returns>
        private static Planet FindNearestPlanet(Planet origin, IReadOnlyList<Planet> candidates)
        {
            return candidates
                .OrderBy(planet => origin?.GetRawDistanceTo(planet) ?? double.MaxValue)
                .ThenBy(planet => planet.InstanceID, StringComparer.Ordinal)
                .FirstOrDefault();
        }
    }
}
