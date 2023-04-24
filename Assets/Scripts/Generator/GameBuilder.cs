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
    private GameSummary _summary;

    /// <summary>
    ///
    /// </summary>
    /// <param name="summary"></param>
    public GameBuilder(GameSummary summary)
    {
        NewGameConfig config = ResourceManager.GetConfig<NewGameConfig>();

        _psGenerator = new PlanetSystemGenerator(summary, config);
        _factionGenerator = new FactionGenerator(summary, config);
        _officerGenerator = new OfficerGenerator(summary, config);
        _buildingGenerator = new BuildingGenerator(summary, config);

        _summary = summary;
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public Game BuildGame()
    {
        // Load resources from XML files and the config file.
        PlanetSystem[] planetSystems = ResourceManager.GetGameNodeData<PlanetSystem>();
        Faction[] factions = ResourceManager.GetGameNodeData<Faction>();
        Building[] buildings = ResourceManager.GetGameNodeData<Building>();
        Officer[] officers = ResourceManager.GetGameNodeData<Officer>();
        NewGameConfig config = ResourceManager.GetConfig<NewGameConfig>();

        // Build galaxy map and set each faction's initial planets/officers.
        PlanetSystem[] galaxyMap = _psGenerator.SelectUnits(planetSystems).GetSelectedUnits();
        IUnitSelectionResult<Officer> selectedOfficers = _officerGenerator.SelectUnits(officers);
        _factionGenerator.DeployUnits(factions, galaxyMap);

        // Decorate planet and officer base stats.
        galaxyMap = _psGenerator.DecorateUnits(galaxyMap);
        officers = _officerGenerator.DecorateUnits(officers);

        // Decorate planets/planet systems with manufacturables (buildings, fleets, regiments, etc).
        _buildingGenerator.RandomizeUnits(buildings, planetSystems);

        return new Game
        {
            Summary = this._summary,
            Factions = factions.ToList<Faction>(),
            GalaxyMap = galaxyMap.ToList<PlanetSystem>(),
            BuildingResearchList = buildings.ToList(),
            UnrecruitedOfficers = selectedOfficers.GetRemainingUnits().ToList(),
        };
    }
}
