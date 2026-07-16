using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Projects galactic-information theme and interaction state into immutable view data.
/// </summary>
public sealed class GalacticInformationDisplayProjector
{
    private readonly Func<UIContext> getUIContext;

    /// <summary>
    /// Creates a galactic-information projector backed by the current strategy UI context.
    /// </summary>
    /// <param name="getUIContext">Returns the current strategy UI context.</param>
    public GalacticInformationDisplayProjector(Func<UIContext> getUIContext)
    {
        this.getUIContext = getUIContext ?? throw new ArgumentNullException(nameof(getUIContext));
    }

    /// <summary>
    /// Projects the current selector interaction state using the active player theme.
    /// </summary>
    /// <param name="state">The controller-owned selector interaction state.</param>
    /// <returns>The complete immutable selector presentation.</returns>
    public GalacticInformationDisplayRenderData Project(GalacticInformationDisplayState state)
    {
        if (!state.Visible)
        {
            return new GalacticInformationDisplayRenderData(
                false,
                default,
                Color.clear,
                null,
                Array.Empty<GalacticInformationCategoryRenderData>(),
                default
            );
        }

        UIContext context = GetRequiredContext();
        FactionTheme playerTheme = GetRequiredPlayerTheme(context);
        GalacticInformationDisplayTheme theme = GetRequiredDisplayTheme(playerTheme);
        SourceRectLayout selector = GetRequiredSelectorLayout(theme);
        Color highlightColor = playerTheme.GetPrimaryColor();
        List<GalacticInformationCategoryRenderData> categories = ProjectCategories(
            theme,
            state,
            highlightColor,
            context
        );
        RectInt selectorBounds = ToRect(selector);
        return new GalacticInformationDisplayRenderData(
            true,
            selectorBounds,
            theme.GetBackgroundColor(),
            ProjectFrame(theme.Frame, selector.Width, selector.Height, context),
            categories,
            ProjectDisplayOffRow(theme, state.DisplayOffHovered, highlightColor)
        );
    }

    /// <summary>
    /// Projects one configured legend at its requested source-space position.
    /// </summary>
    /// <param name="filterMode">The filter whose legend artwork is requested.</param>
    /// <param name="sourcePosition">The requested source-space legend position.</param>
    /// <returns>The immutable legend presentation, or null when required art is unavailable.</returns>
    public GalacticInformationLegendRenderData ProjectLegend(
        GalacticInformationFilterMode filterMode,
        Vector2Int sourcePosition
    )
    {
        UIContext context = GetRequiredContext();
        GalacticInformationDisplayTheme theme = context
            .GetPlayerFactionTheme()
            ?.GalacticInformationDisplay;
        GalacticInformationFilterTheme filter = theme?.GetFilter(filterMode);
        Texture2D legendTexture = context.GetTexture(filter?.LegendImagePath);
        Texture2D closeTexture = context.GetTexture(theme?.CloseUpImagePath);
        SourcePointLayout closeInset = theme?.CloseSourceInset;
        if (legendTexture == null || closeTexture == null || closeInset == null)
            return null;

        Vector2Int legendSize = UILayout.GetTextureSourceSize(legendTexture);
        Vector2Int closeSize = UILayout.GetTextureSourceSize(closeTexture);
        RectInt closeBounds = new RectInt(
            legendSize.x - closeSize.x - closeInset.X,
            closeInset.Y,
            closeSize.x,
            closeSize.y
        );
        return new GalacticInformationLegendRenderData(
            new RectInt(sourcePosition.x, sourcePosition.y, legendSize.x, legendSize.y),
            legendTexture,
            ProjectFrame(theme.Frame, legendSize.x, legendSize.y, context),
            closeBounds,
            closeTexture,
            context.GetTexture(theme.ClosePressedImagePath)
        );
    }

    /// <summary>
    /// Projects configured categories and their nested filter submenus.
    /// </summary>
    /// <param name="theme">The active galactic-information theme.</param>
    /// <param name="state">The current interaction state.</param>
    /// <param name="highlightColor">The active faction highlight color.</param>
    /// <param name="context">The current strategy UI context.</param>
    /// <returns>The projected category presentations.</returns>
    private static List<GalacticInformationCategoryRenderData> ProjectCategories(
        GalacticInformationDisplayTheme theme,
        GalacticInformationDisplayState state,
        Color highlightColor,
        UIContext context
    )
    {
        List<GalacticInformationCategoryRenderData> categories =
            new List<GalacticInformationCategoryRenderData>(theme.Categories.Count);
        for (int i = 0; i < theme.Categories.Count; i++)
        {
            categories.Add(
                ProjectCategory(theme, theme.Categories[i], i, state, highlightColor, context)
            );
        }

        return categories;
    }

    /// <summary>
    /// Projects one configured category and its submenu.
    /// </summary>
    /// <param name="theme">The active galactic-information theme.</param>
    /// <param name="category">The configured category.</param>
    /// <param name="categoryIndex">The category's authored-slot index.</param>
    /// <param name="state">The current interaction state.</param>
    /// <param name="highlightColor">The active faction highlight color.</param>
    /// <param name="context">The current strategy UI context.</param>
    /// <returns>The immutable category presentation.</returns>
    private static GalacticInformationCategoryRenderData ProjectCategory(
        GalacticInformationDisplayTheme theme,
        GalacticInformationCategoryTheme category,
        int categoryIndex,
        GalacticInformationDisplayState state,
        Color highlightColor,
        UIContext context
    )
    {
        SourceRectLayout row = category?.RowSourceLayout;
        SourceRectLayout icon = theme.MenuIconSourceLayout;
        SourceRectLayout text = theme.MenuTextSourceLayout;
        SourceRectLayout arrow = theme.SubmenuArrowSourceLayout;
        if (row == null || icon == null || text == null || arrow == null)
        {
            return new GalacticInformationCategoryRenderData(
                false,
                default,
                default,
                default,
                default,
                null
            );
        }

        bool active = categoryIndex == state.ActiveCategoryIndex;
        GalacticInformationImageRenderData iconData = new GalacticInformationImageRenderData(
            context.GetTexture(category.IconImagePath),
            new RectInt(row.X + icon.X, row.Y + icon.Y, icon.Width, icon.Height)
        );
        GalacticInformationImageRenderData arrowData = new GalacticInformationImageRenderData(
            context.GetTexture(
                active ? theme.SubmenuArrowActiveImagePath : theme.SubmenuArrowInactiveImagePath
            ),
            new RectInt(arrow.X, row.Y + arrow.Y, arrow.Width, arrow.Height)
        );
        GalacticInformationTextRenderData labelData = new GalacticInformationTextRenderData(
            category.Label,
            active ? highlightColor : Color.white,
            new RectInt(row.X + text.X, row.Y + text.Y, row.Width - text.X, text.Height)
        );
        return new GalacticInformationCategoryRenderData(
            true,
            ToRect(row),
            iconData,
            arrowData,
            labelData,
            ProjectSubmenu(
                theme,
                category,
                active,
                state.HoveredFilterIndex,
                highlightColor,
                context
            )
        );
    }

    /// <summary>
    /// Projects one category's submenu and configured filter rows.
    /// </summary>
    /// <param name="theme">The active galactic-information theme.</param>
    /// <param name="category">The configured category.</param>
    /// <param name="visible">Whether the submenu is open.</param>
    /// <param name="hoveredFilterIndex">The hovered filter index.</param>
    /// <param name="highlightColor">The active faction highlight color.</param>
    /// <param name="context">The current strategy UI context.</param>
    /// <returns>The immutable submenu presentation, or null when no layout is configured.</returns>
    private static GalacticInformationSubmenuRenderData ProjectSubmenu(
        GalacticInformationDisplayTheme theme,
        GalacticInformationCategoryTheme category,
        bool visible,
        int hoveredFilterIndex,
        Color highlightColor,
        UIContext context
    )
    {
        SourceRectLayout panel = category?.SubmenuSourceLayout;
        if (panel == null)
            return null;

        List<GalacticInformationFilterRenderData> filters =
            new List<GalacticInformationFilterRenderData>(category.Filters.Count);
        for (int i = 0; i < category.Filters.Count; i++)
        {
            filters.Add(
                ProjectFilter(
                    theme,
                    category.Filters[i],
                    i == hoveredFilterIndex,
                    highlightColor,
                    context
                )
            );
        }

        return new GalacticInformationSubmenuRenderData(
            visible,
            ToRect(panel),
            theme.GetBackgroundColor(),
            ProjectFrame(theme.Frame, panel.Width, panel.Height, context),
            filters
        );
    }

    /// <summary>
    /// Projects one configured submenu filter row.
    /// </summary>
    /// <param name="theme">The active galactic-information theme.</param>
    /// <param name="filter">The configured filter row.</param>
    /// <param name="hovered">Whether the row is hovered.</param>
    /// <param name="highlightColor">The active faction highlight color.</param>
    /// <param name="context">The current strategy UI context.</param>
    /// <returns>The immutable filter-row presentation.</returns>
    private static GalacticInformationFilterRenderData ProjectFilter(
        GalacticInformationDisplayTheme theme,
        GalacticInformationFilterTheme filter,
        bool hovered,
        Color highlightColor,
        UIContext context
    )
    {
        SourceRectLayout row = filter?.RowSourceLayout;
        SourceRectLayout icon = theme.MenuIconSourceLayout;
        SourceRectLayout text = theme.MenuTextSourceLayout;
        if (row == null || icon == null || text == null)
        {
            return new GalacticInformationFilterRenderData(
                filter?.Mode ?? GalacticInformationFilterMode.DisplayOff,
                false,
                default,
                default,
                default
            );
        }

        return new GalacticInformationFilterRenderData(
            filter.Mode,
            true,
            ToRect(row),
            new GalacticInformationImageRenderData(
                context.GetTexture(filter.IconImagePath),
                new RectInt(row.X + icon.X, row.Y + icon.Y, icon.Width, icon.Height)
            ),
            new GalacticInformationTextRenderData(
                filter.Label,
                hovered ? highlightColor : Color.white,
                new RectInt(row.X + text.X, row.Y + text.Y, row.Width - text.X, text.Height)
            )
        );
    }

    /// <summary>
    /// Projects the selector's display-off row.
    /// </summary>
    /// <param name="theme">The active galactic-information theme.</param>
    /// <param name="hovered">Whether the display-off row is hovered.</param>
    /// <param name="highlightColor">The active faction highlight color.</param>
    /// <returns>The immutable display-off row presentation.</returns>
    private static GalacticInformationTextRowRenderData ProjectDisplayOffRow(
        GalacticInformationDisplayTheme theme,
        bool hovered,
        Color highlightColor
    )
    {
        SourceRectLayout row = theme?.DisplayOffRowSourceLayout;
        if (row == null)
            return default;

        RectInt bounds = ToRect(row);
        return new GalacticInformationTextRowRenderData(
            true,
            bounds,
            new GalacticInformationTextRenderData(
                theme.DisplayOffLabel,
                hovered ? highlightColor : Color.white,
                bounds
            )
        );
    }

    /// <summary>
    /// Resolves all eight frame textures for one panel size.
    /// </summary>
    /// <param name="theme">The configured frame theme.</param>
    /// <param name="width">The source-space frame width.</param>
    /// <param name="height">The source-space frame height.</param>
    /// <param name="context">The current strategy UI context.</param>
    /// <returns>The immutable frame presentation.</returns>
    private static GalacticInformationFrameRenderData ProjectFrame(
        GalacticInformationFrameTheme theme,
        int width,
        int height,
        UIContext context
    )
    {
        Texture2D[] textures = new Texture2D[8];
        for (int i = 0; i < textures.Length; i++)
            textures[i] = context.GetTexture(theme?.GetImagePath(i));

        return new GalacticInformationFrameRenderData(width, height, textures);
    }

    /// <summary>
    /// Converts configured source-space layout to immutable bounds.
    /// </summary>
    /// <param name="layout">The configured source-space layout.</param>
    /// <returns>The equivalent immutable bounds.</returns>
    private static RectInt ToRect(SourceRectLayout layout)
    {
        return new RectInt(layout.X, layout.Y, layout.Width, layout.Height);
    }

    /// <summary>
    /// Gets the current strategy UI context and rejects incomplete screen composition.
    /// </summary>
    /// <returns>The current strategy UI context.</returns>
    private UIContext GetRequiredContext()
    {
        return getUIContext()
            ?? throw new InvalidOperationException(
                "Galactic-information projection requires a UI context."
            );
    }

    /// <summary>
    /// Gets the active player theme and rejects an incomplete theme library.
    /// </summary>
    /// <param name="context">The current strategy UI context.</param>
    /// <returns>The active player theme.</returns>
    private static FactionTheme GetRequiredPlayerTheme(UIContext context)
    {
        return context.GetPlayerFactionTheme()
            ?? throw new InvalidOperationException(
                "Galactic-information projection requires a player faction theme."
            );
    }

    /// <summary>
    /// Gets the configured display theme and rejects incomplete player-theme composition.
    /// </summary>
    /// <param name="playerTheme">The active player theme.</param>
    /// <returns>The configured galactic-information display theme.</returns>
    private static GalacticInformationDisplayTheme GetRequiredDisplayTheme(FactionTheme playerTheme)
    {
        return playerTheme.GalacticInformationDisplay
            ?? throw new InvalidOperationException(
                "The player faction theme is missing galactic-information display configuration."
            );
    }

    /// <summary>
    /// Gets the selector layout and rejects incomplete display-theme composition.
    /// </summary>
    /// <param name="theme">The configured galactic-information display theme.</param>
    /// <returns>The selector source-space layout.</returns>
    private static SourceRectLayout GetRequiredSelectorLayout(GalacticInformationDisplayTheme theme)
    {
        return theme.SelectorSourceLayout
            ?? throw new InvalidOperationException(
                "The galactic-information display theme is missing its selector layout."
            );
    }
}
