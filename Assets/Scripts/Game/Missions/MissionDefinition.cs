namespace Rebellion.Game.Missions
{
    public class MissionDefinition
    {
        public string InstanceID { get; set; }
        public string DisplayName { get; set; }
        public OfficerRating ParticipantRating { get; set; }
        public OfficerRating DecoyParticipantRating { get; set; }
        public MissionBehavior Behavior { get; set; }
    }
}
