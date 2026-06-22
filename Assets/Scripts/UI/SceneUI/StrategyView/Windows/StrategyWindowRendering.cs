using System.Collections.Generic;
using Rebellion.Game.Factions;
using Rebellion.Game.Galaxy;
using Rebellion.SceneGraph;

public sealed class StrategyWindowRenderContext
{
    public StrategyWindowRenderContext(
        GameManager gameManager,
        GalaxyMap galaxyMap,
        IReadOnlyList<GalaxyMapSector> sectors,
        Faction playerFaction,
        string playerFactionId,
        bool useUpperButtonLayout
    )
    {
        GameManager = gameManager;
        GalaxyMap = galaxyMap;
        Sectors = sectors;
        PlayerFaction = playerFaction;
        PlayerFactionId = playerFactionId;
        UseUpperButtonLayout = useUpperButtonLayout;
    }

    public GameManager GameManager { get; }
    public GalaxyMap GalaxyMap { get; }
    public IReadOnlyList<GalaxyMapSector> Sectors { get; }
    public Faction PlayerFaction { get; }
    public string PlayerFactionId { get; }
    public bool UseUpperButtonLayout { get; }

    public ISceneNode FindVisibleNode(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId) || GalaxyMap == null)
            return null;

        ISceneNode match = null;
        GalaxyMap.Traverse(node =>
        {
            if (match == null && node?.InstanceID == instanceId)
                match = node;
        });
        return match;
    }
}

public interface IStrategyWindowContent
{
    void RefreshWindow(StrategyWindowRenderContext context, UIWindow window, bool active);
}

public interface IGalaxyMapSectorWindowView
{
    GalaxyMapSector Sector { get; }
    void ReconcileSector(GalaxyMapSector sector);
}

public interface IGalaxyMapPlanetWindowView
{
    GalaxyMapPlanet GalaxyMapPlanet { get; }
    void ReconcilePlanet(GalaxyMapPlanet planet);
}

public interface IPlanetIconWindowView : IGalaxyMapPlanetWindowView
{
    PlanetIcon PlanetIcon { get; }
    void InitializeWindow(GalaxyMapPlanet planet);
}

public interface IStrategyUIContextReceiver
{
    void Initialize(UIContext uiContext);
}

public interface IStrategyUIRuntimeReceiver
{
    void Initialize(StrategyUIRuntime uiRuntime);
}

public interface IStrategyWindowSelectionView
{
    void ClearSelection();
}

public interface IStrategyWindowStatusTargetView
{
    StrategyStatusTarget GetStatusTarget(GalaxyMapPlanet planet);
}

public interface IStrategyWindowDragImageView
{
    bool TryGetDragPreview(int sourceX, int sourceY, out DragPreview preview);
}

public interface IStrategyWindowContextItemsView
{
    List<ISceneNode> GetContextItems();
}

public interface IStrategyWindowScrapItemsView
{
    List<ISceneNode> GetScrapItems();
}

public interface IConstructionWindowControllerReceiver
{
    void InitializeConstruction(ConstructionWindowController constructionWindowController);
}
