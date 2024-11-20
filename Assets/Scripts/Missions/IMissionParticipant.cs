using System;
using System.Collections.Generic;

public enum MissionParticipantSkill
{
    Diplomacy,
    Espionage,
    Combat,
    Leadership,
}

/// <summary>
/// Class used to store and manage the stats of a mission participant.
/// Doing so allows for the calculation of mission success probabilities as
/// well as to improve skills after a mission is completed.
/// </summary>
public interface IMissionParticipant : ISceneNode
{
    // Mission Stats.
    public Dictionary<MissionParticipantSkill, int> Skills { get; set; }

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

    /// <summary>
    ///
    /// </summary>
    public bool IsOnMission();
}
