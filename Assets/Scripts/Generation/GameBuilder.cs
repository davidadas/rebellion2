using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using ICollectionExtensions;
using IEnumerableExtensions;

/// <summary>
/// Represents a class responsible for building the game by generating the galaxy map and decorating it with units.
/// </summary>
public sealed class GameBuilder
{
    private PlanetSystemGenerator _psGenerator;
    private FactionGenerator _factionGenerator;
    private OfficerGenerator _officerGenerator;
    private BuildingGenerator _buildingGenerator;
    private CapitalShipGenerator _csGenerator;
    private GameSummary _summary;

    /// <summary>
    /// Initializes a new instance of the GameBuilder class.
    /// </summary>
    /// <param name="summary">The summary of the game.</param>
    public GameBuilder(GameSummary summary)
    {
        IResourceManager resourceManager = ResourceManager.Instance;

        // Initialize our unit generators.
        _psGenerator = new PlanetSystemGenerator(summary, resourceManager);
        _factionGenerator = new FactionGenerator(summary, resourceManager);
        _officerGenerator = new OfficerGenerator(summary, resourceManager);
        _buildingGenerator = new BuildingGenerator(summary, resourceManager);
        _csGenerator = new CapitalShipGenerator(summary, resourceManager);

        _summary = summary;
    }

    /// <summary>
    /// Gets the reference map for the given game nodes.
    /// </summary>
    /// <param name="nodes">The game nodes to create the reference map from.</param>
    /// <returns>The reference map.</returns>
    private SerializableDictionary<string, ReferenceNode> getReferenceMap(params SceneNode[][] nodes)
    {
        SerializableDictionary<string, ReferenceNode> referenceMap =
            new SerializableDictionary<string, ReferenceNode>();
        List<SceneNode> referenceNodes = new List<SceneNode>();
        referenceNodes.AddAll(nodes);

        foreach (SceneNode node in referenceNodes)
        {
            ReferenceNode reference = new ReferenceNode(node);
            referenceMap[node.GameID] = reference;
        }

        return referenceMap;
    }

    /// <summary>
    /// Builds the game by generating the galaxy map and decorating it with units.
    /// </summary>
    /// <returns>The built game.</returns>
    public Game BuildGame()
    {
        // First, generate our galaxy map with stat decorated planets.
        IUnitGenerationResults<PlanetSystem> psResults = _psGenerator.GenerateUnits();
        PlanetSystem[] galaxyMap = psResults.SelectedUnits;

        // Then decorate the galaxy map with units.
        Building[] buildings = _buildingGenerator.GenerateUnits(galaxyMap).UnitPool;
        Faction[] factions = _factionGenerator.GenerateUnits(galaxyMap).UnitPool;
        CapitalShip[] capitalShips = _csGenerator.GenerateUnits(galaxyMap).UnitPool;
        IUnitGenerationResults<Officer> officerResults = _officerGenerator.GenerateUnits(galaxyMap);

        // Retrieve list of unrecruited officers.
        Officer[] unrecruitedOfficers = officerResults.UnitPool
            .Except(officerResults.SelectedUnits)
            .ToArray();

        // Set the list of reference nodes (used for lookups).
        SerializableDictionary<string, ReferenceNode> referenceMap = getReferenceMap(
            capitalShips,
            buildings
        );

        GalaxyMap Galaxy = new GalaxyMap
        {
            PlanetSystems = galaxyMap.ToList<PlanetSystem>(),
        };

        // Initialize our new game.
        Game game = new Game
        {
            Summary = this._summary,
            Galaxy = Galaxy,
            Factions = factions.ToList<Faction>(),
            UnrecruitedOfficers = unrecruitedOfficers.ToList(),
            ReferenceDictionary = referenceMap,
        };

        return game;
    }
}
