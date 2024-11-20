using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;

/// <summary>
/// Represents a special forces unit that can be used in missions.
/// </summary>
public class SpecialForces : LeafNode, IMissionParticipant, IManufacturable, IMovable
{
    // Construction Info
    public int ConstructionCost { get; set; }
    public int MaintenanceCost { get; set; }
    public int BaseBuildSpeed { get; set; }
    public int RequiredResearchLevel { get; set; }

    // Manufacturing Info
    public string ProducerOwnerID { get; set; }
    public int ManufacturingProgress { get; set; } = 0;
    public ManufacturingStatus ManufacturingStatus { get; set; } = ManufacturingStatus.Building;

    // Movement Info
    public MovementStatus MovementStatus { get; set; }
    public int PositionX { get; set; }
    public int PositionY { get; set; }

    // Mission Info
    public Dictionary<MissionParticipantSkill, int> Skills { get; set; } =
        new Dictionary<MissionParticipantSkill, int>
        {
            { MissionParticipantSkill.Diplomacy, 0 },
            { MissionParticipantSkill.Espionage, 0 },
            { MissionParticipantSkill.Combat, 0 },
            { MissionParticipantSkill.Leadership, 0 },
        };

    /// <summary>
    /// Default constructor used for serialization.
    /// </summary>
    public SpecialForces() { }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public ManufacturingType GetManufacturingType()
    {
        return ManufacturingType.Troop;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="skill"></param>
    /// <param name="value"></param>
    /// <exception cref="InvalidSceneOperationException"></exception>
    public void SetMissionSkillValue(MissionParticipantSkill skill, int value)
    {
        throw new InvalidSceneOperationException("Special forces units cannot set mission skills.");
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public bool IsMovable()
    {
        return MovementStatus == MovementStatus.InTransit && !IsOnMission();
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public bool IsOnMission()
    {
        ISceneNode parent = GetParent();

        // Ensure the parent is a mission and that the mission is not complete.
        return parent is Mission && !(parent as Mission).IsComplete();
    }
}
