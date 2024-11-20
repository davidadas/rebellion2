using System;

public interface IUnitGenerator<TUnit, TDestination>
    where TUnit : BaseGameEntity
    where TDestination : BaseGameEntity
{
    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public GameSummary GetGameSummary();

    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    public IConfig GetConfig();

    /// <summary>
    ///
    /// </summary>
    /// <param name="units"></param>
    /// <returns></returns>
    public TUnit[] SelectUnits(TUnit[] units);

    /// <summary>
    ///
    /// </summary>
    /// <param name="units"></param>
    /// <returns></returns>
    public TUnit[] DecorateUnits(TUnit[] units);

    /// <summary>
    ///
    /// </summary>
    /// <param name="units"></param>
    /// <param name="destinations"></param>
    /// <returns></returns>
    public TUnit[] DeployUnits(TUnit[] units, PlanetSystem[] destinations);

    /// <summary>
    ///
    /// </summary>
    /// <param name="destinations"></param>
    /// <returns></returns>
    public IUnitGenerationResults<TUnit> GenerateUnits(
        TDestination[] destinations = default(TDestination[])
    );
}
