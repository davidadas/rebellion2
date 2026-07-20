using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.Util.Extensions;

namespace Rebellion.AI.Planners
{
    /// <summary>
    /// Builds mission proposals for available mission participants.
    /// </summary>
    public sealed class AIMissionPlanner : IAIProposalPlanner
    {
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

            return CreateMissionProposals(context);
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
                AddReconnaissanceProposals(context, participant, proposals);
                AddRecruitmentProposals(context, participant, proposals);
                AddSubdueUprisingProposals(context, participant, proposals);
                AddDiplomacyProposals(context, participant, proposals);

                if (participant is Officer officer)
                    AddResearchProposals(context, officer, proposals);

                AddRescueProposals(context, participant, proposals);
                AddEspionageProposals(context, participant, availableParticipants, proposals);
                AddInciteUprisingProposals(context, participant, proposals);
                AddSabotageProposals(context, participant, availableParticipants, proposals);
                AddOfficerTargetMissionProposals(context, participant, proposals);
            }

            return proposals;
        }

        private void AddReconnaissanceProposals(
            AITurnContext context,
            IMissionParticipant participant,
            List<AIProposal> proposals
        )
        {
            if (!participant.CanPerformMission(MissionTypeIDs.Reconnaissance))
                return;

            foreach (Planet target in GetReconnaissanceCandidatePlanets(context, participant))
                TryAddProposal(
                    context,
                    proposals,
                    CreateProposal(participant, MissionTypeIDs.Reconnaissance, target)
                );
        }

        private void AddRecruitmentProposals(
            AITurnContext context,
            IMissionParticipant participant,
            List<AIProposal> proposals
        )
        {
            if (participant is not Officer { IsMain: true })
                return;

            if (
                context.Game.GetUnrecruitedOfficers(context.Faction.InstanceID).Count == 0
                || HasActiveMission(context, MissionTypeIDs.Recruitment)
            )
                return;

            if (
                participant.GetEffectiveRating(OfficerRating.Leadership)
                < context.Game.Config.AI.RecruitmentMinimumLeadership
            )
                return;

            foreach (Planet target in GetRecruitmentCandidatePlanets(context))
                TryAddProposal(
                    context,
                    proposals,
                    CreateProposal(participant, MissionTypeIDs.Recruitment, target)
                );
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

        private void AddDiplomacyProposals(
            AITurnContext context,
            IMissionParticipant participant,
            List<AIProposal> proposals
        )
        {
            if (
                participant.GetEffectiveRating(OfficerRating.Diplomacy)
                < context.Game.Config.AI.DiplomacyMinimumSkill
            )
                return;

            foreach (Planet planet in GetDiplomacyCandidatePlanets(context))
                TryAddProposal(
                    context,
                    proposals,
                    CreateProposal(participant, MissionTypeIDs.Diplomacy, planet)
                );
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
                    .Where(officer => officer.IsJedi && officer.IsForceEligible)
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
                TryAddProposal(
                    context,
                    proposals,
                    new AIMissionProposal(
                        new[] { participant },
                        MissionTypeIDs.Rescue,
                        planet,
                        selectedTarget: target,
                        targetOfficer: target
                    )
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
                TryAddProposal(
                    context,
                    proposals,
                    new AIMissionProposal(
                        new[] { participant },
                        MissionTypeIDs.Espionage,
                        planet,
                        decoyParticipants: GetAttackTargetDecoys(
                            context,
                            availableParticipants,
                            participant,
                            MissionTypeIDs.Espionage,
                            planet
                        )
                    )
                );
        }

        private void AddInciteUprisingProposals(
            AITurnContext context,
            IMissionParticipant participant,
            List<AIProposal> proposals
        )
        {
            if (
                !CanPlanHostileMission(context)
                || !participant.CanPerformMission(MissionTypeIDs.InciteUprising)
            )
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
                TryAddProposal(
                    context,
                    proposals,
                    CreateProposal(participant, MissionTypeIDs.InciteUprising, planet)
                );
        }

        private void AddSabotageProposals(
            AITurnContext context,
            IMissionParticipant participant,
            IReadOnlyList<IMissionParticipant> availableParticipants,
            List<AIProposal> proposals
        )
        {
            if (
                !CanPlanHostileMission(context)
                || !participant.CanPerformMission(MissionTypeIDs.Sabotage)
            )
                return;

            foreach (Planet planet in GetSabotageCandidatePlanets(context))
            {
                foreach (IManufacturable target in GetSabotageTargets(context, planet))
                {
                    TryAddProposal(
                        context,
                        proposals,
                        new AIMissionProposal(
                            new[] { participant },
                            MissionTypeIDs.Sabotage,
                            planet,
                            selectedTarget: target,
                            decoyParticipants: GetAttackTargetDecoys(
                                context,
                                availableParticipants,
                                participant,
                                MissionTypeIDs.Sabotage,
                                planet
                            )
                        )
                    );
                }
            }
        }

        private IEnumerable<IMissionParticipant> GetAttackTargetDecoys(
            AITurnContext context,
            IReadOnlyList<IMissionParticipant> availableParticipants,
            IMissionParticipant mainParticipant,
            string missionTypeId,
            Planet target
        )
        {
            if (!context.Assessment.IsAttackTargetBlockedByShields(target))
                return Enumerable.Empty<IMissionParticipant>();

            return availableParticipants
                .Where(candidate =>
                    candidate != mainParticipant && candidate.CanPerformMission(missionTypeId)
                )
                .OrderByDescending(candidate =>
                    candidate.GetEffectiveRating(OfficerRating.Espionage)
                )
                .ThenBy(candidate => GetTravelDistance(candidate, target))
                .ThenBy(candidate => candidate.InstanceID)
                .Take(1);
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
            List<AIProposal> proposals
        )
        {
            if (!CanPlanHostileMission(context))
                return;

            foreach ((Planet planet, Officer targetOfficer) in GetOfficerTargetCandidates(context))
            {
                if (participant.CanPerformMission(MissionTypeIDs.Abduction))
                    TryAddProposal(
                        context,
                        proposals,
                        new AIMissionProposal(
                            new[] { participant },
                            MissionTypeIDs.Abduction,
                            planet,
                            selectedTarget: targetOfficer,
                            targetOfficer: targetOfficer
                        )
                    );

                if (participant.CanPerformMission(MissionTypeIDs.Assassination))
                    TryAddProposal(
                        context,
                        proposals,
                        new AIMissionProposal(
                            new[] { participant },
                            MissionTypeIDs.Assassination,
                            planet,
                            selectedTarget: targetOfficer,
                            targetOfficer: targetOfficer
                        )
                    );
            }
        }

        private void TryAddProposal(
            AITurnContext context,
            List<AIProposal> proposals,
            AIMissionProposal proposal
        )
        {
            if (proposal?.CanExecute(context) == true)
                proposals.Add(proposal);
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
            return context
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
                .Take(context.Game.Config.AI.MissionPlanning.DiplomacyCandidatePlanetLimit);
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
                .ThenBy(planet => planet.InstanceID)
                .Take(context.Game.Config.AI.MissionPlanning.ReconnaissanceCandidatePlanetLimit);
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
                .Take(context.Game.Config.AI.MissionPlanning.ResearchCandidatePlanetLimit);
        }

        private IEnumerable<Planet> GetEspionageCandidatePlanets(AITurnContext context)
        {
            int minimumAge = context.Game.Config.AI.MissionPlanning.EspionageRefreshIntervalTicks;
            return context
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
                .Take(context.Game.Config.AI.MissionPlanning.EspionageCandidatePlanetLimit);
        }

        private IEnumerable<Planet> GetSabotageCandidatePlanets(AITurnContext context)
        {
            return GetFreshEnemyPlanets(context)
                .Where(planet => context.Assessment.GetPlanetBuildingCount(planet) > 0)
                .Shuffle(context.Random)
                .OrderByDescending(context.Assessment.IsAttackTargetBlockedByShields)
                .ThenByDescending(context.Assessment.GetPlanetBuildingCount)
                .Take(context.Game.Config.AI.MissionPlanning.SabotageCandidatePlanetLimit);
        }

        private IEnumerable<IManufacturable> GetSabotageTargets(
            AITurnContext context,
            Planet planet
        )
        {
            return planet
                .GetChildren<IManufacturable>(target =>
                    target.GetOwnerInstanceID() != context.Faction.InstanceID
                    && target.GetManufacturingStatus() == ManufacturingStatus.Complete
                    && target.Movement == null
                    && !HasActiveSabotageTarget(context, target.InstanceID)
                )
                .OrderByDescending(target =>
                    context.Assessment.IsAssaultBlockingShield(planet, target)
                )
                .ThenByDescending(target =>
                    target.GetConstructionCost() + target.GetMaintenanceCost()
                )
                .ThenBy(target => target.InstanceID)
                .Take(context.Game.Config.AI.MissionPlanning.SabotageTargetsPerPlanetLimit);
        }

        private IEnumerable<(Planet Planet, Officer TargetOfficer)> GetOfficerTargetCandidates(
            AITurnContext context
        )
        {
            return context
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
                .Take(context.Game.Config.AI.MissionPlanning.OfficerTargetCandidateLimit);
        }

        private IEnumerable<Planet> GetFreshEnemyPlanets(AITurnContext context)
        {
            int maximumAge = context
                .Game
                .Config
                .AI
                .MissionPlanning
                .HostileMissionMaximumIntelAgeTicks;
            return context.Assessment.EnemyPlanets.Where(planet =>
                context.Assessment.GetPlanetIntelAge(planet) <= maximumAge
            );
        }

        private IEnumerable<Planet> GetRecruitmentCandidatePlanets(AITurnContext context)
        {
            return context
                .Assessment.OwnedPlanets.Where(planet => planet.IsColonized && !planet.IsDestroyed)
                .OrderBy(planet => context.Assessment.GetFactionPopularSupport(planet))
                .ThenBy(planet => planet.InstanceID);
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

                if (
                    CountActiveResearchMissions(context, discipline)
                    >= context
                        .Game
                        .Config
                        .AI
                        .MissionPlanning
                        .MaximumConcurrentResearchMissionsPerDiscipline
                )
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

        private bool CanPlanHostileMission(AITurnContext context)
        {
            return GetActiveMissions(context).Count(mission => IsHostileMission(mission.ConfigKey))
                < context.Game.Config.AI.MissionPlanning.MaximumConcurrentHostileMissions;
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

        private int CountActiveResearchMissions(
            AITurnContext context,
            ResearchDiscipline discipline
        )
        {
            return GetActiveMissions(context)
                .OfType<ResearchMission>()
                .Count(mission => mission.Discipline == discipline);
        }

        private IEnumerable<Mission> GetActiveMissions(AITurnContext context)
        {
            return context
                .Game.GetSceneNodesByType<Mission>()
                .Where(mission => mission.GetOwnerInstanceID() == context.Faction.InstanceID);
        }

        private static bool IsHostileMission(string missionTypeId)
        {
            return missionTypeId == MissionTypeIDs.Sabotage
                || missionTypeId == MissionTypeIDs.Abduction
                || missionTypeId == MissionTypeIDs.Assassination
                || missionTypeId == MissionTypeIDs.InciteUprising;
        }

        private int GetDiplomacyCandidatePriority(AITurnContext context, Planet planet)
        {
            int support = context.Assessment.GetFactionPopularSupport(planet);

            if (context.Assessment.IsOwnedPlanet(planet))
                return 100 - support;

            return context.Assessment.IsNeutralPlanet(planet) ? support : 0;
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
