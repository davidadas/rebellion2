using System.Collections.Generic;
using Rebellion.SceneGraph;

namespace Rebellion.Game
{
    public enum MissionParticipantSkill
    {
        Diplomacy,
        Espionage,
        Combat,
        Leadership,
    }

    /// <summary>
    /// Class used to store and manage the stats of a mission participant.
    /// Doing so allows for the calculation of mission success probabilities as
    /// well as to improve skills after a mission is completed.
    /// </summary>
    public interface IMissionParticipant : ISceneNode, IMovable
    {
        // Mission Stats
        public Dictionary<MissionParticipantSkill, int> Skills { get; set; }
        public bool CanImproveMissionSkill { get; }

        public void SetMissionSkillValue(MissionParticipantSkill skill, int value);

        /// <summary>
        /// Returns whether this participant is qualified to perform the given mission type.
        /// Officers are unrestricted; spec ops check their AllowedMissionTypes.
        /// </summary>
        public bool CanPerformMission(MissionType missionType);

        /// <summary>
        /// Returns whether this participant is currently assigned to a mission.
        /// </summary>
        public bool IsOnMission();
    }
}
