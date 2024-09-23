using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ICollectionExtensions;
using IDictionaryExtensions;
using IEnumerableExtensions;

/// <summary>
///
/// </summary>
public class OfficerGenerator : UnitGenerator<Officer>
{
    /// <summary>
    /// Default constructor, constructs a OfficerGenerator object.
    /// </summary>
    /// <param name="summary">The GameSummary options selected by the player.</param>
    /// <param name="config">The Config containing new game configurations and settings.</param>
    public OfficerGenerator(GameSummary summary, IResourceManager resourceManager)
        : base(summary, resourceManager) { }

    /// <summary>
    ///
    /// </summary>
    /// <param name="officersByFaction"></param>
    /// <returns></returns>
    private Officer[] selectInitialOfficers(Dictionary<string, List<Officer>> officersByFaction)
    {
        List<Officer> selectedOfficers = new List<Officer>();
        string galaxySize = GetGameSummary().GalaxySize.ToString();
        int numAllowedOfficers = GetConfig()
            .GetValue<int>($"Officers.NumInitialOfficers.GalaxySize.{galaxySize}");

        // Set the finalized list of officers for each faction.
        foreach (string ownerGameId in officersByFaction.Keys.ToArray())
        {
            IEnumerable<Officer> reducedOfficers = officersByFaction[ownerGameId]
                // Take a random selection of officers for the game start.
                .TakeWhile((officer, index) => officer.IsMain || index < numAllowedOfficers)
                // Set the OwnerGameID for each officer.
                .Select(
                    (officer) =>
                    {
                        officer.OwnerGameID = ownerGameId;
                        return officer;
                    }
                );
            selectedOfficers.AddAll(reducedOfficers);
        }
        return selectedOfficers.ToArray();
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="planetSystems"></param>
    /// <returns></returns>
    private Dictionary<string, List<SceneNode>> getDestinationMapping(PlanetSystem[] planetSystems)
    {
        // Flatten the list of planets from planet systems.
        // Only pull those planets with a OwnerGameID assigned.
        IEnumerable<Planet> flattenedPlanets = planetSystems
            .SelectMany((planetSystem) => planetSystem.Planets)
            .Where((planet) => planet.OwnerGameID != null);

        // Create an array of fleets and planets.
        List<SceneNode> fleetsAndPlanets = new List<SceneNode>();
        foreach (Planet planet in flattenedPlanets)
        {
            fleetsAndPlanets.Add(planet);
            fleetsAndPlanets.AddAll(planet.Fleets);
        }

        // Create a dictionary of factions to planets and their associated fleets.
        Dictionary<string, List<SceneNode>> destinationMapping = fleetsAndPlanets.Aggregate(
            new Dictionary<string, List<SceneNode>>(),
            (destinationMap, nextDestination) =>
            {
                string ownerGameId =
                    nextDestination
                        .GetType()
                        .GetProperty("OwnerGameID")
                        .GetValue(nextDestination, null) as string;

                List<SceneNode> destinations = destinationMap.GetOrAddValue(
                    ownerGameId,
                    new List<SceneNode>()
                );
                destinations.Add(nextDestination);
                return destinationMap;
            }
        );
        return destinationMapping;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="units"></param>
    /// <returns></returns>
    public override Officer[] SelectUnits(Officer[] units)
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

        return selectInitialOfficers(officersByFaction);
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="units"></param>
    /// <returns></returns>
    public override Officer[] DecorateUnits(Officer[] units)
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
            officer.IsJedi = officer.IsJedi || UnityEngine.Random.value < jediProbability;
        }
        return units;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="units"></param>
    /// <param name="destinations"></param>
    /// <returns></returns>
    public override Officer[] DeployUnits(Officer[] officers, PlanetSystem[] planetSystems)
    {
        Dictionary<string, List<SceneNode>> destinationMapping = getDestinationMapping(
            planetSystems
        );

        foreach (Officer officer in officers)
        {
            List<SceneNode> destinations = destinationMapping[officer.OwnerGameID];
            SceneNode destination;

            if (officer.InitialParentGameID != null)
            {
                destination = destinations.First(
                    (sceneNode) => sceneNode.GameID == officer.InitialParentGameID
                );
            }
            else
            {
                destination = destinations.Shuffle().First();
            }

            System.Type destinationType = destination.GetType();
            if (destinationType.IsAssignableFrom(typeof(Planet)))
            {
                ((Planet)destination).AddChild(officer);
            }
            else
            {
                ((Fleet)destination).AddChild(officer);
            }
        }
        return officers;
    }
}
