using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ICollectionExtensions;
using IEnumerableExtensions;

/// <summary>
/// Represents a class responsible for building the game by generating the galaxy map and decorating it with units.
/// </summary>
public sealed class GameBuilder
{
    private PlanetSystemGenerator psGenerator;
    private FactionGenerator factionGenerator;
    private OfficerGenerator officerGenerator;
    private BuildingGenerator buildingGenerator;
    private CapitalShipGenerator csGenerator;
    private StarfighterGenerator starfighterGenerator;
    private GameSummary summary;

    /// <summary>
    /// Initializes a new instance of the GameBuilder class.
    /// </summary>
    /// <param name="summary">The summary of the game.</param>
    public GameBuilder(GameSummary summary)
    {
        IResourceManager resourceManager = ResourceManager.Instance;

        // Initialize our unit generators.
        psGenerator = new PlanetSystemGenerator(summary, resourceManager);
        factionGenerator = new FactionGenerator(summary, resourceManager);
        officerGenerator = new OfficerGenerator(summary, resourceManager);
        buildingGenerator = new BuildingGenerator(summary, resourceManager);
        csGenerator = new CapitalShipGenerator(summary, resourceManager);
        starfighterGenerator = new StarfighterGenerator(summary, resourceManager);

        this.summary = summary;
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
            foreach (IManufacturable technology in combinedTechnologies)
            {
                Technology reference = new Technology(technology);
                SceneNode sceneNode = technology as SceneNode;
                foreach (string allowedOwnerInstanceID in sceneNode.AllowedOwnerInstanceIDs)
                {
                    if (allowedOwnerInstanceID == faction.InstanceID)
                    {
                        faction.AddTechnologyNode(technology.GetRequiredResearchLevel(), reference);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Builds the game by generating the galaxy map and decorating it with units.
    /// </summary>
    /// <returns>The built game.</returns>
    public Game BuildGame()
    {
        // Generate our galaxy map with stat decorated planets.
        IUnitGenerationResults<PlanetSystem> psResults = psGenerator.GenerateUnits();
        PlanetSystem[] galaxyMap = psResults.SelectedUnits;

        // Decorate the galaxy map with units.
        Faction[] factions = factionGenerator.GenerateUnits(galaxyMap).UnitPool;
        Building[] buildings = buildingGenerator.GenerateUnits(galaxyMap).UnitPool;
        CapitalShip[] capitalShips = csGenerator.GenerateUnits(galaxyMap).UnitPool;
        Starfighter[] starfighters = starfighterGenerator.GenerateUnits(galaxyMap).UnitPool;
        IUnitGenerationResults<Officer> officerResults = officerGenerator.GenerateUnits(galaxyMap);

        // Retrieve list of unrecruited officers.
        Officer[] unrecruitedOfficers = officerResults
            .UnitPool.Except(officerResults.SelectedUnits)
            .ToArray();

        // Initialize each faction's technology tree.
        Dictionary<string, Faction> factionMap = factions.ToDictionary(faction => faction.InstanceID);
        IManufacturable[] combinedTechnologies = Array
            .Empty<IManufacturable>()
            .Concat(buildings)
            .Concat(capitalShips)
            .Concat(starfighters)
            .ToArray();

        // Set the technology tree for each faction.
        SetTechnologyLevels(factionMap, combinedTechnologies);

        // Set the galaxy map.
        GalaxyMap galaxy = new GalaxyMap { PlanetSystems = galaxyMap.ToList<PlanetSystem>() };

        // Initialize our new game.
        Game game = new Game
        {
            Summary = this.summary,
            Factions = factions.ToList<Faction>(),
            Galaxy = galaxy,
            UnrecruitedOfficers = unrecruitedOfficers.ToList(),
        };
        return game;
    }
}
