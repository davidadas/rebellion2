using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

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

            claimKeys.Add(AIClaimKeys.FleetOrder(Fleet.InstanceID));
            claimKeys.Add(AIClaimKeys.FleetColonization(Fleet.InstanceID));

            if (Fleet.Order == null)
                claimKeys.Add(AIClaimKeys.NewColonizationOrder(Fleet.GetOwnerInstanceID()));

            if (TargetPlanet != null)
                claimKeys.Add(AIClaimKeys.PlanetColonization(TargetPlanet.InstanceID));

            if (Fleet.GetParentOfType<Planet>()?.InstanceID != TargetPlanet?.InstanceID)
                claimKeys.Add(AIClaimKeys.FleetMovement(Fleet.InstanceID));

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
                CapitalShip carrier = FindCarrier(Fleet);
                if (carrier == null)
                {
                    Fleet.Order.Status = FleetOrderStatus.Staging;
                    return;
                }

                Fleet.Order.Status = FleetOrderStatus.Readying;
                if (
                    context.Movement?.TryRequestMove(
                        new ISceneNode[] { carrier },
                        TargetPlanet,
                        context.Faction.InstanceID
                    ) != true
                )
                {
                    Fleet.Order.Status = FleetOrderStatus.Staging;
                    return;
                }

                Fleet colonizationFleet = carrier.GetParentOfType<Fleet>();
                if (colonizationFleet != null && colonizationFleet != Fleet)
                {
                    colonizationFleet.RoleType = FleetRoleType.Patrol;
                    colonizationFleet.Order = Fleet.Order;
                    Fleet.Order = null;
                }
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
            if (!IsOwnedBy(context, Fleet) || TargetPlanet == null)
                return false;

            if (
                TargetPlanet.IsColonized || !string.IsNullOrEmpty(TargetPlanet.GetOwnerInstanceID())
            )
                return false;

            FleetOrder order = Fleet.Order;
            if (order == null)
                return Fleet.RoleType == FleetRoleType.Battle;

            return order.OrderType == FleetOrderType.Colonize
                && order.TargetPlanetId == TargetPlanet.InstanceID;
        }

        /// <summary>
        /// Returns the least valuable operational carrier that already holds a ready regiment.
        /// </summary>
        /// <param name="fleet">The fleet supplying the colonization task force.</param>
        /// <returns>A suitable carrier, or null when the fleet cannot colonize.</returns>
        internal static CapitalShip FindCarrier(Fleet fleet)
        {
            return fleet
                ?.GetChildren<CapitalShip>()
                .Where(ship =>
                    ship.ManufacturingStatus == ManufacturingStatus.Complete
                    && ship.Movement == null
                    && ship.GetChildren<Regiment>().Any(IsReadyRegiment)
                )
                .OrderByDescending(ship => ship.HasRole(CapitalShipRole.Transport))
                .ThenBy(ship => ship.GetCombatValue())
                .ThenBy(ship => ship.RegimentCapacity)
                .ThenBy(ship => ship.InstanceID)
                .FirstOrDefault();
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
                .Where(IsReadyRegiment)
                .OrderBy(regiment => regiment.AttackRating + regiment.DefenseRating)
                .ThenBy(regiment => regiment.InstanceID)
                .FirstOrDefault();
        }

        private static bool IsReadyRegiment(Regiment regiment)
        {
            return regiment.ManufacturingStatus == ManufacturingStatus.Complete
                && regiment.Movement == null;
        }

        private void ClearOrder()
        {
            if (
                Fleet?.Order?.OrderType == FleetOrderType.Colonize
                && Fleet.Order.TargetPlanetId == TargetPlanet?.InstanceID
            )
            {
                Fleet.Order = null;
                if (Fleet.RoleType == FleetRoleType.Patrol)
                    Fleet.RoleType = FleetRoleType.Battle;
            }
        }
    }
}
