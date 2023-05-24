using System.Xml.Serialization;

/// <summary>
/// A simple container which acts as a reference for a GameNode. Its primary purpose is to get around an issue
/// preventing the serialization of subclasses within a collection. After an exhausting search, I could find
/// no other way to perform the required serialization. This strongly coupled approach was the result.
/// </summary>
[XmlInclude(typeof(Building))]
[XmlInclude(typeof(CapitalShip))]
[XmlInclude(typeof(Regiment))]
[XmlInclude(typeof(SpecialForces))]
[XmlInclude(typeof(Starfighter))]
public class ReferenceNode
{
    public GameNode Reference;

    /// <summary>
    /// Default constructor.
    /// </summary>
    public ReferenceNode() { }

    /// <summary>
    ///
    /// </summary>
    /// <param name="reference"></param>
    public ReferenceNode(GameNode reference)
    {
        Reference = reference;
    }
}
