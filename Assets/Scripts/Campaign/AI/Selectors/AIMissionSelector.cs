using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Campaign.AI.Proposals;
using Rebellion.Campaign.AI.Scorers;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Units;

namespace Rebellion.Campaign.AI.Selectors
{
    /// <summary>
    /// Assigns decoys to hostile missions and requires partners for special-forces teams.
    /// </summary>
    internal sealed class AIMissionSelector
    {
        private const int _maximumDecoysPerMission = 2;
        private readonly AISelectionState _state;
        private readonly Dictionary<Officer, Planet> _diplomacyCommitments = new();
        private readonly List<IMissionParticipant> _decoys = new();
        private readonly AIMissionProposalScorer _scorer = new();
        private readonly AITurnContext _selectionContext;
        private readonly Dictionary<
            (string MissionType, Planet Target, bool OfficerLed),
            IReadOnlyList<IMissionParticipant>
        > _compatibleParticipants;
        private readonly Dictionary<
            (IReadOnlyList<IMissionParticipant> Participants, long TravelTicks),
            IMissionParticipant
        > _preferredDecoys;

        /// <summary>
        /// Creates a mission selector using shared typed reservation state.
        /// </summary>
        /// <param name="state">Shared selection reservation state.</param>
        /// <param name="selectionContext">Optional unchanged selection scope; reservations remain live.</param>
        internal AIMissionSelector(AISelectionState state, AITurnContext selectionContext = null)
        {
            _state = state;
            _selectionContext = selectionContext;
            if (selectionContext != null)
            {
                _compatibleParticipants = new();
                _preferredDecoys = new();
            }
        }

        /// <summary>
        /// Assembles a hostile mission's protection before its personnel can be reserved.
        /// </summary>
        /// <param name="context">The current faction turn.</param>
        /// <param name="proposal">The scored mission opportunity.</param>
        /// <param name="resolved">The complete team to score and consider for selection.</param>
        /// <returns>True when the mission has an available, executable team.</returns>
        internal bool TryResolve(
            AITurnContext context,
            AIMissionProposal proposal,
            out AIMissionProposal resolved
        )
        {
            resolved = proposal;
            SpecialForces partner = FindLocalInterceptionPartner(context, proposal);
            if (partner != null)
            {
                resolved = new AIMissionProposal(
                    proposal.MainParticipants.Append(partner),
                    proposal.MissionTypeID,
                    proposal.TargetPlanet,
                    proposal.SelectedTarget,
                    proposal.TargetOfficer,
                    proposal.Discipline,
                    proposal.DecoyParticipants,
                    teamIsFinal: true,
                    isInterception: true
                );
                if (!_state.CanReserve(GetReservations(resolved)) || !resolved.CanExecute(context))
                    return false;
                resolved.SetScore(_scorer.Score(context, resolved));
                return true;
            }
            if (
                proposal.IsTeamFinal
                || !context.Assessment.IsEnemyPlanet(proposal.TargetPlanet)
                || proposal.Participant is not (Officer or SpecialForces)
            )
                return true;

            if (!_state.CanReserve(GetReservations(proposal)) || !proposal.CanExecute(context))
                return false;

            bool officerLed = proposal.Participant is Officer;
            IReadOnlyList<IMissionParticipant> compatible = GetCompatibleParticipants(
                context,
                proposal
            );
            IReadOnlyDictionary<IMissionParticipant, Planet> origins = context
                .Assessment
                .MissionParticipantOrigins;
            List<IMissionParticipant> decoys = _decoys;
            decoys.Clear();
            decoys.AddRange(proposal.DecoyParticipants);
            int maximumDecoys = officerLed ? _maximumDecoysPerMission : 1;
            long teamTravelTicks = proposal.EstimateOutboundTravelTicks(context);
            while (decoys.Count < maximumDecoys)
            {
                IMissionParticipant decoy = SelectDecoy(
                    context,
                    proposal,
                    decoys,
                    origins,
                    teamTravelTicks,
                    compatible
                );
                if (decoy == null)
                    break;
                decoys.Add(decoy);
                teamTravelTicks = Math.Max(
                    teamTravelTicks,
                    context.MovementQueries.CalculateTransitTicks(
                        decoy,
                        origins[decoy],
                        proposal.TargetPlanet
                    )
                );
            }
            if (decoys.Count == 0 && (!officerLed || ExceedsUnprotectedFoilRisk(context, proposal)))
                return false;

            resolved = new AIMissionProposal(
                proposal.MainParticipants,
                proposal.MissionTypeID,
                proposal.TargetPlanet,
                proposal.SelectedTarget,
                proposal.TargetOfficer,
                proposal.Discipline,
                decoys,
                teamIsFinal: true,
                isInterception: proposal.IsInterception
            );
            if (!resolved.CanExecute(context))
                return false;

            resolved.SetScore(_scorer.Score(context, resolved));
            return true;
        }

        /// <summary>
        /// Captures fixed role and target eligibility while leaving claims and team exclusions to each resolution.
        /// </summary>
        /// <param name="context">The current faction turn.</param>
        /// <param name="proposal">The mission whose compatible personnel are required.</param>
        /// <returns>Compatible personnel in their assessment order.</returns>
        private IReadOnlyList<IMissionParticipant> GetCompatibleParticipants(
            AITurnContext context,
            AIMissionProposal proposal
        )
        {
            bool officerLed = proposal.Participant is Officer;
            var key = (proposal.MissionTypeID, proposal.TargetPlanet, officerLed);
            bool scoped = _selectionContext != null && ReferenceEquals(_selectionContext, context);
            if (scoped && _compatibleParticipants.TryGetValue(key, out var cached))
                return cached;
            List<IMissionParticipant> compatible = new();
            foreach (
                IMissionParticipant participant in context.Assessment.AvailableMissionParticipants
            )
            {
                if (
                    participant is Officer officer
                        && !context.Assessment.CanAssignOfficerToPlanet(
                            officer,
                            proposal.TargetPlanet
                        )
                    || (
                        officerLed
                            ? !IsAvailableDecoy(context, participant)
                            : participant is not SpecialForces
                    )
                    || participant is SpecialForces unit
                        && (
                            !unit.CanPerformMission(proposal.MissionTypeID)
                            || !context.CanAssignManufacturingInterceptor(
                                unit,
                                proposal.MissionTypeID,
                                proposal.TargetPlanet
                            )
                        )
                )
                    continue;
                compatible.Add(participant);
            }
            if (scoped)
                _compatibleParticipants.Add(key, compatible);
            return compatible;
        }

        /// <summary>
        /// Checks the live mission and transfer claims for one compatible participant.
        /// </summary>
        /// <param name="participant">The personnel whose claims are checked.</param>
        /// <returns>True when neither assignment resource has been reserved.</returns>
        private bool CanReserveParticipant(IMissionParticipant participant)
        {
            string id = participant.InstanceID;
            return string.IsNullOrEmpty(id)
                || _state.CanReserve(
                    new AISelectionReservation(AISelectionReservationKind.MissionActor, id)
                )
                    && _state.CanReserve(
                        new AISelectionReservation(AISelectionReservationKind.UnitTransfer, id)
                    );
        }

        /// <summary>
        /// Finds a second available local guard for an unfinalized manufacturing-hub abduction.
        /// </summary>
        /// <param name="context">The current faction turn.</param>
        /// <param name="proposal">The mission whose main team may be paired.</param>
        /// <returns>The strongest unreserved local partner, or null when no pairing applies.</returns>
        private SpecialForces FindLocalInterceptionPartner(
            AITurnContext context,
            AIMissionProposal proposal
        )
        {
            if (
                proposal.IsTeamFinal
                || !proposal.IsInterception
                || proposal.MissionTypeID != MissionTypeIDs.Abduction
                || proposal.MainParticipants.Count != 1
                || proposal.Participant is not SpecialForces guard
                || !context.IsManufacturingInterceptionAgent(guard)
                || proposal.TargetPlanet.GetOwnerInstanceID() != context.Faction.InstanceID
            )
                return null;

            return context
                .Assessment.AvailableMissionParticipants.OfType<SpecialForces>()
                .Where(unit =>
                    !proposal.Participants.Contains(unit)
                    && context.IsManufacturingInterceptionAgent(unit)
                    && unit.GetParentOfType<Planet>()?.InstanceID
                        == proposal.TargetPlanet.InstanceID
                    && unit.CanPerformMission(MissionTypeIDs.Abduction)
                    && _state.CanReserve(GetParticipantReservations(new[] { unit }))
                )
                .OrderByDescending(unit => unit.GetEffectiveRating(SkillRating.Combat))
                .ThenBy(unit => unit.InstanceID)
                .FirstOrDefault();
        }

        /// <summary>
        /// Checks fixed officer teams before reserving mission resources.
        /// </summary>
        /// <param name="context">Current AI turn context.</param>
        /// <param name="proposal">Proposal to select.</param>
        /// <returns>True when the proposal is valid and nonconflicting.</returns>
        internal bool TrySelect(AITurnContext context, AIProposal proposal)
        {
            if (
                proposal is AIMissionProposal { IsTeamFinal: true } completeTeam
                && completeTeam.MissionTypeID == MissionTypeIDs.Sabotage
                && completeTeam.DecoyParticipants.Count == 0
            )
                return false;

            List<AISelectionReservation> reservations = GetReservations(proposal);
            if (!_state.CanReserve(reservations) || proposal?.CanSelect(context) != true)
                return false;

            if (
                proposal is AIMissionProposal mission
                && mission.Participants.OfType<Officer>().Any()
                && (
                    mission.IsTeamFinal
                    || mission.Participant is Officer
                        && !context.Assessment.IsEnemyPlanet(mission.TargetPlanet)
                )
            )
            {
                MissionOdds odds = context.Assessment.GetMissionOdds(
                    context.MissionQueries,
                    mission.CreateContext()
                );
                if (
                    odds == null
                    || odds.PersonnelLossProbability
                        > context
                            .Game
                            .Config
                            .AI
                            .MissionPlanning
                            .MaximumOfficerMissionLossProbability
                )
                    return false;
            }

            _state.Reserve(reservations);
            if (proposal is AIMissionProposal { MissionTypeID: MissionTypeIDs.Diplomacy } diplomacy)
                foreach (Officer officer in diplomacy.MainParticipants.OfType<Officer>())
                    _diplomacyCommitments[officer] = diplomacy.TargetPlanet;
            return true;
        }

        /// <summary>
        /// Refreshes remaining diplomacy choices against the current mission reservations.
        /// </summary>
        /// <param name="context">The current faction turn.</param>
        /// <param name="pending">The unprocessed proposal queue.</param>
        internal void RefreshDiplomacyScores(AITurnContext context, LinkedList<AIProposal> pending)
        {
            AIMissionProposalScorer scorer = new();
            LinkedListNode<AIProposal> node = pending.First;
            while (node != null)
            {
                LinkedListNode<AIProposal> next = node.Next;
                if (
                    node.Value is AIMissionProposal
                    {
                        MissionTypeID: MissionTypeIDs.Diplomacy
                    } diplomacy
                )
                {
                    if (!_state.CanReserve(GetReservations(diplomacy)))
                        pending.Remove(node);
                    else
                        diplomacy.SetScore(scorer.Score(context, diplomacy, _diplomacyCommitments));
                }
                node = next;
            }
        }

        /// <summary>
        /// Returns typed reservations for a mission-domain proposal.
        /// </summary>
        /// <param name="proposal">Proposal to inspect.</param>
        /// <returns>Reservations required by the proposal.</returns>
        internal static List<AISelectionReservation> GetReservations(AIProposal proposal)
        {
            List<AISelectionReservation> reservations = new();
            if (proposal is AIMissionStagingProposal staging)
            {
                reservations.AddRange(
                    GetParticipantReservations(
                        staging.IsWaiting ? staging.Participants : staging.Mission.Participants
                    )
                );
                if (staging.Mission.IsInterception)
                    Add(
                        reservations,
                        AISelectionReservationKind.MissionOfficer,
                        staging.Mission.TargetOfficer.InstanceID
                    );
                else if (staging.Mission.MissionTypeID == MissionTypeIDs.Sabotage)
                    Add(
                        reservations,
                        AISelectionReservationKind.MissionTarget,
                        staging.Mission.SelectedTarget?.InstanceID
                    );
                return reservations;
            }
            if (proposal is AIAbortMissionProposal abort)
            {
                Add(reservations, AISelectionReservationKind.Mission, abort.Mission?.InstanceID);
                return reservations;
            }

            if (proposal is not AIMissionProposal mission)
                return reservations;

            reservations.AddRange(GetParticipantReservations(mission.Participants));

            if (mission.MissionTypeID == MissionTypeIDs.Recruitment)
                Add(
                    reservations,
                    AISelectionReservationKind.MissionRecruitment,
                    mission.Participant?.OwnerInstanceID
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
            else if (mission.MissionTypeID != MissionTypeIDs.Research)
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
        /// Identifies the actor and movement resources needed by mission personnel.
        /// </summary>
        /// <param name="participants">The personnel used by a mission or preparation action.</param>
        /// <returns>The reservations that prevent competing assignments of those personnel.</returns>
        private static IEnumerable<AISelectionReservation> GetParticipantReservations(
            IEnumerable<IMissionParticipant> participants
        )
        {
            foreach (IMissionParticipant participant in participants)
            {
                if (string.IsNullOrEmpty(participant?.InstanceID))
                    continue;
                yield return new AISelectionReservation(
                    AISelectionReservationKind.MissionActor,
                    participant.InstanceID
                );
                yield return new AISelectionReservation(
                    AISelectionReservationKind.UnitTransfer,
                    participant.InstanceID
                );
            }
        }

        /// <summary>
        /// Validates selected teams with the same assignment rules and marks unused decoys.
        /// </summary>
        /// <param name="context">The faction turn containing the selected proposals.</param>
        internal void FinalizeSelection(AITurnContext context)
        {
            if (context?.SelectedProposals == null || context.Assessment == null)
                return;

            List<AIProposal> selected = context.SelectedProposals.ToList();
            IMissionParticipant[] otherAssignments = selected
                .OfType<AIMissionStagingProposal>()
                .SelectMany(proposal =>
                    proposal.IsWaiting ? proposal.Participants : proposal.Mission.Participants
                )
                .Concat(
                    selected.OfType<AIOfficerCommandProposal>().Select(proposal => proposal.Officer)
                )
                .Concat(
                    selected
                        .OfType<AIMaintenanceRecoveryProposal>()
                        .SelectMany(proposal => proposal.SelectedUnits)
                        .Select(candidate => candidate.Unit)
                        .OfType<IMissionParticipant>()
                )
                .Concat(
                    selected
                        .OfType<AITransferUnitProposal>()
                        .Select(proposal => proposal.Unit)
                        .OfType<IMissionParticipant>()
                )
                .ToArray();
            _state.Reserve(GetParticipantReservations(otherAssignments));
            for (int index = 0; index < selected.Count; index++)
            {
                if (selected[index] is not AIMissionProposal mission)
                    continue;
                selected[index] =
                    TryResolve(context, mission, out AIMissionProposal resolved)
                    && TrySelect(context, resolved)
                        ? resolved
                        : null;
            }
            HashSet<IMissionParticipant> claimedParticipants = selected
                .OfType<AIMissionProposal>()
                .SelectMany(proposal => proposal.Participants)
                .Concat(otherAssignments)
                .ToHashSet();
            foreach (
                SpecialForces unit in context.Assessment.AvailableMissionParticipants.OfType<SpecialForces>()
            )
                if (
                    context.GetSpecialForcesIntent(unit) == SpecialForcesIntent.Decoy
                    && !claimedParticipants.Contains(unit)
                )
                    context.SetSpecialForcesIntent(unit, SpecialForcesIntent.Reserve);
            context.SetSelectedProposals(selected);
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
        /// Returns assessed foil risk, applying the legacy age penalty only without defense estimates.
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
            if (config.MissionDefenseIntelFullWeightTicks > 0)
                return proposal.FoilProbability;
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
        /// <param name="context">The current faction turn.</param>
        /// <param name="mission">The selected mission requiring a decoy.</param>
        /// <param name="decoys">The decoys already assigned to this team.</param>
        /// <param name="origins">Cached origin planets for the available decoys.</param>
        /// <param name="teamTravelTicks">The outbound duration already needed by the team.</param>
        /// <param name="compatible">The fixed compatible pool before reservations and team exclusions.</param>
        /// <returns>The preferred decoy, or null when no compatible unit is available.</returns>
        private IMissionParticipant SelectDecoy(
            AITurnContext context,
            AIMissionProposal mission,
            IReadOnlyList<IMissionParticipant> decoys,
            IReadOnlyDictionary<IMissionParticipant, Planet> origins,
            long teamTravelTicks,
            IReadOnlyList<IMissionParticipant> compatible
        )
        {
            if (_selectionContext != null && ReferenceEquals(_selectionContext, context))
            {
                var key = (compatible, teamTravelTicks);
                if (
                    !_preferredDecoys.TryGetValue(key, out IMissionParticipant preferred)
                    || preferred != null && !CanReserveParticipant(preferred)
                )
                {
                    preferred = FindPreferredDecoy(
                        context,
                        mission,
                        compatible.Where(CanReserveParticipant),
                        origins,
                        teamTravelTicks
                    );
                    _preferredDecoys[key] = preferred;
                }
                if (
                    preferred == null
                    || !mission.Participants.Contains(preferred) && !decoys.Contains(preferred)
                )
                    return preferred;
            }
            return FindPreferredDecoy(
                context,
                mission,
                compatible.Where(participant =>
                    !mission.Participants.Contains(participant)
                    && !decoys.Contains(participant)
                    && CanReserveParticipant(participant)
                ),
                origins,
                teamTravelTicks
            );
        }

        /// <summary>
        /// Finds the highest-ranked compatible decoy using skill, travel, and stable identity ties.
        /// </summary>
        /// <param name="context">The current faction turn.</param>
        /// <param name="mission">The mission requiring protection.</param>
        /// <param name="decoys">The ordered candidate pool.</param>
        /// <param name="origins">The candidates' captured origin planets.</param>
        /// <param name="teamTravelTicks">The duration already required by the primary team.</param>
        /// <returns>The preferred decoy, or null when none is eligible.</returns>
        private static IMissionParticipant FindPreferredDecoy(
            AITurnContext context,
            AIMissionProposal mission,
            IEnumerable<IMissionParticipant> decoys,
            IReadOnlyDictionary<IMissionParticipant, Planet> origins,
            long teamTravelTicks
        )
        {
            IMissionParticipant selected = null;
            double? selectedPriority = null;
            foreach (IMissionParticipant candidate in decoys)
            {
                if (
                    candidate is SpecialForces
                    && !candidate.CanPerformMission(mission.MissionTypeID)
                )
                    continue;

                bool candidateIsSpecialForces = candidate is SpecialForces;
                if (selected == null || candidateIsSpecialForces && selected is not SpecialForces)
                {
                    selected = candidate;
                    selectedPriority = null;
                    continue;
                }
                if (candidateIsSpecialForces != (selected is SpecialForces))
                    continue;

                double candidatePriority = AIMissionProposalScorer.GetParticipantPriority(
                    context,
                    candidate,
                    mission.TargetPlanet,
                    candidate.GetEffectiveRating(SkillRating.Espionage),
                    teamTravelTicks
                );
                selectedPriority ??= AIMissionProposalScorer.GetParticipantPriority(
                    context,
                    selected,
                    mission.TargetPlanet,
                    selected.GetEffectiveRating(SkillRating.Espionage),
                    teamTravelTicks
                );
                int ratingComparison = candidatePriority.CompareTo(selectedPriority.Value);
                if (ratingComparison < 0)
                    continue;

                if (ratingComparison == 0)
                {
                    double candidateDistance = origins[candidate]
                        .GetRawDistanceTo(mission.TargetPlanet);
                    double selectedDistance = origins[selected]
                        .GetRawDistanceTo(mission.TargetPlanet);
                    int distanceComparison = candidateDistance.CompareTo(selectedDistance);
                    if (
                        distanceComparison > 0
                        || distanceComparison == 0
                            && string.Compare(
                                candidate.InstanceID,
                                selected.InstanceID,
                                StringComparison.Ordinal
                            ) >= 0
                    )
                        continue;
                }
                selected = candidate;
                selectedPriority = candidatePriority;
            }
            return selected;
        }
    }
}
