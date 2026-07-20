using System.Collections.Generic;
using Rebellion.AI.Director;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.AI.Proposals
{
    public sealed class AIFleetDefenseProposal : AIProposal
    {
        public AIFleetDefenseProposal(Fleet fleet, Planet targetPlanet)
        {
            Fleet = fleet;
            TargetPlanet = targetPlanet;
        }

        public Fleet Fleet { get; }

        public Planet TargetPlanet { get; }

        public override IReadOnlyList<string> GetClaimKeys()
        {
            if (Fleet == null || TargetPlanet == null)
                return new List<string>();

            return new List<string>
            {
                $"fleet:order:{Fleet.InstanceID}",
                $"fleet:movement:{Fleet.InstanceID}",
                $"planet:defense:{TargetPlanet.InstanceID}",
            };
        }

        public override string GetSortKey()
        {
            return $"fleet-defense:{TargetPlanet?.InstanceID}:{Fleet?.InstanceID}";
        }

        public override bool CanSelect(AITurnContext context)
        {
            return IsStillValid(context);
        }

        public override bool CanExecute(AITurnContext context)
        {
            return IsStillValid(context);
        }

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

        private bool IsStillValid(AITurnContext context)
        {
            if (context?.Faction == null || Fleet == null || TargetPlanet == null)
                return false;

            if (
                Fleet.GetOwnerInstanceID() != context.Faction.InstanceID
                || Fleet.RoleType != FleetRoleType.Battle
                || !Fleet.HasOperationalCapitalShips()
                || TargetPlanet.GetOwnerInstanceID() != context.Faction.InstanceID
            )
                return false;

            if (
                !context.Assessment.IsFactionHeadquarters(TargetPlanet)
                && context.Assessment.GetRequiredOrbitalStrength(TargetPlanet) <= 0
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
