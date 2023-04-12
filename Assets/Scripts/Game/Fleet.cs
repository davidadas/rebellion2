using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
    /// <returns></returns>
    public override GameNode[] GetChildNodes()
    {
        return CapitalShips.ToArray();
    }
}
