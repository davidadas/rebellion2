using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum OfficerRank
{
    None,
    Commander,
    General,
    Admiral,
}

public class Officer : GameNode, IMissionParticipant
{
    // Mission Stats
    public int Diplomacy { get; set; }
    public int Espionage { get; set; }
    public int Combat { get; set; }
    public int Leadership { get; set; }

    // Owner Info
    public string[] AllowedOwnerGameIDs;
    public string OwnerGameID;

    // Variance Info
    public int DiplomacyVariance;
    public int EspionageVariance;
    public int CombatVariance;
    public int LeadershipVariance;
    public int LoyaltyVariance;

    // Research Info
    public int ShipResearch;
    public int ShipResearchVariance;
    public int TroopResearch;
    public int TroopResearchVariance;
    public int FacilityResearch;
    public int FacilityResearchVariance;

    // Character Status
    public bool IsMain;
    public bool CanBetray;
    public bool IsTraitor;
    public bool IsJedi;
    public bool IsKnownJedi;

    // Jedi Info
    public int JediProbability;
    public int JediLevel;
    public int JediLevelVariance;

    // Rank Info
    public OfficerRank[] AllowedRanks;
    public OfficerRank CurrentRank;

    // Other
    public int Loyalty;

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
