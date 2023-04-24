using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ICollectionExtensions;
using IDictionaryExtensions;
using IEnumerableExtensions;
using UnityEngine;

/// <summary>
///
/// </summary>
public class OfficerGenerator : UnitGenerator, IUnitSelector<Officer>, IUnitDecorator<Officer>
{
    /// <summary>
    /// Default constructor, constructs a OfficerGenerator object.
    /// </summary>
    /// <param name="summary">The GameSummary options selected by the player.</param>
    /// <param name="config">The Config containing new game configurations and settings.</param>
    public OfficerGenerator(GameSummary summary, Config config)
        : base(summary, config) { }

    /// <summary>
    ///
    /// </summary>
    /// <param name="units"></param>
    /// <returns></returns>
    public Officer[] DecorateUnits(Officer[] units)
    {
        foreach (Officer officer in units)
        {
            // Set core attributes.
            officer.Diplomacy += Random.Range(0, officer.DiplomacyVariance);
            officer.Espionage += Random.Range(0, officer.EspionageVariance);
            officer.Combat += Random.Range(0, officer.CombatVariance);
            officer.Leadership += Random.Range(0, officer.CombatVariance);
            officer.Loyalty += Random.Range(0, officer.LoyaltyVariance);

            // Set research attributes
            officer.ShipResearch += Random.Range(0, officer.ShipResearchVariance);
            officer.TroopResearch += Random.Range(0, officer.TroopResearchVariance);
            officer.FacilityResearch += Random.Range(0, officer.FacilityResearchVariance);

            // Set Jedi attributes.
            float jediProbability = officer.JediProbability / 100f;
            officer.IsJedi = officer.IsJedi || Random.value < jediProbability;
        }
        return units;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="officersByFaction"></param>
    /// <returns></returns>
    private Officer[] reduceOfficerLists(Dictionary<string, List<Officer>> officersByFaction)
    {
        List<Officer> selectedOfficers = new List<Officer>();
        string gameSize = GetGameSummary().GalaxySize.ToString();
        int numAllowedOfficers = GetConfig().GetValue<int>($"Officers.InitialOfficers.{gameSize}");

        // Set the finalized list of officers for each faction.
        foreach (string ownerGameId in officersByFaction.Keys.ToArray())
        {
            IEnumerable<Officer> reducedOfficers = officersByFaction[ownerGameId].TakeWhile(
                (officer, index) => officer.IsMain || index < numAllowedOfficers
            );
            selectedOfficers.AddAll(reducedOfficers);
        }
        return selectedOfficers.ToArray();
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="units"></param>
    /// <returns></returns>
    public IUnitSelectionResult<Officer> SelectUnits(Officer[] units)
    {
        Dictionary<string, List<Officer>> officersByFaction =
            new Dictionary<string, List<Officer>>();
        IEnumerable<Officer> shuffledUnits = (units.Clone() as Officer[]).Shuffle();

        foreach (Officer officer in shuffledUnits)
        {
            // Add officers which already have an assigned OwnerGameID to the front.
            // These are the officers that will always be assigned at start.
            if (officer.OwnerGameID != null)
            {
                officersByFaction
                    .GetOrAddValue(officer.OwnerGameID, new List<Officer>())
                    .Insert(0, officer); // Add to front of list.
            }
            // Ignore officers allowed by both factions.
            else if (officer.AllowedOwnerGameIDs.Length == 1)
            {
                officersByFaction
                    .GetOrAddValue(officer.AllowedOwnerGameIDs[0], new List<Officer>())
                    .Add(officer); // Add to end of list.
            }
        }

        return new UnitSelectionResult<Officer>(units, reduceOfficerLists(officersByFaction));
    }
}
