using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.AI.Proposals
{
    public sealed class AIColonizationProposal : AIProposal
    {
        public Fleet Fleet { get; }

        public FleetOrderStatus Status { get; }

        public Planet TargetPlanet { get; }

        public AIColonizationProposal(Fleet fleet, FleetOrderStatus status, Planet targetPlanet)
        {
            Fleet = fleet;
            Status = status;
            TargetPlanet = targetPlanet;
        }

        /// <summary>
        /// Returns claims that prevent incompatible fleet actions.
        /// </summary>
        /// <returns>Claim keys for this proposal.</returns>
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

        /// <summary>
        /// Returns a stable sort key for the colonization proposal.
        /// </summary>
        /// <returns>A stable sort key.</returns>
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

        /// <summary>
        /// Returns whether this proposal may be selected.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when the colonization order remains valid.</returns>
        public override bool CanSelect(AITurnContext context)
        {
            return IsStillValid(context);
        }

        /// <summary>
        /// Returns whether this proposal may execute against the current game state.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when the colonization order can still execute.</returns>
        public override bool CanExecute(AITurnContext context)
        {
            return IsStillValid(context);
        }

        /// <summary>
        /// Starts or advances the colonization order.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
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
                if (FindCarrier(Fleet) == null)
                {
                    Fleet.Order.Status = FleetOrderStatus.Staging;
                    return;
                }

                Fleet.Order.Status = FleetOrderStatus.Readying;
                if (context.Movement == null)
                {
                    Fleet.Order.Status = FleetOrderStatus.Staging;
                    return;
                }

                context.Movement.RequestMove(Fleet, TargetPlanet);
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

        /// <summary>
        /// Creates or updates the fleet's colonization order.
        /// </summary>
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

        /// <summary>
        /// Returns whether the proposal still matches live game state.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when colonization remains valid.</returns>
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
                return Fleet.RoleType == FleetRoleType.Colonization;

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

        /// <summary>
        /// Resolves the live target represented by the proposal snapshot.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The live target, or null.</returns>
        private Planet ResolveLiveTarget(AITurnContext context)
        {
            return context.Game?.GetSceneNodeByInstanceID<Planet>(TargetPlanet.InstanceID);
        }

        /// <summary>
        /// Returns whether a planet can be claimed by colonization.
        /// </summary>
        /// <param name="planet">Planet to inspect.</param>
        /// <returns>True when the planet can be claimed.</returns>
        private static bool CanClaim(Planet planet)
        {
            return planet?.IsColonized == false
                && string.IsNullOrEmpty(planet.GetOwnerInstanceID());
        }

        /// <summary>
        /// Returns a ready regiment carried by the colonization fleet.
        /// </summary>
        /// <returns>The ready regiment, or null.</returns>
        private Regiment GetReadyRegiment()
        {
            return Fleet
                .GetRegiments()
                .Where(IsReadyRegiment)
                .OrderBy(regiment => regiment.AttackRating + regiment.DefenseRating)
                .ThenBy(regiment => regiment.InstanceID)
                .FirstOrDefault();
        }

        /// <summary>
        /// Returns whether a regiment is ready for colonization duty.
        /// </summary>
        /// <param name="regiment">Regiment to inspect.</param>
        /// <returns>True when the regiment is ready.</returns>
        private static bool IsReadyRegiment(Regiment regiment)
        {
            return regiment.ManufacturingStatus == ManufacturingStatus.Complete
                && regiment.Movement == null;
        }

        /// <summary>
        /// Clears the proposal's order when it remains attached to the fleet.
        /// </summary>
        private void ClearOrder()
        {
            if (
                Fleet?.Order?.OrderType == FleetOrderType.Colonize
                && Fleet.Order.TargetPlanetId == TargetPlanet?.InstanceID
            )
            {
                Fleet.Order = null;
            }
        }
    }
}
