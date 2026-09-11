using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;

namespace Rebellion.AI.Phases
{
    /// <summary>
    /// Assigns available decoys to officer-led hostile missions.
    /// </summary>
    public sealed class AIMissionDecoyAssignmentPhase : IAITurnPhase
    {
        /// <summary>
        /// Assigns distinct decoys by priority.
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
            List<IMissionParticipant> decoys = context
                .Assessment.AvailableMissionParticipants.Where(participant =>
                    !claimedParticipants.Contains(participant)
                    && IsAvailableDecoy(context, participant)
                )
                .ToList();
            Dictionary<IMissionParticipant, Planet> origins = decoys.ToDictionary(
                participant => participant,
                participant => participant.GetParentOfType<Planet>()
            );
            Dictionary<AIMissionProposal, int> selectedIndexes = selected
                .Select((proposal, index) => (Proposal: proposal as AIMissionProposal, index))
                .Where(entry => entry.Proposal != null)
                .ToDictionary(entry => entry.Proposal, entry => entry.index);

            List<AIMissionProposal> orderedMissions = selected
                .OfType<AIMissionProposal>()
                .Where(proposal => IsOfficerLedHostileMission(context, proposal))
                .OrderByDescending(proposal => GetUnprotectedFoilRisk(context, proposal))
                .ThenByDescending(proposal => proposal.Score)
                .ThenBy(proposal => proposal.GetSortKey(), StringComparer.Ordinal)
                .ToList();
            List<AIMissionProposal> unprotectedRiskyMissions = new List<AIMissionProposal>();
            foreach (AIMissionProposal mission in orderedMissions)
            {
                IMissionParticipant decoy = SelectDecoy(mission, decoys, origins);
                if (decoy == null)
                {
                    if (ExceedsUnprotectedFoilRisk(context, mission))
                        unprotectedRiskyMissions.Add(mission);
                    continue;
                }

                selected[selectedIndexes[mission]] = mission.WithDecoy(decoy);
                decoys.Remove(decoy);
            }

            PairRiskyOfficerMissions(selected, selectedIndexes, unprotectedRiskyMissions);

            foreach (SpecialForces unusedDecoy in decoys.OfType<SpecialForces>())
                context.SetSpecialForcesIntent(unusedDecoy, SpecialForcesIntent.Reserve);
            context.SetSelectedProposals(selected);
        }

        /// <summary>
        /// Combines risky officer missions into protected two-officer teams.
        /// </summary>
        /// <param name="selected">The selected proposal collection.</param>
        /// <param name="selectedIndexes">Selected indexes keyed by mission proposal.</param>
        /// <param name="missions">Risky missions ordered from highest to lowest priority.</param>
        private static void PairRiskyOfficerMissions(
            IList<AIProposal> selected,
            IReadOnlyDictionary<AIMissionProposal, int> selectedIndexes,
            IReadOnlyList<AIMissionProposal> missions
        )
        {
            int index = 0;
            for (; index + 1 < missions.Count; index += 2)
            {
                AIMissionProposal protectedMission = missions[index];
                AIMissionProposal decoyMission = missions[index + 1];
                selected[selectedIndexes[protectedMission]] = protectedMission.WithDecoy(
                    decoyMission.Participant
                );
                selected[selectedIndexes[decoyMission]] = null;
            }

            if (index < missions.Count)
                selected[selectedIndexes[missions[index]]] = null;
        }

        /// <summary>
        /// Returns whether an unclaimed participant may support a mission as a decoy.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="participant">The participant to inspect.</param>
        /// <returns>True when the participant is available for decoy assignment.</returns>
        private static bool IsAvailableDecoy(AITurnContext context, IMissionParticipant participant)
        {
            return participant is Officer
                || participant is SpecialForces specialForces
                    && context.GetSpecialForcesIntent(specialForces) == SpecialForcesIntent.Decoy;
        }

        /// <summary>
        /// Returns whether a selected mission sends an officer into opposing territory.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The mission proposal to inspect.</param>
        /// <returns>True when the mission is eligible for an optional decoy.</returns>
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
        /// Returns the known foil probability plus the uncertainty from aging intelligence.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The unprotected mission proposal.</param>
        /// <returns>The adjusted unprotected foil risk.</returns>
        private static double GetUnprotectedFoilRisk(
            AITurnContext context,
            AIMissionProposal proposal
        )
        {
            GameConfig.AIMissionPlanningConfig config = context.Game.Config.AI.MissionPlanning;
            int refreshInterval = config.EspionageRefreshIntervalTicks;
            int intelAge = context.Assessment.GetPlanetIntelAge(proposal.TargetPlanet);
            if (refreshInterval <= 0 || intelAge == int.MaxValue)
                return 100;

            int staleIntervals = intelAge / refreshInterval;
            double agePenalty =
                staleIntervals * config.HostileMissionIntelAgeFoilPenaltyPerRefreshInterval;
            return Math.Min(100, proposal.FoilProbability + agePenalty);
        }

        /// <summary>
        /// Returns whether an officer mission is too risky to launch without a decoy.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The unprotected mission proposal.</param>
        /// <returns>True when adjusted foil risk exceeds the configured limit.</returns>
        private static bool ExceedsUnprotectedFoilRisk(
            AITurnContext context,
            AIMissionProposal proposal
        )
        {
            return GetUnprotectedFoilRisk(context, proposal)
                > context
                    .Game
                    .Config
                    .AI
                    .MissionPlanning
                    .MaximumUnprotectedOfficerMissionFoilProbability;
        }

        /// <summary>
        /// Selects the preferred compatible decoy.
        /// </summary>
        /// <param name="mission">The selected mission requiring a decoy.</param>
        /// <param name="decoys">Unclaimed decoys available during this turn.</param>
        /// <param name="origins">Cached origin planets for the available decoys.</param>
        /// <returns>The preferred decoy, or null when no compatible unit is available.</returns>
        private static IMissionParticipant SelectDecoy(
            AIMissionProposal mission,
            IEnumerable<IMissionParticipant> decoys,
            IReadOnlyDictionary<IMissionParticipant, Planet> origins
        )
        {
            IMissionParticipant selected = null;
            foreach (IMissionParticipant candidate in decoys)
            {
                if (
                    candidate is SpecialForces
                    && !candidate.CanPerformMission(mission.MissionTypeID)
                )
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
            IMissionParticipant candidate,
            IMissionParticipant selected,
            AIMissionProposal mission,
            IReadOnlyDictionary<IMissionParticipant, Planet> origins
        )
        {
            bool candidateIsSpecialForces = candidate is SpecialForces;
            bool selectedIsSpecialForces = selected is SpecialForces;
            if (candidateIsSpecialForces != selectedIsSpecialForces)
                return candidateIsSpecialForces;

            int ratingComparison = candidate
                .GetEffectiveRating(OfficerRating.Espionage)
                .CompareTo(selected.GetEffectiveRating(OfficerRating.Espionage));
            if (ratingComparison != 0)
                return ratingComparison > 0;

            double candidateDistance = origins[candidate].GetRawDistanceTo(mission.TargetPlanet);
            double selectedDistance = origins[selected].GetRawDistanceTo(mission.TargetPlanet);
            int distanceComparison = candidateDistance.CompareTo(selectedDistance);
            return distanceComparison != 0
                ? distanceComparison < 0
                : string.Compare(
                    candidate.InstanceID,
                    selected.InstanceID,
                    StringComparison.Ordinal
                ) < 0;
        }

    }
}
