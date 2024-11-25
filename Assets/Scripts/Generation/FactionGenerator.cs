using System.Collections.Generic;
using System.Linq;
using IEnumerableExtensions;

/// <summary>
/// Responsible for generating and deploying factions for the scene graph.
/// </summary>
public class FactionGenerator : UnitGenerator<Faction>
{
    /// <summary>
    /// Constructs a FactionGenerator object.
    /// </summary>
    /// <param name="summary">The GameSummary options selected by the player.</param>
    /// <param name="resourceManager">The resource manager from which to load game data.</param>
    public FactionGenerator(GameSummary summary, IResourceManager resourceManager)
        : base(summary, resourceManager) { }

    /// <summary>
    /// Assigns each faction's headquarters to their designated planet.
    /// </summary>
    /// <param name="factions">The factions to assign.</param>
    /// <param name="planetSystems">The planet systems containing the faction's designated HQ.</param>
    private void SetFactionHQs(Faction[] factions, PlanetSystem[] planetSystems)
    {
        Dictionary<string, Faction> hqs = factions.ToDictionary(f => f.HQInstanceID);
        HashSet<string> filledHQs = new HashSet<string>();

        foreach (PlanetSystem planetSystem in planetSystems)
        {
            foreach (Planet planet in planetSystem.Planets)
            {
                if (hqs.TryGetValue(planet.InstanceID, out Faction faction))
                {
                    planet.IsHeadquarters = true;
                    planet.OwnerInstanceID = faction.InstanceID;
                    filledHQs.Add(faction.InstanceID);

                    if (filledHQs.Count == factions.Length)
                    {
                        return;
                    }
                }
            }
        }

        throw new GameException("Invalid planet designated as headquarters.");
    }

    /// <summary>
    /// Assigns starting planets to each faction.
    /// </summary>
    /// <param name="factions">The factions to assign planets to.</param>
    /// <param name="planetSystems">The planet systems to choose from.</param>
    private void SetStartingPlanets(Faction[] factions, PlanetSystem[] planetSystems)
    {
        Config startConfig = GetConfig().GetValue<Config>("Planets.NumInitialPlanets.GalaxySize");
        string galaxySize = GetGameSummary().GalaxySize.ToString();
        int numStartingPlanets = startConfig.GetValue<int>(galaxySize);

        // Select and shuffle starting planets
        Queue<Planet> startingPlanets = new Queue<Planet>(
            planetSystems
                .SelectMany(ps => ps.Planets)
                .Where(planet => planet.IsColonized && !planet.IsHeadquarters)
                .Shuffle()
                .Take(numStartingPlanets * factions.Length)
        );

        // Assign planets to factions
        foreach (Faction faction in factions)
        {
            for (int i = 0; i < numStartingPlanets; i++)
            {
                if (startingPlanets.TryDequeue(out Planet planet))
                {
                    planet.OwnerInstanceID = faction.InstanceID;
                    planet.SetPopularSupport(faction.InstanceID, 100);
                }
                else
                {
                    throw new GameException("Not enough planets to assign to factions.");
                }
            }
        }
    }

    /// <summary>
    /// Selects the units for each faction. As all factions are deployed
    /// at the start of the game, this method does nothing.
    /// </summary>
    /// <param name="factions">The factions to select.</param>
    /// <returns>The unchanged factions array.</returns>
    public override Faction[] SelectUnits(Faction[] factions)
    {
        // No selection necessary, return as-is.
        return factions;
    }

    /// <summary>
    /// Decorates the units of each faction with additional properties.
    /// </summary>
    /// <param name="factions">The factions to decorate.</param>
    /// <returns>The decorated factions.</returns>
    public override Faction[] DecorateUnits(Faction[] factions)
    {
        int researchLevel = GetGameSummary().StartingResearchLevel;

        // Set the research level for each faction and manufacturing type.
        foreach (Faction faction in factions)
        {
            faction.SetResearchLevel(ManufacturingType.Building, researchLevel);
            faction.SetResearchLevel(ManufacturingType.Ship, researchLevel);
            faction.SetResearchLevel(ManufacturingType.Troop, researchLevel);
        }

        return factions;
    }

    /// <summary>
    /// Assigns initial planets to each faction. This method does NOT assign units.
    /// </summary>
    /// <param name="factions">The factions to deploy.</param>
    /// <param name="destinations">The planet systems to deploy to.</param>
    /// <returns>The deployed factions.</returns>
    public override Faction[] DeployUnits(Faction[] factions, PlanetSystem[] destinations)
    {
        SetFactionHQs(factions, destinations);
        SetStartingPlanets(factions, destinations);

        return factions;
    }
}
