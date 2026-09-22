using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Units;
using Rebellion.Systems;

namespace Rebellion.AI.Proposals
{
    /// <summary>
    /// Assigns the initial strategic role of an untyped fleet.
    /// </summary>
    public sealed class AIFleetRoleProposal : AIProposal
    {
        public Fleet Fleet { get; }

        public FleetRoleType Role { get; }

        public IReadOnlyList<CapitalShip> Ships { get; }

        internal override AIProposalPriority Priority => AIProposalPriority.Mandatory;

        /// <summary>
        /// Creates a role proposal for an untyped fleet.
        /// </summary>
        /// <param name="fleet">The fleet to classify.</param>
        /// <param name="role">The role to assign.</param>
        public AIFleetRoleProposal(Fleet fleet, FleetRoleType role)
        {
            Fleet = fleet;
            Role = role;
            Ships = Array.Empty<CapitalShip>();
        }

        /// <summary>
        /// Creates a proposal that extracts transports into individual colonization fleets.
        /// </summary>
        /// <param name="fleet">The untyped source fleet.</param>
        /// <param name="ships">Transport ships to extract.</param>
        public AIFleetRoleProposal(Fleet fleet, IReadOnlyList<CapitalShip> ships)
        {
            Fleet = fleet;
            Role = FleetRoleType.Colonization;
            Ships = ships ?? Array.Empty<CapitalShip>();
        }

        /// <summary>
        /// Returns the deterministic proposal key.
        /// </summary>
        /// <returns>The role and fleet identifier.</returns>
        public override string GetSortKey() => $"fleet-role:{Role}:{Fleet?.InstanceID}";

        /// <summary>
        /// Returns whether the fleet still requires classification.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when the owned fleet remains untyped.</returns>
        public override bool CanSelect(AITurnContext context) => CanAssign(context);

        /// <summary>
        /// Returns whether the role can still be assigned.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True when the owned fleet remains untyped.</returns>
        public override bool CanExecute(AITurnContext context) => CanAssign(context);

        /// <summary>
        /// Assigns the fleet role.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public override void Execute(AITurnContext context)
        {
            if (!CanAssign(context))
                return;

            if (Role != FleetRoleType.Colonization || Ships.Count == 0)
            {
                Fleet.RoleType = Role;
                return;
            }

            if (Ships.Count == 1 && Fleet.GetChildren<CapitalShip>().Count == 1)
            {
                Fleet.RoleType = FleetRoleType.Colonization;
                return;
            }

            FleetSystem fleets = new FleetSystem(context.Game);
            foreach (CapitalShip ship in Ships)
            {
                Fleet colonizationFleet = fleets.CreateFromCapitalShips(
                    new[] { ship },
                    context.Faction.InstanceID
                );
                if (colonizationFleet != null)
                    colonizationFleet.RoleType = FleetRoleType.Colonization;
            }

            if (Fleet.GetParent() != null)
                Fleet.RoleType = FleetRoleType.Battle;
        }

        /// <summary>
        /// Returns whether the ship can be assigned to the requested fleet role.
        /// </summary>
        /// <param name="context">The context value.</param>
        /// <returns>The operation result.</returns>
        private bool CanAssign(AITurnContext context)
        {
            return context?.Faction != null
                && Fleet?.GetOwnerInstanceID() == context.Faction.InstanceID
                && Fleet.RoleType == FleetRoleType.None
                && Role != FleetRoleType.None
                && Ships.All(ship => ship?.GetParent() == Fleet);
        }
    }
}
