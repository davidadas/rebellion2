using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Proposals;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

namespace Rebellion.AI.Phases
{
    /// <summary>
    /// Executes proposals selected for the turn.
    /// </summary>
    public sealed class AIExecutionPhase : IAIIncrementalTurnPhase
    {
        /// <summary>
        /// Executes selected proposals that still pass validation.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public void Execute(AITurnContext context)
        {
            foreach (object _ in ExecuteIncrementally(context)) { }
        }

        /// <summary>
        /// Executes selected proposals one at a time.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>A sequence containing one marker per executed proposal.</returns>
        public IEnumerable<object> ExecuteIncrementally(AITurnContext context)
        {
            if (context?.SelectedProposals == null)
                yield break;

            ApplyCommandAssignments(context);

            foreach (AIProposal proposal in context.SelectedProposals)
            {
                if (proposal?.CanExecute(context) == true)
                    proposal.Execute(context);
                yield return proposal;
            }
        }

        /// <summary>
        /// Resigns officers leaving on selected missions, then moves and appoints the officers
        /// reserved to command selected attack fleets.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        private static void ApplyCommandAssignments(AITurnContext context)
        {
            if (context.OfficerCommands == null || context.Faction == null)
                return;

            List<Officer> missionOfficers = context
                .SelectedProposals.OfType<AIMissionProposal>()
                .SelectMany(proposal => proposal.Participants)
                .OfType<Officer>()
                .Distinct()
                .ToList();
            HashSet<string> missionOfficerIds = missionOfficers
                .Select(officer => officer.InstanceID)
                .ToHashSet();
            foreach (Officer officer in missionOfficers)
            {
                if (officer.CurrentRank != OfficerRank.None)
                {
                    context.OfficerCommands.TrySetRank(
                        officer.InstanceID,
                        OfficerRank.None,
                        context.Faction.InstanceID
                    );
                }
            }

            foreach (
                (
                    Officer assignedOfficer,
                    Fleet assignedFleet,
                    OfficerRank rank
                ) in context.CommandAssignments
            )
            {
                Officer officer = assignedOfficer;
                Fleet fleet = assignedFleet;
                if (
                    officer == null
                    || fleet == null
                    || missionOfficerIds.Contains(officer.InstanceID)
                    || fleet.GetOwnerInstanceID() != context.Faction.InstanceID
                    || officer.GetOwnerInstanceID() != context.Faction.InstanceID
                )
                    continue;

                if (officer.GetParentOfType<Fleet>() != fleet)
                {
                    Planet officerPlanet = officer.GetParentOfType<Planet>();
                    Planet fleetPlanet = fleet.GetParentOfType<Planet>();
                    if (
                        officerPlanet == null
                        || fleetPlanet == null
                        || officerPlanet.InstanceID != fleetPlanet.InstanceID
                        || context.Movement?.TryRequestMove(officer, fleet) != true
                    )
                        continue;
                }

                if (officer.CurrentRank != rank)
                {
                    context.OfficerCommands.TrySetRank(
                        officer.InstanceID,
                        rank,
                        context.Faction.InstanceID
                    );
                }
            }
        }
    }
}
