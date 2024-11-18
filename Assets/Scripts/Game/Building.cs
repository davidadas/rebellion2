using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;

public enum BuildingSlot
{
    Ground,
    Orbit,
}

public enum BuildingType
{
    None,
    Mine,
    Refinery,
    Shipyard,
    TrainingFacility,
    ConstructionFacility,
    OrbitalDefense,
    PlanetaryDefense,
}

/// <summary>
///
/// </summary>
public class Building : LeafNode, IManufacturable, IMovable
{
    // Construction Info
    public int ConstructionCost { get; set; }
    public int MaintenanceCost { get; set; }
    public int BaseBuildSpeed { get; set; }
    public int RequiredResearchLevel { get; set; }

    // Building Info
    public BuildingType BuildingType { get; set; }
    public BuildingSlot BuildingSlot { get; set; }
    public int ProcessRate { get; set; }
    public int Bombardment { get; set; }
    public int WeaponStrength { get; set; }
    public int ShieldStrength { get; set; }

    // Manufacturing Info
    public string ProducerOwnerID { get; set; }
    public int ManufacturingProgress { get; set; } = 0;
    public ManufacturingStatus ManufacturingStatus { get; set; } = ManufacturingStatus.Building;
    public ManufacturingType ProductionType { get; set; }

    // Movement Info
    public int PositionX { get; set; }
    public int PositionY { get; set; }
    public MovementStatus MovementStatus { get; set; }

    /// <summary>
    /// Default constructor.
    /// </summary>
    public Building() { }

    /// <summary>
    /// Returns the building's slot type.
    /// </summary>
    /// <returns>The building's slot type.</returns>
    public BuildingSlot GetBuildingSlot()
    {
        return BuildingSlot;
    }

    /// <summary>
    /// Returns the building's type.
    /// </summary>
    /// <returns>The building's type.</returns>
    public BuildingType GetBuildingType()
    {
        return BuildingType;
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public int GetProcessRate()
    {
        return ProcessRate;
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public ManufacturingType GetProductionType()
    {
        return ProductionType;
    }

    /// <summary>
    /// Returns this node's manuacturing type.
    /// </summary>
    /// <returns>ManufacturingType.Building</returns>
    public ManufacturingType GetManufacturingType()
    {
        return ManufacturingType.Building;
    }

    /// <summary>
    /// Returns this unit's manufacturing status.
    /// </summary>
    /// <returns>The manufacturing status.</returns>
    public ManufacturingStatus GetManufacturingStatus()
    {
        return ManufacturingStatus;
    }

    /// <summary>
    /// Sets the manufacturing status of the building.
    /// </summary>
    /// <param name="status">The manufacturing status to set.</param>
    public void SetManufacturingStatus(ManufacturingStatus status)
    {
        if (status == ManufacturingStatus.Complete && status == ManufacturingStatus.Building)
        {
            throw new ArgumentException(
                "Invalid manufacturing status. Cannot set to Building once Complete."
            );
        }
        ManufacturingStatus = status;
    }

    /// <summary>
    /// Returns this building's ability to move.
    /// </summary>
    /// <returns>True if the building is not in transit; otherwise, false.</returns>
    public bool IsMovable()
    {
        return MovementStatus != MovementStatus.InTransit;
    }
}
