using System.Collections.Generic;

namespace Rebellion.Game.Results
{
    public enum MissionOutcome
    {
        Success,
        Failed,
        Foiled,
    }

    public class MissionCompletedResult : GameResult
    {
        public string MissionInstanceID { get; set; }
        public string MissionName { get; set; }
        public string TargetName { get; set; }
        public List<string> ParticipantInstanceIDs { get; set; } = new List<string>();
        public List<string> ParticipantNames { get; set; } = new List<string>();
        public MissionOutcome Outcome { get; set; }
    }
}
