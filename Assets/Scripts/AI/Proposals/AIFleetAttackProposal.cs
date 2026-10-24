using System.Collections.Generic;
using Rebellion.AI.Demands;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Results;
using Rebellion.Game.Units;
using Rebellion.Simulation;

namespace Rebellion.AI.Proposals
{
    /// <summary>
    /// Proposal to start or advance an attack fleet order.
    /// </summary>
    public sealed class AIFleetAttackProposal : AIProposal
    {
        internal override AIProposalPriority Priority =>
            Status == FleetOrderStatus.Returning
                ? AIProposalPriority.Mandatory
                : AIProposalPriority.Optional;

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

            if (Fleet.Order.Status == FleetOrderStatus.Returning)
            {
                ReturnToFriendlyTerritory(context);
                return;
            }

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
            {
                if (Status == FleetOrderStatus.Returning)
                    order.Status = FleetOrderStatus.Returning;

                return;
            }

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
            if (isAtTarget && TryCompleteAttackOrder(context, liveTarget))
                return;

            if (!isAtTarget)
            {
                if (!CanLaunch(context))
                {
                    Fleet.Order.Status = FleetOrderStatus.Building;
                    return;
                }

                MoveToTarget(context, TargetPlanet);
                return;
            }

            if (!CanEngageLiveTarget(context, liveTarget))
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
                context.Bombardment != null
                && context.BombardmentQueries?.CanExecute(
                    attackingFleets,
                    liveTarget,
                    BombardmentType.Military
                ) == true;
            bool canDamageMilitaryTargets =
                canBombard
                && context.Assessment.GetFleetBombardmentStrength(Fleet)
                    > context.Assessment.GetBombardmentShieldStrength(liveTarget);
            bool shouldBombardMilitaryTargets =
                canDamageMilitaryTargets
                && BombardmentQueries.HasActiveMilitaryTargets(
                    liveTarget,
                    liveTarget.GetOwnerInstanceID()
                );

            if (!shouldBombardMilitaryTargets && ShouldAssault(context, liveTarget))
            {
                ExecuteAssault(context, liveTarget);
                return;
            }

            if (!shouldBombardMilitaryTargets)
            {
                Fleet.Order.Status = FleetOrderStatus.Building;
                return;
            }

            context.Bombardment.TryExecute(attackingFleets, liveTarget, BombardmentType.Military);

            if (!TryCompleteAttackOrder(context, liveTarget))
            {
                if (!CanEngageLiveTarget(context, liveTarget))
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
            context.AddResults(assaultResult.Events);
            context.AddResult(assaultResult.OwnershipChange);
            TryCompleteAttackOrder(context, liveTarget);
        }

        /// <summary>
        /// Finishes an attack after capture and stages the fleet for its next planning pass.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="liveTarget">The current scene-graph target.</param>
        /// <returns>True when the attack has completed.</returns>
        private bool TryCompleteAttackOrder(AITurnContext context, Planet liveTarget)
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

            if (context.Assessment.IsPriorityDefensePlanet(liveTarget))
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
                order.Status = FleetOrderStatus.Staging;
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

            if (context.GetAttackDemand(liveTarget, Fleet)?.IsAssaultBlockedByShields != false)
                return false;

            int requiredRegimentCount = context
                .GetAttackRegimentRequirements(liveTarget, Fleet)
                .Count;
            return context.Assessment.GetReadyFleetRegimentCount(Fleet) >= requiredRegimentCount
                && context.Assessment.GetReadyFleetRegimentAttackStrength(Fleet)
                    >= context.GetAttackRegimentRequirements(liveTarget, Fleet).Strength
                && context.Assessment.GetPlanetaryAssaultSuccessPercent(Fleet, liveTarget)
                    >= context.Game.Config.AI.FleetDeployment.MinimumPlanetaryAssaultSuccessPercent
                && context.PlanetaryAssaultQueries.CanExecute(new List<Fleet> { Fleet }, liveTarget)
                    == true;
        }

        /// <summary>
        /// Returns whether the fleet has enough force to leave staging.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True if the fleet is ready to launch.</returns>
        internal bool CanLaunch(AITurnContext context)
        {
            return context.Assessment.HasCurrentHeadquartersAttackIntel(TargetPlanet)
                && context.StrategicPlan.CanFleetDepart(Fleet)
                && IsReadyToAttack(context, TargetPlanet);
        }

        /// <summary>
        /// Returns whether the fleet satisfies every live attack capability.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="targetPlanet">The attack target.</param>
        /// <returns>True when the fleet is ready to attack.</returns>
        private bool IsReadyToAttack(AITurnContext context, Planet targetPlanet)
        {
            AIAttackDemand demand = context.GetAttackDemand(targetPlanet, Fleet);
            if (demand == null)
                return false;
            int availableCombat = context.Assessment.GetReadyFleetCombatValueAgainstCapitalShips(
                Fleet
            );
            int requiredRegiments = context
                .GetAttackRegimentRequirements(targetPlanet, Fleet)
                .Count;
            return Fleet?.HasOperationalCapitalShips() == true
                && availableCombat > 0
                && availableCombat >= demand.CombatStrength
                && context.Assessment.GetReadyFleetRegimentCount(Fleet) >= requiredRegiments
                && context.Assessment.GetReadyFleetRegimentCapacity(Fleet) >= requiredRegiments
                && context.Assessment.GetReadyFleetRegimentAttackStrength(Fleet)
                    >= context.GetAttackRegimentRequirements(targetPlanet, Fleet).Strength
                && context.Assessment.GetFleetBombardmentStrength(Fleet)
                    >= demand.BombardmentStrength
                && (
                    context.Assessment.CanBombardMilitaryTargets(Fleet, targetPlanet)
                    || context.Assessment.GetPlanetaryAssaultSuccessPercent(Fleet, targetPlanet)
                        >= context
                            .Game
                            .Config
                            .AI
                            .FleetDeployment
                            .MinimumPlanetaryAssaultSuccessPercent
                );
        }

        /// <summary>
        /// Returns an attack fleet to the nearest friendly planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        private void ReturnToFriendlyTerritory(AITurnContext context)
        {
            Planet currentPlanet = Fleet.GetParentOfType<Planet>();
            if (currentPlanet?.GetOwnerInstanceID() == context.Faction.InstanceID)
            {
                ClearOrder();
                return;
            }

            AIFleetMovement.TryMoveToNearestSafeOwnedPlanet(context, Fleet);
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
            if (
                Status != FleetOrderStatus.Returning
                && Fleet.Movement == null
                && !Fleet.IsInCombat
                && !context.Assessment.HasCurrentHeadquartersAttackIntel(TargetPlanet)
            )
                return false;

            string targetOwnerId = TargetPlanet.GetOwnerInstanceID();
            if (
                targetOwnerId == context.Faction.InstanceID
                || string.IsNullOrEmpty(targetOwnerId)
                    && !context.StrategicPlan.CanCompleteOccupation(Fleet, TargetPlanet)
            )
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
        /// Returns whether the live target permits continuing the assigned attack or occupation.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="liveTarget">The current scene-graph target.</param>
        /// <returns>True for an enemy target or a neutral target awaiting this fleet's occupation.</returns>
        private bool CanEngageLiveTarget(AITurnContext context, Planet liveTarget)
        {
            string ownerId = liveTarget?.GetOwnerInstanceID();
            return !string.IsNullOrEmpty(ownerId) && ownerId != context.Faction.InstanceID
                || context.StrategicPlan.CanCompleteOccupation(Fleet, liveTarget);
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
