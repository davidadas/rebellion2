using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Rebellion.Game.Encyclopedia;
using UnityEngine;

/// <summary>
/// Identifies the semantic command represented by an Encyclopedia frame button.
/// </summary>
public enum EncyclopediaWindowCommand
{
    None,
    Close,
    ShowTopic,
    ShowIndex,
}

/// <summary>
/// Identifies one database tab in the authoritative Encyclopedia catalog.
/// </summary>
public enum EncyclopediaWindowTab
{
    AllDatabases,
    Systems,
    Ships,
    Facilities,
    Missions,
    Troops,
    Personnel,
}

/// <summary>
/// Defines the authoritative order, category, and title of Encyclopedia database tabs.
/// </summary>
public static class EncyclopediaWindowTabCatalog
{
    private static readonly EncyclopediaWindowTab[] _tabs =
    {
        EncyclopediaWindowTab.AllDatabases,
        EncyclopediaWindowTab.Systems,
        EncyclopediaWindowTab.Ships,
        EncyclopediaWindowTab.Facilities,
        EncyclopediaWindowTab.Missions,
        EncyclopediaWindowTab.Troops,
        EncyclopediaWindowTab.Personnel,
    };

    public static int Count => _tabs.Length;

    /// <summary>
    /// Gets the semantic database tab at one authored slot.
    /// </summary>
    /// <param name="index">The authored tab slot.</param>
    /// <returns>The semantic tab at the requested slot.</returns>
    public static EncyclopediaWindowTab GetTab(int index)
    {
        if (index < 0 || index >= _tabs.Length)
            throw new ArgumentOutOfRangeException(nameof(index));

        return _tabs[index];
    }

    /// <summary>
    /// Gets the authored slot occupied by one semantic database tab.
    /// </summary>
    /// <param name="tab">The semantic database tab.</param>
    /// <returns>The zero-based authored tab slot.</returns>
    public static int GetIndex(EncyclopediaWindowTab tab)
    {
        return Array.IndexOf(_tabs, tab);
    }

    /// <summary>
    /// Maps a semantic database tab to its catalog category.
    /// </summary>
    /// <param name="tab">The semantic database tab.</param>
    /// <returns>The mapped category, or null for the complete catalog.</returns>
    public static EncyclopediaEntryCategory? GetCategory(EncyclopediaWindowTab tab)
    {
        return tab switch
        {
            EncyclopediaWindowTab.Systems => EncyclopediaEntryCategory.System,
            EncyclopediaWindowTab.Ships => EncyclopediaEntryCategory.Ship,
            EncyclopediaWindowTab.Facilities => EncyclopediaEntryCategory.Facility,
            EncyclopediaWindowTab.Missions => EncyclopediaEntryCategory.Mission,
            EncyclopediaWindowTab.Troops => EncyclopediaEntryCategory.Troop,
            EncyclopediaWindowTab.Personnel => EncyclopediaEntryCategory.Personnel,
            _ => null,
        };
    }

    /// <summary>
    /// Gets the displayed database title for one semantic tab.
    /// </summary>
    /// <param name="tab">The semantic database tab.</param>
    /// <returns>The title displayed above the index.</returns>
    public static string GetTitle(EncyclopediaWindowTab tab)
    {
        return tab switch
        {
            EncyclopediaWindowTab.AllDatabases => "All Databases",
            EncyclopediaWindowTab.Systems => "System Database",
            EncyclopediaWindowTab.Ships => "Ship Database",
            EncyclopediaWindowTab.Facilities => "Facilities Database",
            EncyclopediaWindowTab.Missions => "Missions Database",
            EncyclopediaWindowTab.Troops => "Troop Database",
            EncyclopediaWindowTab.Personnel => "Personnel Database",
            _ => string.Empty,
        };
    }
}

/// <summary>
/// Captures a read-only snapshot of one controller-owned Encyclopedia session.
/// </summary>
public readonly struct EncyclopediaWindowState
{
    /// <summary>
    /// Creates a snapshot of the Encyclopedia controller session state.
    /// </summary>
    /// <param name="panel">Whether the topic detail panel is open.</param>
    /// <param name="activeTab">The selected semantic database tab.</param>
    /// <param name="selectedIndex">The selected entry index.</param>
    /// <param name="searchText">The current entry-name filter.</param>
    public EncyclopediaWindowState(
        bool panel,
        EncyclopediaWindowTab activeTab,
        int selectedIndex,
        string searchText
    )
    {
        Panel = panel;
        ActiveTab = activeTab;
        SelectedIndex = selectedIndex;
        SearchText = searchText ?? string.Empty;
    }

    public bool Panel { get; }

    public EncyclopediaWindowTab ActiveTab { get; }

    public int SelectedIndex { get; }

    public string SearchText { get; }
}

/// <summary>
/// Defines the resolved artwork and optional authored bounds for one Encyclopedia command button.
/// </summary>
public sealed class EncyclopediaDialogButtonRenderData
{
    /// <summary>
    /// Creates immutable render data for an Encyclopedia command button.
    /// </summary>
    /// <param name="command">The semantic command represented by the button.</param>
    /// <param name="texture">The button's current-state texture.</param>
    /// <param name="pressedTexture">The texture shown while the button is pressed.</param>
    /// <param name="sourceRect">The optional authored source-space bounds.</param>
    public EncyclopediaDialogButtonRenderData(
        EncyclopediaWindowCommand command,
        Texture texture,
        Texture pressedTexture,
        RectInt? sourceRect
    )
    {
        Command = command;
        Texture = texture;
        PressedTexture = pressedTexture;
        SourceRect = sourceRect;
    }

    public EncyclopediaWindowCommand Command { get; }

    public Texture Texture { get; }

    public Texture PressedTexture { get; }

    public RectInt? SourceRect { get; }
}

/// <summary>
/// Defines the resolved artwork for one Encyclopedia database tab.
/// </summary>
public sealed class EncyclopediaTabRenderData
{
    /// <summary>
    /// Creates immutable render data for an Encyclopedia database tab.
    /// </summary>
    /// <param name="tab">The semantic database tab.</param>
    /// <param name="texture">The texture for the tab's current state.</param>
    public EncyclopediaTabRenderData(EncyclopediaWindowTab tab, Texture texture)
    {
        Tab = tab;
        Texture = texture;
    }

    public EncyclopediaWindowTab Tab { get; }

    public Texture Texture { get; }
}

/// <summary>
/// Defines one rendered Encyclopedia index row.
/// </summary>
public sealed class EncyclopediaWindowRowRenderData
{
    /// <summary>
    /// Creates immutable render data for one Encyclopedia index row.
    /// </summary>
    /// <param name="entryTypeId">The stable catalog entry identifier.</param>
    /// <param name="name">The displayed entry name.</param>
    /// <param name="selected">Whether the row is selected.</param>
    public EncyclopediaWindowRowRenderData(string entryTypeId, string name, bool selected)
    {
        EntryTypeId = entryTypeId ?? string.Empty;
        Name = name ?? string.Empty;
        Selected = selected;
    }

    public string EntryTypeId { get; }

    public string Name { get; }

    public bool Selected { get; }
}

/// <summary>
/// Contains immutable frame, placement, focus, and command-control presentation data.
/// </summary>
public sealed class EncyclopediaWindowFrameRenderData
{
    /// <summary>
    /// Creates an Encyclopedia frame presentation snapshot.
    /// </summary>
    /// <param name="x">The source-space horizontal window position.</param>
    /// <param name="y">The source-space vertical window position.</param>
    /// <param name="width">The source-space window width.</param>
    /// <param name="height">The source-space window height.</param>
    /// <param name="activeWindow">Whether this window owns keyboard navigation.</param>
    /// <param name="useUpperButtonLayout">Whether command buttons use the upper layout.</param>
    /// <param name="overlayFrameTexture">The faction-themed overlay frame.</param>
    /// <param name="buttonStripTexture">The faction-themed command-button strip.</param>
    /// <param name="dialogButtons">The resolved command buttons in authored slot order.</param>
    public EncyclopediaWindowFrameRenderData(
        int x,
        int y,
        int width,
        int height,
        bool activeWindow,
        bool useUpperButtonLayout,
        Texture overlayFrameTexture,
        Texture buttonStripTexture,
        IReadOnlyList<EncyclopediaDialogButtonRenderData> dialogButtons
    )
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
        ActiveWindow = activeWindow;
        UseUpperButtonLayout = useUpperButtonLayout;
        OverlayFrameTexture = overlayFrameTexture;
        ButtonStripTexture = buttonStripTexture;
        DialogButtons = EncyclopediaWindowRenderData.Copy(dialogButtons);
    }

    public int X { get; }

    public int Y { get; }

    public int Width { get; }

    public int Height { get; }

    public bool ActiveWindow { get; }

    public bool UseUpperButtonLayout { get; }

    public Texture OverlayFrameTexture { get; }

    public Texture ButtonStripTexture { get; }

    public IReadOnlyList<EncyclopediaDialogButtonRenderData> DialogButtons { get; }
}

/// <summary>
/// Contains immutable database-index presentation data.
/// </summary>
public sealed class EncyclopediaWindowIndexRenderData
{
    /// <summary>
    /// Creates an Encyclopedia index-panel presentation snapshot.
    /// </summary>
    /// <param name="activeTab">The selected semantic database tab.</param>
    /// <param name="selectedIndex">The selected entry index.</param>
    /// <param name="searchText">The current entry-name filter.</param>
    /// <param name="tabTitle">The title for the selected database tab.</param>
    /// <param name="tabs">The resolved database tabs in authored slot order.</param>
    /// <param name="rows">The projected index rows.</param>
    public EncyclopediaWindowIndexRenderData(
        EncyclopediaWindowTab activeTab,
        int selectedIndex,
        string searchText,
        string tabTitle,
        IReadOnlyList<EncyclopediaTabRenderData> tabs,
        IReadOnlyList<EncyclopediaWindowRowRenderData> rows
    )
    {
        ActiveTab = activeTab;
        SelectedIndex = selectedIndex;
        SearchText = searchText ?? string.Empty;
        TabTitle = tabTitle ?? string.Empty;
        Tabs = EncyclopediaWindowRenderData.Copy(tabs);
        Rows = EncyclopediaWindowRenderData.Copy(rows);
    }

    public EncyclopediaWindowTab ActiveTab { get; }

    public int SelectedIndex { get; }

    public string SearchText { get; }

    public string TabTitle { get; }

    public IReadOnlyList<EncyclopediaTabRenderData> Tabs { get; }

    public IReadOnlyList<EncyclopediaWindowRowRenderData> Rows { get; }
}

/// <summary>
/// Contains immutable topic-detail presentation data.
/// </summary>
public sealed class EncyclopediaWindowDetailRenderData
{
    /// <summary>
    /// Creates an Encyclopedia topic-detail presentation snapshot.
    /// </summary>
    /// <param name="title">The selected entry's display name.</param>
    /// <param name="text">The selected entry's unwrapped detail text.</param>
    /// <param name="image">The selected entry's resolved image.</param>
    /// <param name="previousDisabled">Whether previous-entry navigation is disabled.</param>
    /// <param name="nextDisabled">Whether next-entry navigation is disabled.</param>
    public EncyclopediaWindowDetailRenderData(
        string title,
        string text,
        Texture image,
        bool previousDisabled,
        bool nextDisabled
    )
    {
        Title = title ?? string.Empty;
        Text = text ?? string.Empty;
        Image = image;
        PreviousDisabled = previousDisabled;
        NextDisabled = nextDisabled;
    }

    public string Title { get; }

    public string Text { get; }

    public Texture Image { get; }

    public bool PreviousDisabled { get; }

    public bool NextDisabled { get; }
}

/// <summary>
/// Contains a complete immutable presentation snapshot for the Encyclopedia window.
/// </summary>
public sealed class EncyclopediaWindowRenderData
{
    /// <summary>
    /// Creates a complete Encyclopedia window presentation snapshot.
    /// </summary>
    /// <param name="panel">Whether the topic detail panel is open.</param>
    /// <param name="frame">The window frame and command-control presentation.</param>
    /// <param name="index">The database-index presentation.</param>
    /// <param name="detail">The topic-detail presentation.</param>
    public EncyclopediaWindowRenderData(
        bool panel,
        EncyclopediaWindowFrameRenderData frame,
        EncyclopediaWindowIndexRenderData index,
        EncyclopediaWindowDetailRenderData detail
    )
    {
        Panel = panel;
        Frame = frame ?? throw new ArgumentNullException(nameof(frame));
        Index = index ?? throw new ArgumentNullException(nameof(index));
        Detail = detail ?? throw new ArgumentNullException(nameof(detail));
    }

    public bool Panel { get; }

    public EncyclopediaWindowFrameRenderData Frame { get; }

    public EncyclopediaWindowIndexRenderData Index { get; }

    public EncyclopediaWindowDetailRenderData Detail { get; }

    /// <summary>
    /// Copies an optional source list into a read-only collection.
    /// </summary>
    /// <typeparam name="T">The element type to copy.</typeparam>
    /// <param name="source">The source collection.</param>
    /// <returns>An isolated read-only copy of the source collection.</returns>
    internal static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> source)
    {
        if (source == null || source.Count == 0)
            return Array.Empty<T>();

        T[] copy = new T[source.Count];
        for (int i = 0; i < source.Count; i++)
            copy[i] = source[i];

        return new ReadOnlyCollection<T>(copy);
    }
}
