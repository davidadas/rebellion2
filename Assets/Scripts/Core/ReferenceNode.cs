using System;
using System.Xml.Serialization;

/// <summary>
/// A simple container which acts as a reference for a SceneNode. Its primary purpose is to get around an issue
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
    public SceneNode Node { get; set; }

    /// <summary>
    /// Default constructor.
    /// </summary>
    public ReferenceNode() { }

    /// <summary>
    /// Initializes the reference node with a scene node, which is the object to be referenced.
    /// </summary>
    /// <param name="node">The scene node to reference.</param>
    public ReferenceNode(SceneNode node)
    {
        Node = node;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public SceneNode GetReference()
    {
        return Node;
    }
}
