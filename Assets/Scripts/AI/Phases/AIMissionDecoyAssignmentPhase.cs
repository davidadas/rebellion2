using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;

namespace Rebellion.AI.Phases
{
    /// <summary>
    /// Assigns reserved special-forces decoys to selected officer-led hostile missions.
    /// </summary>
    public sealed class AIMissionDecoyAssignmentPhase : IAITurnPhase
    {
        /// <summary>
        /// Protects the highest-scored compatible officer missions with distinct decoys.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public void Execute(AITurnContext context)
        {
            if (context?.SelectedProposals == null)
                return;

            List<AIProposal> selected = context.SelectedProposals.ToList();
            HashSet<IMissionParticipant> claimedParticipants = selected
                .OfType<AIMissionProposal>()
                .SelectMany(proposal => proposal.Participants)
                .ToHashSet();
            List<SpecialForces> decoys = context
                .Assessment.AvailableMissionParticipants.OfType<SpecialForces>()
                .Where(unit =>
                    context.GetSpecialForcesIntent(unit) == SpecialForcesIntent.Decoy
                    && !claimedParticipants.Contains(unit)
                )
                .ToList();
            Dictionary<SpecialForces, Planet> origins = decoys.ToDictionary(
                unit => unit,
                unit => unit.GetParentOfType<Planet>()
            );
            Dictionary<AIMissionProposal, int> selectedIndexes = selected
                .Select((proposal, index) => (Proposal: proposal as AIMissionProposal, index))
                .Where(entry => entry.Proposal != null)
                .ToDictionary(entry => entry.Proposal, entry => entry.index);

            foreach (
                AIMissionProposal mission in selected
                    .OfType<AIMissionProposal>()
                    .Where(proposal => IsOfficerLedHostileMission(context, proposal))
                    .OrderByDescending(proposal => proposal.FoilProbability)
                    .ThenByDescending(proposal => proposal.Score)
                    .ThenBy(proposal => proposal.GetSortKey(), StringComparer.Ordinal)
                    .ToList()
            )
            {
                SpecialForces decoy = SelectDecoy(mission, decoys, origins);
                if (decoy == null)
                    continue;

                selected[selectedIndexes[mission]] = mission.WithDecoy(decoy);
                decoys.Remove(decoy);
            }

            foreach (SpecialForces unusedDecoy in decoys)
                context.SetSpecialForcesIntent(unusedDecoy, SpecialForcesIntent.Reserve);
            context.SetSelectedProposals(selected);
        }

        /// <summary>
        /// Returns whether a selected mission sends an officer into opposing territory.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The mission proposal to inspect.</param>
        /// <returns>True when the mission should be eligible for decoy protection.</returns>
        private static bool IsOfficerLedHostileMission(
            AITurnContext context,
            AIMissionProposal proposal
        )
        {
            return proposal.DecoyParticipants.Count == 0
                && proposal.Participant is Officer
                && context.Assessment.IsEnemyPlanet(proposal.TargetPlanet);
        }

        /// <summary>
        /// Selects the strongest compatible decoy, using travel distance as the secondary choice.
        /// </summary>
        /// <param name="mission">The selected mission requiring a decoy.</param>
        /// <param name="decoys">Unclaimed decoys available during this turn.</param>
        /// <param name="origins">Cached origin planets for the available decoys.</param>
        /// <returns>The preferred decoy, or null when no compatible unit is available.</returns>
        private static SpecialForces SelectDecoy(
            AIMissionProposal mission,
            IEnumerable<SpecialForces> decoys,
            IReadOnlyDictionary<SpecialForces, Planet> origins
        )
        {
            SpecialForces selected = null;
            foreach (SpecialForces candidate in decoys)
            {
                if (!candidate.CanPerformMission(mission.MissionTypeID))
                    continue;

                if (selected == null || IsPreferred(candidate, selected, mission, origins))
                    selected = candidate;
            }

            return selected;
        }

        /// <summary>
        /// Returns whether one compatible decoy is preferable to the current selection.
        /// </summary>
        /// <param name="candidate">The candidate decoy.</param>
        /// <param name="selected">The currently preferred decoy.</param>
        /// <param name="mission">The mission requiring a decoy.</param>
        /// <param name="origins">Cached origin planets for the available decoys.</param>
        /// <returns>True when the candidate should replace the current selection.</returns>
        private static bool IsPreferred(
            SpecialForces candidate,
            SpecialForces selected,
            AIMissionProposal mission,
            IReadOnlyDictionary<SpecialForces, Planet> origins
        )
        {
            int ratingComparison = candidate
                .GetEffectiveRating(OfficerRating.Espionage)
                .CompareTo(selected.GetEffectiveRating(OfficerRating.Espionage));
            if (ratingComparison != 0)
                return ratingComparison > 0;

            double candidateDistance = GetDistance(origins[candidate], mission.TargetPlanet);
            double selectedDistance = GetDistance(origins[selected], mission.TargetPlanet);
            int distanceComparison = candidateDistance.CompareTo(selectedDistance);
            return distanceComparison != 0
                ? distanceComparison < 0
                : string.Compare(
                    candidate.InstanceID,
                    selected.InstanceID,
                    StringComparison.Ordinal
                ) < 0;
        }

        /// <summary>
        /// Returns the raw distance between two planets.
        /// </summary>
        /// <param name="origin">The origin planet.</param>
        /// <param name="target">The target planet.</param>
        /// <returns>The raw distance, or the maximum value when either planet is absent.</returns>
        private static double GetDistance(Planet origin, Planet target)
        {
            return origin != null && target != null
                ? origin.GetRawDistanceTo(target)
                : double.MaxValue;
        }
    }
}
