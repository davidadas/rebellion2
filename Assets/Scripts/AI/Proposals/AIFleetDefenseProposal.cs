using System.Collections.Generic;
using Rebellion.AI.Director;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.AI.Proposals
{
    /// <summary>
    /// Proposal to assign a fleet to defend a planet.
    /// </summary>
    public sealed class AIFleetDefenseProposal : AIProposal
    {
        public Fleet Fleet { get; }

        public Planet TargetPlanet { get; }

        /// <summary>
        /// Creates a defense proposal for the supplied fleet and planet.
        /// </summary>
        /// <param name="fleet">The fleet assigned to defend.</param>
        /// <param name="targetPlanet">The planet to defend.</param>
        public AIFleetDefenseProposal(Fleet fleet, Planet targetPlanet)
        {
            Fleet = fleet;
            TargetPlanet = targetPlanet;
        }

        /// <summary>
        /// Returns claims that prevent incompatible fleet actions.
        /// </summary>
        /// <returns>Claim keys for this proposal.</returns>
        public override IReadOnlyList<string> GetClaimKeys()
        {
            if (Fleet == null || TargetPlanet == null)
                return new List<string>();

            return new List<string>
            {
                AIClaimKeys.FleetOrder(Fleet.InstanceID),
                AIClaimKeys.FleetMovement(Fleet.InstanceID),
                AIClaimKeys.PlanetDefense(TargetPlanet.InstanceID),
            };
        }

        /// <summary>
        /// Returns a stable sort key for the fleet-defense proposal.
        /// </summary>
        /// <returns>A stable sort key.</returns>
        public override string GetSortKey()
        {
            return $"fleet-defense:{TargetPlanet?.InstanceID}:{Fleet?.InstanceID}";
        }

        /// <summary>
        /// Returns whether this proposal may be selected.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when the defense order remains valid.</returns>
        public override bool CanSelect(AITurnContext context)
        {
            return IsStillValid(context);
        }

        /// <summary>
        /// Returns whether this proposal may execute against the current game state.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when the defense order can still execute.</returns>
        public override bool CanExecute(AITurnContext context)
        {
            return IsStillValid(context);
        }

        /// <summary>
        /// Assigns the fleet to defend the target planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public override void Execute(AITurnContext context)
        {
            if (!CanExecute(context))
            {
                ClearOrder();
                return;
            }

            EnsureOrder();

            if (Fleet.IsInCombat || Fleet.Movement != null)
                return;

            if (context.Assessment.GetFleetPlanet(Fleet)?.InstanceID == TargetPlanet.InstanceID)
            {
                Fleet.Order.Status = FleetOrderStatus.Ready;
                return;
            }

            if (context.Movement == null)
                return;

            Fleet.Order.Status = FleetOrderStatus.Readying;
            context.Movement.RequestMove(Fleet, TargetPlanet);
        }

        /// <summary>
        /// Creates or updates the fleet's defense order.
        /// </summary>
        private void EnsureOrder()
        {
            FleetOrder order = Fleet.Order;
            if (
                order?.OrderType == FleetOrderType.Defend
                && order.TargetPlanetId == TargetPlanet.InstanceID
            )
                return;

            Fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Defend,
                Status = FleetOrderStatus.Staging,
                TargetPlanetId = TargetPlanet.InstanceID,
            };
        }

        /// <summary>
        /// Returns whether the defense proposal still matches live game state.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when defense remains valid.</returns>
        private bool IsStillValid(AITurnContext context)
        {
            if (!IsOwnedBy(context, Fleet) || !IsOwnedBy(context, TargetPlanet))
                return false;

            if (Fleet.RoleType != FleetRoleType.Battle || !Fleet.HasOperationalCapitalShips())
                return false;

            if (
                !context.Assessment.IsPriorityDefensePlanet(TargetPlanet)
                && context.Assessment.GetRequiredPlanetDefenseStrength(TargetPlanet) <= 0
            )
                return false;

            FleetOrder order = Fleet.Order;
            return order == null
                || order.OrderType == FleetOrderType.Defend
                    && order.TargetPlanetId == TargetPlanet.InstanceID
                || order.Status == FleetOrderStatus.Staging
                    && Fleet.Movement == null
                    && !Fleet.IsInCombat;
        }

        /// <summary>
        /// Clears the proposal's order when it remains attached to the fleet.
        /// </summary>
        private void ClearOrder()
        {
            if (
                Fleet?.Order?.OrderType == FleetOrderType.Defend
                && Fleet.Order.TargetPlanetId == TargetPlanet?.InstanceID
            )
                Fleet.Order = null;
        }
    }
}
