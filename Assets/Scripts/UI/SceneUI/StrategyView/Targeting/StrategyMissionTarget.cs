using Rebellion.Game.Missions;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;

/// <summary>
/// Identifies a strategy planet and optional scene node selected by targeting.
/// </summary>
public sealed class StrategyMissionTarget : ITargetable
{
    public GalaxyMapPlanet Planet { get; }

    public ISceneNode Item { get; }

    public object Target => this;

    /// <summary>
    /// Creates a strategy target for one planet and optional contained item.
    /// </summary>
    /// <param name="planet">The selected galaxy-map planet.</param>
    /// <param name="item">The optional selected scene node.</param>
    public StrategyMissionTarget(GalaxyMapPlanet planet, ISceneNode item)
    {
        Planet = planet;
        Item = item;
    }

    /// <summary>
    /// Resolves the concrete fleet, ship, or planet that can receive movement.
    /// </summary>
    /// <returns>The movement destination, or null when the planet is unavailable.</returns>
    public ISceneNode GetMoveDestination()
    {
        if (Planet?.Planet == null)
            return null;

        if (Item == null)
            return Planet.Planet;

        if (Item is Fleet or CapitalShip)
            return Item;

        ISceneNode parent = Item.GetParent();
        if (parent is Fleet or CapitalShip)
            return parent;

        return Planet.Planet;
    }

    /// <summary>
    /// Resolves the scene node targeted by one mission option.
    /// </summary>
    /// <param name="targetKind">The kind of target accepted by the mission option.</param>
    /// <returns>The selected target when it satisfies the requested target kind.</returns>
    public ISceneNode GetMissionTarget(MissionTargetKind targetKind)
    {
        return targetKind switch
        {
            MissionTargetKind.Planet => Planet?.Planet,
            MissionTargetKind.Manufacturable when Item is IManufacturable => Item,
            MissionTargetKind.Officer when Item is Officer => Item,
            _ => null,
        };
    }
}
