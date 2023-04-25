using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///
/// </summary>
public class Regiment : GameNode, IManufacturable
{
    // Construction Info
    public int ConstructionCost { get; set; }
    public int MaintenanceCost { get; set; }
    public int BaseBuildSpeed { get; set; }
    public string[] AllowedOwnerGameIDs { get; set; }
    public string OwnerGameID { get; set; }
    public int RequiredResearchLevel { get; set; }

    // Regiment Info
    public int AttackRating;
    public int DefenseRating;
    public int DetectionRating;
    public int BombardmentDefense;

    /// <summary>
    /// Default constructor.
    /// </summary>
    public Regiment() { }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public override GameNode[] GetChildNodes()
    {
        // Leaf node.
        return new GameNode[] { };
    }
}
