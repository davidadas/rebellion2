using System.Collections.Generic;
using Rebellion.Game.Research;

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

        private static readonly List<MissionOption> _options = new List<MissionOption>
        {
            CreateOption(MissionTypeIDs.Diplomacy),
            CreateOption(MissionTypeIDs.Espionage),
            CreateOption(
                MissionTypeIDs.Research,
                ResearchDiscipline.ShipDesign,
                "Ship Design Research"
            ),
            CreateOption(
                MissionTypeIDs.Research,
                ResearchDiscipline.FacilityDesign,
                "Facility Design Research"
            ),
            CreateOption(
                MissionTypeIDs.Research,
                ResearchDiscipline.TroopTraining,
                "Troop Training Research"
            ),
            CreateOption(MissionTypeIDs.Reconnaissance),
            CreateOption(MissionTypeIDs.Recruitment),
            CreateOption(MissionTypeIDs.InciteUprising),
            CreateOption(MissionTypeIDs.SubdueUprising),
            CreateOption(MissionTypeIDs.JediTraining),
            CreateOption(MissionTypeIDs.Rescue),
            CreateOption(MissionTypeIDs.Abduction),
            CreateOption(MissionTypeIDs.Assassination),
            CreateOption(MissionTypeIDs.Sabotage),
        };

        public static IReadOnlyList<MissionOption> Options => _options;

        /// <summary>
        /// Returns the mission definition for a mission type ID.
        /// </summary>
        /// <param name="instanceID">The mission type ID to look up.</param>
        /// <returns>The matching definition, or null when the ID is unknown.</returns>
        public static MissionDefinition Get(string instanceID)
        {
            return instanceID != null && _definitions.TryGetValue(instanceID, out var definition)
                ? definition
                : null;
        }

        /// <summary>
        /// Creates a mission definition entry for the catalog.
        /// </summary>
        /// <param name="instanceID">The mission type ID.</param>
        /// <param name="behavior">The behavior used by this mission type.</param>
        /// <param name="participantRating">The rating primary participants use for the mission.</param>
        /// <param name="decoyParticipantRating">The rating decoy participants use for the mission.</param>
        /// <param name="displayName">The display name for the mission.</param>
        /// <returns>The configured mission definition.</returns>
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

        /// <summary>
        /// Creates a selectable mission option from a catalog mission definition.
        /// </summary>
        /// <param name="missionTypeID">The mission type ID used to find the backing definition.</param>
        /// <param name="discipline">The research discipline used by research options.</param>
        /// <param name="displayName">The display name override for this option.</param>
        /// <returns>The configured mission option.</returns>
        private static MissionOption CreateOption(
            string missionTypeID,
            ResearchDiscipline? discipline = null,
            string displayName = null
        )
        {
            MissionDefinition definition = Get(missionTypeID);
            if (definition == null)
                throw new KeyNotFoundException(missionTypeID);

            return new MissionOption(
                missionTypeID,
                displayName ?? definition.DisplayName,
                definition.ParticipantRating,
                definition.DecoyParticipantRating,
                discipline
            );
        }
    }
}
