using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;

/// <summary>
/// Represents the possible slots where a building can be placed.
/// </summary>
public enum BuildingSlot
{
    Ground,
    Orbit,
}

/// <summary>
/// Represents the different types of buildings available in the game.
/// </summary>
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
/// Represents a building in the game, implementing both IManufacturable and IMovable interfaces.
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
    public int WeaponPower { get; set; }

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
    /// Default constructor for the Building class.
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
    /// Returns the rate at which this building processes resources or units.
    /// </summary>
    /// <returns>The process rate of the building.</returns>
    public int GetProcessRate()
    {
        return ProcessRate;
    }

    /// <summary>
    /// Returns the type of production this building is capable of.
    /// </summary>
    /// <returns>The production type of the building.</returns>
    public ManufacturingType GetProductionType()
    {
        return ProductionType;
    }

    /// <summary>
    /// Returns this node's manufacturing type.
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
    /// <param name="manufacturingStatus">The manufacturing status to set.</param>
    /// <exception cref="GameStateException">Thrown when trying to set status to Building after it's Complete.</exception>
    public void SetManufacturingStatus(ManufacturingStatus manufacturingStatus)
    {
        if (
            ManufacturingStatus == ManufacturingStatus.Complete
            && manufacturingStatus == ManufacturingStatus.Building
        )
        {
            throw new GameStateException(
                "Invalid manufacturing status. Cannot set to Building once Complete."
            );
        }
        ManufacturingStatus = manufacturingStatus;
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
