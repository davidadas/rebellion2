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
public interface IMissionParticipant : ISceneNode, IMovable
{
    // Mission Stats
    public Dictionary<MissionParticipantSkill, int> Skills { get; set; }
    public bool CanImproveMissionSkill { get; }

    /// <summary>
    /// Called to set the value of a mission skill. Provides a default implementation
    /// which sets the value of the skill.
    /// </summary>
    /// <param name="skill"></param>
    /// <param name="value"></param>
    public virtual void SetMissionSkillValue(MissionParticipantSkill skill, int value)
    {
        Skills[skill] = value;
    }

    /// <summary>
    /// Called to get the value of a mission skill. Provides a default implementation
    /// which returns the value of the skill.
    /// </summary>
    /// <param name="skill">The skill whose value to get.</param>
    /// <returns>The value of the skill.</returns>
    public int GetMissionSkillValue(MissionParticipantSkill skill)
    {
        return Skills[skill];
    }

    /// <summary>
    /// Called to determine whether the participant is the main character of the game.
    /// </summary>
    public bool IsOnMission();
}
