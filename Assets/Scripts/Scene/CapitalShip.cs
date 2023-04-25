using System.Collections;
using System.Collections.Generic;
using System;
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
    public string[] AllowedOwnerGameIDs { get; set; }
    public string OwnerGameID { get; set; }
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

    /// <summary>
    /// Default constructor.
    /// </summary>
    public CapitalShip() { }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public override GameNode[] GetChildNodes()
    {
        return Officers.ToArray();
    }
}
