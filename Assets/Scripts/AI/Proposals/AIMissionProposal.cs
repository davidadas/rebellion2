using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.SceneGraph;

namespace Rebellion.AI.Proposals
{
    /// <summary>
    /// Proposal to start a mission for a participant team.
    /// </summary>
    public sealed class AIMissionProposal : AIProposal
    {
        /// <summary>
        /// Gives recruitment first claim on its personnel before competing assignments.
        /// </summary>
        internal override AIProposalPriority Priority =>
            MissionTypeID == MissionTypeIDs.Recruitment
                ? AIProposalPriority.Recruitment
                : base.Priority;

        // Cached Identity.
        private readonly string[] _sortKeyParts;
        private string _sortKey;

        // Participants.
        public IReadOnlyList<IMissionParticipant> Participants { get; }

        public IReadOnlyList<IMissionParticipant> MainParticipants { get; }

        public IReadOnlyList<IMissionParticipant> DecoyParticipants { get; }

        public IMissionParticipant Participant => MainParticipants.FirstOrDefault();

        // Mission Definition.
        public string MissionTypeID { get; }
        public Planet TargetPlanet { get; }
        public Officer TargetOfficer { get; }

        public ISceneNode SelectedTarget { get; }

        public ResearchDiscipline? Discipline { get; }

        // Mission Assessment.
        public double FoilProbability { get; private set; }

        internal bool IsTeamFinal { get; }

        internal bool IsInterception { get; }

        internal MissionOdds OperationalOdds { get; private set; }

        /// <summary>
        /// Creates a mission proposal.
        /// </summary>
        /// <param name="mainParticipants">Participants assigned to execute the mission.</param>
        /// <param name="missionTypeId">Mission type ID to start.</param>
        /// <param name="targetPlanet">Planet targeted by the mission.</param>
        /// <param name="selectedTarget">Object selected inside the mission planet.</param>
        /// <param name="targetOfficer">Officer targeted by the mission.</param>
        /// <param name="discipline">Research discipline advanced by the mission.</param>
        /// <param name="decoyParticipants">Participants assigned to distract mission defenders.</param>
        /// <param name="teamIsFinal">Whether the planner has evaluated the complete participant roles.</param>
        /// <param name="isInterception">Whether dated mission intelligence supplies the target window.</param>
        public AIMissionProposal(
            IEnumerable<IMissionParticipant> mainParticipants,
            string missionTypeId,
            Planet targetPlanet,
            ISceneNode selectedTarget = null,
            Officer targetOfficer = null,
            ResearchDiscipline? discipline = null,
            IEnumerable<IMissionParticipant> decoyParticipants = null,
            bool teamIsFinal = false,
            bool isInterception = false
        )
        {
            MainParticipants =
                mainParticipants?.Where(participant => participant != null).ToList()
                ?? new List<IMissionParticipant>();
            DecoyParticipants =
                decoyParticipants?.Where(participant => participant != null).ToList()
                ?? new List<IMissionParticipant>();
            Participants = MainParticipants.Concat(DecoyParticipants).ToList();
            MissionTypeID = missionTypeId;
            TargetPlanet = targetPlanet;
            SelectedTarget = selectedTarget;
            TargetOfficer = targetOfficer;
            Discipline = discipline;
            IsTeamFinal = teamIsFinal;
            IsInterception = isInterception;
            _sortKeyParts = BuildSortKeyParts();
        }

        /// <summary>
        /// Returns a stable sort key for mission selection.
        /// </summary>
        /// <returns>A stable sort key.</returns>
        public override string GetSortKey()
        {
            return _sortKey ??= string.Join(":", _sortKeyParts);
        }

        /// <summary>
        /// Captures immutable identity components before deterministic ordering is requested.
        /// </summary>
        /// <returns>The eagerly evaluated proposal identity components.</returns>
        private string[] BuildSortKeyParts()
        {
            return new[]
            {
                "mission",
                MissionTypeID,
                TargetPlanet?.InstanceID,
                TargetOfficer?.InstanceID,
                SelectedTarget?.InstanceID,
                Discipline?.ToString(),
                string.Join(",", MainParticipants.Select(participant => participant.InstanceID)),
                string.Join(",", DecoyParticipants.Select(participant => participant.InstanceID)),
            };
        }

        /// <summary>
        /// Returns whether this mission proposal may be selected.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True if this mission proposal may be selected.</returns>
        public override bool CanSelect(AITurnContext context)
        {
            if (!IsStillValid())
                return false;

            foreach (IMissionParticipant participant in Participants)
            {
                if (
                    participant is SpecialForces unit
                    && !context.CanAssignManufacturingInterceptor(unit, MissionTypeID, TargetPlanet)
                )
                    return false;
            }

            if (
                MissionTypeID == MissionTypeIDs.Assassination
                && !context.Assessment.IsHostileMissionAtOwnedPlanet(TargetPlanet, TargetOfficer)
            )
                return false;

            foreach (IMissionParticipant participant in Participants)
            {
                if (
                    participant is Officer officer
                    && !context.Assessment.CanAssignOfficerToPlanet(officer, TargetPlanet)
                )
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Returns whether this mission can still be created.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>True if this mission can still be created.</returns>
        public override bool CanExecute(AITurnContext context)
        {
            if (context?.Missions == null || !CanSelect(context))
                return false;
            if (
                MissionTypeID == MissionTypeIDs.Assassination
                && context
                    .Game.GetSceneNodeByInstanceID<Planet>(TargetPlanet.InstanceID)
                    ?.GetOwnerInstanceID() != context.Faction.InstanceID
            )
                return false;

            if (
                IsInterception
                && (
                    !context.Assessment.TryGetOfficerMissionWindow(
                        TargetOfficer,
                        out int arrival,
                        out _,
                        out _
                    )
                    || arrival > 0
                    || !TryGetInterceptionTiming(
                        context,
                        EstimateOutboundTravelTicks(context),
                        out _,
                        out _
                    )
                )
            )
                return false;
            return context.MissionQueries.CanCreateMission(CreateContext());
        }

        /// <summary>
        /// Starts the mission if it still passes validation.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        public override void Execute(AITurnContext context)
        {
            if (!CanExecute(context))
                return;

            context.Missions.InitiateMission(CreateContext());
        }

        /// <summary>
        /// Creates an equivalent proposal with an additional decoy assigned.
        /// </summary>
        /// <param name="decoy">The participant assigned as the decoy.</param>
        /// <returns>A copy of this proposal containing the additional decoy assignment.</returns>
        internal AIMissionProposal WithAdditionalDecoy(IMissionParticipant decoy)
        {
            AIMissionProposal proposal = new AIMissionProposal(
                MainParticipants,
                MissionTypeID,
                TargetPlanet,
                SelectedTarget,
                TargetOfficer,
                Discipline,
                DecoyParticipants.Concat(new[] { decoy }),
                IsTeamFinal,
                IsInterception
            );
            if (HasScore)
                proposal.SetScore(Score);
            proposal.FoilProbability = FoilProbability;
            return proposal;
        }

        /// <summary>
        /// Records the assessed probability that known defenders foil this mission.
        /// </summary>
        /// <param name="probability">The assessed foil probability.</param>
        internal void SetFoilProbability(double probability)
        {
            FoilProbability = probability;
        }

        /// <summary>
        /// Records evaluated odds for this immutable team during the current planning turn.
        /// </summary>
        /// <param name="odds">The operational odds for the proposal's exact participant roles.</param>
        internal void SetOperationalOdds(MissionOdds odds)
        {
            OperationalOdds = odds;
        }

        /// <summary>
        /// Estimates outbound and grouped return travel for this complete mission team.
        /// </summary>
        /// <param name="context">The current faction turn.</param>
        /// <param name="departure">An optional common future departure planet.</param>
        /// <returns>The time until every participant could finish its return journey.</returns>
        internal long EstimateTravelTicks(AITurnContext context, Planet departure = null)
        {
            return EstimateOutboundTravelTicks(context, departure)
                + EstimateReturnTravelTicks(context, departure);
        }

        /// <summary>
        /// Estimates the travel until every participant reaches the mission objective.
        /// </summary>
        /// <param name="context">The current faction turn.</param>
        /// <param name="departure">An optional common future departure planet.</param>
        /// <returns>The slowest participant's outbound travel duration.</returns>
        internal long EstimateOutboundTravelTicks(AITurnContext context, Planet departure = null)
        {
            if (Participants.Count == 0)
                return Enumerable.Empty<int>().Max();

            int maximum = int.MinValue;
            foreach (IMissionParticipant participant in Participants)
            {
                Planet origin = departure ?? participant.GetParentOfType<Planet>();
                int transitTicks =
                    origin.InstanceID == TargetPlanet.InstanceID
                        ? 0
                        : context.MovementQueries.CalculateTransitTicks(
                            participant,
                            origin,
                            TargetPlanet
                        );
                maximum = System.Math.Max(maximum, transitTicks);
            }
            return maximum;
        }

        /// <summary>
        /// Estimates grouped travel from the mission planet back to the team's departure containers.
        /// </summary>
        /// <param name="context">The current faction turn.</param>
        /// <param name="departure">An optional common future departure planet.</param>
        /// <returns>The time until every surviving participant could return.</returns>
        internal long EstimateReturnTravelTicks(AITurnContext context, Planet departure = null)
        {
            if (departure != null)
                return EstimateGroupReturnTravelTicks(context, Participants, departure);

            int returning = 0;
            foreach (
                IGrouping<string, IMissionParticipant> group in Participants.GroupBy(participant =>
                    departure?.InstanceID ?? participant.GetParent()?.InstanceID
                )
            )
            {
                returning = System.Math.Max(
                    returning,
                    EstimateGroupReturnTravelTicks(context, group.Cast<IMovable>().ToArray())
                );
            }
            return returning;
        }

        /// <summary>
        /// Estimates one movement group's return using its shared personnel travel modifier.
        /// </summary>
        /// <param name="context">The current faction turn.</param>
        /// <param name="participants">The complete movement group in participant order.</param>
        /// <param name="departure">An optional common return planet instead of each participant's current planet.</param>
        /// <returns>The slowest participant's return duration, or zero for an empty group.</returns>
        private int EstimateGroupReturnTravelTicks(
            AITurnContext context,
            IReadOnlyList<IMovable> participants,
            Planet departure = null
        )
        {
            if (participants.Count == 0)
                return 0;

            string departureId = departure?.InstanceID;
            int percent = MovementQueries.GetPersonnelGroupTravelTimePercent(participants);
            int returning = 0;
            foreach (IMovable participant in participants)
            {
                Planet destination = departure ?? participant.GetParentOfType<Planet>();
                if ((departureId ?? destination.InstanceID) != TargetPlanet.InstanceID)
                    returning = System.Math.Max(
                        returning,
                        context.MovementQueries.CalculateTransitTicks(
                            participant,
                            TargetPlanet,
                            destination,
                            percent
                        )
                    );
            }
            return returning;
        }

        /// <summary>
        /// Checks whether travel, waiting, and execution fit the observed target's remaining stay.
        /// </summary>
        /// <param name="context">The current turn and observed mission timing.</param>
        /// <param name="outboundTicks">Travel until the complete team reaches the objective.</param>
        /// <param name="waitingTicks">Additional waiting needed for the target to arrive.</param>
        /// <param name="confidence">The remaining window discounted by the report's age.</param>
        /// <param name="preparationTicks">Time until personnel are ready to start from their departure planet.</param>
        /// <returns>True when the expected objective completes no later than the target departs.</returns>
        internal bool TryGetInterceptionTiming(
            AITurnContext context,
            long outboundTicks,
            out long waitingTicks,
            out double confidence,
            long preparationTicks = 0
        )
        {
            waitingTicks = 0;
            confidence = 0;
            if (
                !IsInterception
                || !context.Assessment.TryGetOfficerMissionWindow(
                    TargetOfficer,
                    out int arrival,
                    out int remaining,
                    out int age
                )
            )
                return false;
            waitingTicks = System.Math.Max(0, arrival - preparationTicks);
            double execution = context.MissionQueries.EstimateExecutionTicks(
                MissionTypeID,
                context.Faction.InstanceID
            );
            if (preparationTicks + waitingTicks + outboundTicks + execution > remaining)
                return false;
            confidence = (double)remaining / ((long)remaining + age);
            return true;
        }

        /// <summary>
        /// Returns whether the proposal's actors and targets are still usable.
        /// </summary>
        /// <returns>True if the proposal is still valid.</returns>
        private bool IsStillValid()
        {
            if (MainParticipants.Count == 0 || TargetPlanet == null)
                return false;

            foreach (IMissionParticipant participant in Participants)
            {
                if (!IsParticipantAvailable(participant))
                    return false;
            }

            if (MissionTypeID == MissionTypeIDs.Research && !Discipline.HasValue)
                return false;

            if (RequiresTargetOfficer() && TargetOfficer == null)
                return false;

            if (MissionTypeID == MissionTypeIDs.Sabotage && SelectedTarget == null)
                return false;

            return IsTargetOfficerAvailable();
        }

        /// <summary>
        /// Returns whether this proposal requires an officer target.
        /// </summary>
        /// <returns>True if this mission requires an officer target.</returns>
        private bool RequiresTargetOfficer()
        {
            return MissionTypeID == MissionTypeIDs.Abduction
                || MissionTypeID == MissionTypeIDs.Assassination
                || MissionTypeID == MissionTypeIDs.Rescue;
        }

        /// <summary>
        /// Creates the mission context represented by the proposal.
        /// </summary>
        /// <returns>The mission context.</returns>
        internal MissionContext CreateContext()
        {
            return new MissionContext
            {
                MissionTypeID = MissionTypeID,
                Location = TargetPlanet,
                SelectedTarget = SelectedTarget,
                Discipline = Discipline,
                MainParticipants = MainParticipants.ToList(),
                DecoyParticipants = DecoyParticipants.ToList(),
            };
        }

        /// <summary>
        /// Returns whether a participant can still perform this mission.
        /// </summary>
        /// <param name="participant">The mission participant.</param>
        /// <returns>True when the participant remains available and qualified.</returns>
        private bool IsParticipantAvailable(IMissionParticipant participant)
        {
            if (
                participant?.IsMovable() != true
                || participant.IsOnMission()
                || participant.GetTransitMovement() != null
            )
                return false;

            if (participant is Officer officer && (officer.IsCaptured || officer.IsKilled))
                return false;

            if (
                participant is SpecialForces specialForces
                && specialForces.ManufacturingStatus != ManufacturingStatus.Complete
            )
                return false;

            return participant.CanPerformMission(MissionTypeID);
        }

        /// <summary>
        /// Returns whether the targeted officer remains valid for this mission.
        /// </summary>
        /// <returns>True when no officer is required or the target remains eligible.</returns>
        private bool IsTargetOfficerAvailable()
        {
            if (TargetOfficer == null)
                return true;

            if (TargetOfficer.IsKilled)
                return false;

            return MissionTypeID == MissionTypeIDs.Rescue
                ? TargetOfficer.IsCaptured
                : !TargetOfficer.IsCaptured;
        }
    }
}
