using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum BuildingSlot
{
    Ground,
    Orbit,
}

public class Building : Manufacturable
{
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
