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

        /// <inheritdoc />
        public override IReadOnlyList<string> GetClaimKeys()
        {
            return Fleet == null
                ? new List<string>()
                : new List<string> { AIClaimKeys.FleetOrder(Fleet.InstanceID) };
        }

        /// <inheritdoc />
        public override string GetSortKey()
        {
            return $"fleet-clear-order:{Fleet?.InstanceID}";
        }

        /// <inheritdoc />
        public override bool CanSelect(AITurnContext context)
        {
            return IsStillValid(context);
        }

        /// <inheritdoc />
        public override bool CanExecute(AITurnContext context)
        {
            return IsStillValid(context);
        }

        /// <inheritdoc />
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
