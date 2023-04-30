using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum BuildingSlot
{
    Ground,
    Orbit,
}

/// <summary>
///
/// </summary>
public class Building : GameNode, IManufacturable
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

    /// <summary>
    /// Default constructor.
    /// </summary>
    public Building() { }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public override GameNode[] GetChildNodes()
    {
        // Leaf node
        return new GameNode[] { };
    }
}
