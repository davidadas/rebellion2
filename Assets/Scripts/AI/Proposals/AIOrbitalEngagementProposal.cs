using System.Collections.Generic;
using Rebellion.AI.Director;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.AI.Proposals
{
    /// <summary>
    /// Sends a battle fleet to destroy a known hostile fleet without committing to an invasion.
    /// </summary>
    public sealed class AIOrbitalEngagementProposal : AIProposal
    {
        /// <summary>Gets the fleet assigned to the engagement.</summary>
        public Fleet Fleet { get; }

        /// <summary>Gets the planet containing the hostile fleet.</summary>
        public Planet TargetPlanet { get; }

        /// <summary>Gets the friendly planet to which the fleet should return.</summary>
        public Planet OriginPlanet { get; }

        /// <summary>
        /// Creates an orbital engagement proposal.
        /// </summary>
        /// <param name="fleet">Fleet assigned to the engagement.</param>
        /// <param name="targetPlanet">Planet containing the hostile fleet.</param>
        /// <param name="originPlanet">Friendly planet from which the fleet departs.</param>
        public AIOrbitalEngagementProposal(Fleet fleet, Planet targetPlanet, Planet originPlanet)
        {
            Fleet = fleet;
            TargetPlanet = targetPlanet;
            OriginPlanet = originPlanet;
        }

        /// <inheritdoc />
        public override IReadOnlyList<string> GetClaimKeys()
        {
            List<string> claims = new List<string>();
            if (Fleet == null)
                return claims;

            claims.Add(AIClaimKeys.FleetOrder(Fleet.InstanceID));
            claims.Add(AIClaimKeys.FleetMovement(Fleet.InstanceID));
            if (Fleet.Order == null)
                claims.Add(AIClaimKeys.NewOffensiveOrder(Fleet.GetOwnerInstanceID()));
            if (TargetPlanet != null)
                claims.Add(AIClaimKeys.FleetEngagementTarget(TargetPlanet.InstanceID));

            return claims;
        }

        /// <inheritdoc />
        public override string GetSortKey()
        {
            return $"fleet-engagement:{Fleet?.InstanceID}:{TargetPlanet?.InstanceID}";
        }

        /// <inheritdoc />
        public override bool CanSelect(AITurnContext context)
        {
            return IsStillValid(context);
        }

        /// <inheritdoc />
        public override bool CanExecute(AITurnContext context)
        {
            return IsStillValid(context);
        }

        /// <inheritdoc />
        public override void Execute(AITurnContext context)
        {
            if (!CanExecute(context) || Fleet.Movement != null || Fleet.IsInCombat)
                return;

            EnsureOrder();
            Planet currentPlanet = Fleet.GetParentOfType<Planet>();
            if (Fleet.Order.Status == FleetOrderStatus.Returning)
            {
                if (currentPlanet?.GetOwnerInstanceID() == context.Faction.InstanceID)
                    Fleet.Order = null;
                else
                    ReturnToFriendlyTerritory(context);
                return;
            }

            Planet knownTarget = context.Assessment.GetKnownPlanet(TargetPlanet.InstanceID);
            if (currentPlanet?.InstanceID != TargetPlanet.InstanceID)
            {
                if (context.Assessment.GetStrongestHostileFleetStrength(knownTarget) <= 0)
                {
                    if (currentPlanet?.GetOwnerInstanceID() == context.Faction.InstanceID)
                        Fleet.Order = null;
                    else
                        ReturnToFriendlyTerritory(context);
                    return;
                }

                Fleet.Order.Status = FleetOrderStatus.Readying;
                context.Movement?.RequestMove(Fleet, TargetPlanet);
                return;
            }

            Planet liveTarget = context.Game.GetSceneNodeByInstanceID<Planet>(
                TargetPlanet.InstanceID
            );
            if (liveTarget?.GetOwnerInstanceID() == context.Faction.InstanceID)
            {
                Fleet.Order = null;
                return;
            }

            if (context.Assessment.GetStrongestHostileFleetStrength(knownTarget) > 0)
            {
                Fleet.Order.Status = FleetOrderStatus.Ready;
                return;
            }

            if (
                !string.IsNullOrEmpty(liveTarget?.GetOwnerInstanceID())
                && context.Assessment.IsFleetReadyToAttack(Fleet, knownTarget)
            )
            {
                Fleet.Order = new FleetOrder
                {
                    OrderType = FleetOrderType.Attack,
                    Status = FleetOrderStatus.Ready,
                    TargetPlanetId = liveTarget.InstanceID,
                };
                return;
            }

            ReturnToFriendlyTerritory(context);
        }

        /// <summary>
        /// Creates the durable engagement order when this is a new assignment.
        /// </summary>
        private void EnsureOrder()
        {
            if (Fleet.Order?.OrderType == FleetOrderType.Engage)
                return;

            Fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Engage,
                Status = FleetOrderStatus.Ready,
                TargetPlanetId = TargetPlanet.InstanceID,
                OriginPlanetId = OriginPlanet?.InstanceID ?? string.Empty,
            };
        }

        /// <summary>
        /// Sends the fleet back to its friendly origin or the nearest friendly planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        private void ReturnToFriendlyTerritory(AITurnContext context)
        {
            Fleet.Order.Status = FleetOrderStatus.Returning;
            Planet origin = context.Game.GetSceneNodeByInstanceID<Planet>(
                Fleet.Order.OriginPlanetId
            );
            if (origin?.GetOwnerInstanceID() == context.Faction.InstanceID)
            {
                context.Movement?.RequestMove(Fleet, origin);
                return;
            }

            context.Movement?.EvacuateToNearestFriendlyPlanet(Fleet);
        }

        /// <summary>
        /// Returns whether the proposal still describes an owned battle fleet and known target.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when the engagement can advance.</returns>
        private bool IsStillValid(AITurnContext context)
        {
            if (
                !IsOwnedBy(context, Fleet)
                || Fleet.RoleType != FleetRoleType.Battle
                || TargetPlanet == null
            )
                return false;

            FleetOrder order = Fleet.Order;
            if (order != null)
            {
                return order.OrderType == FleetOrderType.Engage
                    && order.TargetPlanetId == TargetPlanet.InstanceID;
            }

            Planet knownTarget = context.Assessment.GetKnownPlanet(TargetPlanet.InstanceID);
            return context.Assessment.IsEnemyPlanet(knownTarget)
                && context.Assessment.GetStrongestHostileFleetStrength(knownTarget) > 0
                && context.Assessment.CanWinOrbitalCombat(Fleet, knownTarget);
        }
    }
}
