public enum MissionParticipantSkill
{
    None,
    Diplomacy,
    Espionage,
    Combat,
    Leadership
}

/// <summary>
/// Class used to store and manage the stats of a mission participant.
/// Doing so allows for the calculation of mission success probabilities as
/// well as to improve skills after a mission is completed.
/// </summary>
public class MissionParticipant : LeafNode
{
    // Mission Stats.
    public SerializableDictionary<MissionParticipantSkill, int> Skills = new SerializableDictionary<MissionParticipantSkill, int>()
        {
            { MissionParticipantSkill.Diplomacy, 0 },
            { MissionParticipantSkill.Espionage, 0 },
               ///  { MissionParticipantSkill.Combat, 0 },
            { MissionParticipantSkill.Leadership, 0 }
        };

    /// <summary>
    /// Default constructor.
    /// </summary>
    public MissionParticipant() { }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="skill"></param>
    public int GetSkill(MissionParticipantSkill skill)
    {
        return Skills[skill];
    }

    /// <summary>
    /// Sets the value of a skill.
    /// </summary>
    /// <param name="skill">The skill to set.</param>
    /// <param name="value">The value to set the skill to.</param>
    /// <returns>The new value of the skill.</returns>
    public int SetSkill(MissionParticipantSkill skill, int value)
    {
        return Skills[skill] = value;
    }

}
