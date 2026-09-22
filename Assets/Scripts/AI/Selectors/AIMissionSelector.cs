using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Proposals;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;

namespace Rebellion.AI.Selectors
{
    /// <summary>
    /// Assigns available decoys to officer-led hostile missions.
    /// </summary>
    internal sealed class AIMissionSelector
    {
        private const int _maximumDecoysPerMission = 2;
        private readonly AISelectionState _state;

        /// <summary>
        /// Creates a mission selector using shared typed reservation state.
        /// </summary>
        /// <param name="state">Shared selection reservation state.</param>
        internal AIMissionSelector(AISelectionState state)
        {
            _state = state;
        }

        /// <summary>
        /// Attempts to select a mission-domain proposal and reserve its resources.
        /// </summary>
        /// <param name="context">Current AI turn context.</param>
        /// <param name="proposal">Proposal to select.</param>
        /// <returns>True when the proposal is valid and nonconflicting.</returns>
        internal bool TrySelect(AITurnContext context, AIProposal proposal)
        {
            List<AISelectionReservation> reservations = GetReservations(proposal);
            if (!_state.CanReserve(reservations) || proposal?.CanSelect(context) != true)
                return false;

            _state.Reserve(reservations);
            return true;
        }

        /// <summary>
        /// Returns typed reservations for a mission-domain proposal.
        /// </summary>
        /// <param name="proposal">Proposal to inspect.</param>
        /// <returns>Reservations required by the proposal.</returns>
        private static List<AISelectionReservation> GetReservations(AIProposal proposal)
        {
            List<AISelectionReservation> reservations = new();
            if (proposal is AIAbortMissionProposal abort)
            {
                Add(reservations, AISelectionReservationKind.Mission, abort.Mission?.InstanceID);
                return reservations;
            }

            if (proposal is not AIMissionProposal mission)
                return reservations;

            foreach (IMissionParticipant participant in mission.Participants)
                Add(reservations, AISelectionReservationKind.MissionActor, participant.InstanceID);

            if (mission.MissionTypeID == MissionTypeIDs.Recruitment)
                Add(
                    reservations,
                    AISelectionReservationKind.MissionRecruitment,
                    mission.Participant?.OwnerInstanceID
                );
            else if (
                mission.MissionTypeID == MissionTypeIDs.Research
                && mission.Discipline.HasValue
            )
                Add(
                    reservations,
                    AISelectionReservationKind.MissionResearch,
                    mission.Participant?.OwnerInstanceID,
                    mission.Discipline.Value.ToString()
                );
            else if (mission.TargetOfficer != null)
                Add(
                    reservations,
                    AISelectionReservationKind.MissionOfficer,
                    mission.TargetOfficer.InstanceID
                );
            else if (mission.SelectedTarget != null)
                Add(
                    reservations,
                    AISelectionReservationKind.MissionTarget,
                    mission.SelectedTarget.InstanceID
                );
            else
                Add(
                    reservations,
                    AISelectionReservationKind.MissionAtPlanet,
                    mission.TargetPlanet?.InstanceID,
                    mission.MissionTypeID
                );

            return reservations;
        }

        /// <summary>
        /// Adds a reservation only when its primary identity exists.
        /// </summary>
        /// <param name="reservations">Reservation list to update.</param>
        /// <param name="kind">Reservation category.</param>
        /// <param name="primaryId">Primary resource identity.</param>
        /// <param name="secondaryId">Optional secondary identity.</param>
        private static void Add(
            List<AISelectionReservation> reservations,
            AISelectionReservationKind kind,
            string primaryId,
            string secondaryId = null
        )
        {
            if (!string.IsNullOrEmpty(primaryId))
                reservations.Add(new AISelectionReservation(kind, primaryId, secondaryId));
        }

        /// <summary>
        /// Assigns distinct decoys by priority.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        internal void FinalizeSelection(AITurnContext context)
        {
            if (context?.SelectedProposals == null || context.Assessment == null)
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

                selected[selectedIndexes[mission]] = mission.WithAdditionalDecoy(decoy);
                decoys.Remove(decoy);
            }

            PairRiskyOfficerMissions(selected, selectedIndexes, unprotectedRiskyMissions);
            AssignAdditionalDecoys(
                context,
                selected,
                orderedMissions,
                selectedIndexes,
                decoys,
                origins
            );
            RemoveUnsafeOfficerMissions(context, selected);

            foreach (SpecialForces unusedDecoy in decoys.OfType<SpecialForces>())
                context.SetSpecialForcesIntent(unusedDecoy, SpecialForcesIntent.Reserve);
            context.SetSelectedProposals(selected);
        }

        /// <summary>
        /// Assigns a second decoy after every eligible selected mission has had an opportunity to
        /// receive its first.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="selected">The selected proposals being finalized.</param>
        /// <param name="orderedMissions">Hostile officer missions in protection priority order.</param>
        /// <param name="selectedIndexes">Selected indexes keyed by original mission proposal.</param>
        /// <param name="decoys">Unclaimed decoys remaining after first-pass assignment.</param>
        /// <param name="origins">Cached origin planets for available decoys.</param>
        private static void AssignAdditionalDecoys(
            AITurnContext context,
            IList<AIProposal> selected,
            IReadOnlyList<AIMissionProposal> orderedMissions,
            IReadOnlyDictionary<AIMissionProposal, int> selectedIndexes,
            IList<IMissionParticipant> decoys,
            IReadOnlyDictionary<IMissionParticipant, Planet> origins
        )
        {
            foreach (AIMissionProposal originalMission in orderedMissions)
            {
                int selectedIndex = selectedIndexes[originalMission];
                if (
                    selected[selectedIndex] is not AIMissionProposal mission
                    || !CanAssignAdditionalDecoy(context, mission)
                )
                    continue;

                IMissionParticipant decoy = SelectDecoy(mission, decoys, origins);
                if (decoy == null)
                    continue;

                selected[selectedIndex] = mission.WithAdditionalDecoy(decoy);
                decoys.Remove(decoy);
            }
        }

        /// <summary>
        /// Returns whether a hostile officer mission can accept another decoy.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposal">The mission proposal to inspect.</param>
        /// <returns>True when the mission has room for another decoy.</returns>
        private static bool CanAssignAdditionalDecoy(
            AITurnContext context,
            AIMissionProposal proposal
        )
        {
            return proposal.DecoyParticipants.Count > 0
                && proposal.DecoyParticipants.Count < _maximumDecoysPerMission
                && proposal.Participant is Officer
                && context.Assessment.IsEnemyPlanet(proposal.TargetPlanet);
        }

        /// <summary>
        /// Removes officer missions whose finalized participant team exceeds the loss limit.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="selected">The selected proposals after decoy assignment.</param>
        private static void RemoveUnsafeOfficerMissions(
            AITurnContext context,
            IList<AIProposal> selected
        )
        {
            for (int index = 0; index < selected.Count; index++)
            {
                if (
                    selected[index] is not AIMissionProposal mission
                    || !mission.MainParticipants.OfType<Officer>().Any()
                )
                    continue;

                double? personnelLossProbability = mission.PersonnelLossProbability;
                if (!personnelLossProbability.HasValue)
                {
                    MissionOdds odds = context.Missions.GetMissionOdds(
                        mission.CreateRequest(),
                        context.Assessment.GetMissionDetectorCandidates(mission.TargetPlanet)
                    );
                    personnelLossProbability = odds?.PersonnelLossProbability;
                }
                if (
                    personnelLossProbability
                    > context.Game.Config.AI.MissionPlanning.MaximumOfficerMissionLossProbability
                )
                    selected[index] = null;
            }
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
                selected[selectedIndexes[protectedMission]] = protectedMission.WithAdditionalDecoy(
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
