using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

/// <summary>
/// Identifies one semantic Finder command presented by an authored dialog-button slot.
/// </summary>
public enum FinderWindowCommand
{
    None,
    Close,
    Target,
    ShowShips,
    ShowFleets,
    ShowPersonnel,
    ShowSpecialForces,
}

/// <summary>
/// Captures a read-only snapshot of one controller-owned Finder session.
/// </summary>
public readonly struct FinderWindowState
{
    /// <summary>
    /// Creates an immutable Finder session-state snapshot.
    /// </summary>
    /// <param name="mode">The Finder category represented by the window.</param>
    /// <param name="panel">Whether the alternate results panel is active.</param>
    /// <param name="activeTab">The selected tab index.</param>
    /// <param name="selectedIndex">The selected visible-row index.</param>
    /// <param name="searchText">The current case-insensitive name filter.</param>
    public FinderWindowState(
        FinderMode mode,
        bool panel,
        int activeTab,
        int selectedIndex,
        string searchText
    )
    {
        Mode = mode;
        Panel = panel;
        ActiveTab = activeTab;
        SelectedIndex = selectedIndex;
        SearchText = searchText ?? string.Empty;
    }

    /// <summary>
    /// Gets the mode.
    /// </summary>
    public FinderMode Mode { get; }

    /// <summary>
    /// Gets a value indicating whether the panel is enabled.
    /// </summary>
    public bool Panel { get; }

    /// <summary>
    /// Gets the active tab.
    /// </summary>
    public int ActiveTab { get; }

    /// <summary>
    /// Gets the selected index.
    /// </summary>
    public int SelectedIndex { get; }

    /// <summary>
    /// Gets the search text.
    /// </summary>
    public string SearchText { get; }
}

/// <summary>
/// Defines resolved presentation for one Finder command button.
/// </summary>
public sealed class FinderWindowDialogButtonRenderData
{
    /// <summary>
    /// Creates immutable command-button presentation data.
    /// </summary>
    /// <param name="command">The semantic command represented by the button.</param>
    /// <param name="texture">The texture shown in the current state.</param>
    /// <param name="pressedTexture">The texture shown while pressed.</param>
    /// <param name="sourceRect">Optional faction-authored source-space bounds.</param>
    public FinderWindowDialogButtonRenderData(
        FinderWindowCommand command,
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

    /// <summary>
    /// Gets the command.
    /// </summary>
    public FinderWindowCommand Command { get; }

    /// <summary>
    /// Gets the texture.
    /// </summary>
    public Texture Texture { get; }

    /// <summary>
    /// Gets the pressed texture.
    /// </summary>
    public Texture PressedTexture { get; }

    /// <summary>
    /// Gets the source rect.
    /// </summary>
    public RectInt? SourceRect { get; }
}

/// <summary>
/// Defines resolved presentation for one Finder tab.
/// </summary>
public sealed class FinderWindowTabRenderData
{
    /// <summary>
    /// Creates immutable Finder-tab presentation data.
    /// </summary>
    /// <param name="texture">The texture shown in the current state.</param>
    /// <param name="pressedTexture">The texture shown while pressed.</param>
    public FinderWindowTabRenderData(Texture texture, Texture pressedTexture)
    {
        Texture = texture;
        PressedTexture = pressedTexture;
    }

    /// <summary>
    /// Gets the texture.
    /// </summary>
    public Texture Texture { get; }

    /// <summary>
    /// Gets the pressed texture.
    /// </summary>
    public Texture PressedTexture { get; }
}

/// <summary>
/// Defines one visible Finder result row.
/// </summary>
public sealed class FinderWindowRowRenderData
{
    /// <summary>
    /// Creates immutable Finder-row presentation data.
    /// </summary>
    /// <param name="rowId">The stable represented row identifier.</param>
    /// <param name="name">The displayed result name.</param>
    /// <param name="selected">Whether the result is selected.</param>
    /// <param name="counts">The displayed non-zero unit counts.</param>
    public FinderWindowRowRenderData(
        string rowId,
        string name,
        bool selected,
        IReadOnlyList<string> counts
    )
    {
        RowId = rowId ?? string.Empty;
        Name = name ?? string.Empty;
        Selected = selected;
        Counts = FinderWindowRenderData.Copy(counts);
    }

    /// <summary>
    /// Gets the stable represented row identifier.
    /// </summary>
    public string RowId { get; }

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets a value indicating whether the row is selected.
    /// </summary>
    public bool Selected { get; }

    /// <summary>
    /// Gets the counts.
    /// </summary>
    public IReadOnlyList<string> Counts { get; }
}

/// <summary>
/// Defines Finder frame placement, focus, artwork, and command controls.
/// </summary>
public sealed class FinderWindowFrameRenderData
{
    /// <summary>
    /// Creates immutable Finder-frame presentation data.
    /// </summary>
    /// <param name="x">The source-space horizontal window position.</param>
    /// <param name="y">The source-space vertical window position.</param>
    /// <param name="width">The source-space window width.</param>
    /// <param name="height">The source-space window height.</param>
    /// <param name="activeWindow">Whether this window owns keyboard navigation.</param>
    /// <param name="useUpperButtonLayout">Whether command controls use the upper slots.</param>
    /// <param name="backgroundTexture">The resolved results-background texture.</param>
    /// <param name="overlayFrameTexture">The resolved faction overlay texture.</param>
    /// <param name="buttonStripTexture">The resolved command-strip texture.</param>
    /// <param name="dialogButtons">The commands in authored slot order.</param>
    public FinderWindowFrameRenderData(
        int x,
        int y,
        int width,
        int height,
        bool activeWindow,
        bool useUpperButtonLayout,
        Texture backgroundTexture,
        Texture overlayFrameTexture,
        Texture buttonStripTexture,
        IReadOnlyList<FinderWindowDialogButtonRenderData> dialogButtons
    )
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
        ActiveWindow = activeWindow;
        UseUpperButtonLayout = useUpperButtonLayout;
        BackgroundTexture = backgroundTexture;
        OverlayFrameTexture = overlayFrameTexture;
        ButtonStripTexture = buttonStripTexture;
        DialogButtons = FinderWindowRenderData.Copy(dialogButtons);
    }

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
    /// Gets the height.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets a value indicating whether the window is active.
    /// </summary>
    public bool ActiveWindow { get; }

    /// <summary>
    /// Gets a value indicating whether upper button layout is used.
    /// </summary>
    public bool UseUpperButtonLayout { get; }

    /// <summary>
    /// Gets the background texture.
    /// </summary>
    public Texture BackgroundTexture { get; }

    /// <summary>
    /// Gets the overlay frame texture.
    /// </summary>
    public Texture OverlayFrameTexture { get; }

    /// <summary>
    /// Gets the button strip texture.
    /// </summary>
    public Texture ButtonStripTexture { get; }

    /// <summary>
    /// Gets the dialog buttons.
    /// </summary>
    public IReadOnlyList<FinderWindowDialogButtonRenderData> DialogButtons { get; }
}

/// <summary>
/// Contains a complete immutable Finder-window presentation snapshot.
/// </summary>
public sealed class FinderWindowRenderData
{
    /// <summary>
    /// Creates immutable Finder-window presentation data.
    /// </summary>
    /// <param name="mode">The Finder category represented by the window.</param>
    /// <param name="panel">Whether the alternate results panel is active.</param>
    /// <param name="activeTab">The selected tab index.</param>
    /// <param name="selectedIndex">The selected visible-row index.</param>
    /// <param name="searchText">The current result-name filter.</param>
    /// <param name="title">The window title.</param>
    /// <param name="label">The search-field label.</param>
    /// <param name="activeTabText">The active tab title.</param>
    /// <param name="frame">The resolved frame presentation.</param>
    /// <param name="tabs">The resolved tabs in authored slot order.</param>
    /// <param name="rows">The visible result rows.</param>
    public FinderWindowRenderData(
        FinderMode mode,
        bool panel,
        int activeTab,
        int selectedIndex,
        string searchText,
        string title,
        string label,
        string activeTabText,
        FinderWindowFrameRenderData frame,
        IReadOnlyList<FinderWindowTabRenderData> tabs,
        IReadOnlyList<FinderWindowRowRenderData> rows
    )
    {
        Mode = mode;
        Panel = panel;
        ActiveTab = activeTab;
        SelectedIndex = selectedIndex;
        SearchText = searchText ?? string.Empty;
        Title = title ?? string.Empty;
        Label = label ?? string.Empty;
        ActiveTabText = activeTabText ?? string.Empty;
        Frame = frame ?? throw new ArgumentNullException(nameof(frame));
        Tabs = Copy(tabs);
        Rows = Copy(rows);
    }

    /// <summary>
    /// Gets the mode.
    /// </summary>
    public FinderMode Mode { get; }

    /// <summary>
    /// Gets a value indicating whether the panel is enabled.
    /// </summary>
    public bool Panel { get; }

    /// <summary>
    /// Gets the active tab.
    /// </summary>
    public int ActiveTab { get; }

    /// <summary>
    /// Gets the selected index.
    /// </summary>
    public int SelectedIndex { get; }

    /// <summary>
    /// Gets the search text.
    /// </summary>
    public string SearchText { get; }

    /// <summary>
    /// Gets the title.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets the label.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// Gets the active tab text.
    /// </summary>
    public string ActiveTabText { get; }

    /// <summary>
    /// Gets the frame.
    /// </summary>
    public FinderWindowFrameRenderData Frame { get; }

    /// <summary>
    /// Gets the tabs.
    /// </summary>
    public IReadOnlyList<FinderWindowTabRenderData> Tabs { get; }

    /// <summary>
    /// Gets the rows.
    /// </summary>
    public IReadOnlyList<FinderWindowRowRenderData> Rows { get; }

    /// <summary>
    /// Copies an optional collection into an isolated read-only snapshot.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="source">The optional source collection.</param>
    /// <returns>An isolated immutable collection.</returns>
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
