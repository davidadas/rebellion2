using System;

public abstract class UnitGenerator
{
    private GameSummary _summary;
    private IConfig _config;

    /// <summary>
    ///
    /// </summary>
    /// <param name="summary"></param>
    /// <param name="config"></param>
    public UnitGenerator(GameSummary summary, IConfig config)
    {
        this._summary = summary;
        this._config = config;
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
}
