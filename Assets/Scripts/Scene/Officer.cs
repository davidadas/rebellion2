using System.Collections;
using System.Collections.Generic;
using System.Xml.Serialization;
using UnityEngine;

public enum OfficerRank
{
    None,
    Commander,
    General,
    Admiral,
}

public enum OfficerStatus
{
    Available,
    Injured,
    Captured,
    OnMission,
    InTransit,
}

public class Officer : GameNode, IMissionParticipant
{
    // Mission Stats
    public int Diplomacy { get; set; }
    public int Espionage { get; set; }
    public int Combat { get; set; }
    public int Leadership { get; set; }

    // Research Info
    public int ShipResearch;
    public int TroopResearch;
    public int FacilityResearch;

    // Character Status
    public OfficerStatus Status;
    public bool IsMain;
    public bool CanBetray;
    public bool IsTraitor;
    public bool IsJedi;
    public bool IsKnownJedi;
    public int Loyalty;

    // Jedi Info
    public int JediProbability;
    public int JediLevel;
    public int JediLevelVariance;

    // Rank Info
    public OfficerRank[] AllowedRanks;
    public OfficerRank CurrentRank;

    // Owner Info
    [CloneIgnore]
    public string OwnerGameID { get; set; }
    public string[] AllowedOwnerGameIDs;

    // Variance Info
    [XmlIgnoreAttribute]
    public int DiplomacyVariance;

    [XmlIgnoreAttribute]
    public int EspionageVariance;

    [XmlIgnoreAttribute]
    public int CombatVariance;

    [XmlIgnoreAttribute]
    public int LeadershipVariance;

    [XmlIgnoreAttribute]
    public int LoyaltyVariance;

    [XmlIgnoreAttribute]
    public int FacilityResearchVariance;

    [XmlIgnoreAttribute]
    public int TroopResearchVariance;

    [XmlIgnoreAttribute]
    public int ShipResearchVariance;

    /// <summary>
    /// Default constructor
    /// </summary>
    public Officer() { }

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
