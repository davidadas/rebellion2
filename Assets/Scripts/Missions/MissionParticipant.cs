using System;

public enum MissionParticipantSkill
{
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
    public SerializableDictionary<MissionParticipantSkill, int> Skills { get; set; }

    public MissionParticipant()
    {
        Skills = new SerializableDictionary<MissionParticipantSkill, int>
        {
            { MissionParticipantSkill.Diplomacy, 0 },
            { MissionParticipantSkill.Espionage, 0 },
            { MissionParticipantSkill.Combat, 0 },
            { MissionParticipantSkill.Leadership, 0 }
        };
    }
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="skill"></param>
    /// <param name="value"></param>
    public virtual void SetMissionSkillValue(MissionParticipantSkill skill, int value)
    {
        Skills[skill] = value;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="skill"></param>
    /// <returns></returns>
    public int GetMissionSkillValue(MissionParticipantSkill skill)
    {
        return Skills[skill];
    }
}
