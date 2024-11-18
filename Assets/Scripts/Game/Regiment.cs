using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;

/// <summary>
/// Represents a regiment that can be stationed on a planet or capital ship.
/// </summary>
public class Regiment : LeafNode, IManufacturable, IMovable
{
    // Construction Info
    public int ConstructionCost { get; set; }
    public int MaintenanceCost { get; set; }
    public int BaseBuildSpeed { get; set; }
    public int RequiredResearchLevel { get; set; }

    // Regiment Info
    public int AttackRating { get; set; }
    public int DefenseRating { get; set; }
    public int DetectionRating { get; set; }
    public int BombardmentDefense { get; set; }

    // Status Info
    public string ProducerOwnerID { get; set; }
    public int ManufacturingProgress { get; set; } = 0;
    public ManufacturingStatus ManufacturingStatus { get; set; } = ManufacturingStatus.Building;
    public MovementStatus MovementStatus { get; set; }

    // Movement Info
    public int PositionX { get; set; }
    public int PositionY { get; set; }

    /// <summary>
    /// Default constructor used for serialization.
    /// </summary>
    public Regiment() { }

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
    /// <returns></returns>
    public bool IsMovable()
    {
        return MovementStatus == MovementStatus.InTransit;
    }
}
