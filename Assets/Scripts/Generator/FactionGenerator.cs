using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IEnumerableExtensions;
using UnityEngine;

public class FactionGenerator : UnitGenerator, IUnitDeployer<Faction, PlanetSystem>
{
    /// <summary>
    /// Default constructor, constructs a FactionGenerator object.
    /// </summary>
    /// <param name="summary">The GameSummary options selected by the player.</param>
    /// <param name="config">The Config containing new game configurations and settings.</param>
    public FactionGenerator(GameSummary summary, Config config)
        : base(summary, config) { }

    /// <summary>
    /// Assigns each faction's headquarters to their designated planet.
    /// </summary>
    /// <param name="factions">The factions to assign.</param>
    /// <param name="planetSystems">The planet system containing the faction's designated HQ.</param>
    private void setFactionHQs(Faction[] factions, PlanetSystem[] planetSystems)
    {
        Dictionary<string, Faction> hqs = factions.Aggregate(
            new Dictionary<string, Faction>(),
            (dict, nextFaction) =>
            {
                dict.Add(nextFaction.HQGameID, nextFaction);
                return dict;
            }
        );
        List<string> filledHQs = new List<string>(hqs.Count());

        foreach (PlanetSystem planetSystem in planetSystems)
        {
            foreach (Planet planet in planetSystem.Planets)
            {
                if (hqs.Keys.ToList().Contains(planet.GameID))
                {
                    planet.IsHeadquarters = true;
                    planet.OwnerGameID = hqs[planet.GameID].GameID;
                    filledHQs.Add(planet.OwnerGameID);
                }

                // Return if we have filled our array already.
                if (filledHQs.Count() == factions.Length)
                {
                    return;
                }
            }
        }

        throw new GameException("Invalid planet designated as headquarters.");
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="factions"></param>
    /// <param name="planetSystems"></param>
    private void setStartingPlanets(Faction[] factions, PlanetSystem[] planetSystems)
    {
        Config startConfig = GetConfig().GetValue<Config>("Planets.InitialPlanets.GalaxySize");
        string galaxySize = GetGameSummary().GalaxySize.ToString();
        int numStartingPlanets = startConfig.GetValue<int>(galaxySize);

        // Select a complete list of starting planets from list.
        int numPlanets = numStartingPlanets * factions.Length;
        IEnumerable<Planet> startingPlanets = planetSystems
            .SelectMany(ps => ps.Planets)        // Flatten the array.
            .Where(planet => planet.IsColonized) // Select only those that are colonized.
            .Shuffle()                           // Shuffle the array to randomize the list.
            .Take(numPlanets);                   // Take n planets to be the starting list.

        // Assign, randomly, the remaining start planets.
        foreach (Faction faction in factions)
        {
            IEnumerable<Planet> factionStartingPlanets = startingPlanets.Take(numStartingPlanets);

            foreach (Planet planet in factionStartingPlanets)
            {
                planet.OwnerGameID = faction.GameID;
            }
        }
    }

    /// <summary>
    /// Assign initial planets to each faction. This method does NOT assign
    /// fleets, buildings, etc. This will likely confuse most, but keep in
    /// mind it is difficult to find a design pattern that will fit every use case.
    /// </summary>
    /// <param name="units"></param>
    /// <param name="destinations"></param>
    public void DeployUnits(Faction[] units, PlanetSystem[] destinations)
    {
        setFactionHQs(units, destinations);
        setStartingPlanets(units, destinations);
    }
}
