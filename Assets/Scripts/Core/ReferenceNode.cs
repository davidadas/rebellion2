using System;

[PersistableObject]
public class Technology
{
    [PersistableInclude(typeof(Building))]
    [PersistableInclude(typeof(CapitalShip))]
    [PersistableInclude(typeof(SpecialForces))]
    [PersistableInclude(typeof(Starfighter))]
    public SceneNode Node { get; set; }

    /// <summary>
    /// Default constructor.
    /// </summary>
    public Technology() { }

    /// <summary>
    /// Initializes the reference node with a scene node, which is the object to be referenced.
    /// </summary>
    /// <param name="node">The scene node to reference.</param>
    public Technology(SceneNode node)
    {
        Node = node;
    }

    /// <summary>
    /// Gets the referenced scene node.
    /// </summary>
    /// <returns></returns>
    public SceneNode GetReference()
    {
        return Node;
    }
}
