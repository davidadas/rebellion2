using System.Collections.Generic;
using Rebellion.AI.Director;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Systems;

namespace Rebellion.AI.Proposals
{
    /// <summary>
    /// Proposal to start or advance an attack fleet order.
    /// </summary>
    public sealed class AIFleetAttackProposal : AIProposal
    {
        /// <summary>
        /// Creates an attack fleet proposal.
        /// </summary>
        /// <param name="fleet">Fleet that will receive or advance the order.</param>
        /// <param name="orderType">Order assigned to the fleet.</param>
        /// <param name="status">Initial order status.</param>
        /// <param name="targetPlanet">Planet targeted by the order.</param>
        public AIFleetAttackProposal(
            Fleet fleet,
            FleetOrderType orderType,
            FleetOrderStatus status,
            Planet targetPlanet
        )
        {
            Fleet = fleet;
            OrderType = orderType;
            Status = status;
            TargetPlanet = targetPlanet;
        }

        public Fleet Fleet { get; }

        public FleetOrderType OrderType { get; }

        public FleetOrderStatus Status { get; }

        public Planet TargetPlanet { get; }

        /// <summary>
        /// Returns claims that prevent incompatible fleet actions.
        /// </summary>
        /// <returns>Claim keys for this proposal.</returns>
        public override IReadOnlyList<string> GetClaimKeys()
        {
            List<string> claimKeys = new List<string>();

            if (Fleet == null)
                return claimKeys;

            claimKeys.Add($"fleet:order:{Fleet.InstanceID}");

            if (OrderType == FleetOrderType.Attack)
            {
                claimKeys.Add($"fleet:attack:{Fleet.InstanceID}");

                if (TargetPlanet != null)
                    claimKeys.Add($"fleet:attack-target:{TargetPlanet.InstanceID}");

                if (Fleet.GetParentOfType<Planet>() != TargetPlanet)
                    claimKeys.Add($"fleet:movement:{Fleet.InstanceID}");

                if (TargetPlanet != null && Fleet.GetParentOfType<Planet>() == TargetPlanet)
                    claimKeys.Add($"planet:attack:{TargetPlanet.InstanceID}");
            }

            return claimKeys;
        }

        /// <summary>
        /// Returns a stable sort key for attack fleet proposals.
        /// </summary>
        /// <returns>A stable sort key.</returns>
        public override string GetSortKey()
        {
            return string.Join(
                ":",
                "fleet-attack",
                Fleet?.InstanceID,
                OrderType,
                Status,
                TargetPlanet?.InstanceID
            );
        }

        /// <summary>
        /// Returns whether this proposal may be selected.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True if this proposal may be selected.</returns>
        public override bool CanSelect(AITurnContext context)
        {
            return IsStillValid(context);
        }

        /// <summary>
        /// Returns whether this proposal may execute against the current game state.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True if this proposal may execute.</returns>
        public override bool CanExecute(AITurnContext context)
        {
            return IsStillValid(context);
        }

        /// <summary>
        /// Applies or advances the fleet attack order.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public override void Execute(AITurnContext context)
        {
            if (TryClearCompletedAttackOrder(context))
                return;

            if (!CanExecute(context))
                return;

            EnsureOrder();

            if (OrderType == FleetOrderType.Attack)
                ExecuteAttackOrder(context);
        }

        /// <summary>
        /// Assigns the fleet order when it is missing or stale.
        /// </summary>
        private void EnsureOrder()
        {
            FleetOrder order = Fleet.Order;
            if (
                order != null
                && order.OrderType == OrderType
                && order.TargetPlanetId == TargetPlanet.InstanceID
            )
                return;

            Fleet.Order = new FleetOrder
            {
                OrderType = OrderType,
                Status = Status,
                TargetPlanetId = TargetPlanet.InstanceID,
            };
        }

        /// <summary>
        /// Advances the attack order through movement, bombardment, and assault.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        private void ExecuteAttackOrder(AITurnContext context)
        {
            if (Fleet.IsInCombat || Fleet.Movement != null)
                return;

            if (!IsReadyToLaunch(context))
            {
                Fleet.Order.Status = FleetOrderStatus.Staging;
                return;
            }

            if (Fleet.GetParentOfType<Planet>() != TargetPlanet)
            {
                MoveToTarget(context);
                return;
            }

            if (Fleet.Order.Status != FleetOrderStatus.Ready)
            {
                Fleet.Order.Status = FleetOrderStatus.Ready;
                return;
            }

            if (context.Combat == null)
                return;

            if (ShouldAssault(context))
            {
                ExecuteAssault(context);
                return;
            }

            BombardmentResult bombardmentResult = context.Combat.ExecuteOrbitalBombardment(
                new List<Fleet> { Fleet },
                TargetPlanet,
                BombardmentType.Military
            );
            context.AddResult(bombardmentResult);
            context.AddResults(bombardmentResult.Events);
            context.AddResult(bombardmentResult.OwnershipChange);

            if (TryClearCompletedAttackOrder(context) || !CanExecute(context))
                return;

            if (ShouldAssault(context))
                ExecuteAssault(context);
        }

        /// <summary>
        /// Executes a planetary assault with this fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        private void ExecuteAssault(AITurnContext context)
        {
            PlanetaryAssaultResult assaultResult = context.Combat.ExecutePlanetaryAssault(
                new List<Fleet> { Fleet },
                TargetPlanet
            );
            context.AddResult(assaultResult);
            context.AddResult(assaultResult.OwnershipChange);
            TryClearCompletedAttackOrder(context);
        }

        /// <summary>
        /// Clears the attack order after the target changes ownership.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True if the order was cleared.</returns>
        private bool TryClearCompletedAttackOrder(AITurnContext context)
        {
            if (context?.Faction == null || Fleet == null || TargetPlanet == null)
                return false;

            if (Fleet.GetOwnerInstanceID() != context.Faction.InstanceID)
                return false;

            FleetOrder order = Fleet.Order;
            if (
                order == null
                || order.OrderType != FleetOrderType.Attack
                || order.TargetPlanetId != TargetPlanet.InstanceID
            )
                return false;

            if (TargetPlanet.GetOwnerInstanceID() != context.Faction.InstanceID)
                return false;

            Fleet.Order = null;
            return true;
        }

        /// <summary>
        /// Requests movement toward the target planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        private void MoveToTarget(AITurnContext context)
        {
            if (context.Movement == null)
                return;

            Fleet.Order.Status = FleetOrderStatus.Readying;
            context.Movement.RequestMove(Fleet, TargetPlanet);
        }

        /// <summary>
        /// Returns whether the fleet should attempt a planetary assault.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True if the fleet should assault.</returns>
        private bool ShouldAssault(AITurnContext context)
        {
            if (context.Game?.Config?.AI.EnablePlanetaryAssaults != true)
                return false;

            int assaultDivisor = context.Game.Config.Combat.AssaultPersonnelDivisor;
            int assaultStrength = Fleet.GetAssaultStrength(assaultDivisor);
            int minimumStrength = context.Game.Config.AI.FleetDeployment.MinimumAttackStrength;
            int defenseRequirement =
                TargetPlanet.GetDefenseStrength()
                * context.Game.Config.AI.FleetDeployment.AttackStrengthPercentOfDefense
                / 100;
            int requiredStrength = System.Math.Max(minimumStrength, defenseRequirement);

            return assaultStrength >= requiredStrength
                && context.Assessment.GetReadyFleetRegimentCount(Fleet) > 0;
        }

        /// <summary>
        /// Returns whether the fleet has enough force to leave staging.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True if the fleet is ready to launch.</returns>
        private bool IsReadyToLaunch(AITurnContext context)
        {
            return context.Assessment.IsFleetReadyToAttack(Fleet, TargetPlanet);
        }

        /// <summary>
        /// Returns whether the attack proposal still has valid inputs.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True if the proposal is still valid.</returns>
        private bool IsStillValid(AITurnContext context)
        {
            if (context?.Faction == null || Fleet == null || TargetPlanet == null)
                return false;

            if (Fleet.GetOwnerInstanceID() != context.Faction.InstanceID)
                return false;

            if (OrderType == FleetOrderType.Attack)
                return IsValidAttackOrder(context);

            return false;
        }

        /// <summary>
        /// Returns whether the target is still a valid attack target.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True if the attack target is still valid.</returns>
        private bool IsValidAttackOrder(AITurnContext context)
        {
            string targetOwnerId = TargetPlanet.GetOwnerInstanceID();
            if (string.IsNullOrEmpty(targetOwnerId) || targetOwnerId == context.Faction.InstanceID)
                return false;

            FleetOrder order = Fleet.Order;
            return order == null
                || order.OrderType == OrderType && order.TargetPlanetId == TargetPlanet.InstanceID;
        }
    }
}
