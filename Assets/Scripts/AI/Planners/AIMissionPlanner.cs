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
        private readonly Dictionary<
            (string MissionTypeId, string PlanetId),
            List<IMissionParticipant>
        > _decoyCandidates =
            new Dictionary<(string MissionTypeId, string PlanetId), List<IMissionParticipant>>();
        private readonly Dictionary<string, List<IManufacturable>> _sabotageTargets =
            new Dictionary<string, List<IManufacturable>>(StringComparer.Ordinal);
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
            _decoyCandidates.Clear();
            _sabotageTargets.Clear();
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
                )
                .ToList();

            foreach (IMissionParticipant participant in availableParticipants)
            {
                if (AddRecruitmentProposals(context, participant, proposals))
                    continue;

                if (AddDiplomacyProposals(context, participant, proposals))
                    continue;

                AddReconnaissanceProposals(context, participant, availableParticipants, proposals);
                AddSubdueUprisingProposals(context, participant, proposals);

                if (participant is Officer officer)
                    AddResearchProposals(context, officer, proposals);

                AddRescueProposals(context, participant, availableParticipants, proposals);
                AddEspionageProposals(context, participant, availableParticipants, proposals);
                AddInciteUprisingProposals(context, participant, availableParticipants, proposals);
                AddSabotageProposals(context, participant, availableParticipants, proposals);
                AddOfficerTargetMissionProposals(
                    context,
                    participant,
                    availableParticipants,
                    proposals
                );
            }

            return proposals;
        }

        private void AddReconnaissanceProposals(
            AITurnContext context,
            IMissionParticipant participant,
            IReadOnlyList<IMissionParticipant> availableParticipants,
            List<AIProposal> proposals
        )
        {
            if (!participant.CanPerformMission(MissionTypeIDs.Reconnaissance))
                return;

            foreach (Planet target in GetReconnaissanceCandidatePlanets(context, participant))
                TryAddDecoyedProposal(
                    context,
                    proposals,
                    participant,
                    availableParticipants,
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
                || HasActiveMission(context, MissionTypeIDs.Recruitment)
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
                    && !HasActiveMissionAtPlanet(
                        context,
                        MissionTypeIDs.SubdueUprising,
                        planet.InstanceID
                    )
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

        private void AddRescueProposals(
            AITurnContext context,
            IMissionParticipant participant,
            IReadOnlyList<IMissionParticipant> availableParticipants,
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
                    .Assessment.FactionViewPlanets.SelectMany(planet =>
                        context
                            .Assessment.GetKnownOfficers(planet)
                            .Where(officer =>
                                officer.GetOwnerInstanceID() == context.Faction.InstanceID
                                && officer.IsCaptured
                                && !officer.IsKilled
                                && officer.Movement == null
                                && !HasActiveOfficerTargetMission(context, officer.InstanceID)
                            )
                            .Select(officer => (planet, officer))
                    )
                    .OrderByDescending(candidate => candidate.officer.IsMain)
                    .ThenBy(candidate => candidate.planet.InstanceID)
                    .ThenBy(candidate => candidate.officer.InstanceID)
            )
            {
                TryAddDecoyedProposal(
                    context,
                    proposals,
                    participant,
                    availableParticipants,
                    MissionTypeIDs.Rescue,
                    planet,
                    selectedTarget: target,
                    targetOfficer: target
                );
            }
        }

        private void AddEspionageProposals(
            AITurnContext context,
            IMissionParticipant participant,
            IReadOnlyList<IMissionParticipant> availableParticipants,
            List<AIProposal> proposals
        )
        {
            if (!participant.CanPerformMission(MissionTypeIDs.Espionage))
                return;

            foreach (Planet planet in GetEspionageCandidatePlanets(context))
                TryAddDecoyedProposal(
                    context,
                    proposals,
                    participant,
                    availableParticipants,
                    MissionTypeIDs.Espionage,
                    planet
                );
        }

        private void AddInciteUprisingProposals(
            AITurnContext context,
            IMissionParticipant participant,
            IReadOnlyList<IMissionParticipant> availableParticipants,
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
                            context,
                            MissionTypeIDs.InciteUprising,
                            planet.InstanceID
                        )
                    )
            )
                TryAddDecoyedProposal(
                    context,
                    proposals,
                    participant,
                    availableParticipants,
                    MissionTypeIDs.InciteUprising,
                    planet
                );
        }

        private void AddSabotageProposals(
            AITurnContext context,
            IMissionParticipant participant,
            IReadOnlyList<IMissionParticipant> availableParticipants,
            List<AIProposal> proposals
        )
        {
            if (!participant.CanPerformMission(MissionTypeIDs.Sabotage))
                return;

            foreach (Planet planet in GetSabotageCandidatePlanets(context))
            {
                foreach (IManufacturable target in GetSabotageTargets(context, planet))
                {
                    TryAddDecoyedProposal(
                        context,
                        proposals,
                        participant,
                        availableParticipants,
                        MissionTypeIDs.Sabotage,
                        planet,
                        selectedTarget: target
                    );
                }
            }
        }

        /// <summary>
        /// Selects one qualified participant to protect a hostile mission from detection,
        /// preferring renewable special forces before committing officers.
        /// </summary>
        /// <param name="availableParticipants">Participants available during this planning turn.</param>
        /// <param name="mainParticipant">The participant performing the mission.</param>
        /// <param name="missionTypeId">The mission type the decoy must be able to perform.</param>
        /// <param name="target">The mission destination.</param>
        /// <returns>The preferred decoy, or an empty sequence when none is available.</returns>
        private IEnumerable<IMissionParticipant> GetMissionDecoy(
            IReadOnlyList<IMissionParticipant> availableParticipants,
            IMissionParticipant mainParticipant,
            string missionTypeId,
            Planet target
        )
        {
            (string MissionTypeId, string PlanetId) key = (missionTypeId, target?.InstanceID);
            if (!_decoyCandidates.TryGetValue(key, out List<IMissionParticipant> candidates))
            {
                candidates = availableParticipants
                    .Where(candidate => candidate.CanPerformMission(missionTypeId))
                    .OrderBy(candidate => candidate is SpecialForces ? 0 : 1)
                    .ThenBy(candidate => candidate is Officer { IsMain: true } ? 1 : 0)
                    .ThenByDescending(candidate =>
                        candidate.GetEffectiveRating(OfficerRating.Espionage)
                    )
                    .ThenBy(candidate => GetTravelDistance(candidate, target))
                    .ThenBy(candidate => candidate.InstanceID)
                    .ToList();
                _decoyCandidates.Add(key, candidates);
            }

            IMissionParticipant decoy = candidates.FirstOrDefault(candidate =>
                candidate != mainParticipant
            );
            return decoy == null ? Array.Empty<IMissionParticipant>() : new[] { decoy };
        }

        private static double GetTravelDistance(IMissionParticipant participant, Planet target)
        {
            Planet origin = participant?.GetParentOfType<Planet>();
            return origin != null && target != null
                ? origin.GetRawDistanceTo(target)
                : double.MaxValue;
        }

        private void AddOfficerTargetMissionProposals(
            AITurnContext context,
            IMissionParticipant participant,
            IReadOnlyList<IMissionParticipant> availableParticipants,
            List<AIProposal> proposals
        )
        {
            foreach ((Planet planet, Officer targetOfficer) in GetOfficerTargetCandidates(context))
            {
                if (participant.CanPerformMission(MissionTypeIDs.Abduction))
                    TryAddDecoyedProposal(
                        context,
                        proposals,
                        participant,
                        availableParticipants,
                        MissionTypeIDs.Abduction,
                        planet,
                        selectedTarget: targetOfficer,
                        targetOfficer: targetOfficer
                    );

                if (participant.CanPerformMission(MissionTypeIDs.Assassination))
                    TryAddDecoyedProposal(
                        context,
                        proposals,
                        participant,
                        availableParticipants,
                        MissionTypeIDs.Assassination,
                        planet,
                        selectedTarget: targetOfficer,
                        targetOfficer: targetOfficer
                    );
            }
        }

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
        /// Adds a mission proposal with decoys drawn from the available participants.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposals">The proposal list to append to.</param>
        /// <param name="participant">The main mission participant.</param>
        /// <param name="availableParticipants">The participants available as decoys.</param>
        /// <param name="missionTypeId">The mission type identifier.</param>
        /// <param name="targetPlanet">The mission target planet.</param>
        /// <param name="selectedTarget">The optional selected mission target.</param>
        /// <param name="targetOfficer">The optional target officer.</param>
        private void TryAddDecoyedProposal(
            AITurnContext context,
            List<AIProposal> proposals,
            IMissionParticipant participant,
            IReadOnlyList<IMissionParticipant> availableParticipants,
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
                    targetOfficer: targetOfficer,
                    decoyParticipants: GetMissionDecoy(
                        availableParticipants,
                        participant,
                        missionTypeId,
                        targetPlanet
                    )
                )
            );
        }

        private static AIMissionProposal CreateProposal(
            IMissionParticipant participant,
            string missionTypeId,
            Planet target
        )
        {
            return new AIMissionProposal(new[] { participant }, missionTypeId, target);
        }

        private IEnumerable<Planet> GetDiplomacyCandidatePlanets(AITurnContext context)
        {
            return _diplomacyCandidates ??= context
                .Assessment.KnownColonizedPlanets.Where(planet =>
                    (
                        context.Assessment.IsOwnedPlanet(planet)
                        || context.Assessment.IsNeutralPlanet(planet)
                    )
                    && !HasActiveMissionAtPlanet(
                        context,
                        MissionTypeIDs.Diplomacy,
                        planet.InstanceID
                    )
                )
                .Shuffle(context.Random)
                .OrderByDescending(planet => GetDiplomacyCandidatePriority(context, planet))
                .ThenBy(planet => planet.InstanceID)
                .ToList();
        }

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
                    !HasActiveMissionAtPlanet(
                        context,
                        MissionTypeIDs.Reconnaissance,
                        planet.InstanceID
                    )
                )
                .OrderBy(origin.GetRawDistanceTo)
                .ThenBy(planet => planet.InstanceID);
        }

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

        private IEnumerable<Planet> GetEspionageCandidatePlanets(AITurnContext context)
        {
            if (_espionageCandidates != null)
                return _espionageCandidates;

            int minimumAge = context.Game.Config.AI.MissionPlanning.EspionageRefreshIntervalTicks;
            _espionageCandidates = context
                .Assessment.EnemyPlanets.Where(planet =>
                    context.Assessment.GetPlanetIntelAge(planet) >= minimumAge
                    && !HasActiveMissionAtPlanet(
                        context,
                        MissionTypeIDs.Espionage,
                        planet.InstanceID
                    )
                )
                .OrderByDescending(context.Assessment.GetPlanetIntelAge)
                .ThenByDescending(context.Assessment.GetPlanetValue)
                .ThenBy(planet => planet.InstanceID)
                .ToList();
            return _espionageCandidates;
        }

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
                        .Max(target =>
                            context.Assessment.GetSabotageTargetPriorityBonus(planet, target)
                        )
                )
                .ThenByDescending(context.Assessment.GetPlanetBuildingCount)
                .ThenBy(planet => planet.InstanceID)
                .ToList();
        }

        private IEnumerable<IManufacturable> GetSabotageTargets(
            AITurnContext context,
            Planet planet
        )
        {
            if (_sabotageTargets.TryGetValue(planet.InstanceID, out List<IManufacturable> targets))
                return targets;

            List<IManufacturable> eligibleTargets = GetEligibleSabotageTargets(context, planet)
                .ToList();
            int highestPriority = eligibleTargets
                .Select(AIAssessment.GetSabotageTargetPriority)
                .DefaultIfEmpty(0)
                .Max();
            targets = eligibleTargets
                .Where(target => AIAssessment.GetSabotageTargetPriority(target) == highestPriority)
                .OrderByDescending(target =>
                    context.Assessment.GetSabotageTargetPriorityBonus(planet, target)
                )
                .ThenByDescending(target =>
                    target.GetConstructionCost() + target.GetMaintenanceCost()
                )
                .ThenBy(target => target.InstanceID)
                .ToList();
            _sabotageTargets.Add(planet.InstanceID, targets);
            return targets;
        }

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
                    && !HasActiveSabotageTarget(context, target.InstanceID)
                );
        }

        private IEnumerable<(Planet Planet, Officer TargetOfficer)> GetOfficerTargetCandidates(
            AITurnContext context
        )
        {
            return _officerTargetCandidates ??= context
                .Assessment.TargetableEnemyOfficerMissionTargets.Where(candidate =>
                    context.Assessment.GetPlanetIntelAge(candidate.Planet)
                        <= context.Game.Config.AI.MissionPlanning.HostileMissionMaximumIntelAgeTicks
                    && !HasActiveOfficerTargetMission(context, candidate.TargetOfficer.InstanceID)
                )
                .Shuffle(context.Random)
                .OrderByDescending(candidate => candidate.TargetOfficer.IsMain)
                .ThenByDescending(candidate =>
                    GetOfficerTargetCandidatePriority(candidate.TargetOfficer)
                )
                .ToList();
        }

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

        private IEnumerable<Planet> GetRecruitmentCandidatePlanets(AITurnContext context)
        {
            return _recruitmentCandidates ??= context
                .Assessment.OwnedPlanets.Where(planet => planet.IsColonized && !planet.IsDestroyed)
                .OrderBy(planet => context.Assessment.GetFactionPopularSupport(planet))
                .ThenBy(planet => planet.InstanceID)
                .ToList();
        }

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

        private bool HasAvailableResearchDiscipline(
            AITurnContext context,
            Officer officer,
            Planet planet
        )
        {
            return GetAvailableResearchDisciplines(context, officer, planet).Any();
        }

        private int GetAvailableResearchDisciplineCount(
            AITurnContext context,
            Officer officer,
            Planet planet
        )
        {
            return GetAvailableResearchDisciplines(context, officer, planet).Count();
        }

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

        private bool HasActiveMission(AITurnContext context, string missionTypeId)
        {
            return GetActiveMissions(context).Any(mission => mission.ConfigKey == missionTypeId);
        }

        private bool HasActiveMissionAtPlanet(
            AITurnContext context,
            string missionTypeId,
            string planetId
        )
        {
            return GetActiveMissions(context)
                .Any(mission =>
                    mission.ConfigKey == missionTypeId && mission.LocationInstanceID == planetId
                );
        }

        private bool HasActiveOfficerTargetMission(AITurnContext context, string officerId)
        {
            return GetActiveMissions(context)
                .Any(mission =>
                    mission switch
                    {
                        AbductionMission abduction => abduction.TargetOfficerInstanceID
                            == officerId,
                        AssassinationMission assassination => assassination.TargetOfficerInstanceID
                            == officerId,
                        RescueMission rescue => rescue.TargetOfficerInstanceID == officerId,
                        _ => false,
                    }
                );
        }

        private bool HasActiveSabotageTarget(AITurnContext context, string targetId)
        {
            return GetActiveMissions(context)
                .OfType<SabotageMission>()
                .Any(mission => mission.SabotageTargetInstanceID == targetId);
        }

        private IEnumerable<Mission> GetActiveMissions(AITurnContext context)
        {
            return context.Assessment.ActiveMissions;
        }

        private int GetDiplomacyCandidatePriority(AITurnContext context, Planet planet)
        {
            int support = context.Assessment.GetFactionPopularSupport(planet);
            int strategicValue = context.Assessment.GetDiplomacyTargetStrategicValue(planet);

            if (context.Assessment.IsOwnedPlanet(planet))
                return 100 - support + strategicValue;

            return context.Assessment.IsNeutralPlanet(planet) ? support + strategicValue : 0;
        }

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
