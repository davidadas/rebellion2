using Rebellion.Game.Units;
using Rebellion.SceneGraph;

public sealed class StrategyStatusTarget
{
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

    public GalaxyMapPlanet Planet { get; }
    public ISceneNode Item { get; }
    public ManufacturingType? ManufacturingType { get; }
}
