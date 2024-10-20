using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ManufacturingStatus
{
    Building,
    Queued,
    Complete
}

/// <summary>
///
/// </summary>
public interface IManufacturable : IMovable
{
    // Construction Info
    public int ConstructionCost { get; set; }
    public int MaintenanceCost { get; set; }
    public int BaseBuildSpeed { get; set; }

    // Research Info
    public int RequiredResearchLevel { get; set; }

    public ManufacturingStatus ManufacturingStatus { get; set; }
}
