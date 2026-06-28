using System.Collections.Generic;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Game.Missions
{
    /// <summary>
    /// Carries mission creation input from UI and planning code.
    /// </summary>
    public class MissionStartRequest
    {
        public string MissionTypeID { get; set; }
        public ISceneNode Target { get; set; }
        public ISceneNode SpecificTarget { get; set; }
        public List<IMissionParticipant> MainParticipants { get; set; } =
            new List<IMissionParticipant>();
        public List<IMissionParticipant> DecoyParticipants { get; set; } =
            new List<IMissionParticipant>();
        public Officer TargetOfficer { get; set; }
        public ResearchDiscipline? Discipline { get; set; }
    }
}
