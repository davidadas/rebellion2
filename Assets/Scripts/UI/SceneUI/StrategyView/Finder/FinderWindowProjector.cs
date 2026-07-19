using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Produces immutable Finder presentation snapshots without mutating controller or view state.
/// </summary>
internal static class FinderWindowProjector
{
    private static readonly FinderWindowCommand[] _defaultCommands =
    {
        FinderWindowCommand.Close,
        FinderWindowCommand.Target,
    };

    private static readonly FinderWindowCommand[] _fleetCommands =
    {
        FinderWindowCommand.Close,
        FinderWindowCommand.Target,
        FinderWindowCommand.ShowShips,
        FinderWindowCommand.ShowFleets,
    };

    private static readonly FinderWindowCommand[] _personnelCommands =
    {
        FinderWindowCommand.Close,
        FinderWindowCommand.Target,
        FinderWindowCommand.ShowPersonnel,
        FinderWindowCommand.ShowSpecialForces,
    };

    /// <summary>
    /// Creates a complete Finder presentation from one controller-owned session.
    /// </summary>
    /// <param name="uiContext">The current presentation context.</param>
    /// <param name="window">The owning window shell.</param>
    /// <param name="useUpperButtonLayout">Whether commands use the upper button slots.</param>
    /// <param name="session">The controller-owned Finder session.</param>
    /// <param name="tabs">The semantic tabs in display order.</param>
    /// <returns>The immutable Finder presentation snapshot.</returns>
    public static FinderWindowRenderData CreateRenderData(
        UIContext uiContext,
        UIWindow window,
        bool useUpperButtonLayout,
        FinderWindowSession session,
        IReadOnlyList<FinderWindowTab> tabs
    )
    {
        if (uiContext == null)
            throw new ArgumentNullException(nameof(uiContext));
        if (window == null)
            throw new ArgumentNullException(nameof(window));
        if (session == null)
            throw new ArgumentNullException(nameof(session));

        FinderWindowTheme playerTheme = GetFinderTheme(uiContext, null);
        return new FinderWindowRenderData(
            session.Mode,
            session.Panel,
            session.ActiveTab,
            session.SelectedIndex,
            session.SearchText,
            GetWindowTitle(session.Mode, session.Panel),
            GetWindowLabel(session.Mode, session.Panel),
            GetTabText(uiContext, session.Mode, session.Panel, GetItem(tabs, session.ActiveTab)),
            new FinderWindowFrameRenderData(
                window.X,
                window.Y,
                window.Width,
                window.Height,
                window.ActiveWindow,
                useUpperButtonLayout,
                GetBackgroundTexture(uiContext, playerTheme, session.Mode, session.Panel),
                GetTexture(uiContext, playerTheme?.OverlayFrameImagePath),
                GetButtonStripTexture(uiContext, playerTheme, session.Mode, useUpperButtonLayout),
                CreateDialogButtons(uiContext, playerTheme, session.Mode, session.Panel)
            ),
            CreateTabs(uiContext, playerTheme, tabs, session.ActiveTab),
            CreateRows(session.ProjectedRows, session.SelectedIndex)
        );
    }

    /// <summary>
    /// Filters Finder rows by display name without changing source order.
    /// </summary>
    /// <param name="rows">The rows available under the active tab.</param>
    /// <param name="searchText">The case-insensitive name filter.</param>
    /// <returns>The rows whose names contain the filter.</returns>
    public static List<FinderWindowRow> FilterRows(
        IReadOnlyList<FinderWindowRow> rows,
        string searchText
    )
    {
        IReadOnlyList<FinderWindowRow> source = rows ?? Array.Empty<FinderWindowRow>();
        if (string.IsNullOrWhiteSpace(searchText))
            return new List<FinderWindowRow>(source);

        List<FinderWindowRow> filtered = new List<FinderWindowRow>();
        for (int i = 0; i < source.Count; i++)
        {
            FinderWindowRow row = source[i];
            if (row?.Name?.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                filtered.Add(row);
        }

        return filtered;
    }

    /// <summary>
    /// Creates immutable row presentation in projected order.
    /// </summary>
    /// <param name="rows">The projected domain rows.</param>
    /// <param name="selectedIndex">The selected projected row index.</param>
    /// <returns>The result-row presentation snapshots.</returns>
    public static IReadOnlyList<FinderWindowRowRenderData> CreateRows(
        IReadOnlyList<FinderWindowRow> rows,
        int selectedIndex
    )
    {
        IReadOnlyList<FinderWindowRow> source = rows ?? Array.Empty<FinderWindowRow>();
        List<FinderWindowRowRenderData> result = new List<FinderWindowRowRenderData>(source.Count);
        for (int i = 0; i < source.Count; i++)
        {
            FinderWindowRow row = source[i];
            result.Add(
                new FinderWindowRowRenderData(
                    row?.Identity,
                    row?.Name,
                    i == selectedIndex,
                    CreateCountText(row?.Counts)
                )
            );
        }

        return FinderWindowRenderData.Copy(result);
    }

    /// <summary>
    /// Returns the title for one Finder mode and panel.
    /// </summary>
    /// <param name="mode">The active Finder category.</param>
    /// <param name="panel">Whether the alternate results panel is active.</param>
    /// <returns>The displayed window title.</returns>
    public static string GetWindowTitle(FinderMode mode, bool panel)
    {
        return mode switch
        {
            FinderMode.Systems => "Planetary System Finder",
            FinderMode.Fleets => panel ? "Ship Finder" : "Fleet Finder",
            FinderMode.Troops => "Troop Finder",
            FinderMode.Personnel => panel ? "Special Forces Finder" : "Personnel Finder",
            _ => string.Empty,
        };
    }

    /// <summary>
    /// Returns the search label for one Finder mode and panel.
    /// </summary>
    /// <param name="mode">The active Finder category.</param>
    /// <param name="panel">Whether the alternate results panel is active.</param>
    /// <returns>The displayed search label.</returns>
    public static string GetWindowLabel(FinderMode mode, bool panel)
    {
        return mode switch
        {
            FinderMode.Systems => "System Name",
            FinderMode.Fleets => panel ? "Ship Name" : "Fleet Name",
            FinderMode.Troops => "Troop Location",
            FinderMode.Personnel => panel ? "Special Forces Location" : "Personnel Name",
            _ => string.Empty,
        };
    }

    /// <summary>
    /// Creates command-button presentation in authored slot order.
    /// </summary>
    /// <param name="uiContext">The current presentation context.</param>
    /// <param name="theme">The player Finder theme.</param>
    /// <param name="mode">The active Finder category.</param>
    /// <param name="panel">Whether the alternate results panel is active.</param>
    /// <returns>The resolved command-button presentation.</returns>
    private static IReadOnlyList<FinderWindowDialogButtonRenderData> CreateDialogButtons(
        UIContext uiContext,
        FinderWindowTheme theme,
        FinderMode mode,
        bool panel
    )
    {
        IReadOnlyList<FinderWindowCommand> commands = GetCommands(mode);
        List<FinderWindowDialogButtonRenderData> buttons =
            new List<FinderWindowDialogButtonRenderData>(commands.Count);
        for (int i = 0; i < commands.Count; i++)
        {
            FinderWindowCommand command = commands[i];
            WindowButtonImageTheme buttonTheme = GetButtonTheme(theme, command);
            bool active = IsCommandActive(command, panel);
            buttons.Add(
                new FinderWindowDialogButtonRenderData(
                    command,
                    GetTexture(uiContext, buttonTheme?.GetImagePath(active)),
                    GetTexture(uiContext, buttonTheme?.GetImagePath(true)),
                    GetSourceRect(buttonTheme?.SourceLayout)
                )
            );
        }

        return FinderWindowRenderData.Copy(buttons);
    }

    /// <summary>
    /// Creates tab presentation in semantic tab order.
    /// </summary>
    /// <param name="uiContext">The current presentation context.</param>
    /// <param name="playerTheme">The player Finder theme.</param>
    /// <param name="tabs">The semantic Finder tabs.</param>
    /// <param name="activeTab">The selected tab index.</param>
    /// <returns>The resolved tab presentation.</returns>
    private static IReadOnlyList<FinderWindowTabRenderData> CreateTabs(
        UIContext uiContext,
        FinderWindowTheme playerTheme,
        IReadOnlyList<FinderWindowTab> tabs,
        int activeTab
    )
    {
        IReadOnlyList<FinderWindowTab> source = tabs ?? Array.Empty<FinderWindowTab>();
        List<FinderWindowTabRenderData> result = new List<FinderWindowTabRenderData>(source.Count);
        for (int i = 0; i < source.Count; i++)
        {
            FinderWindowTab tab = source[i];
            result.Add(
                new FinderWindowTabRenderData(
                    GetTabTexture(uiContext, playerTheme, tab, i == activeTab),
                    GetTabTexture(uiContext, playerTheme, tab, true)
                )
            );
        }

        return FinderWindowRenderData.Copy(result);
    }

    /// <summary>
    /// Converts positive count values to Finder row text.
    /// </summary>
    /// <param name="counts">The projected aggregate counts.</param>
    /// <returns>The non-zero count strings.</returns>
    private static IReadOnlyList<string> CreateCountText(IReadOnlyList<int> counts)
    {
        if (counts == null || counts.Count == 0)
            return Array.Empty<string>();

        List<string> result = new List<string>(counts.Count);
        for (int i = 0; i < counts.Count; i++)
        {
            if (counts[i] > 0)
                result.Add(counts[i].ToString());
        }

        return FinderWindowRenderData.Copy(result);
    }

    /// <summary>
    /// Returns commands for one Finder category.
    /// </summary>
    /// <param name="mode">The active Finder category.</param>
    /// <returns>The commands in authored slot order.</returns>
    private static IReadOnlyList<FinderWindowCommand> GetCommands(FinderMode mode)
    {
        return mode switch
        {
            FinderMode.Fleets => _fleetCommands,
            FinderMode.Personnel => _personnelCommands,
            _ => _defaultCommands,
        };
    }

    /// <summary>
    /// Returns the configured theme for one semantic Finder command.
    /// </summary>
    /// <param name="theme">The active Finder theme.</param>
    /// <param name="command">The semantic command.</param>
    /// <returns>The configured button theme, or null.</returns>
    private static WindowButtonImageTheme GetButtonTheme(
        FinderWindowTheme theme,
        FinderWindowCommand command
    )
    {
        return command switch
        {
            FinderWindowCommand.Close => theme?.CloseButton,
            FinderWindowCommand.Target => theme?.TargetButton,
            FinderWindowCommand.ShowShips => theme?.ShipButton,
            FinderWindowCommand.ShowFleets => theme?.FleetButton,
            FinderWindowCommand.ShowPersonnel => theme?.PersonnelButton,
            FinderWindowCommand.ShowSpecialForces => theme?.SpecialForcesButton,
            _ => null,
        };
    }

    /// <summary>
    /// Reports whether a panel-selection command is active.
    /// </summary>
    /// <param name="command">The semantic Finder command.</param>
    /// <param name="panel">Whether the alternate results panel is active.</param>
    /// <returns>True when the command represents the active panel.</returns>
    private static bool IsCommandActive(FinderWindowCommand command, bool panel)
    {
        return command switch
        {
            FinderWindowCommand.ShowShips => panel,
            FinderWindowCommand.ShowFleets => !panel,
            FinderWindowCommand.ShowPersonnel => !panel,
            FinderWindowCommand.ShowSpecialForces => panel,
            _ => false,
        };
    }

    /// <summary>
    /// Resolves the background texture for one Finder mode and panel.
    /// </summary>
    /// <param name="uiContext">The current presentation context.</param>
    /// <param name="theme">The player Finder theme.</param>
    /// <param name="mode">The active Finder category.</param>
    /// <param name="panel">Whether the alternate results panel is active.</param>
    /// <returns>The resolved background texture, or null.</returns>
    private static Texture GetBackgroundTexture(
        UIContext uiContext,
        FinderWindowTheme theme,
        FinderMode mode,
        bool panel
    )
    {
        string path = mode switch
        {
            FinderMode.Systems => theme?.SystemFinderBackgroundImagePath,
            FinderMode.Fleets => panel
                ? theme?.ShipFinderBackgroundImagePath
                : theme?.FleetFinderBackgroundImagePath,
            FinderMode.Troops => theme?.TroopFinderBackgroundImagePath,
            FinderMode.Personnel => panel
                ? theme?.SpecialForcesFinderBackgroundImagePath
                : theme?.PersonnelFinderBackgroundImagePath,
            _ => null,
        };
        return GetTexture(uiContext, path);
    }

    /// <summary>
    /// Resolves the command-strip texture for the active layout.
    /// </summary>
    /// <param name="uiContext">The current presentation context.</param>
    /// <param name="theme">The player Finder theme.</param>
    /// <param name="mode">The active Finder category.</param>
    /// <param name="useUpperButtonLayout">Whether commands use upper slots.</param>
    /// <returns>The resolved strip texture, or null.</returns>
    private static Texture GetButtonStripTexture(
        UIContext uiContext,
        FinderWindowTheme theme,
        FinderMode mode,
        bool useUpperButtonLayout
    )
    {
        if (useUpperButtonLayout)
            return null;

        string path = mode is FinderMode.Fleets or FinderMode.Personnel
            ? theme?.FourButtonStripImagePath
            : theme?.TwoButtonStripImagePath;
        return GetTexture(uiContext, path);
    }

    /// <summary>
    /// Resolves one Finder tab texture for a requested state.
    /// </summary>
    /// <param name="uiContext">The current presentation context.</param>
    /// <param name="playerTheme">The player Finder theme.</param>
    /// <param name="tab">The represented Finder tab.</param>
    /// <param name="active">Whether the active or pressed texture is requested.</param>
    /// <returns>The resolved tab texture, or null.</returns>
    private static Texture GetTabTexture(
        UIContext uiContext,
        FinderWindowTheme playerTheme,
        FinderWindowTab tab,
        bool active
    )
    {
        if (tab?.IsNeutral == true)
            return GetTexture(uiContext, playerTheme?.NeutralSystemsButton?.GetImagePath(active));

        if (tab?.IsUnexplored == true)
            return GetTexture(
                uiContext,
                playerTheme?.UnexploredSystemsButton?.GetImagePath(active)
            );

        if (!string.IsNullOrEmpty(tab?.FactionInstanceId))
        {
            FinderWindowTheme factionTheme = GetFinderTheme(uiContext, tab.FactionInstanceId);
            return GetTexture(
                uiContext,
                (factionTheme?.SystemsButton ?? playerTheme?.SystemsButton)?.GetImagePath(active)
            );
        }

        return GetTexture(uiContext, playerTheme?.AllSystemsButton?.GetImagePath(active));
    }

    /// <summary>
    /// Returns the displayed title for one Finder tab.
    /// </summary>
    /// <param name="uiContext">The current presentation context.</param>
    /// <param name="mode">The active Finder category.</param>
    /// <param name="panel">Whether the alternate results panel is active.</param>
    /// <param name="tab">The active Finder tab.</param>
    /// <returns>The displayed tab title.</returns>
    private static string GetTabText(
        UIContext uiContext,
        FinderMode mode,
        bool panel,
        FinderWindowTab tab
    )
    {
        if (tab == null)
            return string.Empty;
        if (tab.IsAll)
            return mode == FinderMode.Fleets && panel ? "All Ships" : GetAllTabText(mode);
        if (tab.IsNeutral)
            return "Neutral Systems";
        if (tab.IsUnexplored)
            return "Unexplored Systems";

        FinderWindowTheme theme = GetFinderTheme(uiContext, tab.FactionInstanceId);
        string factionName = tab.FactionDisplayName;
        return mode switch
        {
            FinderMode.Systems => GetThemeText(theme?.SystemsText, factionName + " Systems"),
            FinderMode.Fleets => panel
                ? GetThemeText(theme?.ShipsText, factionName + " Ships")
                : GetThemeText(theme?.FleetsText, factionName + " Fleets"),
            FinderMode.Troops => GetThemeText(theme?.TroopsText, factionName + " Troops"),
            FinderMode.Personnel => GetThemeText(theme?.PersonnelText, factionName + " Personnel"),
            _ => string.Empty,
        };
    }

    /// <summary>
    /// Returns the all-results title for a Finder category.
    /// </summary>
    /// <param name="mode">The active Finder category.</param>
    /// <returns>The displayed all-results title.</returns>
    private static string GetAllTabText(FinderMode mode)
    {
        return mode switch
        {
            FinderMode.Systems => "All Systems",
            FinderMode.Fleets => "All Fleets",
            _ => string.Empty,
        };
    }

    /// <summary>
    /// Uses configured theme text when available.
    /// </summary>
    /// <param name="text">The configured text.</param>
    /// <param name="fallback">The fallback text.</param>
    /// <returns>The configured text or fallback.</returns>
    private static string GetThemeText(string text, string fallback)
    {
        return string.IsNullOrEmpty(text) ? fallback : text;
    }

    /// <summary>
    /// Gets a Finder theme for the player or one explicit faction.
    /// </summary>
    /// <param name="uiContext">The current presentation context.</param>
    /// <param name="factionInstanceId">The optional faction identifier.</param>
    /// <returns>The resolved Finder theme, or null.</returns>
    private static FinderWindowTheme GetFinderTheme(UIContext uiContext, string factionInstanceId)
    {
        FactionTheme theme = string.IsNullOrEmpty(factionInstanceId)
            ? uiContext?.GetPlayerFactionTheme()
            : uiContext?.GetTheme(factionInstanceId);
        return theme?.StrategyWindows?.Finder;
    }

    /// <summary>
    /// Resolves a texture through the shared presentation cache.
    /// </summary>
    /// <param name="uiContext">The current presentation context.</param>
    /// <param name="path">The optional resource path.</param>
    /// <returns>The resolved texture, or null.</returns>
    private static Texture2D GetTexture(UIContext uiContext, string path)
    {
        return string.IsNullOrEmpty(path) ? null : uiContext?.GetTexture(path);
    }

    /// <summary>
    /// Converts optional theme bounds to an immutable source rectangle.
    /// </summary>
    /// <param name="layout">The optional configured source layout.</param>
    /// <returns>The configured rectangle, or null.</returns>
    private static RectInt? GetSourceRect(SourceRectLayout layout)
    {
        return layout == null ? null : new RectInt(layout.X, layout.Y, layout.Width, layout.Height);
    }

    /// <summary>
    /// Returns an item at a valid index.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The optional item collection.</param>
    /// <param name="index">The requested index.</param>
    /// <returns>The item, or the default value when unavailable.</returns>
    private static T GetItem<T>(IReadOnlyList<T> items, int index)
    {
        return items != null && index >= 0 && index < items.Count ? items[index] : default;
    }
}
