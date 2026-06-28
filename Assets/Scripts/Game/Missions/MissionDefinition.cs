namespace Rebellion.Game.Missions
{
    /// <summary>
    /// Defines the configuration and behavior used by one mission type.
    /// </summary>
    public class MissionDefinition
    {
        public string InstanceID { get; set; }
        public string DisplayName { get; set; }
        public OfficerRating ParticipantRating { get; set; }
        public OfficerRating DecoyParticipantRating { get; set; }
        public MissionBehavior Behavior { get; set; }
    }
}
