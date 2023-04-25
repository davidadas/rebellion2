using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///
/// </summary>
public interface IManufacturable
{
    // Construction Info
    public int ConstructionCost { get; set; }
    public int MaintenanceCost { get; set; }
    public int BaseBuildSpeed { get; set; }

    // Owner Info
    public string[] AllowedOwnerGameIDs { get; set; }
    public string OwnerGameID { get; set; }

    // Research Info
    public int RequiredResearchLevel { get; set; }
}
