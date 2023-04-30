using System.Collections;
using System.Collections.Generic;
using ICollectionExtensions;

public class Fleet : GameNode
{
    public List<CapitalShip> CapitalShips = new List<CapitalShip>();
    public Planet Location;

    /// <summary>
    /// Default constructor.
    /// </summary>
    public Fleet() { }

    /// <summary>
    ///
    /// </summary>
    /// <param name="capitalShip"></param>
    public void AddCapitalShip(CapitalShip capitalShip)
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
    public void AddOfficer(Officer officer)
    {
        CapitalShips[0].AddOfficer(officer);
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
