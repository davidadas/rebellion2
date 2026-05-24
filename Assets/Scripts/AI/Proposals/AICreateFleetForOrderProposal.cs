using System.Collections.Generic;
using Rebellion.AI.Director;
using Rebellion.Game.Units;
using Rebellion.Game.World;

namespace Rebellion.AI.Proposals
{
    /// <summary>
    /// Proposal to create a fleet with an initial order.
    /// </summary>
    public sealed class AICreateFleetForOrderProposal : AIProposal
    {
        /// <summary>
        /// Creates a fleet creation proposal.
        /// </summary>
        /// <param name="factionId">Faction that will own the fleet.</param>
        /// <param name="stagingPlanet">Planet where the fleet will be created.</param>
        /// <param name="orderType">Order assigned to the fleet.</param>
        /// <param name="status">Initial order status.</param>
        /// <param name="targetPlanet">Planet targeted by the order.</param>
        public AICreateFleetForOrderProposal(
            string factionId,
            Planet stagingPlanet,
            FleetOrderType orderType,
            FleetOrderStatus status,
            Planet targetPlanet
        )
        {
            FactionId = factionId;
            StagingPlanet = stagingPlanet;
            OrderType = orderType;
            Status = status;
            TargetPlanet = targetPlanet;
        }

        public string FactionId { get; }

        public Planet StagingPlanet { get; }

        public FleetOrderType OrderType { get; }

        public FleetOrderStatus Status { get; }

        public Planet TargetPlanet { get; }

        /// <summary>
        /// Returns claims that prevent duplicate fleet creation for the same order target.
        /// </summary>
        /// <returns>Claim keys for this proposal.</returns>
        public override IReadOnlyList<string> GetClaimKeys()
        {
            List<string> claimKeys = new List<string>();

            if (!string.IsNullOrEmpty(FactionId))
                claimKeys.Add($"fleet:create:{FactionId}:{OrderType}");

            if (TargetPlanet != null)
            {
                claimKeys.Add($"fleet:create-target:{OrderType}:{TargetPlanet.InstanceID}");

                if (OrderType == FleetOrderType.Attack)
                    claimKeys.Add($"fleet:attack-target:{TargetPlanet.InstanceID}");
            }

            return claimKeys;
        }

        /// <summary>
        /// Returns a stable sort key for fleet creation proposals.
        /// </summary>
        /// <returns>A stable sort key.</returns>
        public override string GetSortKey()
        {
            return string.Join(
                ":",
                "create-fleet",
                FactionId,
                OrderType,
                Status,
                StagingPlanet?.InstanceID,
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
        /// Creates the fleet and assigns its initial order.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public override void Execute(AITurnContext context)
        {
            if (!CanExecute(context))
                return;

            Fleet fleet = context.Faction.CreateFleet(roleType: FleetRoleType.Battle);
            fleet.Order = new FleetOrder
            {
                OrderType = OrderType,
                Status = Status,
                TargetPlanetId = TargetPlanet.InstanceID,
            };

            context.Game.AttachNode(fleet, StagingPlanet);
        }

        /// <summary>
        /// Returns whether the fleet creation proposal still has valid inputs.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True if the proposal is still valid.</returns>
        private bool IsStillValid(AITurnContext context)
        {
            if (
                context?.Game == null
                || context.Faction == null
                || StagingPlanet == null
                || TargetPlanet == null
                || string.IsNullOrEmpty(FactionId)
                || context.Faction.InstanceID != FactionId
            )
                return false;

            if (StagingPlanet.GetOwnerInstanceID() != FactionId)
                return false;

            if (!StagingPlanet.IsColonized || StagingPlanet.IsDestroyed)
                return false;

            if (OrderType == FleetOrderType.Attack)
                return IsValidAttackFleetCreation(context);

            return false;
        }

        /// <summary>
        /// Returns whether an attack fleet can still be created for the target.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True if an attack fleet can be created.</returns>
        private bool IsValidAttackFleetCreation(AITurnContext context)
        {
            string targetOwnerId = TargetPlanet.GetOwnerInstanceID();
            if (string.IsNullOrEmpty(targetOwnerId) || targetOwnerId == FactionId)
                return false;

            if (context.Assessment.IdleBattleFleets.Count > 0)
                return false;

            return !HasAttackFleetForTarget(context);
        }

        /// <summary>
        /// Returns whether another attack fleet already targets this planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True if an attack fleet already targets this planet.</returns>
        private bool HasAttackFleetForTarget(AITurnContext context)
        {
            foreach (Fleet fleet in context.Assessment.AttackOrderedFleets)
            {
                if (fleet.Order?.TargetPlanetId == TargetPlanet.InstanceID)
                    return true;
            }

            return false;
        }
    }
}
