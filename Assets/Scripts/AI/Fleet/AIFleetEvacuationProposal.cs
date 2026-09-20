using System.Collections.Generic;
using Rebellion.AI.Core;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.AI.Fleets
{
    /// <summary>
    /// Returns a fleet that cannot act at its hostile location to friendly territory.
    /// </summary>
    public sealed class AIFleetEvacuationProposal : AIProposal
    {
        public Fleet Fleet { get; }

        public Planet HostilePlanet { get; }

        internal override AIProposalPriority Priority => AIProposalPriority.Mandatory;

        /// <summary>
        /// Creates a proposal to evacuate a fleet from its current hostile planet.
        /// </summary>
        /// <param name="fleet">Fleet to evacuate.</param>
        /// <param name="hostilePlanet">Hostile planet currently containing the fleet.</param>
        public AIFleetEvacuationProposal(Fleet fleet, Planet hostilePlanet)
        {
            Fleet = fleet;
            HostilePlanet = hostilePlanet;
        }

        /// <summary>
        /// Returns claims preventing another proposal from ordering or moving this fleet.
        /// </summary>
        /// <returns>Fleet order and movement claims.</returns>
        public override IReadOnlyList<string> GetClaimKeys()
        {
            if (Fleet == null)
                return new List<string>();

            return new List<string>
            {
                AIClaimKeys.FleetOrder(Fleet.InstanceID),
                AIClaimKeys.FleetMovement(Fleet.InstanceID),
            };
        }

        /// <summary>
        /// Returns a stable sort key for this evacuation.
        /// </summary>
        /// <returns>Stable evacuation key.</returns>
        public override string GetSortKey()
        {
            return $"fleet-evacuation:{Fleet?.InstanceID}:{HostilePlanet?.InstanceID}";
        }

        /// <summary>
        /// Returns whether the fleet remains stationary at the recorded hostile planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when evacuation remains valid.</returns>
        public override bool CanSelect(AITurnContext context)
        {
            return IsStillValid(context);
        }

        /// <summary>
        /// Returns whether the fleet remains stationary at the recorded hostile planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when evacuation remains valid.</returns>
        public override bool CanExecute(AITurnContext context)
        {
            return IsStillValid(context);
        }

        /// <summary>
        /// Clears the fleet's stale assignment and evacuates it to friendly territory.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public override void Execute(AITurnContext context)
        {
            if (!CanExecute(context))
                return;

            Fleet.Order = null;
            context.Movement?.EvacuateToNearestFriendlyPlanet(Fleet);
        }

        /// <summary>
        /// Returns whether the proposal still matches live fleet and planet state.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when the fleet still requires evacuation.</returns>
        private bool IsStillValid(AITurnContext context)
        {
            return context?.Faction != null
                && Fleet?.GetOwnerInstanceID() == context.Faction.InstanceID
                && HostilePlanet != null
                && Fleet.Movement == null
                && !Fleet.IsInCombat
                && Fleet.GetParentOfType<Planet>()?.InstanceID == HostilePlanet.InstanceID
                && !string.IsNullOrEmpty(HostilePlanet.GetOwnerInstanceID())
                && HostilePlanet.GetOwnerInstanceID() != context.Faction.InstanceID
                && !context.AttackRequirements.CanAct(Fleet, HostilePlanet);
        }
    }
}
