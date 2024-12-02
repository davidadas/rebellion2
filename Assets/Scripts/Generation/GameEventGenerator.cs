/// <summary>
///
/// </summary>
public class GameEventGenerator : UnitGenerator<GameEvent>
{
    /// <summary>
    /// Constructs an EventGenerator object.
    /// </summary>
    /// <param name="summary">The GameSummary options selected by the player.</param>
    /// <param name="resourceManager">The resource manager from which to load game data.</param>
    public GameEventGenerator(GameSummary summary, IResourceManager resourceManager)
        : base(summary, resourceManager) { }

    /// <summary>
    ///
    /// </summary>
    /// <param name="events"></param>
    /// <returns></returns>
    public override GameEvent[] DecorateUnits(GameEvent[] events)
    {
        return events;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="events"></param>
    /// <param name="destinations"></param>
    /// <returns></returns>
    public override GameEvent[] DeployUnits(GameEvent[] events, PlanetSystem[] destinations)
    {
        return events;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="events"></param>
    /// <returns></returns>
    public override GameEvent[] SelectUnits(GameEvent[] events)
    {
        return events;
    }
}
