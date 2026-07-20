using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.AI.Proposals
{
    public sealed class AIColonizationProposal : AIProposal
    {
        public AIColonizationProposal(Fleet fleet, FleetOrderStatus status, Planet targetPlanet)
        {
            Fleet = fleet;
            Status = status;
            TargetPlanet = targetPlanet;
        }

        public Fleet Fleet { get; }

        public FleetOrderStatus Status { get; }

        public Planet TargetPlanet { get; }

        public override IReadOnlyList<string> GetClaimKeys()
        {
            List<string> claimKeys = new List<string>();
            if (Fleet == null)
                return claimKeys;

            claimKeys.Add($"fleet:order:{Fleet.InstanceID}");
            claimKeys.Add($"fleet:colonize:{Fleet.InstanceID}");

            if (Fleet.Order == null)
                claimKeys.Add($"faction:new-colonization-order:{Fleet.GetOwnerInstanceID()}");

            if (TargetPlanet != null)
                claimKeys.Add($"planet:colonize:{TargetPlanet.InstanceID}");

            if (Fleet.GetParentOfType<Planet>()?.InstanceID != TargetPlanet?.InstanceID)
                claimKeys.Add($"fleet:movement:{Fleet.InstanceID}");

            return claimKeys;
        }

        public override string GetSortKey()
        {
            return string.Join(
                ":",
                "fleet-colonize",
                Fleet?.InstanceID,
                Status,
                TargetPlanet?.InstanceID
            );
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
            EnsureOrder();

            if (Fleet.IsInCombat || Fleet.Movement != null)
                return;

            if (!Fleet.HasOperationalCapitalShips())
            {
                Fleet.Order.Status = FleetOrderStatus.Staging;
                return;
            }

            if (!context.Assessment.CanFleetDepartHeadquarters(Fleet))
            {
                Fleet.Order.Status = FleetOrderStatus.Staging;
                return;
            }

            if (Fleet.GetParentOfType<Planet>()?.InstanceID != TargetPlanet.InstanceID)
            {
                Fleet.Order.Status = FleetOrderStatus.Readying;
                context.Movement?.RequestMove(Fleet, TargetPlanet);
                return;
            }

            Planet liveTarget = ResolveLiveTarget(context);
            if (!CanClaim(liveTarget))
            {
                ClearOrder();
                return;
            }

            Regiment regiment = GetReadyRegiment();
            if (regiment == null)
            {
                Fleet.Order.Status = FleetOrderStatus.Staging;
                return;
            }

            Fleet.Order.Status = FleetOrderStatus.Ready;
            context.Movement?.RequestMove(regiment, liveTarget);

            if (liveTarget.GetOwnerInstanceID() == context.Faction.InstanceID)
                ClearOrder();
        }

        private void EnsureOrder()
        {
            FleetOrder order = Fleet.Order;
            if (
                order?.OrderType == FleetOrderType.Colonize
                && order.TargetPlanetId == TargetPlanet.InstanceID
            )
                return;

            Fleet.Order = new FleetOrder
            {
                OrderType = FleetOrderType.Colonize,
                Status = Status,
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
                || TargetPlanet.IsColonized
                || !string.IsNullOrEmpty(TargetPlanet.GetOwnerInstanceID())
            )
                return false;

            FleetOrder order = Fleet.Order;
            return order == null
                || order.OrderType == FleetOrderType.Colonize
                    && order.TargetPlanetId == TargetPlanet.InstanceID;
        }

        private Planet ResolveLiveTarget(AITurnContext context)
        {
            return context.Game?.GetSceneNodeByInstanceID<Planet>(TargetPlanet.InstanceID);
        }

        private static bool CanClaim(Planet planet)
        {
            return planet?.IsColonized == false
                && string.IsNullOrEmpty(planet.GetOwnerInstanceID());
        }

        private Regiment GetReadyRegiment()
        {
            return Fleet
                .GetRegiments()
                .Where(regiment =>
                    regiment.ManufacturingStatus == ManufacturingStatus.Complete
                    && regiment.Movement == null
                )
                .OrderBy(regiment => regiment.AttackRating + regiment.DefenseRating)
                .ThenBy(regiment => regiment.InstanceID)
                .FirstOrDefault();
        }

        private void ClearOrder()
        {
            if (
                Fleet?.Order?.OrderType == FleetOrderType.Colonize
                && Fleet.Order.TargetPlanetId == TargetPlanet?.InstanceID
            )
                Fleet.Order = null;
        }
    }
}
