using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Proposals;
using Rebellion.AI.Scorers;
using Rebellion.Game;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Random;
using OfficerRating = Rebellion.Game.Units.SkillRating;

namespace Rebellion.AI.Planners
{
    /// <summary>
    /// Builds mission proposals for available mission participants.
    /// </summary>
    public sealed class AIMissionPlanner : IAIIncrementalProposalPlanner
    {
        // Planning State.
        private readonly AIMissionCandidatePool _candidateSelector = new AIMissionCandidatePool();
        private readonly AIMissionCandidatePool _waitingCandidateSelector =
            new AIMissionCandidatePool();
        private readonly Dictionary<string, List<IManufacturable>> _sabotageTargets =
            new Dictionary<string, List<IManufacturable>>(StringComparer.Ordinal);
        private readonly HashSet<string> _activeMissionTypes = new HashSet<string>(
            StringComparer.Ordinal
        );
        private readonly HashSet<(string MissionTypeId, string PlanetId)> _activeMissionPlanets =
            new HashSet<(string MissionTypeId, string PlanetId)>();
        private readonly HashSet<string> _activeOfficerTargets = new HashSet<string>(
            StringComparer.Ordinal
        );
        private readonly HashSet<string> _activeSabotageTargets = new HashSet<string>(
            StringComparer.Ordinal
        );
        private List<Planet> _diplomacyCandidates;
        private Officer _preferredRecruiter;
        private List<Planet> _espionageCandidates;
        private List<Planet> _freshEnemyPlanets;
        private List<(Planet Planet, Officer TargetOfficer)> _officerTargetCandidates;
        private List<Planet> _recruitmentCandidates;
        private List<Planet> _sabotageCandidates;
        private readonly List<IMissionParticipant> _sabotageParticipants = new();

        // Research Disciplines.
        private readonly ResearchDiscipline[] _researchDisciplines =
        {
            ResearchDiscipline.ShipDesign,
            ResearchDiscipline.FacilityDesign,
            ResearchDiscipline.TroopTraining,
        };

        /// <summary>
        /// Returns mission proposals for the current AI turn.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Mission proposals generated for this faction.</returns>
        public List<AIProposal> Plan(AITurnContext context)
        {
            List<AIProposal> proposals = new();
            foreach (object step in PlanIncrementally(context, proposals)) { }
            return proposals;
        }

        /// <summary>
        /// Builds mission proposals with cooperative checkpoints between actors and targets.
        /// </summary>
        /// <param name="context">The current faction turn.</param>
        /// <param name="proposals">Output retained until every alternative has been considered.</param>
        /// <returns>Completed actor or sabotage-target work units in their established order.</returns>
        public IEnumerable<object> PlanIncrementally(
            AITurnContext context,
            List<AIProposal> proposals
        )
        {
            if (proposals == null)
                throw new ArgumentNullException(nameof(proposals));
            if (context?.Game == null || context.Faction == null || context.Missions == null)
                yield break;

            AssignSpecialForcesIntent(context);
            _candidateSelector.Reset();
            _waitingCandidateSelector.Reset();
            _sabotageTargets.Clear();
            _sabotageParticipants.Clear();
            BuildActiveMissionIndexes(context.Assessment.ActiveMissions);
            _diplomacyCandidates = null;
            _preferredRecruiter = null;
            _espionageCandidates = null;
            _freshEnemyPlanets = null;
            _officerTargetCandidates = null;
            _recruitmentCandidates = null;
            _sabotageCandidates = null;
            foreach (object step in CreateMissionProposals(context, proposals))
                yield return step;

            foreach (object step in CreateMissionStagingProposals(context, proposals))
                yield return step;
            foreach (object step in CreateMissionWaitingProposals(context, proposals))
                yield return step;
        }

        /// <summary>
        /// Offers a safe departure planet when several sabotage objectives justify relocating a team.
        /// </summary>
        /// <param name="context">The current faction turn.</param>
        /// <param name="proposals">The retained mission alternatives and resulting relocation choices.</param>
        /// <returns>A checkpoint after considering each retained team opportunity.</returns>
        private IEnumerable<object> CreateMissionStagingProposals(
            AITurnContext context,
            List<AIProposal> proposals
        )
        {
            HashSet<string> sustainedTargets = proposals
                .OfType<AIMissionProposal>()
                .Where(mission => mission.MissionTypeID == MissionTypeIDs.Sabotage)
                .Select(mission => mission.TargetPlanet)
                .Distinct()
                .Where(planet => GetEligibleSabotageTargets(context, planet).Take(2).Count() > 1)
                .Select(planet => planet.InstanceID)
                .ToHashSet(StringComparer.Ordinal);
            foreach (
                AIMissionProposal mission in proposals
                    .OfType<AIMissionProposal>()
                    .Where(mission =>
                        mission.IsTeamFinal
                        && mission.MissionTypeID == MissionTypeIDs.Sabotage
                        && sustainedTargets.Contains(mission.TargetPlanet.InstanceID)
                        && mission.CanSelect(context)
                        && context.Assessment.IsEnemyPlanet(mission.TargetPlanet)
                        && mission.Participants.All(participant =>
                            participant.GetParentOfType<Planet>() != null
                        )
                    )
                    .ToArray()
            )
            {
                List<(AIMissionStagingProposal Proposal, long Ticks)> candidates = new();
                foreach (Planet planet in context.Assessment.OwnedPlanets)
                {
                    if (planet.IsDestroyed || context.Assessment.HasKnownHostileFleet(planet))
                        continue;
                    AIMissionStagingProposal candidate = new(mission, planet);
                    if (candidate.Participants.Count == 0)
                        continue;
                    (long direct, long staged) = candidate.EstimateTravelTicks(context);
                    if (staged < direct)
                        candidates.Add((candidate, staged));
                }
                AIMissionStagingProposal best = candidates
                    .OrderBy(candidate => candidate.Ticks)
                    .ThenBy(
                        candidate => candidate.Proposal.Destination.InstanceID,
                        StringComparer.Ordinal
                    )
                    .Select(candidate => candidate.Proposal)
                    .FirstOrDefault(candidate => candidate.CanSelect(context));
                if (best != null)
                    proposals.Add(best);
                yield return mission;
            }
        }

        /// <summary>
        /// Offers present personnel the option to await teammates already traveling to their planet.
        /// </summary>
        /// <param name="context">The current faction turn.</param>
        /// <param name="proposals">Receives scored one-turn waiting alternatives.</param>
        /// <returns>A checkpoint after each objective and candidate leader.</returns>
        private IEnumerable<object> CreateMissionWaitingProposals(
            AITurnContext context,
            List<AIProposal> proposals
        )
        {
            List<IMissionParticipant> inbound = context
                .Faction.GetOwnedUnitsByType<Officer>()
                .Cast<IMissionParticipant>()
                .Concat(context.Assessment.OwnedSpecialForces)
                .Where(participant =>
                    participant.GetParent() is Planet planet
                    && context.Assessment.IsOwnedPlanet(planet)
                    && participant.GetTransitMovement()?.TicksRemaining() > 0
                    && !participant.IsOnMission()
                    && participant.CanPerformMission(MissionTypeIDs.Sabotage)
                    && (
                        participant is not Officer officer
                        || (
                            !officer.IsCaptured
                            && !officer.IsKilled
                            && officer.InjuryPoints == 0
                            && officer.CurrentRank == OfficerRank.None
                            && officer.GetEffectiveRating(OfficerRating.Diplomacy)
                                < context.Game.Config.AI.DiplomacyMinimumSkill
                        )
                    )
                    && (
                        participant is not SpecialForces unit
                        || unit.ManufacturingStatus == ManufacturingStatus.Complete
                    )
                )
                .OrderBy(participant => participant.GetParent()?.InstanceID, StringComparer.Ordinal)
                .ThenBy(participant => participant.InstanceID, StringComparer.Ordinal)
                .ToList();
            GameConfig.AIMissionPlanningConfig config = context.Game.Config.AI.MissionPlanning;
            List<(
                IMissionParticipant Leader,
                Planet Departure,
                IReadOnlyList<IMissionParticipant> Candidates
            )> assemblies = new();
            foreach (
                IGrouping<Planet, IMissionParticipant> arrivals in inbound.GroupBy(participant =>
                    (Planet)participant.GetParent()
                )
            )
            {
                Planet departure = arrivals.Key;
                if (context.Assessment.HasKnownHostileFleet(departure))
                    continue;
                List<IMissionParticipant> present = context
                    .Assessment.AvailableMissionParticipants.Where(participant =>
                        participant.GetParent() == departure
                        && participant.CanPerformMission(MissionTypeIDs.Sabotage)
                        && (
                            participant is SpecialForces
                            || _sabotageParticipants.Contains(participant)
                        )
                    )
                    .ToList();
                if (present.Count == 0)
                    continue;
                List<IMissionParticipant> candidates = present
                    .Concat(arrivals)
                    .OrderByDescending(participant =>
                        AIMissionProposalScorer.GetParticipantPriority(
                            context,
                            participant,
                            departure,
                            participant.GetEffectiveRating(SkillRating.Espionage)
                                + participant.GetEffectiveRating(SkillRating.Combat),
                            departure: departure
                        )
                    )
                    .ThenBy(participant => participant.InstanceID, StringComparer.Ordinal)
                    .Take(Math.Max(1, config.SabotageTeamCandidateCount))
                    .ToList();
                foreach (
                    IMissionParticipant leader in candidates.Take(
                        Math.Max(1, config.SabotageTeamAlternativesPerTarget)
                    )
                )
                    assemblies.Add((leader, departure, candidates));
            }

            foreach (Planet planet in GetSabotageCandidatePlanets(context))
            {
                var leaders = assemblies
                    .OrderByDescending(assembly =>
                        AIMissionProposalScorer.GetParticipantPriority(
                            context,
                            assembly.Leader,
                            planet,
                            assembly.Leader.GetEffectiveRating(SkillRating.Espionage)
                                + assembly.Leader.GetEffectiveRating(SkillRating.Combat),
                            departure: assembly.Departure
                        )
                    )
                    .ThenBy(assembly => assembly.Departure.GetRawDistanceTo(planet))
                    .ThenBy(assembly => assembly.Leader.InstanceID, StringComparer.Ordinal)
                    .Take(Math.Max(1, config.SabotageTeamAlternativesPerTarget))
                    .ToList();
                foreach (IManufacturable target in GetSabotageTargets(context, planet))
                foreach (var assembly in leaders)
                {
                    IReadOnlyList<IMissionParticipant> teammates =
                        assembly.Leader is Officer
                            ? assembly.Candidates
                            : assembly
                                .Candidates.Where(participant => participant is SpecialForces)
                                .ToList();
                    AddSabotageTeamProposals(
                        context,
                        planet,
                        (ISceneNode)target,
                        assembly.Leader,
                        teammates,
                        proposals,
                        assembly.Departure
                    );
                    yield return assembly.Leader;
                }
            }
        }

        /// <summary>
        /// Assigns turn-scoped primary and decoy roles to available special-forces units.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        internal static void AssignSpecialForcesIntent(AITurnContext context)
        {
            if (context?.Assessment == null)
                return;

            IReadOnlyList<IMissionParticipant> availableParticipants = context
                .Assessment
                .AvailableMissionParticipants;
            List<SpecialForces> availableSpecialForces = availableParticipants
                .OfType<SpecialForces>()
                .ToList();
            SetSpecialForcesIntent(
                context,
                availableSpecialForces,
                SpecialForcesIntent.PrimaryAgent
            );
            List<Officer> availableOfficers = availableParticipants.OfType<Officer>().ToList();
            if (availableOfficers.Count == 0)
                return;

            foreach (
                IGrouping<string, SpecialForces> roleUnits in availableSpecialForces.GroupBy(
                    GetMissionCapabilitySetKey,
                    StringComparer.Ordinal
                )
            )
            {
                List<SpecialForces> orderedRoleUnits = roleUnits
                    .OrderByDescending(context.IsLocalEspionageAgent)
                    .ThenBy(context.IsManufacturingInterceptionAgent)
                    .ToList();
                if (OfficersCoverEveryMissionCapability(availableOfficers, orderedRoleUnits[0]))
                    SetSpecialForcesIntent(
                        context,
                        orderedRoleUnits
                            .Skip(1)
                            .Where(unit =>
                                !context.IsLocalEspionageAgent(unit)
                                && !context.IsManufacturingInterceptionAgent(unit)
                            ),
                        SpecialForcesIntent.Decoy
                    );
            }
        }

        /// <summary>
        /// Assigns one turn-scoped intent to each supplied special-forces unit.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="units">Units receiving the intent.</param>
        /// <param name="intent">Intent to assign.</param>
        private static void SetSpecialForcesIntent(
            AITurnContext context,
            IEnumerable<SpecialForces> units,
            SpecialForcesIntent intent
        )
        {
            foreach (SpecialForces unit in units)
                context.SetSpecialForcesIntent(unit, intent);
        }

        /// <summary>
        /// Returns whether available officers can replace every capability in a special-forces role.
        /// </summary>
        /// <param name="officers">Officers available during this turn.</param>
        /// <param name="specialForces">Representative unit for the special-forces role.</param>
        /// <returns>True when every role mission can be performed by an officer.</returns>
        private static bool OfficersCoverEveryMissionCapability(
            IEnumerable<Officer> officers,
            SpecialForces specialForces
        )
        {
            return specialForces.AllowedMissionTypeIDs.Count > 0
                && specialForces.AllowedMissionTypeIDs.All(missionTypeId =>
                    officers.Any(officer => officer.CanPerformMission(missionTypeId))
                );
        }

        /// <summary>
        /// Returns a stable identifier for a special-forces mission role.
        /// </summary>
        /// <param name="unit">Special-forces unit to inspect.</param>
        /// <returns>The ordered mission-capability identifier.</returns>
        private static string GetMissionCapabilitySetKey(SpecialForces unit)
        {
            return string.Join(
                "|",
                unit.AllowedMissionTypeIDs.OrderBy(
                    missionTypeId => missionTypeId,
                    StringComparer.Ordinal
                )
            );
        }

        /// <summary>
        /// Creates mission proposals.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposals">The retained mission alternatives being built.</param>
        /// <returns>Checkpoints between completed actors and sabotage targets.</returns>
        private IEnumerable<object> CreateMissionProposals(
            AITurnContext context,
            List<AIProposal> proposals
        )
        {
            AddJediTrainingProposals(context, proposals);
            HashSet<IMissionParticipant> jediTrainers = proposals
                .OfType<AIMissionProposal>()
                .Where(proposal => proposal.MissionTypeID == MissionTypeIDs.JediTraining)
                .Select(proposal => proposal.Participant)
                .ToHashSet();
            List<IMissionParticipant> availableParticipants = context
                .Assessment.AvailableMissionParticipants.Where(participant =>
                    !jediTrainers.Contains(participant)
                    && (
                        participant is not SpecialForces specialForces
                        || context.GetSpecialForcesIntent(specialForces)
                            == SpecialForcesIntent.PrimaryAgent
                    )
                )
                .ToList();

            foreach (IMissionParticipant participant in availableParticipants)
            {
                if (
                    participant is SpecialForces interceptor
                    && context.IsManufacturingInterceptionAgent(interceptor)
                )
                {
                    AddOfficerTargetMissionProposals(context, participant, proposals);
                    yield return participant;
                    continue;
                }
                if (AddRecruitmentProposals(context, participant, proposals))
                {
                    yield return participant;
                    continue;
                }

                if (AddDiplomacyProposals(context, participant, proposals))
                {
                    yield return participant;
                    continue;
                }

                AddReconnaissanceProposals(context, participant, proposals);
                AddSubdueUprisingProposals(context, participant, proposals);

                if (participant is Officer officer)
                    AddResearchProposals(context, officer, proposals);

                AddRescueProposals(context, participant, proposals);
                AddEspionageProposals(context, participant, proposals);
                AddInciteUprisingProposals(context, participant, proposals);
                if (
                    participant.CanPerformMission(MissionTypeIDs.Sabotage)
                    && participant.GetParentOfType<Planet>() != null
                )
                    _sabotageParticipants.Add(participant);
                AddOfficerTargetMissionProposals(context, participant, proposals);
                yield return participant;
            }

            foreach (object step in AddSabotageProposals(context, proposals))
                yield return step;
        }

        /// <summary>
        /// Adds reconnaissance proposals.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="participant">The mission participant.</param>
        /// <param name="proposals">The proposal collection to update.</param>
        private void AddReconnaissanceProposals(
            AITurnContext context,
            IMissionParticipant participant,
            List<AIProposal> proposals
        )
        {
            if (!participant.CanPerformMission(MissionTypeIDs.Reconnaissance))
                return;

            foreach (Planet target in GetReconnaissanceCandidatePlanets(context, participant))
                TryAddMissionProposal(
                    context,
                    proposals,
                    participant,
                    MissionTypeIDs.Reconnaissance,
                    target
                );
        }

        /// <summary>
        /// Adds recruitment work for the preferred recruiter and reserves that officer from
        /// competing mission assignments while unrecruited officers remain.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="participant">The participant being considered.</param>
        /// <param name="proposals">The proposal collection to update.</param>
        /// <returns>True when recruitment proposals reserve the participant.</returns>
        private bool AddRecruitmentProposals(
            AITurnContext context,
            IMissionParticipant participant,
            List<AIProposal> proposals
        )
        {
            if (participant != GetPreferredRecruiter(context))
                return false;

            if (
                context.Game.GetUnrecruitedOfficers(context.Faction.InstanceID).Count == 0
                || HasActiveMission(MissionTypeIDs.Recruitment)
            )
                return false;

            int proposalCount = proposals.Count;
            foreach (Planet target in GetRecruitmentCandidatePlanets(context))
                TryAddProposal(
                    context,
                    proposals,
                    CreateProposal(participant, MissionTypeIDs.Recruitment, target)
                );

            return proposals.Count > proposalCount;
        }

        /// <summary>
        /// Returns the qualified main officer whose assignment to recruitment preserves the
        /// faction's strongest diplomats.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>The preferred recruiter, or null when no main officer qualifies.</returns>
        private Officer GetPreferredRecruiter(AITurnContext context)
        {
            return _preferredRecruiter ??= context
                .Assessment.AvailableMissionParticipants.OfType<Officer>()
                .Where(officer => officer.IsMain)
                .Where(officer =>
                    officer.GetEffectiveRating(OfficerRating.Leadership)
                    >= context.Game.Config.AI.RecruitmentMinimumLeadership
                )
                .OrderBy(officer => officer.GetEffectiveRating(OfficerRating.Diplomacy))
                .ThenByDescending(officer => officer.GetEffectiveRating(OfficerRating.Leadership))
                .ThenBy(officer => officer.InstanceID)
                .FirstOrDefault();
        }

        /// <summary>
        /// Adds subdue uprising proposals.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="participant">The mission participant.</param>
        /// <param name="proposals">The proposal collection to update.</param>
        private void AddSubdueUprisingProposals(
            AITurnContext context,
            IMissionParticipant participant,
            List<AIProposal> proposals
        )
        {
            if (!participant.CanPerformMission(MissionTypeIDs.SubdueUprising))
                return;

            foreach (
                Planet planet in context.Assessment.OwnedPlanets.Where(planet =>
                    planet.IsInUprising
                    && !HasActiveMissionAtPlanet(MissionTypeIDs.SubdueUprising, planet.InstanceID)
                )
            )
                TryAddProposal(
                    context,
                    proposals,
                    CreateProposal(participant, MissionTypeIDs.SubdueUprising, planet)
                );
        }

        /// <summary>
        /// Adds diplomacy work for a qualified participant and reserves that participant from
        /// competing mission assignments while a valid diplomacy target remains.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="participant">The participant being considered.</param>
        /// <param name="proposals">The proposal collection to update.</param>
        /// <returns>True when diplomacy proposals reserve the participant.</returns>
        private bool AddDiplomacyProposals(
            AITurnContext context,
            IMissionParticipant participant,
            List<AIProposal> proposals
        )
        {
            if (
                participant.GetEffectiveRating(OfficerRating.Diplomacy)
                < context.Game.Config.AI.DiplomacyMinimumSkill
            )
                return false;

            int proposalCount = proposals.Count;
            foreach (Planet planet in GetDiplomacyCandidatePlanets(context))
                TryAddProposal(
                    context,
                    proposals,
                    CreateProposal(participant, MissionTypeIDs.Diplomacy, planet)
                );

            return proposals.Count > proposalCount;
        }

        /// <summary>
        /// Adds research proposals.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="officer">The officer to evaluate.</param>
        /// <param name="proposals">The proposal collection to update.</param>
        private void AddResearchProposals(
            AITurnContext context,
            Officer officer,
            List<AIProposal> proposals
        )
        {
            foreach (Planet planet in GetResearchCandidatePlanets(context, officer))
            {
                foreach (
                    ResearchDiscipline discipline in GetAvailableResearchDisciplines(
                        context,
                        officer,
                        planet
                    )
                )
                {
                    TryAddProposal(
                        context,
                        proposals,
                        new AIMissionProposal(
                            new[] { officer },
                            MissionTypeIDs.Research,
                            planet,
                            discipline: discipline
                        )
                    );
                }
            }
        }

        /// <summary>
        /// Adds jedi training proposals.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposals">The proposal collection to update.</param>
        private void AddJediTrainingProposals(AITurnContext context, List<AIProposal> proposals)
        {
            int maximumStudents = context
                .Game
                .Config
                .AI
                .MissionPlanning
                .MaximumJediTrainingStudents;
            foreach (Planet planet in context.Assessment.OwnedPlanets)
            {
                List<Officer> availableJedi = context
                    .Assessment.AvailableMissionParticipants.OfType<Officer>()
                    .Where(officer => officer.GetParentOfType<Planet>() == planet)
                    .Where(officer => officer.CanPerformMission(MissionTypeIDs.JediTraining))
                    .Where(officer => officer.IsForceSensitive && officer.IsForceEligible)
                    .ToList();
                Officer trainer = availableJedi
                    .Where(officer => JediTrainingMission.CanLeadTraining(officer, context.Game))
                    .OrderByDescending(officer => officer.ForceRank)
                    .ThenBy(officer => officer.InstanceID)
                    .FirstOrDefault();
                if (trainer == null)
                    continue;

                List<IMissionParticipant> participants = new List<IMissionParticipant> { trainer };
                participants.AddRange(
                    availableJedi
                        .Where(officer =>
                            officer != trainer && officer.ForceRank < trainer.ForceRank
                        )
                        .OrderBy(officer => officer.ForceRank)
                        .ThenBy(officer => officer.InstanceID)
                        .Take(maximumStudents)
                );
                if (participants.Count < 2)
                    continue;

                TryAddProposal(
                    context,
                    proposals,
                    new AIMissionProposal(participants, MissionTypeIDs.JediTraining, planet)
                );
            }
        }

        /// <summary>
        /// Adds rescue proposals.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="participant">The mission participant.</param>
        /// <param name="proposals">The proposal collection to update.</param>
        private void AddRescueProposals(
            AITurnContext context,
            IMissionParticipant participant,
            List<AIProposal> proposals
        )
        {
            if (
                participant is not SpecialForces
                || !participant.CanPerformMission(MissionTypeIDs.Rescue)
            )
                return;

            foreach (
                (Planet planet, Officer target) in context
                    .Faction.GetOwnedUnitsByType<Officer>()
                    .Where(officer =>
                        officer.IsCaptured
                        && !officer.IsKilled
                        && officer.Movement == null
                        && !HasActiveOfficerTargetMission(officer.InstanceID)
                    )
                    .Select(officer => (planet: officer.GetParentOfType<Planet>(), officer))
                    .Where(candidate => candidate.planet != null)
                    .OrderByDescending(candidate => candidate.officer.IsMain)
                    .ThenBy(candidate => candidate.planet.InstanceID)
                    .ThenBy(candidate => candidate.officer.InstanceID)
            )
            {
                TryAddMissionProposal(
                    context,
                    proposals,
                    participant,
                    MissionTypeIDs.Rescue,
                    planet,
                    selectedTarget: target,
                    targetOfficer: target
                );
            }
        }

        /// <summary>
        /// Adds espionage proposals.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="participant">The mission participant.</param>
        /// <param name="proposals">The proposal collection to update.</param>
        private void AddEspionageProposals(
            AITurnContext context,
            IMissionParticipant participant,
            List<AIProposal> proposals
        )
        {
            if (!participant.CanPerformMission(MissionTypeIDs.Espionage))
                return;

            Planet localPlanet = participant.GetParentOfType<Planet>();
            if (
                participant is SpecialForces specialForces
                && context.IsLocalEspionageAgent(specialForces)
                && localPlanet != null
                && !HasActiveMissionAtPlanet(MissionTypeIDs.Espionage, localPlanet.InstanceID)
            )
                TryAddMissionProposal(
                    context,
                    proposals,
                    participant,
                    MissionTypeIDs.Espionage,
                    localPlanet
                );

            foreach (Planet planet in GetEspionageCandidatePlanets(context))
                TryAddMissionProposal(
                    context,
                    proposals,
                    participant,
                    MissionTypeIDs.Espionage,
                    planet
                );
        }

        /// <summary>
        /// Adds incite uprising proposals.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="participant">The mission participant.</param>
        /// <param name="proposals">The proposal collection to update.</param>
        private void AddInciteUprisingProposals(
            AITurnContext context,
            IMissionParticipant participant,
            List<AIProposal> proposals
        )
        {
            if (!participant.CanPerformMission(MissionTypeIDs.InciteUprising))
                return;

            foreach (
                Planet planet in GetFreshEnemyPlanets(context)
                    .Where(planet =>
                        !planet.IsInUprising
                        && !HasActiveMissionAtPlanet(
                            MissionTypeIDs.InciteUprising,
                            planet.InstanceID
                        )
                    )
            )
                TryAddMissionProposal(
                    context,
                    proposals,
                    participant,
                    MissionTypeIDs.InciteUprising,
                    planet
                );
        }

        /// <summary>
        /// Builds bounded complete sabotage teams before mission scoring and selection.
        /// </summary>
        /// <param name="context">The current faction turn.</param>
        /// <param name="proposals">The proposal collection to extend.</param>
        /// <returns>Checkpoints after evaluating each target and leader pair.</returns>
        private IEnumerable<object> AddSabotageProposals(
            AITurnContext context,
            List<AIProposal> proposals
        )
        {
            if (_sabotageParticipants.Count == 0)
                yield break;

            GameConfig.AIMissionPlanningConfig config = context.Game.Config.AI.MissionPlanning;
            List<IMissionParticipant> available = context
                .Assessment.AvailableMissionParticipants.Where(participant =>
                    participant.CanPerformMission(MissionTypeIDs.Sabotage)
                    && participant.GetParentOfType<Planet>() != null
                    && (participant is SpecialForces || _sabotageParticipants.Contains(participant))
                )
                .ToList();
            foreach (Planet planet in GetSabotageCandidatePlanets(context))
            {
                List<IMissionParticipant> leaders = _sabotageParticipants
                    .Where(participant =>
                        participant is not Officer officer
                        || context.Assessment.CanAssignOfficerToPlanet(officer, planet)
                    )
                    .OrderByDescending(participant =>
                        AIMissionProposalScorer.GetParticipantPriority(
                            context,
                            participant,
                            planet,
                            participant.GetEffectiveRating(SkillRating.Espionage)
                                + participant.GetEffectiveRating(SkillRating.Combat)
                        )
                    )
                    .ThenBy(participant =>
                        participant.GetParentOfType<Planet>().GetRawDistanceTo(planet)
                    )
                    .ThenBy(participant => participant.InstanceID, StringComparer.Ordinal)
                    .Take(Math.Max(1, config.SabotageTeamAlternativesPerTarget))
                    .ToList();
                Dictionary<bool, List<IMissionParticipant>> candidatesByLeaderKind = new();
                foreach (IManufacturable target in GetSabotageTargets(context, planet))
                foreach (IMissionParticipant leader in leaders)
                {
                    bool allowsOfficers = leader is Officer;
                    if (!candidatesByLeaderKind.TryGetValue(allowsOfficers, out var candidates))
                    {
                        candidates = available
                            .Where(participant => allowsOfficers || participant is SpecialForces)
                            .Where(participant =>
                                participant is not Officer officer
                                || context.Assessment.CanAssignOfficerToPlanet(officer, planet)
                            )
                            .OrderByDescending(participant => participant is SpecialForces)
                            .ThenByDescending(participant =>
                                AIMissionProposalScorer.GetParticipantPriority(
                                    context,
                                    participant,
                                    planet,
                                    participant.GetEffectiveRating(SkillRating.Espionage)
                                )
                            )
                            .ThenByDescending(participant =>
                                participant.GetEffectiveRating(SkillRating.Combat)
                            )
                            .ThenBy(participant =>
                                participant.GetParentOfType<Planet>().GetRawDistanceTo(planet)
                            )
                            .ThenBy(participant => participant.InstanceID, StringComparer.Ordinal)
                            .Take(Math.Max(1, config.SabotageTeamCandidateCount))
                            .ToList();
                        candidatesByLeaderKind.Add(allowsOfficers, candidates);
                    }
                    AddSabotageTeamProposals(
                        context,
                        planet,
                        (ISceneNode)target,
                        leader,
                        candidates,
                        proposals
                    );
                    yield return leader;
                }
            }
        }

        /// <summary>
        /// Builds an objective-capable main group before assigning its separate decoy protection.
        /// </summary>
        /// <param name="context">The current faction turn.</param>
        /// <param name="planet">The known mission destination.</param>
        /// <param name="target">The exact sabotage objective.</param>
        /// <param name="leader">The first main participant.</param>
        /// <param name="candidates">The bounded pool of eligible helpers.</param>
        /// <param name="proposals">The collection receiving scored team alternatives.</param>
        /// <param name="departure">Optional departure planet after current arrivals complete.</param>
        private void AddSabotageTeamProposals(
            AITurnContext context,
            Planet planet,
            ISceneNode target,
            IMissionParticipant leader,
            IReadOnlyList<IMissionParticipant> candidates,
            List<AIProposal> proposals,
            Planet departure = null
        )
        {
            AIMissionProposal team = new(
                new[] { leader },
                MissionTypeIDs.Sabotage,
                planet,
                selectedTarget: target,
                teamIsFinal: true
            );
            // Additional personnel cannot improve the optimistic score of this sabotage target:
            // objective success is already bounded at 100%, while travel and officer costs cannot fall.
            AIMissionCandidatePool pool =
                departure == null ? _candidateSelector : _waitingCandidateSelector;
            if (!pool.CanRetain(context, team, departure))
                return;
            Mission mission;
            bool valid =
                departure == null
                    ? context.MissionQueries.TryCreateMission(team.CreateContext(), out mission)
                    : context.MissionQueries.TryCreateMissionAfterArrival(
                        team.CreateContext(),
                        departure,
                        out mission
                    );
            if (!valid)
                return;

            GameConfig.AIMissionPlanningConfig config = context.Game.Config.AI.MissionPlanning;
            int maximumSize = Math.Max(1, config.MaximumSabotageTeamSize);
            int maximumMains =
                config.MaximumSabotageMainParticipants > 0
                    ? Math.Min(config.MaximumSabotageMainParticipants, maximumSize - 1)
                    : maximumSize - 1;
            List<IMissionParticipant> mains = new() { leader };
            long outboundTicks = team.EstimateOutboundTravelTicks(context, departure);
            List<IMissionParticipant> mainCandidates = candidates
                .Where(candidate => candidate != leader)
                .OrderByDescending(candidate =>
                    AIMissionProposalScorer.GetParticipantPriority(
                        context,
                        candidate,
                        planet,
                        mission.GetObjectiveSuccessProbability(
                            new[] { candidate },
                            context.Game,
                            planet,
                            target
                        ),
                        outboundTicks,
                        departure
                    )
                )
                .ThenByDescending(candidate => candidate.GetEffectiveRating(SkillRating.Espionage))
                .ThenBy(candidate => candidate.GetParentOfType<Planet>().GetRawDistanceTo(planet))
                .ThenBy(candidate => candidate.InstanceID, StringComparer.Ordinal)
                .ToList();
            for (int index = 0; mains.Count <= maximumMains; index++)
            {
                double objectiveProbability = mission.GetObjectiveSuccessProbability(
                    mains,
                    context.Game,
                    planet,
                    target
                );
                if (
                    objectiveProbability >= config.MinimumSabotageSuccessPercent
                    && TryAddProtectedSabotageTeam(
                        context,
                        planet,
                        target,
                        mains,
                        candidates,
                        proposals,
                        departure
                    )
                )
                    return;
                if (index >= mainCandidates.Count)
                    return;
                mains.Add(mainCandidates[index]);
            }
        }

        /// <summary>
        /// Adds the first sufficiently protected version of a fixed main group without consuming its slots.
        /// </summary>
        /// <param name="context">The current faction turn.</param>
        /// <param name="planet">The observed destination.</param>
        /// <param name="target">The observed sabotage objective.</param>
        /// <param name="mains">The main participants whose objective strength must be preserved.</param>
        /// <param name="candidates">The bounded pool of eligible helpers.</param>
        /// <param name="proposals">The collection receiving the complete team.</param>
        /// <param name="departure">Optional departure planet after current arrivals complete.</param>
        /// <returns>True when a complete team meets the success and officer-risk requirements.</returns>
        private bool TryAddProtectedSabotageTeam(
            AITurnContext context,
            Planet planet,
            ISceneNode target,
            IReadOnlyList<IMissionParticipant> mains,
            IReadOnlyList<IMissionParticipant> candidates,
            List<AIProposal> proposals,
            Planet departure = null
        )
        {
            GameConfig.AIMissionPlanningConfig config = context.Game.Config.AI.MissionPlanning;
            int remainingSlots = Math.Max(0, config.MaximumSabotageTeamSize - mains.Count);
            int maximumDecoys =
                config.MaximumSabotageDecoyParticipants > 0
                    ? Math.Min(config.MaximumSabotageDecoyParticipants, remainingSlots)
                    : remainingSlots;
            List<IMissionParticipant> decoys = new();
            AIMissionProposal mainGroup = new(mains, MissionTypeIDs.Sabotage, planet);
            long outboundTicks = mainGroup.EstimateOutboundTravelTicks(context, departure);
            List<IMissionParticipant> decoyCandidates = candidates
                .Except(mains)
                .OrderByDescending(candidate => candidate is SpecialForces)
                .ThenByDescending(candidate =>
                    AIMissionProposalScorer.GetParticipantPriority(
                        context,
                        candidate,
                        planet,
                        candidate.GetEffectiveRating(SkillRating.Espionage),
                        outboundTicks,
                        departure
                    )
                )
                .ThenByDescending(candidate => candidate.GetEffectiveRating(SkillRating.Combat))
                .ThenBy(candidate => candidate.GetParentOfType<Planet>().GetRawDistanceTo(planet))
                .ThenBy(candidate => candidate.InstanceID, StringComparer.Ordinal)
                .Take(maximumDecoys)
                .ToList();
            if (decoyCandidates.Count == 0)
                return false;
            MissionContext optimisticTeam = new()
            {
                MissionTypeID = MissionTypeIDs.Sabotage,
                OwnerInstanceId = context.Faction.InstanceID,
                Location = planet,
                SelectedTarget = target,
                MainParticipants = mains.ToList(),
                DecoyParticipants = decoyCandidates,
            };
            bool canMeetFloor = context.Assessment.CanMeetOperationalMissionSuccess(
                context.MissionQueries,
                optimisticTeam,
                config.MinimumSabotageSuccessPercent,
                departure
            );
            if (!canMeetFloor)
                return false;
            foreach (IMissionParticipant candidate in decoyCandidates)
            {
                decoys.Add(candidate);
                AIMissionProposal protectedTeam = new(
                    mains,
                    MissionTypeIDs.Sabotage,
                    planet,
                    selectedTarget: target,
                    decoyParticipants: decoys,
                    teamIsFinal: true
                );
                AIMissionStagingProposal waiting =
                    departure == null
                        ? null
                        : AIMissionStagingProposal.WaitForArrivals(protectedTeam, departure);
                if (
                    waiting != null
                    && (waiting.Participants.Count == 0 || waiting.WaitingTicks <= 0)
                )
                    continue;
                MissionOdds odds = EvaluateSabotageTeam(context, protectedTeam, departure);
                if (
                    odds == null
                    || odds.OverallSuccessProbability <= 0
                    || odds.OverallSuccessProbability < config.MinimumSabotageSuccessPercent
                )
                    continue;
                if (protectedTeam.Participants.OfType<Officer>().Any())
                {
                    MissionOdds completeOdds =
                        departure == null
                            ? context.Assessment.GetMissionOdds(
                                context.MissionQueries,
                                protectedTeam.CreateContext()
                            )
                            : context.Assessment.GetMissionOddsAfterArrival(
                                context.MissionQueries,
                                protectedTeam.CreateContext(),
                                departure,
                                false
                            );
                    if (
                        completeOdds == null
                        || completeOdds.PersonnelLossProbability
                            > config.MaximumOfficerMissionLossProbability
                    )
                        continue;
                }
                if (departure == null)
                    TryAddProposal(context, proposals, protectedTeam);
                else
                {
                    if (!waiting.CanSelect(context))
                        continue;
                    _waitingCandidateSelector.TryAdd(context, proposals, protectedTeam, waiting);
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Evaluates and caches odds for one exact sabotage team without changing gameplay.
        /// </summary>
        /// <param name="context">The current faction turn.</param>
        /// <param name="proposal">The immutable participant roles being considered.</param>
        /// <param name="departure">Optional departure planet after current arrivals complete.</param>
        /// <returns>The operational mission odds, or null when the team is invalid.</returns>
        private static MissionOdds EvaluateSabotageTeam(
            AITurnContext context,
            AIMissionProposal proposal,
            Planet departure = null
        )
        {
            if (departure != null)
            {
                MissionOdds future = context.Assessment.GetMissionOddsAfterArrival(
                    context.MissionQueries,
                    proposal.CreateContext(),
                    departure,
                    true,
                    context.Game.Config.AI.MissionPlanning.MinimumSabotageSuccessPercent
                );
                proposal.SetOperationalOdds(future);
                return future;
            }
            context.Assessment.TryGetOperationalMissionOdds(
                context.MissionQueries,
                proposal.CreateContext(),
                context.Game.Config.AI.MissionPlanning.MinimumSabotageSuccessPercent,
                out MissionOdds odds
            );
            proposal.SetOperationalOdds(odds);
            return odds;
        }

        /// <summary>
        /// Adds officer target mission proposals.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="participant">The mission participant.</param>
        /// <param name="proposals">The proposal collection to update.</param>
        private void AddOfficerTargetMissionProposals(
            AITurnContext context,
            IMissionParticipant participant,
            List<AIProposal> proposals
        )
        {
            foreach ((Planet planet, Officer targetOfficer) in GetOfficerTargetCandidates(context))
            {
                bool interception = context.Assessment.TryGetOfficerMissionWindow(
                    targetOfficer,
                    out _,
                    out _,
                    out _
                );
                Officer selectedTarget = targetOfficer;
                if (interception)
                {
                    selectedTarget = (Officer)targetOfficer.CreateCopy();
                    selectedTarget.SetParent(targetOfficer.GetParent());
                    selectedTarget.Movement = null;
                }
                foreach (
                    string missionType in new[]
                    {
                        MissionTypeIDs.Abduction,
                        MissionTypeIDs.Assassination,
                    }
                )
                {
                    if (
                        !participant.CanPerformMission(missionType)
                        || participant is SpecialForces interceptor
                            && !context.CanAssignManufacturingInterceptor(
                                interceptor,
                                missionType,
                                planet
                            )
                        || missionType == MissionTypeIDs.Assassination
                            && !context.Assessment.IsHostileMissionAtOwnedPlanet(
                                planet,
                                targetOfficer
                            )
                    )
                        continue;
                    AIMissionProposal proposal = new(
                        new[] { participant },
                        missionType,
                        planet,
                        selectedTarget: selectedTarget,
                        targetOfficer: targetOfficer,
                        isInterception: interception
                    );
                    if (!interception)
                    {
                        TryAddProposal(context, proposals, proposal);
                        continue;
                    }
                    if (proposal.CanExecute(context))
                        TryAddProposal(context, proposals, proposal);
                    else
                    {
                        foreach (
                            Planet destination in context
                                .Assessment.OwnedPlanets.OrderBy(candidate =>
                                    candidate.GetRawDistanceTo(planet)
                                )
                                .ThenBy(candidate => candidate.InstanceID)
                        )
                        {
                            AIMissionStagingProposal preparation = new(proposal, destination);
                            if (!preparation.CanSelect(context))
                                continue;
                            _candidateSelector.TryAdd(context, proposals, proposal, preparation);
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Attempts to add proposal.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposals">The proposal collection to update.</param>
        /// <param name="proposal">The proposal to evaluate.</param>
        private void TryAddProposal(
            AITurnContext context,
            List<AIProposal> proposals,
            AIMissionProposal proposal
        )
        {
            if (proposal == null)
                return;
            if (
                proposal
                    .Participants.OfType<Officer>()
                    .Any(officer =>
                        !context.Assessment.CanAssignOfficerToPlanet(officer, proposal.TargetPlanet)
                    )
            )
                return;

            _candidateSelector.TryAdd(context, proposals, proposal);
        }

        /// <summary>
        /// Adds a mission proposal for one primary participant.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposals">The proposal list to append to.</param>
        /// <param name="participant">The main mission participant.</param>
        /// <param name="missionTypeId">The mission type identifier.</param>
        /// <param name="targetPlanet">The mission target planet.</param>
        /// <param name="selectedTarget">The optional selected mission target.</param>
        /// <param name="targetOfficer">The optional target officer.</param>
        private void TryAddMissionProposal(
            AITurnContext context,
            List<AIProposal> proposals,
            IMissionParticipant participant,
            string missionTypeId,
            Planet targetPlanet,
            ISceneNode selectedTarget = null,
            Officer targetOfficer = null
        )
        {
            TryAddProposal(
                context,
                proposals,
                new AIMissionProposal(
                    new[] { participant },
                    missionTypeId,
                    targetPlanet,
                    selectedTarget: selectedTarget,
                    targetOfficer: targetOfficer
                )
            );
        }

        /// <summary>
        /// Creates a single-participant mission proposal.
        /// </summary>
        /// <param name="participant">The mission participant.</param>
        /// <param name="missionTypeId">The mission type identifier.</param>
        /// <param name="target">The mission target.</param>
        /// <returns>The mission proposal.</returns>
        private static AIMissionProposal CreateProposal(
            IMissionParticipant participant,
            string missionTypeId,
            Planet target
        )
        {
            return new AIMissionProposal(new[] { participant }, missionTypeId, target);
        }

        /// <summary>
        /// Returns owned and neutral planets eligible for diplomacy.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Candidate planets in strategic-priority order.</returns>
        private IEnumerable<Planet> GetDiplomacyCandidatePlanets(AITurnContext context)
        {
            return _diplomacyCandidates ??= context
                .Assessment.KnownColonizedPlanets.Where(planet =>
                    (
                        context.Assessment.IsOwnedPlanet(planet)
                        || context.Assessment.IsNeutralPlanet(planet)
                    ) && !HasActiveMissionAtPlanet(MissionTypeIDs.Diplomacy, planet.InstanceID)
                )
                .Shuffle(context.Random)
                .OrderByDescending(planet => GetDiplomacyCandidatePriority(context, planet))
                .ThenBy(planet => planet.InstanceID)
                .ToList();
        }

        /// <summary>
        /// Returns unexplored planets reachable from a participant's current planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="participant">The mission participant.</param>
        /// <returns>Candidate planets in distance order.</returns>
        private IEnumerable<Planet> GetReconnaissanceCandidatePlanets(
            AITurnContext context,
            IMissionParticipant participant
        )
        {
            Planet origin = participant.GetParentOfType<Planet>();
            if (origin == null)
                return Enumerable.Empty<Planet>();

            return context
                .Assessment.UnexploredPlanets.Where(planet =>
                    !HasActiveMissionAtPlanet(MissionTypeIDs.Reconnaissance, planet.InstanceID)
                )
                .OrderBy(origin.GetRawDistanceTo)
                .ThenBy(planet => planet.InstanceID);
        }

        /// <summary>
        /// Returns owned planets where an officer can conduct useful research.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="officer">The officer to evaluate.</param>
        /// <returns>Candidate planets ordered by available research value.</returns>
        private IEnumerable<Planet> GetResearchCandidatePlanets(
            AITurnContext context,
            Officer officer
        )
        {
            return context
                .Assessment.OwnedPlanets.Where(planet =>
                    HasAvailableResearchDiscipline(context, officer, planet)
                )
                .Shuffle(context.Random)
                .OrderByDescending(planet =>
                    GetAvailableResearchDisciplineCount(context, officer, planet)
                )
                .ThenByDescending(planet => GetStrongestResearchRating(context, officer, planet))
                .ThenBy(planet => planet.InstanceID);
        }

        /// <summary>
        /// Returns enemy planets whose intelligence is old enough to refresh.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Candidate planets ordered by intelligence age and value.</returns>
        private IEnumerable<Planet> GetEspionageCandidatePlanets(AITurnContext context)
        {
            if (_espionageCandidates != null)
                return _espionageCandidates;

            int minimumAge = context.Game.Config.AI.MissionPlanning.EspionageRefreshIntervalTicks;
            _espionageCandidates = context
                .Assessment.EnemyPlanets.Where(planet =>
                    (
                        context.Assessment.GetPlanetIntelAge(planet) >= minimumAge
                        || !context.Assessment.HasMissionDefenseIntelligence(planet)
                    ) && !HasActiveMissionAtPlanet(MissionTypeIDs.Espionage, planet.InstanceID)
                )
                .OrderByDescending(context.Assessment.GetPlanetIntelAge)
                .ThenByDescending(context.Assessment.GetPlanetValue)
                .ThenBy(planet => planet.InstanceID)
                .ToList();
            return _espionageCandidates;
        }

        /// <summary>
        /// Returns enemy planets containing eligible sabotage targets.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Candidate planets ordered by campaign relevance and target value.</returns>
        private IEnumerable<Planet> GetSabotageCandidatePlanets(AITurnContext context)
        {
            return _sabotageCandidates ??= context
                .Assessment.EnemyPlanets.Where(planet => GetSabotageTargets(context, planet).Any())
                .Shuffle(context.Random)
                .OrderByDescending(context.Assessment.IsAttackPreparationTarget)
                .ThenByDescending(planet =>
                    GetSabotageTargets(context, planet)
                        .Max(target =>
                            AIMissionProposalScorer.GetSabotageTargetValue(context, planet, target)
                        )
                )
                .ThenByDescending(context.Assessment.GetPlanetBuildingCount)
                .ThenBy(planet => planet.InstanceID)
                .ToList();
        }

        /// <summary>
        /// Returns eligible sabotage targets in configured priority order.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The planet to evaluate.</param>
        /// <returns>Eligible targets in strategic-priority order.</returns>
        private IEnumerable<IManufacturable> GetSabotageTargets(
            AITurnContext context,
            Planet planet
        )
        {
            if (_sabotageTargets.TryGetValue(planet.InstanceID, out List<IManufacturable> targets))
                return targets;

            targets = GetEligibleSabotageTargets(context, planet)
                .OrderByDescending(target =>
                    AIMissionProposalScorer.GetSabotageTargetValue(context, planet, target)
                )
                .ThenByDescending(target =>
                    target.GetConstructionCost() + target.GetMaintenanceCost()
                )
                .ThenBy(target => target.InstanceID)
                .ToList();
            if (targets.Count > 0)
            {
                double highestPriority = AIMissionProposalScorer.GetSabotageTargetValue(
                    context,
                    planet,
                    targets[0]
                );
                targets.RemoveAll(target =>
                    AIMissionProposalScorer.GetSabotageTargetValue(context, planet, target)
                    != highestPriority
                );
            }
            targets = SelectDistinctSabotageTargetTypes(targets);
            _sabotageTargets.Add(planet.InstanceID, targets);
            return targets;
        }

        /// <summary>
        /// Keeps the highest-priority representative of each manufacturable definition. Destroying
        /// another identical unit at the same planet has the same strategic value, so duplicate
        /// definitions do not need separate mission candidates.
        /// </summary>
        /// <param name="orderedTargets">Targets in descending strategic-priority order.</param>
        /// <returns>One deterministic representative per manufacturable definition.</returns>
        private static List<IManufacturable> SelectDistinctSabotageTargetTypes(
            IEnumerable<IManufacturable> orderedTargets
        )
        {
            HashSet<string> seenTypeIds = new HashSet<string>(StringComparer.Ordinal);
            List<IManufacturable> distinctTargets = new List<IManufacturable>();
            foreach (IManufacturable target in orderedTargets)
            {
                string typeId = target?.TypeID;
                if (string.IsNullOrEmpty(typeId))
                {
                    distinctTargets.Add(target);
                    continue;
                }

                if (seenTypeIds.Add(typeId))
                    distinctTargets.Add(target);
            }

            return distinctTargets;
        }

        /// <summary>
        /// Returns completed, stationary targets allowed by the planet's current campaign.
        /// Attack campaigns exclude economic facilities. Other planets offer production facilities
        /// first and economic facilities only when no production target remains available.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The planet to evaluate.</param>
        /// <returns>Eligible sabotage targets.</returns>
        private IEnumerable<IManufacturable> GetEligibleSabotageTargets(
            AITurnContext context,
            Planet planet
        )
        {
            List<IManufacturable> eligible = planet
                .GetChildren<IManufacturable>()
                .Concat(
                    context
                        .Assessment.GetHostileFleets(planet)
                        .SelectMany(fleet => fleet.GetChildren<CapitalShip>())
                )
                .Where(target =>
                    target.GetOwnerInstanceID() != context.Faction.InstanceID
                    && target.GetManufacturingStatus() == ManufacturingStatus.Complete
                    && target.Movement == null
                    && !HasActiveSabotageTarget(target.InstanceID)
                )
                .ToList();

            if (context.Assessment.IsAttackPreparationTarget(planet))
            {
                return eligible.Where(target =>
                    target is not Building building
                    || building.IsDefenseFacility()
                    || building.GetBuildingType()
                        is BuildingType.Shipyard
                            or BuildingType.TrainingFacility
                            or BuildingType.ConstructionFacility
                );
            }

            List<IManufacturable> criticalGarrison = eligible
                .Where(target =>
                    target is Regiment && context.Assessment.IsGarrisonSabotageCritical(planet)
                )
                .ToList();
            if (criticalGarrison.Count > 0)
                return criticalGarrison;

            List<IManufacturable> productionFacilities = eligible
                .Where(target =>
                    target
                        is Building
                        {
                            BuildingType: BuildingType.Shipyard
                                or BuildingType.TrainingFacility
                                or BuildingType.ConstructionFacility,
                        }
                )
                .ToList();
            return productionFacilities.Count > 0
                ? productionFacilities
                : eligible.Where(target =>
                    target is Building { BuildingType: BuildingType.Mine or BuildingType.Refinery }
                );
        }

        /// <summary>
        /// Returns known enemy officers eligible for a targeted mission.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Eligible target planets and officers in priority order.</returns>
        private IEnumerable<(Planet Planet, Officer TargetOfficer)> GetOfficerTargetCandidates(
            AITurnContext context
        )
        {
            return _officerTargetCandidates ??= context
                .Assessment.TargetableEnemyOfficerMissionTargets.Where(candidate =>
                    !HasActiveOfficerTargetMission(candidate.TargetOfficer.InstanceID)
                    && (
                        context.Assessment.TryGetOfficerMissionWindow(
                            candidate.TargetOfficer,
                            out _,
                            out _,
                            out _
                        )
                        || context.Assessment.GetPlanetIntelAge(candidate.Planet)
                            <= context
                                .Game
                                .Config
                                .AI
                                .MissionPlanning
                                .HostileMissionMaximumIntelAgeTicks
                            && context.Assessment.HasMissionDefenseIntelligence(candidate.Planet)
                    )
                )
                .Shuffle(context.Random)
                .OrderByDescending(candidate => candidate.TargetOfficer.IsMain)
                .ThenByDescending(candidate =>
                    AIMissionProposalScorer
                        .GetOfficerTargetUtility(context, candidate.TargetOfficer)
                        .Value
                )
                .ToList();
        }

        /// <summary>
        /// Returns enemy planets whose intelligence is recent enough for hostile missions.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Enemy planets within the configured intelligence age.</returns>
        private IEnumerable<Planet> GetFreshEnemyPlanets(AITurnContext context)
        {
            if (_freshEnemyPlanets != null)
                return _freshEnemyPlanets;

            int maximumAge = context
                .Game
                .Config
                .AI
                .MissionPlanning
                .HostileMissionMaximumIntelAgeTicks;
            _freshEnemyPlanets = context
                .Assessment.EnemyPlanets.Where(planet =>
                    context.Assessment.GetPlanetIntelAge(planet) <= maximumAge
                    && context.Assessment.HasMissionDefenseIntelligence(planet)
                )
                .ToList();
            return _freshEnemyPlanets;
        }

        /// <summary>
        /// Returns safe owned planets suitable for recruitment.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Candidate planets ordered by popular support.</returns>
        private IEnumerable<Planet> GetRecruitmentCandidatePlanets(AITurnContext context)
        {
            return _recruitmentCandidates ??= context
                .Assessment.OwnedPlanets.Where(planet => planet.IsColonized && !planet.IsDestroyed)
                .OrderBy(planet => context.Assessment.GetFactionPopularSupport(planet))
                .ThenBy(planet => planet.InstanceID)
                .ToList();
        }

        /// <summary>
        /// Returns research disciplines an officer can advance at a planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="officer">The officer to evaluate.</param>
        /// <param name="planet">The planet to evaluate.</param>
        /// <returns>Available, qualified research disciplines.</returns>
        private IEnumerable<ResearchDiscipline> GetAvailableResearchDisciplines(
            AITurnContext context,
            Officer officer,
            Planet planet
        )
        {
            foreach (ResearchDiscipline discipline in _researchDisciplines)
            {
                if (context.Faction.IsResearchExhausted(discipline))
                    continue;

                if (!ResearchMission.HasResearchFacility(planet, discipline))
                    continue;

                if (officer.GetBaseRating(discipline) <= 0)
                    continue;

                yield return discipline;
            }
        }

        /// <summary>
        /// Returns whether an officer can advance any research discipline at a planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="officer">The officer to evaluate.</param>
        /// <param name="planet">The planet to evaluate.</param>
        /// <returns>True when at least one discipline is available.</returns>
        private bool HasAvailableResearchDiscipline(
            AITurnContext context,
            Officer officer,
            Planet planet
        )
        {
            return GetAvailableResearchDisciplines(context, officer, planet).Any();
        }

        /// <summary>
        /// Returns the number of research disciplines an officer can advance at a planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="officer">The officer to evaluate.</param>
        /// <param name="planet">The planet to evaluate.</param>
        /// <returns>The available discipline count.</returns>
        private int GetAvailableResearchDisciplineCount(
            AITurnContext context,
            Officer officer,
            Planet planet
        )
        {
            return GetAvailableResearchDisciplines(context, officer, planet).Count();
        }

        /// <summary>
        /// Returns an officer's strongest applicable research rating at a planet.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="officer">The officer to evaluate.</param>
        /// <param name="planet">The planet to evaluate.</param>
        /// <returns>The strongest rating, or zero when no discipline is available.</returns>
        private int GetStrongestResearchRating(
            AITurnContext context,
            Officer officer,
            Planet planet
        )
        {
            return GetAvailableResearchDisciplines(context, officer, planet)
                .Select(officer.GetBaseRating)
                .DefaultIfEmpty()
                .Max();
        }

        /// <summary>
        /// Indexes active mission types, locations, and selected targets for this planning turn.
        /// </summary>
        /// <param name="missions">The active missions visible to the faction.</param>
        private void BuildActiveMissionIndexes(IEnumerable<Mission> missions)
        {
            _activeMissionTypes.Clear();
            _activeMissionPlanets.Clear();
            _activeOfficerTargets.Clear();
            _activeSabotageTargets.Clear();

            foreach (Mission mission in missions ?? Enumerable.Empty<Mission>())
            {
                _activeMissionTypes.Add(mission.ConfigKey);
                _activeMissionPlanets.Add((mission.ConfigKey, mission.LocationInstanceID));

                string officerTargetId = mission switch
                {
                    AbductionMission abduction => abduction.TargetOfficerInstanceID,
                    AssassinationMission assassination => assassination.TargetOfficerInstanceID,
                    RescueMission rescue => rescue.TargetOfficerInstanceID,
                    _ => null,
                };
                if (!string.IsNullOrEmpty(officerTargetId))
                    _activeOfficerTargets.Add(officerTargetId);

                if (
                    mission is SabotageMission sabotage
                    && !string.IsNullOrEmpty(sabotage.SabotageTargetInstanceID)
                )
                    _activeSabotageTargets.Add(sabotage.SabotageTargetInstanceID);
            }
        }

        /// <summary>
        /// Returns whether an active mission has the supplied type.
        /// </summary>
        /// <param name="missionTypeId">The mission type identifier.</param>
        /// <returns>True when a matching mission is active.</returns>
        private bool HasActiveMission(string missionTypeId)
        {
            return _activeMissionTypes.Contains(missionTypeId);
        }

        /// <summary>
        /// Returns whether an active mission has the supplied type and planet.
        /// </summary>
        /// <param name="missionTypeId">The mission type identifier.</param>
        /// <param name="planetId">The planet instance identifier.</param>
        /// <returns>True when a matching mission is active.</returns>
        private bool HasActiveMissionAtPlanet(string missionTypeId, string planetId)
        {
            return _activeMissionPlanets.Contains((missionTypeId, planetId));
        }

        /// <summary>
        /// Returns whether an active mission targets the supplied officer.
        /// </summary>
        /// <param name="officerId">The officer instance identifier.</param>
        /// <returns>True when a matching mission is active.</returns>
        private bool HasActiveOfficerTargetMission(string officerId)
        {
            return _activeOfficerTargets.Contains(officerId);
        }

        /// <summary>
        /// Returns whether an active sabotage mission targets the supplied unit.
        /// </summary>
        /// <param name="targetId">The target instance identifier.</param>
        /// <returns>True when a matching mission is active.</returns>
        private bool HasActiveSabotageTarget(string targetId)
        {
            return _activeSabotageTargets.Contains(targetId);
        }

        /// <summary>
        /// Returns diplomacy candidate priority.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The planet to evaluate.</param>
        /// <returns>The calculated value.</returns>
        private double GetDiplomacyCandidatePriority(AITurnContext context, Planet planet)
        {
            return AIMissionProposalScorer.GetDiplomacyTargetValue(context, planet);
        }
    }
}
