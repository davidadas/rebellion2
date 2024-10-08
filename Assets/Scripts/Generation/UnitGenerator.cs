using System;

/// <summary>
///
/// </summary>
/// <typeparam name="TUnit"></typeparam>
class UnitGenerationResults<TUnit> : IUnitGenerationResults<TUnit>
    where TUnit : SceneNode
{
    public TUnit[] UnitPool { get; set; }
    public TUnit[] SelectedUnits { get; set; }
    public TUnit[] DeployedUnits { get; set; }

    public UnitGenerationResults(TUnit[] unitPool, TUnit[] selectedUnits, TUnit[] deployedUnits)
    {
        UnitPool = unitPool;
        SelectedUnits = selectedUnits;
        DeployedUnits = deployedUnits;
    }
}

/// <summary>
///
/// </summary>
/// <typeparam name="TUnit"></typeparam>
public abstract class UnitGenerator<TUnit> : IUnitGenerator<TUnit, PlanetSystem>
    where TUnit : SceneNode
{
    private GameSummary _summary;
    private IResourceManager _resourceManager;
    private IConfig _config;

    /// <summary>
    ///
    /// </summary>
    /// <param name="summary"></param>
    /// <param name="resourceManager"></param>
    public UnitGenerator(GameSummary summary, IResourceManager resourceManager)
    {
        _summary = summary;
        _resourceManager = resourceManager;
        _config = _resourceManager.GetConfig<NewGameConfig>();
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public GameSummary GetGameSummary()
    {
        return _summary;
    }

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public IConfig GetConfig()
    {
        return _config;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="units"></param>
    /// <returns></returns>
    public abstract TUnit[] SelectUnits(TUnit[] units);

    /// <summary>
    ///
    /// </summary>
    /// <param name="units"></param>
    /// <returns></returns>
    public abstract TUnit[] DecorateUnits(TUnit[] units);

    /// <summary>
    ///
    /// </summary>
    /// <param name="units"></param>
    /// <param name="destinations"></param>
    /// <returns></returns>
    public abstract TUnit[] DeployUnits(
        TUnit[] units,
        PlanetSystem[] destinations = default(PlanetSystem[])
    );

    /// <summary>
    ///
    /// </summary>
    /// <param name="destinations"></param>
    /// <returns></returns>
    public IUnitGenerationResults<TUnit> GenerateUnits(
        PlanetSystem[] destinations = default(PlanetSystem[])
    )
    {
        // Load game data from file.
        TUnit[] unitPool = _resourceManager.GetSceneNodeData<TUnit>();

        // Select units which shall appear in game.
        TUnit[] selectedUnits = SelectUnits(unitPool);

        // Decorate the selected units.
        selectedUnits = DecorateUnits(selectedUnits);

        // Deploy units to the scene.
        TUnit[] deployedUnits = DeployUnits(selectedUnits, destinations);

        return new UnitGenerationResults<TUnit>(unitPool, selectedUnits, deployedUnits);
    }
}
