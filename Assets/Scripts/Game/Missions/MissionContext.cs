using System.Collections.Generic;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Game.Missions
{
    /// <summary>
    /// Bundles all inputs needed to create a mission.
    /// </summary>
    public class MissionContext
    {
        /// <summary>
        /// Game state used to resolve creation rules.
        /// </summary>
        public GameRoot Game { get; set; }

        /// <summary>
        /// Mission type requested by the caller.
        /// </summary>
        public string MissionTypeID { get; set; }

        /// <summary>
        /// Instance ID of the faction that will own the mission.
        /// </summary>
        public string OwnerInstanceId { get; set; }

        /// <summary>
        /// Scene node where the mission will be attached.
        /// </summary>
        public ISceneNode Target { get; set; }

        /// <summary>
        /// Optional scene node selected inside the mission target.
        /// </summary>
        public ISceneNode SpecificTarget { get; set; }

        /// <summary>
        /// Primary participants assigned to perform the mission.
        /// </summary>
        public List<IMissionParticipant> MainParticipants { get; set; }

        /// <summary>
        /// Decoy participants assigned to distract defenders.
        /// </summary>
        public List<IMissionParticipant> DecoyParticipants { get; set; }

        /// <summary>
        /// The specific officer to target for Abduction, Assassination, and Rescue missions.
        /// </summary>
        public Officer TargetOfficer { get; set; }

        /// <summary>
        /// The research discipline for Research missions.
        /// </summary>
        public ResearchDiscipline? Discipline { get; set; }
    }
}
