using System.Collections.Generic;
using System.Linq;
using UnityEngine.EventSystems;

public interface IStrategyContextMenuProvider
{
    bool TryBuildContextMenu(
        StrategyContextMenuProviderContext context,
        out StrategyContextMenuData menu
    );
    void BeginContextTargeting(StrategyContextMenuProviderContext context, int action);
}

public sealed class StrategyContextMenuProviderContext
{
    public StrategyContextMenuProviderContext(
        UIWindow window,
        StrategyContextMenuLayout layout,
        IStrategyContextMenuActions actions,
        PointerEventData eventData,
        int x,
        int y
    )
    {
        Window = window;
        Layout = layout;
        Actions = actions;
        EventData = eventData;
        X = x;
        Y = y;
    }

    public UIWindow Window { get; }
    public StrategyContextMenuLayout Layout { get; }
    public IStrategyContextMenuActions Actions { get; }
    public PointerEventData EventData { get; }
    public int X { get; }
    public int Y { get; }
}

public readonly struct StrategyContextMenuLayout
{
    public StrategyContextMenuLayout(
        int facilityMenuWidth,
        int planetSystemMenuWidth,
        int defenseMenuWidth,
        int missionsMenuWidth,
        int fallbackMenuWidth
    )
    {
        FacilityMenuWidth = facilityMenuWidth;
        PlanetSystemMenuWidth = planetSystemMenuWidth;
        DefenseMenuWidth = defenseMenuWidth;
        MissionsMenuWidth = missionsMenuWidth;
        FallbackMenuWidth = fallbackMenuWidth;
    }

    public int FacilityMenuWidth { get; }
    public int PlanetSystemMenuWidth { get; }
    public int DefenseMenuWidth { get; }
    public int MissionsMenuWidth { get; }
    public int FallbackMenuWidth { get; }
}

public sealed class StrategyContextMenuData
{
    public StrategyContextMenuData(
        UIWindow window,
        int x,
        int y,
        int width,
        IReadOnlyList<StrategyMenuCommand> commands
    )
    {
        Window = window;
        X = x;
        Y = y;
        Width = width;
        Commands = commands?.ToList() ?? new List<StrategyMenuCommand>();
    }

    public UIWindow Window { get; }
    public int X { get; }
    public int Y { get; }
    public int Width { get; }
    public IReadOnlyList<StrategyMenuCommand> Commands { get; }
}
