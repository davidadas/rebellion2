using System.Collections.Generic;
using Rebellion.AI.Director;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.AI.Proposals
{
    /// <summary>
    /// Proposal to transfer a unit between containers.
    /// </summary>
    public sealed class AITransferUnitProposal : AIProposal
    {
        /// <summary>
        /// Creates a unit transfer proposal.
        /// </summary>
        /// <param name="sourceContainer">Container currently holding the unit.</param>
        /// <param name="destination">Container that should receive the unit.</param>
        /// <param name="unit">Unit to transfer.</param>
        /// <param name="targetFleet">Fleet being reinforced.</param>
        /// <param name="targetPlanet">Attack target associated with the transfer.</param>
        public AITransferUnitProposal(
            ISceneNode sourceContainer,
            ISceneNode destination,
            IManufacturable unit,
            Fleet targetFleet,
            Planet targetPlanet
        )
        {
            SourceContainer = sourceContainer;
            Destination = destination;
            Unit = unit;
            TargetFleet = targetFleet;
            TargetPlanet = targetPlanet;
        }

        public ISceneNode SourceContainer { get; }

        public ISceneNode Destination { get; }

        public IManufacturable Unit { get; }

        public Fleet TargetFleet { get; }

        public Planet TargetPlanet { get; }

        /// <summary>
        /// Returns claims that prevent incompatible unit transfers.
        /// </summary>
        /// <returns>Claim keys for this proposal.</returns>
        public override IReadOnlyList<string> GetClaimKeys()
        {
            List<string> claimKeys = new List<string>();

            if (Unit != null)
                claimKeys.Add($"unit:transfer:{Unit.InstanceID}");

            if (SourceContainer != null)
            {
                claimKeys.Add($"container:transfer-source:{SourceContainer.InstanceID}");

                if (SourceContainer is Fleet sourceFleet)
                    claimKeys.Add($"fleet:order:{sourceFleet.InstanceID}");
            }

            if (Destination != null)
            {
                claimKeys.Add($"container:transfer-target:{Destination.InstanceID}");

                if (Destination is Fleet targetFleet)
                    claimKeys.Add($"fleet:transfer-target:{targetFleet.InstanceID}");
            }

            return claimKeys;
        }

        /// <summary>
        /// Returns a stable sort key for transfer proposals.
        /// </summary>
        /// <returns>A stable sort key.</returns>
        public override string GetSortKey()
        {
            return string.Join(
                ":",
                "transfer-unit",
                SourceContainer?.InstanceID,
                Destination?.InstanceID,
                Unit?.InstanceID,
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
            return IsStillValid(context) && context.Movement != null;
        }

        /// <summary>
        /// Requests the unit transfer.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public override void Execute(AITurnContext context)
        {
            if (!CanExecute(context))
                return;

            context.Movement.RequestMove(Unit, Destination);
        }

        /// <summary>
        /// Returns whether the transfer proposal still has valid inputs.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True if the transfer is still valid.</returns>
        private bool IsStillValid(AITurnContext context)
        {
            if (
                context?.Faction == null
                || SourceContainer == null
                || Destination == null
                || Unit == null
                || TargetPlanet == null
            )
                return false;

            if (SourceContainer == Destination)
                return false;

            if (SourceContainer.GetOwnerInstanceID() != context.Faction.InstanceID)
                return false;

            if (Destination.GetOwnerInstanceID() != context.Faction.InstanceID)
                return false;

            if (Unit.GetOwnerInstanceID() != context.Faction.InstanceID)
                return false;

            if (Unit.GetParent() != SourceContainer)
                return false;

            if (Unit.Movement != null || Unit.ManufacturingStatus != ManufacturingStatus.Complete)
                return false;

            if (SourceContainer is Fleet sourceFleet && !CanMoveFromSourceFleet(sourceFleet))
                return false;

            if (TargetFleet != null && !CanMoveToTargetFleet())
                return false;

            string targetOwnerId = TargetPlanet.GetOwnerInstanceID();
            return !string.IsNullOrEmpty(targetOwnerId)
                && targetOwnerId != context.Faction.InstanceID;
        }

        /// <summary>
        /// Returns whether a source fleet may donate units.
        /// </summary>
        /// <param name="sourceFleet">Fleet currently holding the unit.</param>
        /// <returns>True if the source fleet may donate units.</returns>
        private bool CanMoveFromSourceFleet(Fleet sourceFleet)
        {
            if (sourceFleet.Movement != null || sourceFleet.IsInCombat)
                return false;

            return sourceFleet.Order == null;
        }

        /// <summary>
        /// Returns whether the target fleet can receive this transfer.
        /// </summary>
        /// <returns>True if the target fleet can receive the unit.</returns>
        private bool CanMoveToTargetFleet()
        {
            if (TargetFleet.Movement != null || TargetFleet.IsInCombat)
                return false;

            if (TargetFleet.Order?.OrderType != FleetOrderType.Attack)
                return false;

            return TargetFleet.Order.TargetPlanetId == TargetPlanet.InstanceID;
        }
    }
}
