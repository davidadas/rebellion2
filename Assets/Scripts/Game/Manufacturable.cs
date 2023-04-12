using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Manufacturable : GameNode
{
    // Construction Info
    public int ConstructionCost;
    public int Maintenancecost;
    public int BaseBuildSpeed;

    // Owner Info
    public string[] AllowedOwnerGameIDs;
    public string OwnerGameID;

    // Research Info
    public int RequiredResearchLevel;

    /// <summary>
    /// Default constructor.
    /// </summary>
    public Manufacturable() { }
}
