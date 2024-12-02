using System.Linq;

public class RegimentGenerator : UnitGenerator<Regiment>
{
    /// <summary>
    /// Constructs an EventGenerator object.
    /// </summary>
    /// <param name="summary">The GameSummary options selected by the player.</param>
    /// <param name="resourceManager">The resource manager from which to load game data.</param>
    public RegimentGenerator(GameSummary summary, IResourceManager resourceManager)
        : base(summary, resourceManager) { }

    /// <summary>
    ///
    /// </summary>
    /// <param name="regiments"></param>
    /// <returns></returns>
    public override Regiment[] SelectUnits(Regiment[] regiments)
    {
        return regiments
            .Where(regiment =>
                regiment.RequiredResearchLevel <= GetGameSummary().StartingResearchLevel
            )
            .ToArray();
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="regiments"></param>
    /// <returns></returns>
    public override Regiment[] DecorateUnits(Regiment[] regiments)
    {
        return regiments;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="regiments"></param>
    /// <param name="destinations"></param>
    /// <returns></returns>
    public override Regiment[] DeployUnits(Regiment[] regiments, PlanetSystem[] destinations)
    {
        return regiments;
    }
}
