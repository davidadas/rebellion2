using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Starfighter : Manufacturable
{
    public int SquadronSize;
    public int DetectionRating;
    public int Bombardment;
    public int ShieldStrength;

    public int Hyperdrive;
    public int SublightSpeed;
    public int Agility;

    public int LaserCannon;
    public int IonCannon;
    public int Torpedoes;

    public int LaserRange;
    public int IonRange;
    public int TorpedoRange;

    /// <summary>
    /// Default constructor.
    /// </summary>
    public Starfighter() { }

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
