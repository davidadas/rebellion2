using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using IEnumerableExtensions;
using UnityEngine;

/// <summary>
/// WARNING: This class is considered a placeholder and is likely to change in the future.
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
    ///
    /// </summary>
    /// <param name="summary"></param>
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
    ///
    /// </summary>
    /// <returns></returns>
    public Game BuildGame()
    {
        // First, generate our galaxy map with stat decorated planets.
        IUnitGenerationResults<PlanetSystem> psResults = _psGenerator.GenerateUnits();
        PlanetSystem[] galaxyMap = psResults.SelectedUnits;

        // Then decorate the galaxy map with units.
        Building[] buildings = _buildingGenerator.GenerateUnits(galaxyMap).UnitPool;
        Faction[] factions = _factionGenerator.GenerateUnits(galaxyMap).UnitPool;
        CapitalShip[] capitalShips = _csGenerator.GenerateUnits(galaxyMap).DeployedUnits;
        IUnitGenerationResults<Officer> officerResults = _officerGenerator.GenerateUnits(galaxyMap);

        // Retrieve list of unrecruited officers.
        Officer[] unrecruitedOfficers = officerResults.UnitPool
            .Except(officerResults.SelectedUnits)
            .ToArray();

        return new Game
        {
            Summary = this._summary,
            Factions = factions.ToList<Faction>(),
            GalaxyMap = galaxyMap.ToList<PlanetSystem>(),
            BuildingResearchList = buildings.ToList(),
            UnrecruitedOfficers = unrecruitedOfficers.ToList(),
        };
    }
}
