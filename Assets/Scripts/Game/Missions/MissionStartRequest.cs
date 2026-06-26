using System.Collections.Generic;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Common;

namespace Rebellion.Game.Missions
{
    public class MissionStartRequest
    {
        public string MissionTypeID { get; set; }
        public string OwnerInstanceID { get; set; }
        public ISceneNode Target { get; set; }
        public ISceneNode SpecificTarget { get; set; }
        public List<IMissionParticipant> MainParticipants { get; set; } =
            new List<IMissionParticipant>();
        public List<IMissionParticipant> DecoyParticipants { get; set; } =
            new List<IMissionParticipant>();
        public IRandomNumberProvider RandomProvider { get; set; }
        public Officer TargetOfficer { get; set; }
        public ResearchDiscipline? Discipline { get; set; }

        public MissionStartRequest CreateResolved(
            string ownerInstanceID,
            List<IMissionParticipant> mainParticipants,
            List<IMissionParticipant> decoyParticipants,
            ISceneNode target
        )
        {
            return new MissionStartRequest
            {
                MissionTypeID = MissionTypeID,
                OwnerInstanceID = ownerInstanceID,
                Target = target,
                SpecificTarget = SpecificTarget,
                MainParticipants = mainParticipants,
                DecoyParticipants = decoyParticipants,
                RandomProvider = RandomProvider,
                TargetOfficer = TargetOfficer,
                Discipline = Discipline,
            };
        }
    }
}
