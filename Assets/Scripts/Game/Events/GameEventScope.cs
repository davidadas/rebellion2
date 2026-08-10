namespace Rebellion.Game.Events
{
    /// <summary>
    /// Controls whether an event has one global schedule or one schedule per planet.
    /// </summary>
    public enum GameEventScope
    {
        Global,
        EachPlanet,
    }

    /// <summary>
    /// Filters planets included by a per-planet event scope.
    /// </summary>
    public enum PlanetScopeOwnership
    {
        Any,
        Owned,
        Neutral,
    }
}
