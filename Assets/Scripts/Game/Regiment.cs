using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Represents a regiment that can be stationed on a planet or capital ship.
/// </summary>
public class Regiment : LeafNode, IManufacturable
{
    // Construction Info
    public int ConstructionCost { get; set; }
    public int MaintenanceCost { get; set; }
    public int BaseBuildSpeed { get; set; }
    public int RequiredResearchLevel { get; set; }

    // Regiment Info
    public int AttackRating;
    public int DefenseRating;
    public int DetectionRating;
    public int BombardmentDefense;

    // Owner Info
    [CloneIgnore]
    public string OwnerGameID { get; set; }
    public string[] AllowedOwnerGameIDs;

    /// <summary>
    /// Default constructor used for serialization.
    /// </summary>
    public Regiment() { }

}
