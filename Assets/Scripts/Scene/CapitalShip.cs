using System.Collections;
using System.Collections.Generic;
using System;
using ICollectionExtensions;
using UnityEngine;

public enum PrimaryWeaponType
{
    Turbolaser,
    IonCannon,
    LaserCannon,
}

/// <summary>
///
/// </summary>
public class CapitalShip : GameNode, IManufacturable
{
    // Manufacture Info
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
    public Regiment[] Regiments;
    public Starfighter[] Starfighters;

    // Weapon Info.
    public SerializableDictionary<PrimaryWeaponType, int[]> PrimaryWeapons =
        new SerializableDictionary<PrimaryWeaponType, int[]>()
        {
            { PrimaryWeaponType.Turbolaser, new int[5] },
            { PrimaryWeaponType.IonCannon, new int[5] },
            { PrimaryWeaponType.LaserCannon, new int[5] }
        };
    public int WeaponRecharge;
    public int Bombardment;

    // Misc Info
    public int TractorBeamPower;
    public int TractorBeamnRange;
    public bool HasGravityWell;
    public int DetectionRating;

    // Owner Info
    [CloneIgnore]
    public string OwnerGameID { get; set; }
    public string[] AllowedOwnerGameIDs;

    /// <summary>
    /// Default constructor.
    /// </summary>
    public CapitalShip() { }

    /// <summary>
    ///
    /// </summary>
    /// <param name="starfighters"></param>
    public void AddStarfighters(Starfighter[] starfighters)
    {
        int capacity = StarfighterCapacity - Starfighters.Length;
        if (starfighters.Length > capacity)
        {
            throw new GameException(
                $"Adding starfighters to \"{this.DisplayName}\" would exceed its capacity."
            );
        }
        Starfighters.AddAll(starfighters);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="regiments"></param>
    public void AddRegiments(Regiment[] regiments)
    {
        int capacity = RegimentCapacity - Regiments.Length;
        if (regiments.Length > capacity)
        {
            throw new GameException(
                $"Adding starfighters to \"{this.DisplayName}\" would exceed its capacity."
            );
        }
        Regiments.AddAll(regiments);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="officer"></param>
    public void AddOfficer(Officer officer)
    {
        if (this.OwnerGameID != officer.OwnerGameID)
        {
            throw new SceneException(officer, this, SceneExceptionType.Access);
        }
        Officers.Add(officer);
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public override GameNode[] GetChildNodes()
    {
        return Officers.ToArray();
    }
}
