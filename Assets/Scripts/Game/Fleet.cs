using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using ICollectionExtensions;

public class Fleet : ContainerNode, IMovable
{
    // Movement Info
    public MovementStatus MovementStatus { get; set; }
    public int PositionX { get; set; }
    public int PositionY { get; set; }

    // Child Nodes
    public List<CapitalShip> CapitalShips { get; set; } = new List<CapitalShip>();

    /// <summary>
    /// Default constructor.
    /// </summary>
    public Fleet() { }

    /// <summary>
    /// Constructor that initializes the fleet with an owner.
    /// </summary>
    /// <param name="capitalShip">The capital ship to add to the fleet.</param>
    /// <exception cref="SceneAccessException">Thrown when the child does not share OwnerInstanceID with parent..</exception>
    private void AddCapitalShip(CapitalShip capitalShip)
    {
        if (this.OwnerInstanceID != capitalShip.OwnerInstanceID)
        {
            throw new SceneAccessException(capitalShip, this);
        }
        CapitalShips.Add(capitalShip);
    }

    /// <summary>
    /// Adds an officer to the fleet.
    /// </summary>
    /// <param name="officer">The officer to add to the fleet.</param>
    /// <exception cref="SceneAccessException">Thrown when the child does not share OwnerInstanceID with parent.</exception>
    private void AddOfficer(Officer officer)
    {
        if (this.OwnerInstanceID != officer.OwnerInstanceID)
        {
            throw new SceneAccessException(officer, this);
        }
        CapitalShips[0].AddOfficer(officer);
    }

    /// <summary>
    /// Adds a child to the node.
    /// </summary>
    /// <param name="child">The child node to add.</param>
    /// <exception cref="SceneAccessException">Thrown when the child does not share OwnerInstanceID with parent.</exception>
    public override void AddChild(ISceneNode child)
    {
        if (child is CapitalShip)
        {
            AddCapitalShip(child as CapitalShip);
        }
        else if (child is Officer officer)
        {
            AddOfficer(officer);
        }
    }

    /// <summary>
    /// Removes a child from the node.
    /// </summary>
    /// <param name="child">The child node to remove.</param>
    public override void RemoveChild(ISceneNode child)
    {
        if (child is CapitalShip capitalShip)
        {
            CapitalShips.Remove(capitalShip);
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public bool IsMovable()
    {
        return MovementStatus != MovementStatus.InTransit;
    }

    /// <summary>
    /// Retrieves the children of the node.
    /// </summary>
    /// <returns>An array of child nodes.</returns>
    public override IEnumerable<ISceneNode> GetChildren()
    {
        return CapitalShips.ToArray();
    }
}
