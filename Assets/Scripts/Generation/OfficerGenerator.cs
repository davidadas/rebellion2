using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ICollectionExtensions;
using IDictionaryExtensions;
using IEnumerableExtensions;
using UnityEngine;

public class OfficerGenerator : UnitGenerator<Officer>
{
    /// <summary>
    /// Constructs an OfficerGenerator object.
    /// </summary>
    /// <param name="summary">The game summary with options selected by the player.</param>
    /// <param name="resourceManager">The resource manager for accessing configuration settings.</param>
    public OfficerGenerator(GameSummary summary, IResourceManager resourceManager)
        : base(summary, resourceManager) { }

    /// <summary>
    /// Selects the initial set of officers for each faction based on game configuration.
    /// </summary>
    /// <param name="officersByFaction">Dictionary of officers grouped by faction.</param>
    /// <returns>Array of selected officers.</returns>
    private Officer[] SelectInitialOfficers(Dictionary<string, List<Officer>> officersByFaction)
    {
        List<Officer> selectedOfficers = new List<Officer>();
        string galaxySize = GetGameSummary().GalaxySize.ToString();
        int numAllowedOfficers = GetConfig()
            .GetValue<int>($"Officers.NumInitialOfficers.GalaxySize.{galaxySize}");

        foreach (KeyValuePair<string, List<Officer>> entry in officersByFaction)
        {
            string ownerInstanceId = entry.Key;
            List<Officer> officers = entry.Value;

            IEnumerable<Officer> reducedOfficers = officers
                .TakeWhile((officer, index) => officer.IsMain || index < numAllowedOfficers)
                .Select(officer =>
                {
                    officer.OwnerInstanceID = ownerInstanceId;
                    return officer;
                });

            selectedOfficers.AddRange(reducedOfficers);
        }

        return selectedOfficers.ToArray();
    }

    /// <summary>
    /// Creates a mapping of factions to potential deployment destinations.
    /// </summary>
    /// <param name="planetSystems">Array of planet systems in the game.</param>
    /// <returns>Dictionary mapping faction IDs to a list of destinations (planets and fleets).</returns>
    private Dictionary<string, List<ISceneNode>> GetDestinationMapping(PlanetSystem[] planetSystems)
    {
        IEnumerable<Planet> flattenedPlanets = planetSystems
            .SelectMany(planetSystem => planetSystem.Planets)
            .Where(planet => planet.OwnerInstanceID != null);

        List<ISceneNode> destinations = new List<ISceneNode>();
        foreach (Planet planet in flattenedPlanets)
        {
            destinations.Add(planet);
            destinations.AddRange(planet.Fleets);
        }

        Dictionary<string, List<ISceneNode>> destinationMapping =
            new Dictionary<string, List<ISceneNode>>();
        foreach (ISceneNode destination in destinations)
        {
            string ownerInstanceId = destination.OwnerInstanceID;

            if (!destinationMapping.ContainsKey(ownerInstanceId))
            {
                destinationMapping[ownerInstanceId] = new List<ISceneNode>();
            }

            destinationMapping[ownerInstanceId].Add(destination);
        }

        return destinationMapping;
    }

    /// <summary>
    /// Selects officers for deployment based on their allowed factions and ownership.
    /// </summary>
    /// <param name="units">Array of all available officers.</param>
    /// <returns>Array of selected officers.</returns>
    public override Officer[] SelectUnits(Officer[] units)
    {
        Dictionary<string, List<Officer>> officersByFaction =
            new Dictionary<string, List<Officer>>();
        Officer[] shuffledUnits = ((Officer[])units.Clone()).Shuffle().ToArray();

        foreach (Officer officer in shuffledUnits)
        {
            if (officer.OwnerInstanceID != null)
            {
                if (!officersByFaction.ContainsKey(officer.OwnerInstanceID))
                {
                    officersByFaction[officer.OwnerInstanceID] = new List<Officer>();
                }
                // Add to front.
                officersByFaction[officer.OwnerInstanceID].Insert(0, officer);
            }
            else if (officer.AllowedOwnerInstanceIDs.Count == 1)
            {
                string allowedOwnerId = officer.AllowedOwnerInstanceIDs[0];
                if (!officersByFaction.ContainsKey(allowedOwnerId))
                {
                    officersByFaction[allowedOwnerId] = new List<Officer>();
                }
                // Add to end.
                officersByFaction[allowedOwnerId].Add(officer);
            }
        }

        return SelectInitialOfficers(officersByFaction);
    }

    /// <summary>
    /// Decorates officers with additional randomized attributes.
    /// </summary>
    /// <param name="units">Array of officers to decorate.</param>
    /// <returns>Array of decorated officers.</returns>
    public override Officer[] DecorateUnits(Officer[] units)
    {
        foreach (Officer officer in units)
        {
            (MissionParticipantSkill skill, int variance)[] skillVariances = new[]
            {
                (MissionParticipantSkill.Diplomacy, officer.DiplomacyVariance),
                (MissionParticipantSkill.Espionage, officer.EspionageVariance),
                (MissionParticipantSkill.Combat, officer.CombatVariance),
                (MissionParticipantSkill.Leadership, officer.LeadershipVariance),
            };

            foreach ((MissionParticipantSkill skill, int variance) in skillVariances)
            {
                officer.SetSkillValue(
                    skill,
                    officer.GetSkillValue(skill) + Random.Range(0, variance)
                );
            }

            officer.Loyalty += Random.Range(0, officer.LoyaltyVariance);
            officer.ShipResearch += Random.Range(0, officer.ShipResearchVariance);
            officer.TroopResearch += Random.Range(0, officer.TroopResearchVariance);
            officer.FacilityResearch += Random.Range(0, officer.FacilityResearchVariance);

            float jediProbability = officer.JediProbability / 100f;
            officer.IsJedi = officer.IsJedi || UnityEngine.Random.value < jediProbability;
        }

        return units;
    }

    /// <summary>
    /// Deploys officers to initial destinations based on the game configuration.
    /// </summary>
    /// <param name="officers">Array of officers to deploy.</param>
    /// <param name="planetSystems">Array of planet systems in the game.</param>
    /// <returns>Array of deployed officers.</returns>
    public override Officer[] DeployUnits(Officer[] officers, PlanetSystem[] planetSystems)
    {
        Dictionary<string, List<ISceneNode>> destinationMapping = GetDestinationMapping(
            planetSystems
        );

        foreach (Officer officer in officers)
        {
            List<ISceneNode> destinations = destinationMapping[officer.OwnerInstanceID];
            ISceneNode destination;

            if (officer.InitialParentInstanceID != null)
            {
                destination = destinations.First(sceneNode =>
                    sceneNode.InstanceID == officer.InitialParentInstanceID
                );
            }
            else
            {
                destination = destinations.Shuffle().First();
            }

            if (destination is Planet planet)
            {
                planet.AddChild(officer);
            }
            else if (destination is Fleet fleet)
            {
                fleet.AddChild(officer);
            }
        }

        return officers;
    }
}
