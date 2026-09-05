using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Extensions;

namespace Rebellion.AI.Planners
{
    /// <summary>
    /// Builds mission proposals for available mission participants.
    /// </summary>
    public sealed class AIMissionPlanner : IAIProposalPlanner
    {
        // Planning State.
        private readonly AIMissionCandidateSelector _candidateSelector =
            new AIMissionCandidateSelector();
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
            if (context?.Game == null || context.Faction == null || context.Missions == null)
                return new List<AIProposal>();

            _candidateSelector.Reset();
            _sabotageTargets.Clear();
            BuildActiveMissionIndexes(context.Assessment.ActiveMissions);
            _diplomacyCandidates = null;
            _preferredRecruiter = null;
            _espionageCandidates = null;
            _freshEnemyPlanets = null;
            _officerTargetCandidates = null;
            _recruitmentCandidates = null;
            _sabotageCandidates = null;
            List<AIProposal> proposals = CreateMissionProposals(context);
            return proposals;
        }

        /// <summary>
        /// Creates mission proposals.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Mission proposals for the available participants.</returns>
        private List<AIProposal> CreateMissionProposals(AITurnContext context)
        {
            List<AIProposal> proposals = new List<AIProposal>();
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
                if (AddRecruitmentProposals(context, participant, proposals))
                    continue;

                if (AddDiplomacyProposals(context, participant, proposals))
                    continue;

                AddReconnaissanceProposals(context, participant, proposals);
                AddSubdueUprisingProposals(context, participant, proposals);

                if (participant is Officer officer)
                    AddResearchProposals(context, officer, proposals);

                AddRescueProposals(context, participant, proposals);
                AddEspionageProposals(context, participant, proposals);
                AddInciteUprisingProposals(context, participant, proposals);
                AddSabotageProposals(context, participant, proposals);
                AddOfficerTargetMissionProposals(context, participant, proposals);
            }

            return proposals;
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
        /// Adds sabotage proposals.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="participant">The mission participant.</param>
        /// <param name="proposals">The proposal collection to update.</param>
        private void AddSabotageProposals(
            AITurnContext context,
            IMissionParticipant participant,
            List<AIProposal> proposals
        )
        {
            if (!participant.CanPerformMission(MissionTypeIDs.Sabotage))
                return;

            foreach (Planet planet in GetSabotageCandidatePlanets(context))
            {
                foreach (IManufacturable target in GetSabotageTargets(context, planet))
                {
                    TryAddMissionProposal(
                        context,
                        proposals,
                        participant,
                        MissionTypeIDs.Sabotage,
                        planet,
                        selectedTarget: target
                    );
                }
            }
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
                if (participant.CanPerformMission(MissionTypeIDs.Abduction))
                    TryAddMissionProposal(
                        context,
                        proposals,
                        participant,
                        MissionTypeIDs.Abduction,
                        planet,
                        selectedTarget: targetOfficer,
                        targetOfficer: targetOfficer
                    );

                if (participant.CanPerformMission(MissionTypeIDs.Assassination))
                    TryAddMissionProposal(
                        context,
                        proposals,
                        participant,
                        MissionTypeIDs.Assassination,
                        planet,
                        selectedTarget: targetOfficer,
                        targetOfficer: targetOfficer
                    );
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
                    context.Assessment.GetPlanetIntelAge(planet) >= minimumAge
                    && !HasActiveMissionAtPlanet(MissionTypeIDs.Espionage, planet.InstanceID)
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
            return _sabotageCandidates ??= GetFreshEnemyPlanets(context)
                .Concat(
                    context.Assessment.EnemyPlanets.Where(
                        context.Assessment.IsAttackTargetBlockedByShields
                    )
                )
                .GroupBy(planet => planet.InstanceID, StringComparer.Ordinal)
                .Select(group => group.First())
                .Where(planet => GetSabotageTargets(context, planet).Any())
                .Shuffle(context.Random)
                .OrderByDescending(context.Assessment.IsAttackPreparationTarget)
                .ThenByDescending(planet =>
                    GetSabotageTargets(context, planet)
                        .Max(target => context.SabotageTargets.GetPriorityBonus(planet, target))
                )
                .ThenByDescending(context.Assessment.GetPlanetBuildingCount)
                .ThenBy(planet => planet.InstanceID)
                .ToList();
        }

        /// <summary>
        /// Returns the highest-priority sabotage tier available on a planet.
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

            List<IManufacturable> eligibleTargets = GetEligibleSabotageTargets(context, planet)
                .ToList();
            AISabotageTargetTier highestPriority = eligibleTargets
                .Select(AISabotageTargetPolicy.GetTier)
                .DefaultIfEmpty(AISabotageTargetTier.Infrastructure)
                .Max();
            targets = eligibleTargets
                .Where(target => AISabotageTargetPolicy.GetTier(target) == highestPriority)
                .OrderByDescending(target =>
                    context.SabotageTargets.GetPriorityBonus(planet, target)
                )
                .ThenByDescending(target =>
                    target.GetConstructionCost() + target.GetMaintenanceCost()
                )
                .ThenBy(target => target.InstanceID)
                .ToList();
            _sabotageTargets.Add(planet.InstanceID, targets);
            return targets;
        }

        /// <summary>
        /// Returns completed enemy units and facilities not already targeted for sabotage.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The planet to evaluate.</param>
        /// <returns>Eligible sabotage targets.</returns>
        private IEnumerable<IManufacturable> GetEligibleSabotageTargets(
            AITurnContext context,
            Planet planet
        )
        {
            return planet
                .GetChildren<IManufacturable>()
                .Where(target =>
                    target.GetOwnerInstanceID() != context.Faction.InstanceID
                    && target.GetManufacturingStatus() == ManufacturingStatus.Complete
                    && target.Movement == null
                    && !HasActiveSabotageTarget(target.InstanceID)
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
                    context.Assessment.GetPlanetIntelAge(candidate.Planet)
                        <= context.Game.Config.AI.MissionPlanning.HostileMissionMaximumIntelAgeTicks
                    && !HasActiveOfficerTargetMission(candidate.TargetOfficer.InstanceID)
                )
                .Shuffle(context.Random)
                .OrderByDescending(candidate => candidate.TargetOfficer.IsMain)
                .ThenByDescending(candidate =>
                    GetOfficerTargetCandidatePriority(candidate.TargetOfficer)
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
        private int GetDiplomacyCandidatePriority(AITurnContext context, Planet planet)
        {
            int support = context.Assessment.GetFactionPopularSupport(planet);
            int strategicValue = context.Assessment.GetDiplomacyTargetStrategicValue(planet);

            if (context.Assessment.IsOwnedPlanet(planet))
            {
                int supportRisk = context.Assessment.GetDefensiveSupportRisk(planet);
                return 100
                    - support
                    + strategicValue
                    + supportRisk
                        * context.Game.Config.AI.MissionPlanning.DiplomacySectorSupportRiskWeight;
            }

            return context.Assessment.IsNeutralPlanet(planet) ? support + strategicValue : 0;
        }

        /// <summary>
        /// Returns officer target candidate priority.
        /// </summary>
        /// <param name="officer">The officer to evaluate.</param>
        /// <returns>The calculated value.</returns>
        private int GetOfficerTargetCandidatePriority(Officer officer)
        {
            return officer.GetEffectiveRating(OfficerRating.Combat)
                + officer.GetEffectiveRating(OfficerRating.Espionage)
                + officer.GetEffectiveRating(OfficerRating.Diplomacy)
                + officer.GetEffectiveRating(OfficerRating.Leadership)
                + officer.GetBaseRating(ResearchDiscipline.ShipDesign)
                + officer.GetBaseRating(ResearchDiscipline.FacilityDesign)
                + officer.GetBaseRating(ResearchDiscipline.TroopTraining);
        }
    }
}
