using System.Collections;
using System.Collections.Generic;

public enum BuildingSlot
{
    Ground,
    Orbit,
}

/// <summary>
///
/// </summary>
public class Building : LeafNode, IManufacturable
{
    // Construction Info
    public int ConstructionCost { get; set; }
    public int MaintenanceCost { get; set; }
    public int BaseBuildSpeed { get; set; }
    public int RequiredResearchLevel { get; set; }

    // Building Info
    public int ProcessRate;
    public int ProductionRate;
    public int Bombardment;
    public int WeaponStrength;
    public int ShieldStrength;
    public BuildingSlot Slot;

    // Status Info
    public ManufacturingStatus ManufacturingStatus { get; set; }
    public MovementStatus MovementStatus { get; set; }

    /// <summary>
    /// Default constructor.
    /// </summary>
    public Building() { }
}
