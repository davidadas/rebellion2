using System.Collections.Generic;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

namespace Rebellion.Game.Missions
{
    /// <summary>
    /// Bundles all inputs needed to create a mission.
    /// </summary>
    public class MissionContext
    {
        public GameRoot Game { get; set; }
        public string OwnerInstanceId { get; set; }
        public ISceneNode Target { get; set; }
        public ISceneNode SpecificTarget { get; set; }
        public List<IMissionParticipant> MainParticipants { get; set; }
        public List<IMissionParticipant> DecoyParticipants { get; set; }
        public IRandomNumberProvider RandomProvider { get; set; }

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
