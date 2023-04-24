using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Regiment : Manufacturable
{
    public int AttackRating;
    public int DefenseRating;
    public int DetectionRating;
    public int BombardmentDefense;

    /// <summary>
    /// Default constructor.
    /// </summary>
    public Regiment() { }

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
