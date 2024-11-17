using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using ICollectionExtensions;

public enum PrimaryWeaponType
{
    Turbolaser,
    IonCannon,
    LaserCannon,
}

/// <summary>
/// Represents a capital ship in the game.
/// </summary>
public class CapitalShip : SceneNode, IManufacturable, IMovable
{
    // Manufacture Info
    public string ProducerOwnerID { get; set; }
    public int ConstructionCost { get; set; }
    public int MaintenanceCost { get; set; }
    public int BaseBuildSpeed { get; set; }
    public int RequiredResearchLevel { get; set; }

    // Hull, Shield, and Repair Info
    public int HullStrength;
    public int DamageControl;
    public int MaxShieldStrength;
    public int ShieldRechargeRate;

    // Movement Info
    public int Hyperdrive;
    public int SublightSpeed;
    public int Maneuverability;

    // Capacity Info
    public int StarfighterCapacity;
    public int RegimentCapacity;

    // Unit Info
    public List<Officer> Officers = new List<Officer>();
    public List<Regiment> Regiments = new List<Regiment>();
    public List<Starfighter> Starfighters = new List<Starfighter>();

    // Weapon Info.
    public Dictionary<PrimaryWeaponType, int[]> PrimaryWeapons = new Dictionary<
        PrimaryWeaponType,
        int[]
    >()
    {
        { PrimaryWeaponType.Turbolaser, new int[5] },
        { PrimaryWeaponType.IonCannon, new int[5] },
        { PrimaryWeaponType.LaserCannon, new int[5] },
    };
    public int WeaponRecharge;
    public int Bombardment;

    // Manufacturing Info
    public int ManufacturingProgress { get; set; } = 0;
    public ManufacturingStatus ManufacturingStatus { get; set; } = ManufacturingStatus.Building;

    // Movement Info
    public MovementStatus MovementStatus { get; set; }
    public int PositionX { get; set; }
    public int PositionY { get; set; }

    // Misc Info
    public int TractorBeamPower;
    public int TractorBeamnRange;
    public bool HasGravityWell;
    public int DetectionRating;

    // Owner Info
    public string InitialParentTypeID { get; set; }

    /// <summary>
    /// Default constructor.
    /// </summary>
    public CapitalShip() { }

    /// <summary>
    /// Adds a starfighter to the capital ship.
    /// </summary>
    /// <param name="starfighter">The starfighter to add</param>
    /// <exception cref="GameException">Thrown when adding the starfighter would exceed the capacity.</exception>
    public void AddStarfighter(Starfighter starfighter)
    {
        if (Starfighters.Count >= StarfighterCapacity)
        {
            throw new GameException(
                $"Adding starfighters to \"{this.DisplayName}\" would exceed its capacity."
            );
        }
        Starfighters.Add(starfighter);
    }

    /// <summary>
    /// Adds a regiment to the capital ship.
    /// </summary>
    /// <param name="regiment">The regiment to add.</param>
    /// <exception cref="GameException">Thrown when adding the regiment would exceed the capacity.</exception>
    public void AddRegiment(Regiment regiment)
    {
        if (Regiments.Count >= RegimentCapacity)
        {
            throw new GameException(
                $"Adding regiments to \"{this.DisplayName}\" would exceed its capacity."
            );
        }
        Regiments.Add(regiment);
    }

    /// <summary>
    /// Adds an officer to the capital ship.
    /// </summary>
    /// <param name="officer"></param>
    /// <exception cref="SceneAccessException">Thrown when the officer is not allowed to be added.</exception>
    public void AddOfficer(Officer officer)
    {
        if (this.OwnerTypeID != officer.OwnerTypeID)
        {
            throw new SceneAccessException(officer, this);
        }
        Officers.Add(officer);
    }

    /// <summary>
    /// Adds a child to the capital ship.
    /// </summary>
    /// <param name="child">The child to add</param>
    /// <exception cref="SceneAccessException">Thrown when the child is not allowed to be added.</exception>
    public override void AddChild(SceneNode child)
    {
        if (child is Starfighter starfighter)
        {
            AddStarfighter(starfighter);
        }
        else if (child is Regiment regiment)
        {
            AddRegiment(regiment);
        }
        else if (child is Officer officer)
        {
            AddOfficer(officer);
        }
    }

    /// <summary>
    /// Adds a child to the capital ship.
    /// </summary>
    /// <param name="child">The child to remove</param>
    public override void RemoveChild(SceneNode child)
    {
        if (child is Starfighter starfighter)
        {
            Starfighters.Remove(starfighter);
        }
        else if (child is Regiment regiment)
        {
            Regiments.Remove(regiment);
        }
        else if (child is Officer officer)
        {
            Officers.Remove(officer);
        }
    }

    /// <summary>
    /// Returns the manufacturing manufacturing type of the manufacturable.
    /// </summary>
    /// <returns>ManufacturingType.Ship</returns>
    public ManufacturingType GetManufacturingType()
    {
        return ManufacturingType.Ship;
    }

    /// <summary>
    /// The movement status of the capital ship.
    /// </summary>
    /// <returns>True if the capital ship is movable, false otherwise.</returns>
    public bool IsMovable()
    {
        return MovementStatus != MovementStatus.InTransit;
    }

    /// <summary>
    /// Retrieves the children of the node.
    /// </summary>
    /// <returns>The children of the node.</returns>
    public override IEnumerable<SceneNode> GetChildren()
    {
        return Officers
            .Cast<SceneNode>()
            .Concat(Starfighters.Cast<SceneNode>())
            .Concat(Regiments.Cast<SceneNode>());
    }
}
