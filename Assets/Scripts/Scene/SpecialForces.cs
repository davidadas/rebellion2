/// <summary>
///
/// </summary>
public class SpecialForces : GameNode, IManufacturable, IMissionParticipant
{
    // Construction Info
    public int ConstructionCost { get; set; }
    public int MaintenanceCost { get; set; }
    public int BaseBuildSpeed { get; set; }
    public string[] AllowedOwnerGameIDs { get; set; }
    public string OwnerGameID { get; set; }
    public int RequiredResearchLevel { get; set; }

    // Mission Stats
    public int Diplomacy { get; set; }
    public int Espionage { get; set; }
    public int Combat { get; set; }
    public int Leadership { get; set; }

    /// <summary>
    /// Default constructor
    /// </summary>
    public SpecialForces() { }

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