using Rebellion.Util.Serialization;

/// <summary>
/// Maps one planet-destroying ship type to its presentation movie.
/// </summary>
[PersistableObject(Name = "Cutscene")]
public sealed class PlanetDestructionCutscene
{
    [PersistableAttribute]
    public string ShipTypeID { get; set; }

    [PersistableAttribute]
    public string Path { get; set; }
}
