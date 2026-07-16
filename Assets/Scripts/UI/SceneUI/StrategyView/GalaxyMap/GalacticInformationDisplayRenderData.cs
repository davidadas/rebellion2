using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Captures controller-owned galactic-information menu interaction state.
/// </summary>
public readonly struct GalacticInformationDisplayState
{
    /// <summary>
    /// Creates an immutable galactic-information interaction-state snapshot.
    /// </summary>
    /// <param name="visible">Whether the selector is open.</param>
    /// <param name="activeCategoryIndex">The open category index, or negative when none is open.</param>
    /// <param name="hoveredFilterIndex">The hovered filter index, or negative when none is hovered.</param>
    /// <param name="displayOffHovered">Whether the display-off row is hovered.</param>
    public GalacticInformationDisplayState(
        bool visible,
        int activeCategoryIndex,
        int hoveredFilterIndex,
        bool displayOffHovered
    )
    {
        Visible = visible;
        ActiveCategoryIndex = activeCategoryIndex;
        HoveredFilterIndex = hoveredFilterIndex;
        DisplayOffHovered = displayOffHovered;
    }

    /// <summary>
    /// Gets whether the selector is open.
    /// </summary>
    public bool Visible { get; }

    /// <summary>
    /// Gets the open category index, or a negative value when none is open.
    /// </summary>
    public int ActiveCategoryIndex { get; }

    /// <summary>
    /// Gets the hovered filter index, or a negative value when none is hovered.
    /// </summary>
    public int HoveredFilterIndex { get; }

    /// <summary>
    /// Gets whether the display-off row is hovered.
    /// </summary>
    public bool DisplayOffHovered { get; }
}

/// <summary>
/// Contains a complete immutable galactic-information selector presentation snapshot.
/// </summary>
public sealed class GalacticInformationDisplayRenderData
{
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

    /// <summary>
    /// Gets whether the selector is visible.
    /// </summary>
    public bool Visible { get; }

    /// <summary>
    /// Gets the selector's source-space bounds.
    /// </summary>
    public RectInt SelectorBounds { get; }

    /// <summary>
    /// Gets the selector background color.
    /// </summary>
    public Color BackgroundColor { get; }

    /// <summary>
    /// Gets the selector frame presentation.
    /// </summary>
    public GalacticInformationFrameRenderData Frame { get; }

    /// <summary>
    /// Gets the category presentations in authored-slot order.
    /// </summary>
    public IReadOnlyList<GalacticInformationCategoryRenderData> Categories { get; }

    /// <summary>
    /// Gets the display-off row presentation.
    /// </summary>
    public GalacticInformationTextRowRenderData DisplayOffRow { get; }
}

/// <summary>
/// Defines resolved presentation for one galactic-information category.
/// </summary>
public sealed class GalacticInformationCategoryRenderData
{
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

    /// <summary>
    /// Gets whether the category slot is visible.
    /// </summary>
    public bool Visible { get; }

    /// <summary>
    /// Gets the category hit bounds.
    /// </summary>
    public RectInt HitBounds { get; }

    /// <summary>
    /// Gets the category icon presentation.
    /// </summary>
    public GalacticInformationImageRenderData Icon { get; }

    /// <summary>
    /// Gets the submenu-arrow presentation.
    /// </summary>
    public GalacticInformationImageRenderData Arrow { get; }

    /// <summary>
    /// Gets the category label presentation.
    /// </summary>
    public GalacticInformationTextRenderData Label { get; }

    /// <summary>
    /// Gets the category submenu presentation.
    /// </summary>
    public GalacticInformationSubmenuRenderData Submenu { get; }
}

/// <summary>
/// Contains immutable presentation for one galactic-information submenu.
/// </summary>
public sealed class GalacticInformationSubmenuRenderData
{
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

    /// <summary>
    /// Gets whether the submenu is visible.
    /// </summary>
    public bool Visible { get; }

    /// <summary>
    /// Gets the submenu's source-space bounds.
    /// </summary>
    public RectInt Bounds { get; }

    /// <summary>
    /// Gets the submenu background color.
    /// </summary>
    public Color BackgroundColor { get; }

    /// <summary>
    /// Gets the submenu frame presentation.
    /// </summary>
    public GalacticInformationFrameRenderData Frame { get; }

    /// <summary>
    /// Gets the filter rows in authored-slot order.
    /// </summary>
    public IReadOnlyList<GalacticInformationFilterRenderData> Filters { get; }
}

/// <summary>
/// Defines resolved presentation and semantic identity for one submenu filter row.
/// </summary>
public sealed class GalacticInformationFilterRenderData
{
    /// <summary>
    /// Creates immutable filter-row presentation data.
    /// </summary>
    /// <param name="mode">The semantic filter selected by the row.</param>
    /// <param name="visible">Whether the filter row is visible.</param>
    /// <param name="hitBounds">The filter row hit bounds.</param>
    /// <param name="icon">The filter icon presentation.</param>
    /// <param name="label">The filter label presentation.</param>
    public GalacticInformationFilterRenderData(
        GalacticInformationFilterMode mode,
        bool visible,
        RectInt hitBounds,
        GalacticInformationImageRenderData icon,
        GalacticInformationTextRenderData label
    )
    {
        Mode = mode;
        Visible = visible;
        HitBounds = hitBounds;
        Icon = icon;
        Label = label;
    }

    /// <summary>
    /// Gets the semantic filter selected by the row.
    /// </summary>
    public GalacticInformationFilterMode Mode { get; }

    /// <summary>
    /// Gets whether the filter row is visible.
    /// </summary>
    public bool Visible { get; }

    /// <summary>
    /// Gets the filter row hit bounds.
    /// </summary>
    public RectInt HitBounds { get; }

    /// <summary>
    /// Gets the filter icon presentation.
    /// </summary>
    public GalacticInformationImageRenderData Icon { get; }

    /// <summary>
    /// Gets the filter label presentation.
    /// </summary>
    public GalacticInformationTextRenderData Label { get; }
}

/// <summary>
/// Defines a resolved image texture and source-space placement.
/// </summary>
public readonly struct GalacticInformationImageRenderData
{
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

    /// <summary>
    /// Gets the resolved texture.
    /// </summary>
    public Texture2D Texture { get; }

    /// <summary>
    /// Gets the source-space image bounds.
    /// </summary>
    public RectInt Bounds { get; }
}

/// <summary>
/// Defines resolved text, color, and source-space placement.
/// </summary>
public readonly struct GalacticInformationTextRenderData
{
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

    /// <summary>
    /// Gets the displayed text.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Gets the displayed text color.
    /// </summary>
    public Color Color { get; }

    /// <summary>
    /// Gets the source-space text bounds.
    /// </summary>
    public RectInt Bounds { get; }
}

/// <summary>
/// Defines one text row's visibility, hit bounds, and label presentation.
/// </summary>
public readonly struct GalacticInformationTextRowRenderData
{
    /// <summary>
    /// Creates immutable text-row presentation data.
    /// </summary>
    /// <param name="visible">Whether the row is visible.</param>
    /// <param name="hitBounds">The row hit bounds.</param>
    /// <param name="label">The row label presentation.</param>
    public GalacticInformationTextRowRenderData(
        bool visible,
        RectInt hitBounds,
        GalacticInformationTextRenderData label
    )
    {
        Visible = visible;
        HitBounds = hitBounds;
        Label = label;
    }

    /// <summary>
    /// Gets whether the row is visible.
    /// </summary>
    public bool Visible { get; }

    /// <summary>
    /// Gets the row hit bounds.
    /// </summary>
    public RectInt HitBounds { get; }

    /// <summary>
    /// Gets the row label presentation.
    /// </summary>
    public GalacticInformationTextRenderData Label { get; }
}

/// <summary>
/// Defines resolved textures and dimensions for an eight-section information frame.
/// </summary>
public sealed class GalacticInformationFrameRenderData
{
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

    /// <summary>
    /// Gets the source-space frame width.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the source-space frame height.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the eight resolved frame textures in authored order.
    /// </summary>
    public IReadOnlyList<Texture2D> Textures { get; }
}

/// <summary>
/// Defines resolved presentation for the retained galactic-information legend.
/// </summary>
public sealed class GalacticInformationLegendRenderData
{
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

    /// <summary>
    /// Gets the source-space legend bounds.
    /// </summary>
    public RectInt Bounds { get; }

    /// <summary>
    /// Gets the resolved legend texture.
    /// </summary>
    public Texture2D Texture { get; }

    /// <summary>
    /// Gets the legend frame presentation.
    /// </summary>
    public GalacticInformationFrameRenderData Frame { get; }

    /// <summary>
    /// Gets the source-space close-control bounds.
    /// </summary>
    public RectInt CloseBounds { get; }

    /// <summary>
    /// Gets the resolved idle close texture.
    /// </summary>
    public Texture2D CloseTexture { get; }

    /// <summary>
    /// Gets the resolved pressed close texture.
    /// </summary>
    public Texture2D ClosePressedTexture { get; }
}
