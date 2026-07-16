using System;
using System.Collections.Generic;
using Rebellion.Game.Encyclopedia;
using UnityEngine;

/// <summary>
/// Produces immutable Encyclopedia snapshots without mutating controller or view state.
/// </summary>
internal static class EncyclopediaWindowProjector
{
    /// <summary>
    /// Creates a complete Encyclopedia presentation from one controller-owned session.
    /// </summary>
    /// <param name="uiContext">The current UI catalog, theme, and texture context.</param>
    /// <param name="window">The window shell being rendered.</param>
    /// <param name="useUpperButtonLayout">Whether commands use the upper button layout.</param>
    /// <param name="session">The controller-owned Encyclopedia session.</param>
    /// <returns>The immutable Encyclopedia presentation snapshot.</returns>
    public static EncyclopediaWindowRenderData CreateRenderData(
        UIContext uiContext,
        UIWindow window,
        bool useUpperButtonLayout,
        EncyclopediaWindowSession session
    )
    {
        if (uiContext == null)
            throw new ArgumentNullException(nameof(uiContext));
        if (window == null)
            throw new ArgumentNullException(nameof(window));
        if (session == null)
            throw new ArgumentNullException(nameof(session));

        EncyclopediaEntry selectedEntry = GetSelectedEntry(
            session.ProjectedEntries,
            session.SelectedIndex
        );
        EncyclopediaWindowTheme theme = uiContext
            .GetPlayerFactionTheme()
            ?.StrategyWindows?.Encyclopedia;
        return new EncyclopediaWindowRenderData(
            session.Panel,
            new EncyclopediaWindowFrameRenderData(
                window.X,
                window.Y,
                window.Width,
                window.Height,
                window.ActiveWindow,
                useUpperButtonLayout,
                uiContext.GetTexture(theme?.OverlayFrameImagePath),
                useUpperButtonLayout ? null : uiContext.GetTexture(theme?.ButtonStripImagePath),
                CreateDialogButtons(uiContext, theme, session.Panel)
            ),
            new EncyclopediaWindowIndexRenderData(
                session.ActiveTab,
                session.SelectedIndex,
                session.SearchText,
                EncyclopediaWindowTabCatalog.GetTitle(session.ActiveTab),
                CreateTabs(uiContext, theme, session.ActiveTab),
                CreateRows(session.ProjectedEntries, session.SelectedIndex)
            ),
            new EncyclopediaWindowDetailRenderData(
                selectedEntry?.DisplayName,
                selectedEntry?.GetInfoText(),
                uiContext.GetTexture(selectedEntry?.ImagePath),
                session.SelectedIndex <= 0,
                session.SelectedIndex < 0
                    || session.SelectedIndex >= session.ProjectedEntries.Count - 1
            )
        );
    }

    /// <summary>
    /// Returns catalog entries visible under one controller-owned session state.
    /// </summary>
    /// <param name="catalog">The Encyclopedia catalog to query.</param>
    /// <param name="factionInstanceId">The faction viewing the catalog.</param>
    /// <param name="state">The current controller session state.</param>
    /// <returns>The visible entries in display order.</returns>
    public static List<EncyclopediaEntry> GetVisibleEntries(
        EncyclopediaCatalog catalog,
        string factionInstanceId,
        EncyclopediaWindowState state
    )
    {
        if (catalog == null)
            return new List<EncyclopediaEntry>();

        return FilterEntries(
            catalog.GetRows(
                EncyclopediaWindowTabCatalog.GetCategory(state.ActiveTab),
                factionInstanceId
            ),
            state.SearchText
        );
    }

    /// <summary>
    /// Filters Encyclopedia entries by display name without changing source order.
    /// </summary>
    /// <param name="source">The entries available under the active tab.</param>
    /// <param name="searchText">The case-insensitive display-name filter.</param>
    /// <returns>The entries whose display names contain the filter.</returns>
    public static List<EncyclopediaEntry> FilterEntries(
        IReadOnlyList<EncyclopediaEntry> source,
        string searchText
    )
    {
        IReadOnlyList<EncyclopediaEntry> entries = source ?? Array.Empty<EncyclopediaEntry>();
        if (string.IsNullOrWhiteSpace(searchText))
            return new List<EncyclopediaEntry>(entries);

        List<EncyclopediaEntry> filtered = new List<EncyclopediaEntry>();
        for (int i = 0; i < entries.Count; i++)
        {
            EncyclopediaEntry entry = entries[i];
            if (entry?.DisplayName?.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                filtered.Add(entry);
        }

        return filtered;
    }

    /// <summary>
    /// Creates immutable index-row presentation from projected entries.
    /// </summary>
    /// <param name="entries">The projected Encyclopedia entries.</param>
    /// <param name="selectedIndex">The selected projected entry index.</param>
    /// <returns>The rendered index rows.</returns>
    public static IReadOnlyList<EncyclopediaWindowRowRenderData> CreateRows(
        IReadOnlyList<EncyclopediaEntry> entries,
        int selectedIndex
    )
    {
        IReadOnlyList<EncyclopediaEntry> source = entries ?? Array.Empty<EncyclopediaEntry>();
        List<EncyclopediaWindowRowRenderData> rows = new List<EncyclopediaWindowRowRenderData>(
            source.Count
        );
        for (int i = 0; i < source.Count; i++)
        {
            rows.Add(
                new EncyclopediaWindowRowRenderData(
                    source[i]?.TypeID,
                    source[i]?.DisplayName,
                    i == selectedIndex
                )
            );
        }

        return EncyclopediaWindowRenderData.Copy(rows);
    }

    /// <summary>
    /// Returns the selected projected entry when its index is valid.
    /// </summary>
    /// <param name="entries">The projected entries.</param>
    /// <param name="selectedIndex">The selected entry index.</param>
    /// <returns>The selected entry, or null.</returns>
    private static EncyclopediaEntry GetSelectedEntry(
        IReadOnlyList<EncyclopediaEntry> entries,
        int selectedIndex
    )
    {
        return entries != null && selectedIndex >= 0 && selectedIndex < entries.Count
            ? entries[selectedIndex]
            : null;
    }

    /// <summary>
    /// Resolves command-button artwork in authored slot order.
    /// </summary>
    /// <param name="uiContext">The current UI texture context.</param>
    /// <param name="theme">The current faction Encyclopedia theme.</param>
    /// <param name="panel">Whether the topic detail panel is open.</param>
    /// <returns>The semantic command-button snapshots.</returns>
    private static IReadOnlyList<EncyclopediaDialogButtonRenderData> CreateDialogButtons(
        UIContext uiContext,
        EncyclopediaWindowTheme theme,
        bool panel
    )
    {
        return new[]
        {
            CreateDialogButton(
                uiContext,
                theme?.CloseButton,
                EncyclopediaWindowCommand.Close,
                false
            ),
            CreateDialogButton(
                uiContext,
                theme?.TopicButton,
                EncyclopediaWindowCommand.ShowTopic,
                panel
            ),
            CreateDialogButton(
                uiContext,
                theme?.IndexButton,
                EncyclopediaWindowCommand.ShowIndex,
                !panel
            ),
        };
    }

    /// <summary>
    /// Resolves one semantic command button.
    /// </summary>
    /// <param name="uiContext">The current UI texture context.</param>
    /// <param name="theme">The configured button theme.</param>
    /// <param name="command">The semantic command represented by the button.</param>
    /// <param name="active">Whether the command is the active panel mode.</param>
    /// <returns>The resolved command-button snapshot.</returns>
    private static EncyclopediaDialogButtonRenderData CreateDialogButton(
        UIContext uiContext,
        WindowButtonImageTheme theme,
        EncyclopediaWindowCommand command,
        bool active
    )
    {
        Texture texture = uiContext.GetTexture(theme?.GetImagePath(active));
        Texture pressedTexture = uiContext.GetTexture(theme?.GetImagePath(true)) ?? texture;
        return new EncyclopediaDialogButtonRenderData(
            command,
            texture,
            pressedTexture,
            CopySourceRect(theme?.SourceLayout)
        );
    }

    /// <summary>
    /// Resolves database-tab artwork in the authoritative semantic order.
    /// </summary>
    /// <param name="uiContext">The current UI texture context.</param>
    /// <param name="theme">The current faction Encyclopedia theme.</param>
    /// <param name="activeTab">The selected semantic database tab.</param>
    /// <returns>The semantic database-tab snapshots.</returns>
    private static IReadOnlyList<EncyclopediaTabRenderData> CreateTabs(
        UIContext uiContext,
        EncyclopediaWindowTheme theme,
        EncyclopediaWindowTab activeTab
    )
    {
        List<EncyclopediaTabRenderData> tabs = new List<EncyclopediaTabRenderData>(
            EncyclopediaWindowTabCatalog.Count
        );
        for (int index = 0; index < EncyclopediaWindowTabCatalog.Count; index++)
        {
            EncyclopediaWindowTab tab = EncyclopediaWindowTabCatalog.GetTab(index);
            tabs.Add(
                new EncyclopediaTabRenderData(
                    tab,
                    GetTabTexture(uiContext, theme, tab, tab == activeTab)
                )
            );
        }

        return EncyclopediaWindowRenderData.Copy(tabs);
    }

    /// <summary>
    /// Resolves one semantic database tab texture.
    /// </summary>
    /// <param name="uiContext">The current UI texture context.</param>
    /// <param name="theme">The current faction Encyclopedia theme.</param>
    /// <param name="tab">The semantic database tab.</param>
    /// <param name="active">Whether the tab is selected.</param>
    /// <returns>The resolved tab texture.</returns>
    private static Texture GetTabTexture(
        UIContext uiContext,
        EncyclopediaWindowTheme theme,
        EncyclopediaWindowTab tab,
        bool active
    )
    {
        string path = tab switch
        {
            EncyclopediaWindowTab.AllDatabases => theme?.AllDatabasesButton?.GetImagePath(active),
            EncyclopediaWindowTab.Systems => theme?.SystemsButton?.GetImagePath(active),
            EncyclopediaWindowTab.Ships => theme?.ShipButton?.GetImagePath(active),
            EncyclopediaWindowTab.Facilities => theme?.FacilityButton?.GetImagePath(active),
            EncyclopediaWindowTab.Missions => theme?.MissionsButton?.GetImagePath(active),
            EncyclopediaWindowTab.Troops => theme?.TroopButton?.GetImagePath(active),
            EncyclopediaWindowTab.Personnel => theme?.PersonnelButton?.GetImagePath(active),
            _ => null,
        };
        return uiContext.GetTexture(path);
    }

    /// <summary>
    /// Copies optional configured source-space button bounds.
    /// </summary>
    /// <param name="layout">The configured button layout.</param>
    /// <returns>The copied source rectangle, or null.</returns>
    private static RectInt? CopySourceRect(SourceRectLayout layout)
    {
        return layout == null ? null : new RectInt(layout.X, layout.Y, layout.Width, layout.Height);
    }
}
