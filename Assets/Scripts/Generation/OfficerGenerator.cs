using System.Collections.Generic;
using System.Linq;
using ICollectionExtensions;
using IDictionaryExtensions;
using IEnumerableExtensions;
using UnityEngine;

/// <summary>
/// Responsible for generating and deploying officers in the game.
/// </summary>
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

        foreach (var (ownerInstanceId, officers) in officersByFaction)
        {
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
        var destinationMapping = new Dictionary<string, List<ISceneNode>>();

        IEnumerable<Planet> ownedPlanets = planetSystems
            .SelectMany(ps => ps.Planets)
            .Where(p => p.OwnerInstanceID != null);

        foreach (Planet planet in ownedPlanets)
        {
            AddDestination(destinationMapping, planet);
            foreach (Fleet fleet in planet.Fleets)
            {
                AddDestination(destinationMapping, fleet);
            }
        }

        return destinationMapping;
    }

    /// <summary>
    /// Adds a destination to the destination mapping.
    /// </summary>
    /// <param name="mapping">The destination mapping to update.</param>
    /// <param name="destination">The destination to add.</param>
    private void AddDestination(
        Dictionary<string, List<ISceneNode>> mapping,
        ISceneNode destination
    )
    {
        if (!mapping.ContainsKey(destination.OwnerInstanceID))
        {
            mapping[destination.OwnerInstanceID] = new List<ISceneNode>();
        }
        mapping[destination.OwnerInstanceID].Add(destination);
    }

    /// <summary>
    /// Selects officers for deployment based on their allowed factions and ownership.
    /// </summary>
    /// <param name="units">Array of all available officers.</param>
    /// <returns>Array of selected officers.</returns>
    public override Officer[] SelectUnits(Officer[] units)
    {
        var officersByFaction = new Dictionary<string, List<Officer>>();
        Officer[] shuffledUnits = units.Shuffle().ToArray();

        foreach (Officer officer in shuffledUnits)
        {
            string factionId =
                officer.OwnerInstanceID
                ?? (
                    officer.AllowedOwnerInstanceIDs.Count == 1
                        ? officer.AllowedOwnerInstanceIDs[0]
                        : null
                );

            if (factionId != null)
            {
                if (!officersByFaction.ContainsKey(factionId))
                {
                    officersByFaction[factionId] = new List<Officer>();
                }

                if (officer.OwnerInstanceID != null)
                {
                    officersByFaction[factionId].Insert(0, officer); // Add to front
                }
                else
                {
                    officersByFaction[factionId].Add(officer); // Add to end
                }
            }
        }

        return SelectInitialOfficers(officersByFaction);
    }

    /// <summary>
    /// Decorates an officer's skills with random variances.
    /// </summary>
    /// <param name="officer">The officer to decorate.</param>
    private void DecorateOfficerSkills(Officer officer)
    {
        var skillVariances = new[]
        {
            (MissionParticipantSkill.Diplomacy, officer.DiplomacyVariance),
            (MissionParticipantSkill.Espionage, officer.EspionageVariance),
            (MissionParticipantSkill.Combat, officer.CombatVariance),
            (MissionParticipantSkill.Leadership, officer.LeadershipVariance),
        };

        foreach (var (skill, variance) in skillVariances)
        {
            officer.SetSkillValue(skill, officer.GetSkillValue(skill) + Random.Range(0, variance));
        }
    }

    /// <summary>
    /// Decorates an officer's attributes with random variances.
    /// </summary>
    /// <param name="officer">The officer to decorate.</param>
    private void DecorateOfficerAttributes(Officer officer)
    {
        officer.Loyalty += Random.Range(0, officer.LoyaltyVariance);
        officer.ShipResearch += Random.Range(0, officer.ShipResearchVariance);
        officer.TroopResearch += Random.Range(0, officer.TroopResearchVariance);
        officer.FacilityResearch += Random.Range(0, officer.FacilityResearchVariance);
    }

    /// <summary>
    /// Determines if an officer is a Jedi based on probability.
    /// </summary>
    /// <param name="officer">The officer to check.</param>
    private void DetermineJediStatus(Officer officer)
    {
        float jediProbability = officer.JediProbability / 100f;
        officer.IsJedi = officer.IsJedi || Random.value < jediProbability;
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
            DecorateOfficerSkills(officer);
            DecorateOfficerAttributes(officer);
            DetermineJediStatus(officer);
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
            ISceneNode destination = GetOfficerDestination(officer, destinations);

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

    /// <summary>
    /// Determines the destination for an officer.
    /// </summary>
    /// <param name="officer">The officer to deploy.</param>
    /// <param name="destinations">List of possible destinations.</param>
    /// <returns>The selected destination for the officer.</returns>
    private ISceneNode GetOfficerDestination(Officer officer, List<ISceneNode> destinations)
    {
        if (officer.InitialParentInstanceID != null)
        {
            return destinations.First(node => node.InstanceID == officer.InitialParentInstanceID);
        }
        return destinations.Shuffle().First();
    }
}
