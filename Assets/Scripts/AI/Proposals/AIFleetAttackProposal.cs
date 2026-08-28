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
        public Fleet Fleet { get; }

        public FleetOrderType OrderType { get; }

        public FleetOrderStatus Status { get; }

        public Planet TargetPlanet { get; }

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

            if (OrderType == FleetOrderType.Attack)
            {
                claimKeys.Add(AIClaimKeys.FleetAttack(Fleet.InstanceID));

                if (Fleet.Order == null)
                    claimKeys.Add(AIClaimKeys.NewOffensiveOrder(Fleet.GetOwnerInstanceID()));

                if (TargetPlanet != null)
                    claimKeys.Add(AIClaimKeys.FleetAttackTarget(TargetPlanet.InstanceID));

                if (Fleet.GetParentOfType<Planet>()?.InstanceID != TargetPlanet.InstanceID)
                    claimKeys.Add(AIClaimKeys.FleetMovement(Fleet.InstanceID));

                if (
                    TargetPlanet != null
                    && Fleet.GetParentOfType<Planet>()?.InstanceID == TargetPlanet.InstanceID
                )
                    claimKeys.Add(AIClaimKeys.PlanetAttack(TargetPlanet.InstanceID));
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
            if (!CanExecute(context))
            {
                ClearOrder();
                return;
            }

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

            Planet liveTarget = ResolveLiveTarget(context);
            bool isAtTarget =
                Fleet.GetParentOfType<Planet>()?.InstanceID == TargetPlanet.InstanceID;
            if (isAtTarget && TryClearCompletedAttackOrder(context, liveTarget))
                return;

            if (!isAtTarget)
            {
                if (!IsReadyToLaunch(context))
                {
                    Fleet.Order.Status = FleetOrderStatus.Building;
                    return;
                }

                MoveToTarget(context, TargetPlanet);
                return;
            }

            if (!IsLiveTargetHostile(context, liveTarget))
            {
                ClearOrder();
                return;
            }

            if (Fleet.Order.Status != FleetOrderStatus.Ready)
            {
                Fleet.Order.Status = FleetOrderStatus.Ready;
                return;
            }

            List<Fleet> attackingFleets = new List<Fleet> { Fleet };
            bool canBombard =
                context.Bombardment?.CanExecute(
                    attackingFleets,
                    liveTarget,
                    BombardmentType.Military
                ) == true;
            bool canDamageMilitaryTargets =
                canBombard
                && context.Assessment.GetFleetBombardmentStrength(Fleet)
                    > BombardmentSystem.GetBombardmentShieldStrength(liveTarget);
            bool shouldBombardMilitaryTargets =
                canDamageMilitaryTargets
                && BombardmentSystem.HasActiveMilitaryTargets(
                    liveTarget,
                    liveTarget.GetOwnerInstanceID()
                );

            if (!shouldBombardMilitaryTargets && ShouldAssault(context, liveTarget))
            {
                ExecuteAssault(context, liveTarget);
                return;
            }

            if (!canDamageMilitaryTargets)
                return;

            BombardmentResult bombardmentResult = context.Bombardment.Execute(
                attackingFleets,
                liveTarget,
                BombardmentType.Military
            );
            context.AddResult(bombardmentResult);
            context.AddResults(bombardmentResult.Events);
            context.AddResult(bombardmentResult.OwnershipChange);

            if (!TryClearCompletedAttackOrder(context, liveTarget))
            {
                if (!IsLiveTargetHostile(context, liveTarget))
                    ClearOrder();
            }
        }

        /// <summary>
        /// Executes a planetary assault with this fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="liveTarget">The current scene-graph target.</param>
        private void ExecuteAssault(AITurnContext context, Planet liveTarget)
        {
            if (context.PlanetaryAssault == null)
                return;

            PlanetaryAssaultResult assaultResult = context.PlanetaryAssault.Execute(
                new List<Fleet> { Fleet },
                liveTarget
            );
            context.AddResult(assaultResult);
            context.AddResult(assaultResult.OwnershipChange);
            TryClearCompletedAttackOrder(context, liveTarget);
        }

        /// <summary>
        /// Clears the attack order after the target changes ownership.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="liveTarget">The current scene-graph target.</param>
        /// <returns>True if the order was cleared.</returns>
        private bool TryClearCompletedAttackOrder(AITurnContext context, Planet liveTarget)
        {
            if (!IsOwnedBy(context, Fleet) || TargetPlanet == null)
                return false;

            FleetOrder order = Fleet.Order;
            if (
                order == null
                || order.OrderType != FleetOrderType.Attack
                || order.TargetPlanetId != TargetPlanet.InstanceID
            )
                return false;

            if (liveTarget?.GetOwnerInstanceID() != context.Faction.InstanceID)
                return false;

            if (context.Assessment.IsCapturedEnemyHeadquarters(liveTarget))
            {
                Fleet.Order = new FleetOrder
                {
                    OrderType = FleetOrderType.Defend,
                    Status = FleetOrderStatus.Ready,
                    TargetPlanetId = liveTarget.InstanceID,
                };
            }
            else
            {
                ClearOrder();
            }

            return true;
        }

        /// <summary>
        /// Requests movement toward the target planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="liveTarget">The current scene-graph target.</param>
        private void MoveToTarget(AITurnContext context, Planet liveTarget)
        {
            if (context.Movement == null)
                return;

            Fleet.Order.Status = FleetOrderStatus.Readying;
            context.Movement.RequestMove(Fleet, liveTarget);
        }

        /// <summary>
        /// Returns whether the fleet should attempt a planetary assault.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="liveTarget">The current scene-graph target.</param>
        /// <returns>True if the fleet should assault.</returns>
        private bool ShouldAssault(AITurnContext context, Planet liveTarget)
        {
            if (
                context.Game?.Config?.AI.EnablePlanetaryAssaults != true
                || context.PlanetaryAssault == null
            )
                return false;

            if (context.Assessment.IsAssaultBlockedByShields(liveTarget))
                return false;

            return context.Assessment.GetReadyFleetRegimentCount(Fleet) > 0
                && context.Assessment.GetReadyFleetRegimentAttackStrength(Fleet)
                    >= context.Assessment.GetRequiredAttackRegimentStrength(Fleet, liveTarget)
                && context.Assessment.GetPlanetaryAssaultSuccessPercent(Fleet, liveTarget)
                    >= context.Game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultSuccessPercent
                && context.PlanetaryAssault.CanExecute(new List<Fleet> { Fleet }, liveTarget)
                    == true;
        }

        /// <summary>
        /// Returns whether the fleet has enough force to leave staging.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True if the fleet is ready to launch.</returns>
        private bool IsReadyToLaunch(AITurnContext context)
        {
            return context.Assessment.CanFleetDepartHeadquarters(Fleet)
                && context.Assessment.IsFleetReadyToAttack(Fleet, TargetPlanet);
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
            if (order == null)
                return true;

            if (order.OrderType != OrderType)
                return false;

            if (order.TargetPlanetId == TargetPlanet.InstanceID)
                return true;

            return order.Status is FleetOrderStatus.Building or FleetOrderStatus.Staging
                && Fleet.Movement == null
                && !Fleet.IsInCombat;
        }

        /// <summary>
        /// Resolves the current target instance from the game state.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The live target planet, or null when it no longer exists.</returns>
        private Planet ResolveLiveTarget(AITurnContext context)
        {
            return context?.Game?.GetSceneNodeByInstanceID<Planet>(TargetPlanet?.InstanceID);
        }

        /// <summary>
        /// Returns whether the live target remains hostile to the acting faction.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="liveTarget">The current scene-graph target.</param>
        /// <returns>True when the target has a different owner.</returns>
        private bool IsLiveTargetHostile(AITurnContext context, Planet liveTarget)
        {
            string ownerId = liveTarget?.GetOwnerInstanceID();
            return !string.IsNullOrEmpty(ownerId) && ownerId != context.Faction.InstanceID;
        }

        /// <summary>
        /// Clears the matching order from the fleet.
        /// </summary>
        private void ClearOrder()
        {
            if (
                Fleet?.Order?.OrderType == OrderType
                && Fleet.Order.TargetPlanetId == TargetPlanet?.InstanceID
            )
                Fleet.Order = null;
        }
    }
}
