using System.Collections.Generic;
using Rebellion.SceneGraph;

namespace Rebellion.Game
{
    /// <summary>
    /// Represents a special forces unit that can be used in missions.
    /// </summary>
    public class SpecialForces : LeafNode, IMissionParticipant, IManufacturable, IMovable
    {
        // Construction Info
        public int ConstructionCost { get; set; }
        public int MaintenanceCost { get; set; }
        public int BaseBuildSpeed { get; set; }
        public int ResearchOrder { get; set; }
        public int ResearchDifficulty { get; set; }

        // Manufacturing Info
        public string ProducerOwnerID { get; set; }
        public string ProducerPlanetID { get; set; }
        public int ManufacturingProgress { get; set; } = 0;
        public ManufacturingStatus ManufacturingStatus { get; set; } = ManufacturingStatus.Building;

        // Movement Info
        public MovementState Movement { get; set; }

        // Mission Qualification
        public List<MissionType> AllowedMissionTypes { get; set; } = new List<MissionType>();

        // Mission Skill Related
        public Dictionary<MissionParticipantSkill, int> Skills { get; set; } =
            new Dictionary<MissionParticipantSkill, int>
            {
                { MissionParticipantSkill.Diplomacy, 0 },
                { MissionParticipantSkill.Espionage, 0 },
                { MissionParticipantSkill.Combat, 0 },
                { MissionParticipantSkill.Leadership, 0 },
            };
        public bool CanImproveMissionSkill => false;

        public bool CanPerformMission(MissionType missionType) =>
            AllowedMissionTypes.Contains(missionType);

        /// <summary>
        /// Default constructor used for deserialization.
        /// </summary>
        public SpecialForces() { }

        /// <summary>
        /// Returns the manufacturing type for this unit.
        /// </summary>
        /// <returns>The manufacturing type.</returns>
        public ManufacturingType GetManufacturingType()
        {
            return ManufacturingType.Troop;
        }

        public void SetMissionSkillValue(MissionParticipantSkill skill, int value) =>
            Skills[skill] = value;

        /// <summary>
        /// Returns whether the special forces unit can be ordered to move.
        /// </summary>
        /// <returns>True if the unit is not in transit and not on a mission; otherwise, false.</returns>
        public bool IsMovable()
        {
            return Movement == null && !IsOnMission();
        }

        /// <summary>
        /// Returns whether the special forces unit is currently assigned to a mission.
        /// </summary>
        /// <returns>True if the unit's parent is a <see cref="Mission"/>; otherwise, false.</returns>
        public bool IsOnMission()
        {
            return GetParent() is Mission;
        }
    }
}
