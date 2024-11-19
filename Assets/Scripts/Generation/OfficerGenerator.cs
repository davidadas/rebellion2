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
        foreach (string ownerInstanceId in officersByFaction.Keys.ToArray())
        {
            IEnumerable<Officer> reducedOfficers = officersByFaction[ownerInstanceId]
                // Take a random selection of officers for the game start.
                .TakeWhile((officer, index) => officer.IsMain || index < numAllowedOfficers)
                // Set the OwnerInstanceID for each officer.
                .Select(
                    (officer) =>
                    {
                        officer.OwnerInstanceID = ownerInstanceId;
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
        // Only pull those planets with a OwnerInstanceID assigned.
        IEnumerable<Planet> flattenedPlanets = planetSystems
            .SelectMany((planetSystem) => planetSystem.Planets)
            .Where((planet) => planet.OwnerInstanceID != null);

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
                string ownerInstanceId = nextDestination.OwnerInstanceID;

                List<SceneNode> destinations = destinationMap.GetOrAddValue(
                    ownerInstanceId,
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
            // Add officers which already have an assigned OwnerInstanceID to the front.
            // These are the officers that will always be assigned at start.
            if (officer.OwnerInstanceID != null)
            {
                officersByFaction
                    .GetOrAddValue(officer.OwnerInstanceID, new List<Officer>())
                    .Insert(0, officer); // Add to front of list.
            }
            // Ignore officers allowed by both factions.
            else if (officer.AllowedOwnerInstanceIDs.Count == 1)
            {
                officersByFaction
                    .GetOrAddValue(officer.AllowedOwnerInstanceIDs[0], new List<Officer>())
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
            // Set mission skills.
            (MissionParticipantSkill, int)[] skillVariances = new[]
            {
                (MissionParticipantSkill.Diplomacy, officer.DiplomacyVariance),
                (MissionParticipantSkill.Espionage, officer.EspionageVariance),
                (MissionParticipantSkill.Combat, officer.CombatVariance),
                (MissionParticipantSkill.Leadership, officer.LeadershipVariance),
            };

            foreach (var (skill, variance) in skillVariances)
            {
                officer.Skills[skill] += Random.Range(0, variance);
            }

            // Set loyalty.
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
            List<SceneNode> destinations = destinationMapping[officer.OwnerInstanceID];
            SceneNode destination;

            if (officer.InitialParentInstanceID != null)
            {
                destination = destinations.First(
                    (sceneNode) => sceneNode.InstanceID == officer.InitialParentInstanceID
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
