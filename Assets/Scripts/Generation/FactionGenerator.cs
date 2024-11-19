using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IEnumerableExtensions;

/// <summary>
///
/// </summary>
public class FactionGenerator : UnitGenerator<Faction>
{
    /// <summary>
    /// Default constructor, constructs a FactionGenerator object.
    /// </summary>
    /// <param name="summary">The GameSummary options selected by the player.</param>
    /// <param name="resourceManager">The resource manager from which to load game data.</param>
    public FactionGenerator(GameSummary summary, IResourceManager resourceManager)
        : base(summary, resourceManager) { }

    /// <summary>
    /// Assigns each faction's headquarters to their designated planet.
    /// </summary>
    /// <param name="factions">The factions to assign.</param>
    /// <param name="planetSystems">The planet system containing the faction's designated HQ.</param>
    private void SetFactionHQs(Faction[] factions, PlanetSystem[] planetSystems)
    {
        Dictionary<string, Faction> hqs = factions.Aggregate(
            new Dictionary<string, Faction>(),
            (dict, nextFaction) =>
            {
                dict.Add(nextFaction.HQInstanceID, nextFaction);
                return dict;
            }
        );
        List<string> filledHQs = new List<string>(hqs.Count());

        foreach (PlanetSystem planetSystem in planetSystems)
        {
            foreach (Planet planet in planetSystem.Planets)
            {
                if (hqs.Keys.ToList().Contains(planet.InstanceID))
                {
                    planet.IsHeadquarters = true;
                    planet.OwnerInstanceID = hqs[planet.InstanceID].InstanceID;
                    filledHQs.Add(planet.OwnerInstanceID);
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
    private void SetStartingPlanets(Faction[] factions, PlanetSystem[] planetSystems)
    {
        Config startConfig = GetConfig().GetValue<Config>("Planets.NumInitialPlanets.GalaxySize");
        string galaxySize = GetGameSummary().GalaxySize.ToString();
        int numStartingPlanets = startConfig.GetValue<int>(galaxySize);

        // Select a complete list of starting planets from list.
        int numPlanets = numStartingPlanets * factions.Length;
        IEnumerable<Planet> startingPlanets = planetSystems
            .SelectMany(ps => ps.Planets) // Flatten the array.
            .Where(planet => planet.IsColonized && planet.IsHeadquarters == false) // Select only those that are colonized & not HQs.
            .Shuffle() // Shuffle the array to randomize the list.
            .Take(numPlanets); // Take n planets to be the starting list.

        // Assign, randomly, the remaining start planets.
        foreach (Faction faction in factions)
        {
            IEnumerable<Planet> factionStartingPlanets = startingPlanets.Take(numStartingPlanets);
            foreach (Planet planet in factionStartingPlanets)
            {
                planet.OwnerInstanceID = faction.InstanceID;
                planet.SetPopularSupport(faction.InstanceID, 100);
            }
        }
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="factions"></param>
    /// <returns></returns>
    public override Faction[] SelectUnits(Faction[] factions)
    {
        int researchLevel = GetGameSummary().StartingResearchLevel;

        // Set the research level for each faction.
        foreach (Faction faction in factions)
        {
            faction.SetManufacturingResearchLevel(ManufacturingType.Building, researchLevel);
            faction.SetManufacturingResearchLevel(ManufacturingType.Ship, researchLevel);
            faction.SetManufacturingResearchLevel(ManufacturingType.Troop, researchLevel);
        }

        return factions;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="factions"></param>
    /// <returns></returns>
    public override Faction[] DecorateUnits(Faction[] factions)
    {
        // No op.
        return factions;
    }

    /// <summary>
    /// Assign initial planets to each faction. This method does NOT assign units.
    /// </summary>
    /// <param name="factions"></param>
    /// <param name="destinations"></param>
    /// <returns></returns>
    public override Faction[] DeployUnits(Faction[] factions, PlanetSystem[] destinations)
    {
        SetFactionHQs(factions, destinations);
        SetStartingPlanets(factions, destinations);

        return factions;
    }
}
