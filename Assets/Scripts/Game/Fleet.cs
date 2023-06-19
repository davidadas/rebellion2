using System.Collections;
using System.Collections.Generic;
using ICollectionExtensions;

public class Fleet : GameNode
{
    public List<CapitalShip> CapitalShips = new List<CapitalShip>();

    // Owner Info
    [CloneIgnore]
    public string OwnerGameID { get; set; }
    public string[] AllowedOwnerGameIDs;

    /// <summary>
    /// Default constructor.
    /// </summary>
    public Fleet() { }

    /// <summary>
    ///
    /// </summary>
    /// <param name="capitalShip"></param>
    private void AddCapitalShip(CapitalShip capitalShip)
    {
        if (this.OwnerGameID != capitalShip.OwnerGameID)
        {
            throw new SceneException(capitalShip, this, SceneExceptionType.Access);
        }
        CapitalShips.Add(capitalShip);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="officer"></param>
    private void AddOfficer(Officer officer)
    {
        CapitalShips[0].Attach(officer);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="childNode"></param>
    protected override void AddChildNode(GameNode childNode)
    {
        if (childNode is CapitalShip)
        {
            AddCapitalShip((CapitalShip)childNode);
        }
        else if (childNode is Officer)
        {
            AddOfficer((Officer)childNode);
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="childNode"></param>
    protected override void RemoveChildNode(GameNode childNode)
    {
        if (childNode is CapitalShip)
        {
            CapitalShips.Remove((CapitalShip)childNode);
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public override GameNode[] GetChildNodes()
    {
        return CapitalShips.ToArray();
    }
}
