using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Captures controller-owned galactic-information menu interaction state.
/// </summary>
public readonly struct GalacticInformationDisplayState
{
    public bool Visible { get; }

    public int ActiveCategoryIndex { get; }

    public int HoveredFilterIndex { get; }

    public bool DisplayOffHovered { get; }

    public GalacticInformationFilterMode SelectedFilterMode { get; }

    /// <summary>
    /// Creates an immutable galactic-information interaction-state snapshot.
    /// </summary>
    /// <param name="visible">Whether the selector is open.</param>
    /// <param name="activeCategoryIndex">The open category index, or negative when none is open.</param>
    /// <param name="hoveredFilterIndex">The hovered filter index, or negative when none is hovered.</param>
    /// <param name="displayOffHovered">Whether the display-off row is hovered.</param>
    /// <param name="selectedFilterMode">The filter currently applied to the map.</param>
    public GalacticInformationDisplayState(
        bool visible,
        int activeCategoryIndex,
        int hoveredFilterIndex,
        bool displayOffHovered,
        GalacticInformationFilterMode selectedFilterMode
    )
    {
        Visible = visible;
        ActiveCategoryIndex = activeCategoryIndex;
        HoveredFilterIndex = hoveredFilterIndex;
        DisplayOffHovered = displayOffHovered;
        SelectedFilterMode = selectedFilterMode;
    }
}

/// <summary>
/// Contains a complete immutable galactic-information selector presentation snapshot.
/// </summary>
public sealed class GalacticInformationDisplayRenderData
{
    public bool Visible { get; }

    public RectInt SelectorBounds { get; }

    public Color BackgroundColor { get; }

    public GalacticInformationFrameRenderData Frame { get; }

    public IReadOnlyList<GalacticInformationCategoryRenderData> Categories { get; }

    public GalacticInformationTextRowRenderData DisplayOffRow { get; }

    /// <summary>
    /// Creates immutable selector presentation data.
    /// </summary>
    /// <param name="visible">Whether the selector is visible.</param>
    /// <param name="selectorBounds">The selector's source-space bounds.</param>
    /// <param name="backgroundColor">The selector background color.</param>
    /// <param name="frame">The selector frame presentation.</param>
    /// <param name="categories">The category presentations in authored-slot order.</param>
    /// <param name="displayOffRow">The display-off row presentation.</param>
    public GalacticInformationDisplayRenderData(
        bool visible,
        RectInt selectorBounds,
        Color backgroundColor,
        GalacticInformationFrameRenderData frame,
        IReadOnlyList<GalacticInformationCategoryRenderData> categories,
        GalacticInformationTextRowRenderData displayOffRow
    )
    {
        Visible = visible;
        SelectorBounds = selectorBounds;
        BackgroundColor = backgroundColor;
        Frame = frame;
        Categories = GalaxyMapRenderData.Copy(categories);
        DisplayOffRow = displayOffRow;
    }
}

/// <summary>
/// Defines resolved presentation for one galactic-information category.
/// </summary>
public sealed class GalacticInformationCategoryRenderData
{
    public bool Visible { get; }

    public RectInt HitBounds { get; }

    public GalacticInformationImageRenderData Icon { get; }

    public GalacticInformationImageRenderData Arrow { get; }

    public GalacticInformationTextRenderData Label { get; }

    public GalacticInformationSubmenuRenderData Submenu { get; }

    /// <summary>
    /// Creates immutable category presentation data.
    /// </summary>
    /// <param name="visible">Whether the category slot is visible.</param>
    /// <param name="hitBounds">The category hit bounds.</param>
    /// <param name="icon">The category icon presentation.</param>
    /// <param name="arrow">The submenu-arrow presentation.</param>
    /// <param name="label">The category label presentation.</param>
    /// <param name="submenu">The category submenu presentation.</param>
    public GalacticInformationCategoryRenderData(
        bool visible,
        RectInt hitBounds,
        GalacticInformationImageRenderData icon,
        GalacticInformationImageRenderData arrow,
        GalacticInformationTextRenderData label,
        GalacticInformationSubmenuRenderData submenu
    )
    {
        Visible = visible;
        HitBounds = hitBounds;
        Icon = icon;
        Arrow = arrow;
        Label = label;
        Submenu = submenu;
    }
}

/// <summary>
/// Contains immutable presentation for one galactic-information submenu.
/// </summary>
public sealed class GalacticInformationSubmenuRenderData
{
    public bool Visible { get; }

    public RectInt Bounds { get; }

    public Color BackgroundColor { get; }

    public GalacticInformationFrameRenderData Frame { get; }

    public IReadOnlyList<GalacticInformationFilterRenderData> Filters { get; }

    /// <summary>
    /// Creates immutable submenu presentation data.
    /// </summary>
    /// <param name="visible">Whether the submenu is visible.</param>
    /// <param name="bounds">The submenu's source-space bounds.</param>
    /// <param name="backgroundColor">The submenu background color.</param>
    /// <param name="frame">The submenu frame presentation.</param>
    /// <param name="filters">The filter rows in authored-slot order.</param>
    public GalacticInformationSubmenuRenderData(
        bool visible,
        RectInt bounds,
        Color backgroundColor,
        GalacticInformationFrameRenderData frame,
        IReadOnlyList<GalacticInformationFilterRenderData> filters
    )
    {
        Visible = visible;
        Bounds = bounds;
        BackgroundColor = backgroundColor;
        Frame = frame;
        Filters = GalaxyMapRenderData.Copy(filters);
    }
}

/// <summary>
/// Defines resolved presentation and semantic identity for one submenu filter row.
/// </summary>
public sealed class GalacticInformationFilterRenderData
{
    public GalacticInformationFilterMode Mode { get; }

    public bool Visible { get; }

    public RectInt HitBounds { get; }

    public GalacticInformationImageRenderData CheckMark { get; }

    public GalacticInformationImageRenderData Icon { get; }

    public GalacticInformationTextRenderData Label { get; }

    /// <summary>
    /// Creates immutable filter-row presentation data.
    /// </summary>
    /// <param name="mode">The semantic filter selected by the row.</param>
    /// <param name="visible">Whether the filter row is visible.</param>
    /// <param name="hitBounds">The filter row hit bounds.</param>
    /// <param name="checkMark">The selected-state check-mark presentation.</param>
    /// <param name="icon">The filter icon presentation.</param>
    /// <param name="label">The filter label presentation.</param>
    public GalacticInformationFilterRenderData(
        GalacticInformationFilterMode mode,
        bool visible,
        RectInt hitBounds,
        GalacticInformationImageRenderData checkMark,
        GalacticInformationImageRenderData icon,
        GalacticInformationTextRenderData label
    )
    {
        Mode = mode;
        Visible = visible;
        HitBounds = hitBounds;
        CheckMark = checkMark;
        Icon = icon;
        Label = label;
    }
}

/// <summary>
/// Defines a resolved image texture and source-space placement.
/// </summary>
public readonly struct GalacticInformationImageRenderData
{
    public Texture2D Texture { get; }

    public RectInt Bounds { get; }

    /// <summary>
    /// Creates immutable image presentation data.
    /// </summary>
    /// <param name="texture">The resolved texture.</param>
    /// <param name="bounds">The source-space image bounds.</param>
    public GalacticInformationImageRenderData(Texture2D texture, RectInt bounds)
    {
        Texture = texture;
        Bounds = bounds;
    }
}

/// <summary>
/// Defines resolved text, color, and source-space placement.
/// </summary>
public readonly struct GalacticInformationTextRenderData
{
    public string Text { get; }

    public Color Color { get; }

    public RectInt Bounds { get; }

    /// <summary>
    /// Creates immutable text presentation data.
    /// </summary>
    /// <param name="text">The displayed text.</param>
    /// <param name="color">The displayed text color.</param>
    /// <param name="bounds">The source-space text bounds.</param>
    public GalacticInformationTextRenderData(string text, Color color, RectInt bounds)
    {
        Text = text ?? string.Empty;
        Color = color;
        Bounds = bounds;
    }
}

/// <summary>
/// Defines one text row's visibility, hit bounds, and label presentation.
/// </summary>
public readonly struct GalacticInformationTextRowRenderData
{
    public bool Visible { get; }

    public RectInt HitBounds { get; }

    public GalacticInformationImageRenderData CheckMark { get; }

    public GalacticInformationTextRenderData Label { get; }

    /// <summary>
    /// Creates immutable text-row presentation data.
    /// </summary>
    /// <param name="visible">Whether the row is visible.</param>
    /// <param name="hitBounds">The row hit bounds.</param>
    /// <param name="checkMark">The selected-state check-mark presentation.</param>
    /// <param name="label">The row label presentation.</param>
    public GalacticInformationTextRowRenderData(
        bool visible,
        RectInt hitBounds,
        GalacticInformationImageRenderData checkMark,
        GalacticInformationTextRenderData label
    )
    {
        Visible = visible;
        HitBounds = hitBounds;
        CheckMark = checkMark;
        Label = label;
    }
}

/// <summary>
/// Defines resolved textures and dimensions for an eight-section information frame.
/// </summary>
public sealed class GalacticInformationFrameRenderData
{
    public int Width { get; }

    public int Height { get; }

    public IReadOnlyList<Texture2D> Textures { get; }

    /// <summary>
    /// Creates immutable frame presentation data.
    /// </summary>
    /// <param name="width">The source-space frame width.</param>
    /// <param name="height">The source-space frame height.</param>
    /// <param name="textures">The eight resolved frame textures in authored order.</param>
    public GalacticInformationFrameRenderData(
        int width,
        int height,
        IReadOnlyList<Texture2D> textures
    )
    {
        Width = width;
        Height = height;
        Textures = GalaxyMapRenderData.Copy(textures);
    }
}

/// <summary>
/// Defines resolved presentation for the retained galactic-information legend.
/// </summary>
public sealed class GalacticInformationLegendRenderData
{
    public RectInt Bounds { get; }

    public Texture2D Texture { get; }

    public GalacticInformationFrameRenderData Frame { get; }

    public RectInt CloseBounds { get; }

    public Texture2D CloseTexture { get; }

    public Texture2D ClosePressedTexture { get; }

    /// <summary>
    /// Creates immutable legend presentation data.
    /// </summary>
    /// <param name="bounds">The source-space legend bounds.</param>
    /// <param name="texture">The resolved legend texture.</param>
    /// <param name="frame">The legend frame presentation.</param>
    /// <param name="closeBounds">The source-space close-control bounds.</param>
    /// <param name="closeTexture">The resolved idle close texture.</param>
    /// <param name="closePressedTexture">The resolved pressed close texture.</param>
    public GalacticInformationLegendRenderData(
        RectInt bounds,
        Texture2D texture,
        GalacticInformationFrameRenderData frame,
        RectInt closeBounds,
        Texture2D closeTexture,
        Texture2D closePressedTexture
    )
    {
        Bounds = bounds;
        Texture = texture;
        Frame = frame;
        CloseBounds = closeBounds;
        CloseTexture = closeTexture;
        ClosePressedTexture = closePressedTexture;
    }
}
