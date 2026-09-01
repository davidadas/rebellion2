using System.Collections.Generic;
using Rebellion.AI.Director;
using Rebellion.Game.Units;

namespace Rebellion.AI.Proposals
{
    public sealed class AIClearFleetOrderProposal : AIProposal
    {
        private readonly FleetOrder _expectedOrder;

        public Fleet Fleet { get; }

        public AIClearFleetOrderProposal(Fleet fleet, FleetOrder expectedOrder)
        {
            Fleet = fleet;
            _expectedOrder = expectedOrder;
        }

        /// <summary>
        /// Returns the claim that prevents another action from modifying the fleet order.
        /// </summary>
        /// <returns>The fleet-order claim.</returns>
        public override IReadOnlyList<string> GetClaimKeys()
        {
            return Fleet == null
                ? new List<string>()
                : new List<string> { AIClaimKeys.FleetOrder(Fleet.InstanceID) };
        }

        /// <summary>
        /// Returns a stable sort key for the clear-order proposal.
        /// </summary>
        /// <returns>A stable sort key.</returns>
        public override string GetSortKey()
        {
            return $"fleet-clear-order:{Fleet?.InstanceID}";
        }

        /// <summary>
        /// Returns whether this proposal may be selected.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when the fleet still holds the order.</returns>
        public override bool CanSelect(AITurnContext context)
        {
            return IsStillValid(context);
        }

        /// <summary>
        /// Returns whether this proposal may execute against the current game state.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when the fleet order can still be cleared.</returns>
        public override bool CanExecute(AITurnContext context)
        {
            return IsStillValid(context);
        }

        /// <summary>
        /// Clears the fleet order when it remains current.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public override void Execute(AITurnContext context)
        {
            if (CanExecute(context))
                Fleet.Order = null;
        }

        /// <summary>
        /// Returns whether the recorded order still belongs to the live fleet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when the order can be cleared.</returns>
        private bool IsStillValid(AITurnContext context)
        {
            return context?.Faction != null
                && Fleet?.GetOwnerInstanceID() == context.Faction.InstanceID
                && ReferenceEquals(Fleet.Order, _expectedOrder);
        }
    }
}
