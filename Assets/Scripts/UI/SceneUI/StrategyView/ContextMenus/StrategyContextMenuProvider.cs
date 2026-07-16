using System.Collections.Generic;
using System.Linq;
using UnityEngine.EventSystems;

/// <summary>
/// Builds context-menu presentation for one supported strategy window.
/// </summary>
public interface IStrategyContextMenuProvider
{
    /// <summary>
    /// Tries to build a context menu for the current invocation.
    /// </summary>
    /// <param name="context">The active context-menu invocation.</param>
    /// <param name="request">Receives the completed command request.</param>
    /// <param name="width">Receives the authored menu width.</param>
    /// <returns>True when the provider handled the invocation.</returns>
    bool TryCreateContextMenu(
        StrategyContextMenuProviderContext context,
        out ContextMenuRequest request,
        out int width
    );
}

/// <summary>
/// Contains the immutable pointer and layout data for one context-menu invocation.
/// </summary>
public sealed class StrategyContextMenuProviderContext : IStrategyContextMenuSource
{
    /// <summary>
    /// Creates a context-menu provider invocation.
    /// </summary>
    /// <param name="window">The source window.</param>
    /// <param name="layout">The authored menu layout.</param>
    /// <param name="eventData">The source pointer event.</param>
    /// <param name="x">The source-space horizontal position.</param>
    /// <param name="y">The source-space vertical position.</param>
    public StrategyContextMenuProviderContext(
        UIWindow window,
        StrategyContextMenuLayout layout,
        PointerEventData eventData,
        int x,
        int y
    )
    {
        Window = window;
        Layout = layout;
        EventData = eventData;
        X = x;
        Y = y;
    }

    /// <summary>
    /// Gets the window.
    /// </summary>
    public UIWindow Window { get; }

    /// <summary>
    /// Gets the layout.
    /// </summary>
    public StrategyContextMenuLayout Layout { get; }

    /// <summary>
    /// Gets the event data.
    /// </summary>
    public PointerEventData EventData { get; }

    /// <summary>
    /// Gets the horizontal coordinate.
    /// </summary>
    public int X { get; }

    /// <summary>
    /// Gets the vertical coordinate.
    /// </summary>
    public int Y { get; }
}

/// <summary>
/// Contains the authored menu widths used by context-menu providers.
/// </summary>
public readonly struct StrategyContextMenuLayout
{
    /// <summary>
    /// Creates an authored context-menu layout.
    /// </summary>
    /// <param name="facilityMenuWidth">The facility menu width.</param>
    /// <param name="fleetMenuWidth">The standard fleet menu width.</param>
    /// <param name="fleetBombardmentMenuWidth">The fleet menu width when bombardment is available.</param>
    /// <param name="planetSystemMenuWidth">The planet-system menu width.</param>
    /// <param name="defenseMenuWidth">The defense menu width.</param>
    /// <param name="missionsMenuWidth">The missions menu width.</param>
    /// <param name="fallbackMenuWidth">The fallback menu width.</param>
    public StrategyContextMenuLayout(
        int facilityMenuWidth,
        int fleetMenuWidth,
        int fleetBombardmentMenuWidth,
        int planetSystemMenuWidth,
        int defenseMenuWidth,
        int missionsMenuWidth,
        int fallbackMenuWidth
    )
    {
        FacilityMenuWidth = facilityMenuWidth;
        FleetMenuWidth = fleetMenuWidth;
        FleetBombardmentMenuWidth = fleetBombardmentMenuWidth;
        PlanetSystemMenuWidth = planetSystemMenuWidth;
        DefenseMenuWidth = defenseMenuWidth;
        MissionsMenuWidth = missionsMenuWidth;
        FallbackMenuWidth = fallbackMenuWidth;
    }

    /// <summary>
    /// Gets the facility menu width.
    /// </summary>
    public int FacilityMenuWidth { get; }

    /// <summary>
    /// Gets the standard fleet menu width.
    /// </summary>
    public int FleetMenuWidth { get; }

    /// <summary>
    /// Gets the fleet menu width used when bombardment is available.
    /// </summary>
    public int FleetBombardmentMenuWidth { get; }

    /// <summary>
    /// Gets the planet-system menu width.
    /// </summary>
    public int PlanetSystemMenuWidth { get; }

    /// <summary>
    /// Gets the defense menu width.
    /// </summary>
    public int DefenseMenuWidth { get; }

    /// <summary>
    /// Gets the missions menu width.
    /// </summary>
    public int MissionsMenuWidth { get; }

    /// <summary>
    /// Gets the fallback menu width.
    /// </summary>
    public int FallbackMenuWidth { get; }
}

/// <summary>
/// Contains the immutable presentation for one strategy context menu.
/// </summary>
public sealed class StrategyContextMenuData
{
    /// <summary>
    /// Creates a strategy context-menu presentation.
    /// </summary>
    /// <param name="window">The source window.</param>
    /// <param name="x">The source-space horizontal position.</param>
    /// <param name="y">The source-space vertical position.</param>
    /// <param name="width">The authored base width.</param>
    /// <param name="commands">The displayed commands.</param>
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

    /// <summary>
    /// Gets the window.
    /// </summary>
    public UIWindow Window { get; }

    /// <summary>
    /// Gets the horizontal coordinate.
    /// </summary>
    public int X { get; }

    /// <summary>
    /// Gets the vertical coordinate.
    /// </summary>
    public int Y { get; }

    /// <summary>
    /// Gets the width.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the commands.
    /// </summary>
    public IReadOnlyList<StrategyMenuCommand> Commands { get; }
}
