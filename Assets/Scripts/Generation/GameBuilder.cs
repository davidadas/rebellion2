using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Builds the game by generating the galaxy map and populating it with units and factions.
/// </summary>
public sealed class GameBuilder
{
    private readonly GameSummary summary;
    private readonly PlanetSystemGenerator planetSystemGenerator;
    private readonly FactionGenerator factionGenerator;
    private readonly OfficerGenerator officerGenerator;
    private readonly BuildingGenerator buildingGenerator;
    private readonly CapitalShipGenerator capitalShipGenerator;
    private readonly StarfighterGenerator starfighterGenerator;
    private readonly RegimentGenerator regimentGenerator;
    private readonly GameEventGenerator gameEventGenerator;

    /// <summary>
    /// Initializes a new instance of the GameBuilder class.
    /// </summary>
    /// <param name="summary">The summary of the game to be built.</param>
    public GameBuilder(GameSummary summary)
    {
        this.summary = summary;
        IResourceManager resourceManager = ResourceManager.Instance;

        planetSystemGenerator = new PlanetSystemGenerator(summary, resourceManager);
        factionGenerator = new FactionGenerator(summary, resourceManager);
        officerGenerator = new OfficerGenerator(summary, resourceManager);
        buildingGenerator = new BuildingGenerator(summary, resourceManager);
        capitalShipGenerator = new CapitalShipGenerator(summary, resourceManager);
        starfighterGenerator = new StarfighterGenerator(summary, resourceManager);
        regimentGenerator = new RegimentGenerator(summary, resourceManager);
        gameEventGenerator = new GameEventGenerator(summary, resourceManager);
    }

    /// <summary>
    /// Builds the game by generating all necessary components.
    /// </summary>
    /// <returns>The fully constructed game.</returns>
    public Game BuildGame()
    {
        PlanetSystem[] galaxyMap = GenerateGalaxyMap();
        Faction[] factions = GenerateFactions(galaxyMap);

        Building[] buildings = GenerateBuildings(galaxyMap);
        CapitalShip[] capitalShips = GenerateCapitalShips(galaxyMap);
        Starfighter[] starfighters = GenerateStarfighters(galaxyMap);
        Regiment[] regiments = GenerateRegiments(galaxyMap);

        IUnitGenerationResults<Officer> officerResults = GenerateOfficers(galaxyMap);
        Officer[] unrecruitedOfficers = GetUnrecruitedOfficers(officerResults);

        GameEvent[] gameEvents = GenerateGameEvents(galaxyMap);

        SetupFactionTechnologies(factions, buildings, capitalShips, starfighters, regiments);

        return CreateGame(galaxyMap, factions, gameEvents, unrecruitedOfficers);
    }

    /// <summary>
    /// Generates the galaxy map.
    /// </summary>
    /// <returns>An array of PlanetSystem objects representing the galaxy map.</returns>
    private PlanetSystem[] GenerateGalaxyMap()
    {
        return planetSystemGenerator.GenerateUnits().SelectedUnits;
    }

    /// <summary>
    /// Generates factions for the game.
    /// </summary>
    /// <param name="galaxyMap">The galaxy map to use for generation.</param>
    /// <returns>An array of Faction objects.</returns>
    private Faction[] GenerateFactions(PlanetSystem[] galaxyMap)
    {
        return factionGenerator.GenerateUnits(galaxyMap).UnitPool;
    }

    /// <summary>
    /// Generates buildings for the game.
    /// </summary>
    /// <param name="galaxyMap">The galaxy map to use for generation.</param>
    /// <returns>An array of Building objects.</returns>
    private Building[] GenerateBuildings(PlanetSystem[] galaxyMap)
    {
        return buildingGenerator.GenerateUnits(galaxyMap).UnitPool;
    }

    /// <summary>
    /// Generates capital ships for the game.
    /// </summary>
    /// <param name="galaxyMap">The galaxy map to use for generation.</param>
    /// <returns>An array of CapitalShip objects.</returns>
    private CapitalShip[] GenerateCapitalShips(PlanetSystem[] galaxyMap)
    {
        return capitalShipGenerator.GenerateUnits(galaxyMap).UnitPool;
    }

    /// <summary>
    /// Generates starfighters for the game.
    /// </summary>
    /// <param name="galaxyMap">The galaxy map to use for generation.</param>
    /// <returns>An array of Starfighter objects.</returns>
    private Starfighter[] GenerateStarfighters(PlanetSystem[] galaxyMap)
    {
        return starfighterGenerator.GenerateUnits(galaxyMap).UnitPool;
    }

    /// <summary>
    /// Generates regiments for the game.
    /// </summary>
    /// <param name="galaxyMap">The galaxy map to use for generation.</param>
    /// <returns>An array of Regiment objects.</returns>
    private Regiment[] GenerateRegiments(PlanetSystem[] galaxyMap)
    {
        return regimentGenerator.GenerateUnits(galaxyMap).UnitPool;
    }

    /// <summary>
    /// Generates officers for the game.
    /// </summary>
    /// <param name="galaxyMap">The galaxy map to use for generation.</param>
    /// <returns>The results of officer generation, including both selected and unselected officers.</returns>
    private IUnitGenerationResults<Officer> GenerateOfficers(PlanetSystem[] galaxyMap)
    {
        return officerGenerator.GenerateUnits(galaxyMap);
    }

    /// <summary>
    /// Retrieves the list of unrecruited officers.
    /// </summary>
    /// <param name="officerResults">The results of officer generation.</param>
    /// <returns>An array of Officer objects representing unrecruited officers.</returns>
    private Officer[] GetUnrecruitedOfficers(IUnitGenerationResults<Officer> officerResults)
    {
        return officerResults.UnitPool.Except(officerResults.SelectedUnits).ToArray();
    }

    /// <summary>
    /// Generates game events for the game.
    /// </summary>
    /// <param name="galaxyMap">The galaxy map to use for generation.</param>
    /// <returns>An array of GameEvent objects.</returns>
    private GameEvent[] GenerateGameEvents(PlanetSystem[] galaxyMap)
    {
        return gameEventGenerator.GenerateUnits(galaxyMap).UnitPool;
    }

    /// <summary>
    /// Sets up the technology trees for all factions.
    /// </summary>
    /// <param name="factions">The array of factions.</param>
    /// <param name="buildings">The array of buildings.</param>
    /// <param name="capitalShips">The array of capital ships.</param>
    /// <param name="starfighters">The array of starfighters.</param>
    /// <param name="regiments">The array of regiments.</param>
    private void SetupFactionTechnologies(
        Faction[] factions,
        Building[] buildings,
        CapitalShip[] capitalShips,
        Starfighter[] starfighters,
        Regiment[] regiments
    )
    {
        Dictionary<string, Faction> factionMap = factions.ToDictionary(faction =>
            faction.InstanceID
        );
        IManufacturable[] combinedTechnologies = buildings
            .Cast<IManufacturable>()
            .Concat(capitalShips)
            .Concat(starfighters)
            .Concat(regiments)
            .ToArray();

        SetTechnologyLevels(factionMap, combinedTechnologies);
    }

    /// <summary>
    /// Sets the technology tree for the factions in the game.
    /// </summary>
    /// <param name="factionMap">The map of factions in the game.</param>
    /// <param name="combinedTechnologies">The combined technologies in the game.</param>
    private void SetTechnologyLevels(
        Dictionary<string, Faction> factionMap,
        IManufacturable[] combinedTechnologies
    )
    {
        foreach (Faction faction in factionMap.Values)
        {
            foreach (IManufacturable manufacturable in combinedTechnologies)
            {
                Technology reference = new Technology(manufacturable);
                foreach (string allowedOwnerInstanceID in manufacturable.AllowedOwnerInstanceIDs)
                {
                    if (allowedOwnerInstanceID == faction.InstanceID)
                    {
                        faction.AddTechnologyNode(
                            manufacturable.GetRequiredResearchLevel(),
                            reference
                        );
                    }
                }
            }
        }
    }

    /// <summary>
    /// Creates and returns the final Game object.
    /// </summary>
    /// <param name="galaxyMap">The generated galaxy map.</param>
    /// <param name="factions">The generated factions.</param>
    /// <param name="gameEvents">The generated game events.</param>
    /// <param name="unrecruitedOfficers">The list of unrecruited officers.</param>
    /// <returns>A fully initialized Game object.</returns>
    private Game CreateGame(
        PlanetSystem[] galaxyMap,
        Faction[] factions,
        GameEvent[] gameEvents,
        Officer[] unrecruitedOfficers
    )
    {
        GalaxyMap galaxy = new GalaxyMap { PlanetSystems = galaxyMap.ToList() };

        return new Game
        {
            EventPool = gameEvents.ToList(),
            Summary = this.summary,
            Factions = factions.ToList(),
            Galaxy = galaxy,
            UnrecruitedOfficers = unrecruitedOfficers.ToList(),
        };
    }
}
