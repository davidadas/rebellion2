/// <summary>
///
/// </summary>
public class SpecialForces : GameLeaf, IManufacturable, IMissionParticipant
{
    // Construction Info
    public int ConstructionCost { get; set; }
    public int MaintenanceCost { get; set; }
    public int BaseBuildSpeed { get; set; }
    public int RequiredResearchLevel { get; set; }

    // Mission Stats
    public int Diplomacy { get; set; }
    public int Espionage { get; set; }
    public int Combat { get; set; }
    public int Leadership { get; set; }

    // Owner Info
    [CloneIgnore]
    public string OwnerGameID { get; set; }
    public string[] AllowedOwnerGameIDs;

    /// <summary>
    /// Default constructor
    /// </summary>
    public SpecialForces() { }
}
