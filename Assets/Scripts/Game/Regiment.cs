using System.Collections;
using System.Collections.Generic;

/// <summary>
///
/// </summary>
public class Regiment : GameLeaf, IManufacturable
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
    /// Default constructor.
    /// </summary>
    public Regiment() { }
}
