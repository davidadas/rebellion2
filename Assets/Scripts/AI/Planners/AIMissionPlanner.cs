using System.Collections.Generic;
using System.Linq;
using Rebellion.AI.Director;
using Rebellion.AI.Proposals;
using Rebellion.Game.Factions;
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
            {
                return new List<AIProposal>();
            }

            return CreateMissionProposals(context);
        }

        /// <summary>
        /// Creates mission proposals for all available mission participants.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Mission proposals generated for this faction.</returns>
        private List<AIProposal> CreateMissionProposals(AITurnContext context)
        {
            List<AIProposal> proposals = new List<AIProposal>();

            foreach (
                IMissionParticipant participant in context.Assessment.AvailableMissionParticipants
            )
            {
                AddRecruitmentProposals(context, participant, proposals);
                AddDiplomacyProposals(context, participant, proposals);

                if (participant is Officer officer)
                    AddResearchProposals(context, officer, proposals);

                AddSabotageProposals(context, participant, proposals);
                AddOfficerTargetMissionProposals(context, participant, proposals);
            }

            return proposals;
        }

        /// <summary>
        /// Adds a recruitment proposal when a main officer meets the leadership gate.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="participant">The participant to evaluate.</param>
        /// <param name="proposals">The proposal list to update.</param>
        private void AddRecruitmentProposals(
            AITurnContext context,
            IMissionParticipant participant,
            List<AIProposal> proposals
        )
        {
            if (!participant.IsMainCharacter())
                return;

            if (context.Game.GetUnrecruitedOfficers(context.Faction.InstanceID).Count == 0)
                return;

            if (
                participant.GetMissionSkillValue(MissionParticipantSkill.Leadership)
                < context.Game.Config.AI.RecruitmentMinimumLeadership
            )
            {
                return;
            }

            foreach (Planet target in GetRecruitmentCandidatePlanets(context))
                TryAddProposal(
                    context,
                    proposals,
                    new AIMissionProposal(participant, MissionType.Recruitment, target)
                );
        }

        /// <summary>
        /// Adds diplomacy proposals for friendly and neutral planets.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="participant">The participant to evaluate.</param>
        /// <param name="proposals">The proposal list to update.</param>
        private void AddDiplomacyProposals(
            AITurnContext context,
            IMissionParticipant participant,
            List<AIProposal> proposals
        )
        {
            if (
                participant.GetMissionSkillValue(MissionParticipantSkill.Diplomacy)
                < context.Game.Config.AI.DiplomacyMinimumSkill
            )
            {
                return;
            }

            foreach (Planet planet in GetDiplomacyCandidatePlanets(context))
                TryAddProposal(
                    context,
                    proposals,
                    new AIMissionProposal(participant, MissionType.Diplomacy, planet)
                );
        }

        /// <summary>
        /// Adds research proposals for owned planets with idle facilities.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="officer">The officer to evaluate.</param>
        /// <param name="proposals">The proposal list to update.</param>
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
                        context.Faction,
                        officer,
                        planet
                    )
                )
                    TryAddProposal(
                        context,
                        proposals,
                        new AIMissionProposal(officer, MissionType.Research, planet, discipline)
                    );
            }
        }

        /// <summary>
        /// Adds sabotage mission proposals for enemy planets.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="participant">The participant to evaluate.</param>
        /// <param name="proposals">The proposal list to update.</param>
        private void AddSabotageProposals(
            AITurnContext context,
            IMissionParticipant participant,
            List<AIProposal> proposals
        )
        {
            if (!CanConsiderCombatMissions(participant))
                return;

            foreach (Planet planet in GetSabotageCandidatePlanets(context))
                TryAddProposal(
                    context,
                    proposals,
                    new AIMissionProposal(participant, MissionType.Sabotage, planet)
                );
        }

        /// <summary>
        /// Adds targeted hostile mission proposals for enemy officers.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="participant">The participant to evaluate.</param>
        /// <param name="proposals">The proposal list to update.</param>
        private void AddOfficerTargetMissionProposals(
            AITurnContext context,
            IMissionParticipant participant,
            List<AIProposal> proposals
        )
        {
            if (!CanConsiderCombatMissions(participant))
                return;

            foreach ((Planet planet, Officer targetOfficer) in GetOfficerTargetCandidates(context))
            {
                TryAddProposal(
                    context,
                    proposals,
                    new AIMissionProposal(participant, MissionType.Abduction, planet, targetOfficer)
                );
                TryAddProposal(
                    context,
                    proposals,
                    new AIMissionProposal(
                        participant,
                        MissionType.Assassination,
                        planet,
                        targetOfficer
                    )
                );
            }
        }

        /// <summary>
        /// Adds a proposal if it can currently execute.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="proposals">The proposal list to update.</param>
        /// <param name="proposal">The proposal to add.</param>
        private void TryAddProposal(
            AITurnContext context,
            List<AIProposal> proposals,
            AIMissionProposal proposal
        )
        {
            if (proposal?.CanExecute(context) != true)
                return;

            proposals.Add(proposal);
        }

        /// <summary>
        /// Returns bounded diplomacy planet candidates.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Diplomacy candidate planets.</returns>
        private IEnumerable<Planet> GetDiplomacyCandidatePlanets(AITurnContext context)
        {
            return context
                .Assessment.KnownColonizedPlanets.Where(planet =>
                    context.Assessment.IsOwnedPlanet(planet)
                    || context.Assessment.IsNeutralPlanet(planet)
                )
                .Shuffle(context.Random)
                .OrderByDescending(planet => GetDiplomacyCandidatePriority(context, planet))
                .Take(context.Game.Config.AI.MissionPlanning.DiplomacyCandidatePlanetLimit);
        }

        /// <summary>
        /// Returns bounded research planet candidates.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="officer">The officer to assign.</param>
        /// <returns>Research candidate planets.</returns>
        private IEnumerable<Planet> GetResearchCandidatePlanets(
            AITurnContext context,
            Officer officer
        )
        {
            return context
                .Assessment.KnownColonizedPlanets.Where(context.Assessment.IsOwnedPlanet)
                .Where(planet => HasAvailableResearchDiscipline(context.Faction, officer, planet))
                .Shuffle(context.Random)
                .OrderByDescending(planet =>
                    GetAvailableResearchDisciplineCount(context.Faction, officer, planet)
                )
                .ThenByDescending(planet =>
                    GetStrongestResearchSkill(context.Faction, officer, planet)
                )
                .Take(context.Game.Config.AI.MissionPlanning.ResearchCandidatePlanetLimit);
        }

        /// <summary>
        /// Returns bounded sabotage planet candidates.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Sabotage candidate planets.</returns>
        private IEnumerable<Planet> GetSabotageCandidatePlanets(AITurnContext context)
        {
            return context
                .Assessment.KnownColonizedPlanets.Where(planet =>
                    context.Assessment.IsEnemyPlanet(planet)
                    && context.Assessment.GetPlanetBuildingCount(planet) > 0
                )
                .Shuffle(context.Random)
                .OrderByDescending(context.Assessment.GetPlanetBuildingCount)
                .Take(context.Game.Config.AI.MissionPlanning.SabotageCandidatePlanetLimit);
        }

        /// <summary>
        /// Returns bounded enemy officer mission candidates.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Targeted officer mission candidates.</returns>
        private IEnumerable<(Planet Planet, Officer TargetOfficer)> GetOfficerTargetCandidates(
            AITurnContext context
        )
        {
            return context
                .Assessment.TargetableEnemyOfficerMissionTargets.Shuffle(context.Random)
                .OrderByDescending(candidate => candidate.TargetOfficer.IsMain)
                .ThenByDescending(candidate =>
                    GetOfficerTargetCandidatePriority(candidate.TargetOfficer)
                )
                .Take(context.Game.Config.AI.MissionPlanning.OfficerTargetCandidateLimit);
        }

        /// <summary>
        /// Returns owned planets eligible for recruitment missions.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <returns>Recruitment candidate planets.</returns>
        private IEnumerable<Planet> GetRecruitmentCandidatePlanets(AITurnContext context)
        {
            return context
                .Assessment.OwnedPlanets.Where(planet => planet.IsColonized && !planet.IsDestroyed)
                .OrderBy(planet => context.Assessment.GetFactionPopularSupport(planet))
                .ThenBy(planet => planet.InstanceID);
        }

        /// <summary>
        /// Returns research disciplines available to the officer on the planet.
        /// </summary>
        /// <param name="faction">The faction issuing the mission.</param>
        /// <param name="officer">The officer to assign.</param>
        /// <param name="planet">The owned planet to evaluate.</param>
        /// <returns>Available research disciplines.</returns>
        private IEnumerable<ResearchDiscipline> GetAvailableResearchDisciplines(
            Faction faction,
            Officer officer,
            Planet planet
        )
        {
            foreach (ResearchDiscipline discipline in _researchDisciplines)
            {
                if (faction.IsResearchExhausted(discipline))
                    continue;

                if (planet.GetIdleManufacturingFacilities(discipline.ToManufacturingType()) <= 0)
                    continue;

                if (officer.GetResearchSkill(discipline) <= 0)
                    continue;

                yield return discipline;
            }
        }

        /// <summary>
        /// Returns whether any research discipline is available on the planet.
        /// </summary>
        /// <param name="faction">The faction issuing the mission.</param>
        /// <param name="officer">The officer to assign.</param>
        /// <param name="planet">The owned planet to evaluate.</param>
        /// <returns>True if at least one research discipline is available.</returns>
        private bool HasAvailableResearchDiscipline(Faction faction, Officer officer, Planet planet)
        {
            return GetAvailableResearchDisciplines(faction, officer, planet).Any();
        }

        /// <summary>
        /// Returns how many research disciplines are available on the planet.
        /// </summary>
        /// <param name="faction">The faction issuing the mission.</param>
        /// <param name="officer">The officer to assign.</param>
        /// <param name="planet">The owned planet to evaluate.</param>
        /// <returns>The available research discipline count.</returns>
        private int GetAvailableResearchDisciplineCount(
            Faction faction,
            Officer officer,
            Planet planet
        )
        {
            return GetAvailableResearchDisciplines(faction, officer, planet).Count();
        }

        /// <summary>
        /// Returns the strongest available research skill for the planet.
        /// </summary>
        /// <param name="faction">The faction issuing the mission.</param>
        /// <param name="officer">The officer to assign.</param>
        /// <param name="planet">The owned planet to evaluate.</param>
        /// <returns>The strongest available research skill.</returns>
        private int GetStrongestResearchSkill(Faction faction, Officer officer, Planet planet)
        {
            return GetAvailableResearchDisciplines(faction, officer, planet)
                .Select(officer.GetResearchSkill)
                .DefaultIfEmpty()
                .Max();
        }

        /// <summary>
        /// Returns whether the participant can consider combat missions.
        /// </summary>
        /// <param name="participant">The participant to inspect.</param>
        /// <returns>True if the participant has combat mission capability.</returns>
        private bool CanConsiderCombatMissions(IMissionParticipant participant)
        {
            return participant.GetMissionSkillValue(MissionParticipantSkill.Combat) > 0;
        }

        /// <summary>
        /// Returns the coarse diplomacy candidate priority.
        /// </summary>
        /// <param name="context">The current AI turn context.</param>
        /// <param name="planet">The planet to inspect.</param>
        /// <returns>The coarse diplomacy candidate priority.</returns>
        private int GetDiplomacyCandidatePriority(AITurnContext context, Planet planet)
        {
            int support = context.Assessment.GetFactionPopularSupport(planet);

            if (context.Assessment.IsOwnedPlanet(planet))
                return 100 - support;

            if (context.Assessment.IsNeutralPlanet(planet))
                return support;

            return 0;
        }

        /// <summary>
        /// Returns the coarse enemy officer target priority.
        /// </summary>
        /// <param name="officer">The officer to inspect.</param>
        /// <returns>The coarse enemy officer target priority.</returns>
        private int GetOfficerTargetCandidatePriority(Officer officer)
        {
            return officer.GetMissionSkillValue(MissionParticipantSkill.Combat)
                + officer.GetMissionSkillValue(MissionParticipantSkill.Espionage)
                + officer.GetMissionSkillValue(MissionParticipantSkill.Diplomacy)
                + officer.GetMissionSkillValue(MissionParticipantSkill.Leadership)
                + officer.GetResearchSkill(ResearchDiscipline.ShipDesign)
                + officer.GetResearchSkill(ResearchDiscipline.FacilityDesign)
                + officer.GetResearchSkill(ResearchDiscipline.TroopTraining);
        }
    }
}
