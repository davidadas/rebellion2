using System.Collections.Generic;

namespace Rebellion.Game.Missions
{
    public static class MissionDefinitionCatalog
    {
        private static readonly Dictionary<string, MissionDefinition> _definitions = new Dictionary<
            string,
            MissionDefinition
        >
        {
            {
                MissionTypeIDs.Abduction,
                Create(
                    MissionTypeIDs.Abduction,
                    new AbductionMissionBehavior(),
                    OfficerRating.Combat,
                    OfficerRating.Espionage
                )
            },
            {
                MissionTypeIDs.Assassination,
                Create(
                    MissionTypeIDs.Assassination,
                    new AssassinationMissionBehavior(),
                    OfficerRating.Combat,
                    OfficerRating.Espionage
                )
            },
            {
                MissionTypeIDs.Diplomacy,
                Create(
                    MissionTypeIDs.Diplomacy,
                    new DiplomacyMissionBehavior(),
                    OfficerRating.Diplomacy
                )
            },
            {
                MissionTypeIDs.Espionage,
                Create(
                    MissionTypeIDs.Espionage,
                    new EspionageMissionBehavior(),
                    OfficerRating.Espionage,
                    OfficerRating.Espionage
                )
            },
            {
                MissionTypeIDs.InciteUprising,
                Create(
                    MissionTypeIDs.InciteUprising,
                    new InciteUprisingMissionBehavior(),
                    OfficerRating.Leadership,
                    OfficerRating.Espionage,
                    "Incite Uprising"
                )
            },
            {
                MissionTypeIDs.JediTraining,
                Create(
                    MissionTypeIDs.JediTraining,
                    new JediTrainingMissionBehavior(),
                    OfficerRating.Diplomacy,
                    displayName: "Jedi Training"
                )
            },
            {
                MissionTypeIDs.Reconnaissance,
                Create(
                    MissionTypeIDs.Reconnaissance,
                    new ReconnaissanceMissionBehavior(),
                    OfficerRating.Espionage,
                    OfficerRating.Espionage
                )
            },
            {
                MissionTypeIDs.Recruitment,
                Create(
                    MissionTypeIDs.Recruitment,
                    new RecruitmentMissionBehavior(),
                    OfficerRating.Leadership
                )
            },
            {
                MissionTypeIDs.Research,
                Create(MissionTypeIDs.Research, new ResearchMissionBehavior(), OfficerRating.None)
            },
            {
                MissionTypeIDs.Rescue,
                Create(
                    MissionTypeIDs.Rescue,
                    new RescueMissionBehavior(),
                    OfficerRating.Combat,
                    OfficerRating.Espionage
                )
            },
            {
                MissionTypeIDs.Sabotage,
                Create(
                    MissionTypeIDs.Sabotage,
                    new SabotageMissionBehavior(),
                    OfficerRating.Combat,
                    OfficerRating.Espionage
                )
            },
            {
                MissionTypeIDs.SubdueUprising,
                Create(
                    MissionTypeIDs.SubdueUprising,
                    new SubdueUprisingMissionBehavior(),
                    OfficerRating.Leadership,
                    displayName: "Subdue Uprising"
                )
            },
        };

        public static MissionDefinition Get(string instanceID)
        {
            return instanceID != null && _definitions.TryGetValue(instanceID, out var definition)
                ? definition
                : null;
        }

        private static MissionDefinition Create(
            string instanceID,
            MissionBehavior behavior,
            OfficerRating participantRating,
            OfficerRating decoyParticipantRating = OfficerRating.None,
            string displayName = null
        )
        {
            return new MissionDefinition
            {
                InstanceID = instanceID,
                DisplayName = displayName ?? instanceID,
                ParticipantRating = participantRating,
                DecoyParticipantRating = decoyParticipantRating,
                Behavior = behavior,
            };
        }
    }
}
