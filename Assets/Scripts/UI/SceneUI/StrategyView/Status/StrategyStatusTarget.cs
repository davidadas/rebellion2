using Rebellion.Game.Units;
using Rebellion.SceneGraph;

/// <summary>
/// Identifies the entity or manufacturing lane represented by a status window.
/// </summary>
public sealed class StrategyStatusTarget
{
    /// <summary>
    /// Creates one status target.
    /// </summary>
    /// <param name="planet">The strategy planet associated with the target.</param>
    /// <param name="item">The represented scene node, when applicable.</param>
    /// <param name="manufacturingType">The represented manufacturing lane, when applicable.</param>
    public StrategyStatusTarget(
        GalaxyMapPlanet planet,
        ISceneNode item,
        ManufacturingType? manufacturingType = null
    )
    {
        Planet = planet;
        Item = item;
        ManufacturingType = manufacturingType;
    }

    /// <summary>
    /// Gets the planet.
    /// </summary>
    public GalaxyMapPlanet Planet { get; }

    /// <summary>
    /// Gets the item.
    /// </summary>
    public ISceneNode Item { get; }

    /// <summary>
    /// Gets the manufacturing type.
    /// </summary>
    public ManufacturingType? ManufacturingType { get; }
}
