using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Represents a special forces unit that can be used in missions.
/// </summary>
public class SpecialForces : MissionParticipant, IManufacturable
{
    // Construction Info
    public int ConstructionCost { get; set; }
    public int MaintenanceCost { get; set; }
    public int BaseBuildSpeed { get; set; }
    public int RequiredResearchLevel { get; set; }

    // Status Info
    public ManufacturingStatus ManufacturingStatus { get; set; }
    public MovementStatus MovementStatus { get; set; }

    /// <summary>
    /// Default constructor used for serialization.
    /// </summary>
    public SpecialForces() { }

    /// <summary>
    /// Determines if the unit is currently on a mission.
    /// </summary>
    /// <returns>True if the unit is on a mission, otherwise false.</returns>
    public bool IsOnMission()
    {
        return GetParent() is Mission;
    }

    public override void SetMissionSkillValue(MissionParticipantSkill skill, int value)
    {
        throw new InvalidSceneOperationException("Special forces units cannot set mission skills.");
    }
}
