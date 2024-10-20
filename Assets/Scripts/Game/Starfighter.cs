using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Represents a starfighter squadron that can be stationed on a planet or capital ship.
/// </summary>
public class Starfighter : LeafNode, IManufacturable
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
    
    // Status Info
    public ManufacturingStatus ManufacturingStatus { get; set; }
    public MovementStatus MovementStatus { get; set; }

    /// <summary>
    /// Default constructor used for serialization.
    /// </summary>
    public Starfighter() { }

}
