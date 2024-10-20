using System.Collections;
using System.Collections.Generic;
using System.Xml.Serialization;

/// <summary>
/// 
/// </summary>
public enum OfficerRank
{
    None,
    Commander,
    General,
    Admiral,
}

public class Officer : MissionParticipant, IMovable
{
    // Research Info
    public int ShipResearch;
    public int TroopResearch;
    public int FacilityResearch;

    // Officer Info
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
    public string InitialParentTypeID;

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

    // Status Info
    public MovementStatus MovementStatus { get; set; }

    /// <summary>
    /// Default constructor
    /// </summary>
    public Officer() { }

}
