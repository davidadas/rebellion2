using System.Collections.Generic;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

namespace Rebellion.Game.Missions
{
    /// <summary>
    /// Carries mission creation input before and after live scene graph resolution.
    /// </summary>
    public class MissionStartRequest
    {
        public GameRoot Game { get; set; }
        public string MissionTypeID { get; set; }
        public string OwnerInstanceID { get; set; }
        public ISceneNode Target { get; set; }
        public ISceneNode SpecificTarget { get; set; }
        public List<IMissionParticipant> MainParticipants { get; set; } =
            new List<IMissionParticipant>();
        public List<IMissionParticipant> DecoyParticipants { get; set; } =
            new List<IMissionParticipant>();
        public Officer TargetOfficer { get; set; }
        public ResearchDiscipline? Discipline { get; set; }

        /// <summary>
        /// Creates a copy of this request with resolved owner, participant, and target references.
        /// </summary>
        /// <param name="ownerInstanceID">The faction that owns the mission.</param>
        /// <param name="mainParticipants">The resolved primary mission participants.</param>
        /// <param name="decoyParticipants">The resolved decoy mission participants.</param>
        /// <param name="target">The resolved mission target.</param>
        /// <returns>The resolved mission start request.</returns>
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
                Game = Game,
                OwnerInstanceID = ownerInstanceID,
                Target = target,
                SpecificTarget = SpecificTarget,
                MainParticipants = mainParticipants,
                DecoyParticipants = decoyParticipants,
                TargetOfficer = TargetOfficer,
                Discipline = Discipline,
            };
        }
    }
}
