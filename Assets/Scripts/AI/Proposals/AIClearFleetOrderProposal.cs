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

        public override IReadOnlyList<string> GetClaimKeys()
        {
            return Fleet == null
                ? new List<string>()
                : new List<string> { AIClaimKeys.FleetOrder(Fleet.InstanceID) };
        }

        public override string GetSortKey()
        {
            return $"fleet-clear-order:{Fleet?.InstanceID}";
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
            if (CanExecute(context))
                Fleet.Order = null;
        }

        private bool IsStillValid(AITurnContext context)
        {
            return context?.Faction != null
                && Fleet?.GetOwnerInstanceID() == context.Faction.InstanceID
                && ReferenceEquals(Fleet.Order, _expectedOrder);
        }
    }
}
