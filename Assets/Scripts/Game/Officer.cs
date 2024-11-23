using System.Collections;
using System.Collections.Generic;
using System.Drawing;

/// <summary>
///
/// </summary>
public enum OfficerRank
{
    None,
    Commander,
    General,
    Admiral,
}

/// <summary>
/// Represents an officer that can be used in missions.
/// </summary>
public class Officer : LeafNode, IMissionParticipant, IMovable
{
    // Research Info
    public int ShipResearch { get; set; }
    public int TroopResearch { get; set; }
    public int FacilityResearch { get; set; }

    // Officer Info
    public bool IsMain { get; set; }
    public bool IsCaptured { get; set; }
    public bool CanBetray { get; set; }
    public bool IsTraitor { get; set; }
    public bool IsForceSensitive { get; set; }
    public int Loyalty { get; set; }

    // Jedi Info
    public bool IsJedi { get; set; }

    [PersistableIgnore]
    public int JediProbability { get; set; }
    public int JediLevel { get; set; }
    public int JediLevelVariance { get; set; }

    // Rank Info
    public OfficerRank[] AllowedRanks { get; set; }
    public OfficerRank CurrentRank { get; set; }

    // Owner Info
    public string InitialParentInstanceID { get; set; }

    // Variance Info
    [PersistableIgnore]
    public int DiplomacyVariance { get; set; }

    [PersistableIgnore]
    public int EspionageVariance { get; set; }

    [PersistableIgnore]
    public int CombatVariance { get; set; }

    [PersistableIgnore]
    public int LeadershipVariance { get; set; }

    [PersistableIgnore]
    public int LoyaltyVariance { get; set; }

    [PersistableIgnore]
    public int FacilityResearchVariance { get; set; }

    [PersistableIgnore]
    public int TroopResearchVariance { get; set; }

    [PersistableIgnore]
    public int ShipResearchVariance { get; set; }

    // Movement Info
    public MovementStatus MovementStatus { get; set; }
    public int PositionX { get; set; }
    public int PositionY { get; set; }

    // Mission Skill Related
    public Dictionary<MissionParticipantSkill, int> Skills { get; set; } =
        new Dictionary<MissionParticipantSkill, int>
        {
            { MissionParticipantSkill.Diplomacy, 0 },
            { MissionParticipantSkill.Espionage, 0 },
            { MissionParticipantSkill.Combat, 0 },
            { MissionParticipantSkill.Leadership, 0 },
        };
    public bool CanImproveMissionSkill => true;

    /// <summary>
    /// Default constructor used for serialization.
    /// </summary>
    public Officer() { }

    /// <summary>
    ///
    /// </summary>
    /// <param name="skill"></param>
    /// <returns></returns>
    public int GetSkillValue(MissionParticipantSkill skill)
    {
        return Skills[skill];
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="skill"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public int SetSkillValue(MissionParticipantSkill skill, int value)
    {
        return Skills[skill] = value;
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public bool IsOnMission()
    {
        return GetParent() is Mission;
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public bool IsMovable()
    {
        return MovementStatus == MovementStatus.Idle && !this.IsOnMission();
    }
}
