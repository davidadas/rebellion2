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

public class Officer : GameNode
{
    // Owner Info
    public string[] AllowedOwnerGameIDs;
    public string OwnerGameID;

    // Stats & Modifiers
    public int Diplomacy;
    public int Espionage;
    public int Combat;
    public int Leadership;
    public int Loyalty;

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

    // Jedi Info
    public int JediProbability;
    public int JediLevel;
    public int JediLevelVariance;

    // Rank Info
    public OfficerRank[] AllowedRanks;
    public OfficerRank CurrentRank;

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
