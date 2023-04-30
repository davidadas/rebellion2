using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Starfighter : GameNode, IManufacturable
{
    // Construction Info
    public int ConstructionCost { get; set; }
    public int MaintenanceCost { get; set; }
    public int BaseBuildSpeed { get; set; }
    public int RequiredResearchLevel { get; set; }

    // General Info
    public int SquadronSize;
    public int DetectionRating;
    public int Bombardment;
    public int ShieldStrength;

    // Maneuverability Info
    public int Hyperdrive;
    public int SublightSpeed;
    public int Agility;

    // Weapon Info
    public int LaserCannon;
    public int IonCannon;
    public int Torpedoes;

    // Weapon Range Info
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
