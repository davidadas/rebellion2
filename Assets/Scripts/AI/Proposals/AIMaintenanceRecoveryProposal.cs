using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.SceneGraph;

namespace Rebellion.AI.Proposals
{
    /// <summary>
    /// Retires a selector-resolved set of units to restore maintenance headroom.
    /// </summary>
    public sealed class AIMaintenanceRecoveryProposal : AIProposal
    {
        internal override AIProposalPriority Priority => AIProposalPriority.MaintenanceRecovery;
        internal IReadOnlyList<Candidate> Candidates { get; }
        internal IReadOnlyList<Candidate> SelectedUnits { get; }

        /// <summary>
        /// Creates a recovery request from current legal retirement alternatives.
        /// </summary>
        /// <param name="candidates">The alternatives with their contributions to current needs.</param>
        internal AIMaintenanceRecoveryProposal(IReadOnlyList<Candidate> candidates)
            : this(candidates, Array.Empty<Candidate>()) { }

        /// <summary>
        /// Creates a request or its exact selector-resolved retirement action.
        /// </summary>
        /// <param name="candidates">The original alternatives.</param>
        /// <param name="selectedUnits">The units selected for retirement.</param>
        private AIMaintenanceRecoveryProposal(
            IReadOnlyList<Candidate> candidates,
            IReadOnlyList<Candidate> selectedUnits
        )
        {
            Candidates = candidates;
            SelectedUnits = selectedUnits;
        }

        /// <summary>
        /// Returns a stable key shared by the request and its resolved action.
        /// </summary>
        /// <returns>The recovery request key.</returns>
        public override string GetSortKey() => "maintenance-recovery";

        /// <summary>
        /// Returns whether the faction currently needs deliberate recovery.
        /// </summary>
        /// <param name="context">The current faction turn.</param>
        /// <returns>True while recovery is enabled and maintenance is negative.</returns>
        public override bool CanSelect(AITurnContext context)
        {
            return context?.Maintenance != null
                && context.Game.Config.AI.Selection.MaintenanceRecovery.Enabled
                && ResourceProductionQueries.CalculateMaintenanceHeadroom(
                    context.Game,
                    context.Faction
                ) < 0
                && Candidates.Count > 0;
        }

        /// <summary>
        /// Revalidates the resolved units against live state before executing.
        /// </summary>
        /// <param name="context">The current faction turn.</param>
        /// <returns>True when at least one chosen unit remains available.</returns>
        public override bool CanExecute(AITurnContext context)
        {
            return CanSelect(context)
                && SelectedUnits.Any(candidate => CanRetire(context, candidate.Unit));
        }

        /// <summary>
        /// Scraps the chosen units in order, stopping when the recovery target is met.
        /// </summary>
        /// <param name="context">The current faction turn.</param>
        public override void Execute(AITurnContext context)
        {
            if (!CanExecute(context))
                return;

            int target = Math.Max(
                0,
                context.Game.Config.AI.Selection.MaintenanceRecovery.TargetHeadroom
            );
            foreach (Candidate candidate in SelectedUnits)
            {
                if (
                    ResourceProductionQueries.CalculateMaintenanceHeadroom(
                        context.Game,
                        context.Faction
                    ) >= target
                )
                    break;
                if (CanRetire(context, candidate.Unit))
                    context.Maintenance.TryScrap(
                        new[] { candidate.Unit },
                        context.Faction.InstanceID
                    );
            }
        }

        /// <summary>
        /// Returns the exact action selected without mutating the original request.
        /// </summary>
        /// <param name="selectedUnits">The ordered units selected for retirement.</param>
        /// <returns>The executable recovery action.</returns>
        internal AIMaintenanceRecoveryProposal WithSelection(IReadOnlyList<Candidate> selectedUnits)
        {
            return new AIMaintenanceRecoveryProposal(Candidates, selectedUnits);
        }

        /// <summary>
        /// Filters unavailable units and containers whose removal could lose occupants.
        /// </summary>
        /// <param name="context">The current faction turn.</param>
        /// <param name="unit">The unit being considered.</param>
        /// <returns>True when the unit may enter the retirement choice.</returns>
        internal static bool CanRetire(AITurnContext context, IManufacturable unit)
        {
            ISceneNode node = unit;
            if (
                context?.Faction == null
                || unit == null
                || unit.GetMaintenanceCost() <= 0
                || unit.GetManufacturingStatus() != ManufacturingStatus.Complete
                || node.GetOwnerInstanceID() != context.Faction.InstanceID
                || !node.IsActive()
                || node.GetParent() == null
                || context.Game.GetSceneNodeByInstanceID<ISceneNode>(node.InstanceID) != node
                || unit is SpecialForces { IsRetired: true }
                || node.GetParentOfType<Mission>() != null
                || unit is IMissionParticipant participant && participant.IsOnMission()
                || unit is IMovable movable && movable.GetTransitMovement() != null
            )
                return false;

            Fleet fleet = node.GetParentOfType<Fleet>();
            if (fleet?.IsInCombat == true || fleet?.RoleType == FleetRoleType.Colonization)
                return false;
            if (unit is CapitalShip ship && ship.GetChildren(includeDisabled: true).Any())
                return false;
            if (
                unit is Building building
                && building.BuildingType
                    is BuildingType.Mine
                        or BuildingType.Refinery
                        or BuildingType.Headquarters
            )
                return false;

            if (
                unit is Regiment regiment
                && regiment.GetParent() is Planet planet
                && !planet.IsColonized
                && planet
                    .GetChildren<Regiment>()
                    .Count(other =>
                        other.GetOwnerInstanceID() == context.Faction.InstanceID
                        && other.ManufacturingStatus == ManufacturingStatus.Complete
                        && other.Movement == null
                    ) <= 1
            )
                return false;

            return true;
        }

        /// <summary>
        /// Holds one shared capability requirement and its assessed supply.
        /// </summary>
        internal sealed class Requirement
        {
            internal double Required { get; }
            internal double Available { get; }
            internal double Weight { get; }
            internal double ProtectedMinimum { get; }

            /// <summary>
            /// Captures the requirement before any retirements are selected.
            /// </summary>
            /// <param name="required">The desired capability.</param>
            /// <param name="available">The assessed capability supply.</param>
            /// <param name="weight">The priority of the capability.</param>
            /// <param name="protectedMinimum">The capability that must not be weakened.</param>
            internal Requirement(
                double required,
                double available,
                double weight,
                double protectedMinimum = 0
            )
            {
                Required = Math.Max(0, required);
                Available = Math.Max(0, available);
                Weight = Math.Max(0, weight);
                ProtectedMinimum = Math.Min(Available, Math.Max(0, protectedMinimum));
            }
        }

        /// <summary>
        /// Connects one retirement alternative to the requirements it supplies.
        /// </summary>
        internal sealed class Candidate
        {
            internal IManufacturable Unit { get; }
            internal IReadOnlyList<KeyValuePair<Requirement, double>> Contributions { get; }

            /// <summary>
            /// Captures a legal unit and its contribution to affected requirements.
            /// </summary>
            /// <param name="unit">The unit that may be retired.</param>
            /// <param name="contributions">The capability supplied to each affected requirement.</param>
            internal Candidate(
                IManufacturable unit,
                IReadOnlyList<KeyValuePair<Requirement, double>> contributions
            )
            {
                Unit = unit;
                Contributions = contributions;
            }
        }
    }
}
