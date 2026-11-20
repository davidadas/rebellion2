using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Projects galactic-information theme and interaction state into immutable view data.
/// </summary>
public sealed class GalacticInformationDisplayProjector
{
    private const int _legendWidth = 244;
    private const int _legendPadding = 10;
    private const int _legendHeaderHeight = 32;
    private const int _legendRowHeight = 22;
    private const int _legendKeyHeadingHeight = 24;

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
        Texture2D checkMarkTexture = context.GetTexture(
            playerTheme.StrategyContextMenuTheme?.CheckMarkImagePath
        );
        List<GalacticInformationCategoryRenderData> categories = ProjectCategories(
            theme,
            state,
            highlightColor,
            checkMarkTexture,
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
    /// <param name="sourcePosition">An optional initial position replacing the themed legend position.</param>
    /// <returns>The immutable legend presentation, or null when the filter is unavailable.</returns>
    public GalacticInformationLegendRenderData ProjectLegend(
        GalacticInformationFilterMode filterMode,
        Vector2Int? sourcePosition = null
    )
    {
        UIContext context = GetRequiredContext();
        FactionTheme playerTheme = context.GetPlayerFactionTheme();
        GalacticInformationDisplayTheme theme = playerTheme?.GalacticInformationDisplay;
        GalacticInformationFilterTheme filter = GalacticInformationMarkerProjector.ResolveFilter(
            playerTheme,
            filterMode
        );
        if (filter == null)
            return null;

        Vector2Int position =
            sourcePosition
            ?? new Vector2Int(theme.LegendSourcePosition.X, theme.LegendSourcePosition.Y);

        List<GalacticInformationLegendRowRenderData> rows = new();
        AddLegendRanges(rows, filter, context);
        int keyHeadingY = _legendHeaderHeight + rows.Count * _legendRowHeight + 4;
        int keysY = keyHeadingY + _legendKeyHeadingHeight;
        int keyCount = AddLegendKeys(rows, filter.Mode, keysY, context);
        int height = keysY + ((keyCount + 1) / 2) * _legendRowHeight + _legendPadding;
        Color textColor = theme.GetActiveFilterLabelColor();
        RectInt closeBounds = new RectInt(_legendWidth - 25, 5, 20, 20);
        return new GalacticInformationLegendRenderData(
            new RectInt(position.x, position.y, _legendWidth, height),
            new GalacticInformationTextRenderData(
                filter.Label,
                textColor,
                new RectInt(_legendPadding, 7, closeBounds.x - _legendPadding - 8, 20)
            ),
            theme.GetBackgroundColor(),
            new GalacticInformationTextRenderData(
                "Marker colors",
                textColor,
                new RectInt(_legendPadding, keyHeadingY, _legendWidth - 2 * _legendPadding, 20)
            ),
            rows,
            ProjectFrame(theme.Frame, _legendWidth, height, context),
            closeBounds
        );
    }

    /// <summary>
    /// Gets the compact legend control's position below the map display label.
    /// </summary>
    /// <returns>The control bounds within the strategy surface.</returns>
    public RectInt GetLegendControlBounds()
    {
        SourceRectLayout label = GetRequiredPlayerTheme(
            GetRequiredContext()
        ).GalacticInformationDisplay.ActiveFilterLabelSourceLayout;
        return new RectInt(label.X, label.Y + label.Height + 4, 22, 22);
    }

    /// <summary>
    /// Returns whether a map display has a configured legend.
    /// </summary>
    /// <param name="mode">The selected map display.</param>
    /// <returns>True when a selected filter is configured.</returns>
    public bool HasLegend(GalacticInformationFilterMode mode) =>
        GalacticInformationMarkerProjector.ResolveFilter(
            GetRequiredPlayerTheme(GetRequiredContext()),
            mode
        ) != null;

    /// <summary>
    /// Adds reachable marker bands using the same ordered thresholds as map evaluation.
    /// </summary>
    /// <param name="rows">The legend rows receiving the range samples.</param>
    /// <param name="filter">The filter supplying thresholds and labels.</param>
    /// <param name="context">The presentation context supplying marker artwork.</param>
    private static void AddLegendRanges(
        List<GalacticInformationLegendRowRenderData> rows,
        GalacticInformationFilterTheme filter,
        UIContext context
    )
    {
        bool binary =
            filter.Mode
            is GalacticInformationFilterMode.Uprisings
                or GalacticInformationFilterMode.IdleShipyards
                or GalacticInformationFilterMode.IdleTrainingFacilities
                or GalacticInformationFilterMode.IdleConstructionYards
                or GalacticInformationFilterMode.UnitShieldGenerators;
        bool percentage = filter.Mode == GalacticInformationFilterMode.PopularSupport;
        long minimum =
            filter.Mode == GalacticInformationFilterMode.AvailableEnergy ? int.MinValue : 0;
        long maximum =
            binary ? 1
            : percentage ? 100
            : int.MaxValue;
        long ceiling = (long)int.MaxValue + 1;
        int[] thresholds = { filter.LowThreshold, filter.MediumThreshold, filter.HighThreshold };
        PlanetIcons icons = context.GetPlayerFactionTheme().GalaxyBackground.PlanetIcons;
        for (int index = 3; index >= 0; index--)
        {
            long lower = index == 0 ? int.MinValue : thresholds[index - 1];
            long start = Math.Max(minimum, lower);
            long end = Math.Min(maximum, ceiling - 1);
            if (start <= end)
            {
                string label =
                    binary && start == end
                        ? start == 0
                            ? $"No {filter.Label.ToLowerInvariant()}"
                            : filter.Label
                        : FormatLegendRange(start, end, percentage);
                rows.Add(
                    new GalacticInformationLegendRowRenderData(
                        new RectInt(
                            _legendPadding,
                            _legendHeaderHeight + rows.Count * _legendRowHeight,
                            _legendWidth - 2 * _legendPadding,
                            _legendRowHeight
                        ),
                        context.GetTexture(
                            GalacticInformationMarkerProjector.GetPlanetIconPath(icons, index)
                        ),
                        label
                    )
                );
            }
            ceiling = Math.Min(ceiling, lower);
        }
    }

    /// <summary>
    /// Formats one reachable numeric marker band.
    /// </summary>
    /// <param name="minimum">The inclusive lower bound.</param>
    /// <param name="maximum">The inclusive upper bound.</param>
    /// <param name="percentage">Whether values represent popular-support percentages.</param>
    /// <returns>The visible range label.</returns>
    private static string FormatLegendRange(long minimum, long maximum, bool percentage)
    {
        string suffix = percentage ? "%" : string.Empty;
        if (minimum == int.MinValue)
            return $"Below {maximum + 1}{suffix}";
        if (maximum == int.MaxValue)
            return $"{minimum}+";
        return minimum == maximum ? $"{minimum}{suffix}" : $"{minimum}–{maximum}{suffix}";
    }

    /// <summary>
    /// Adds faction and exploration marker keys without inspecting live planet state.
    /// </summary>
    /// <param name="rows">The legend rows receiving marker keys.</param>
    /// <param name="mode">The selected map display.</param>
    /// <param name="top">The first key row's vertical position.</param>
    /// <param name="context">The context supplying presentation-safe faction identities.</param>
    /// <returns>The number of marker keys added.</returns>
    private static int AddLegendKeys(
        List<GalacticInformationLegendRowRenderData> rows,
        GalacticInformationFilterMode mode,
        int top,
        UIContext context
    )
    {
        int count = 0;
        foreach (var faction in context.GetFactionPresentations())
            AddLegendKey(
                rows,
                ref count,
                top,
                faction.GetDisplayName(),
                context.GetTexture(
                    GalacticInformationMarkerProjector.GetPlanetIconPath(
                        context.GetTheme(faction.InstanceID)?.GalaxyBackground?.PlanetIcons,
                        2
                    )
                )
            );
        AddLegendKey(
            rows,
            ref count,
            top,
            "Neutral",
            context.GetTexture(
                GalacticInformationMarkerProjector.GetPlanetIconPath(
                    context.GetTheme(null)?.GalaxyBackground?.PlanetIcons,
                    2
                )
            )
        );
        AddLegendKey(
            rows,
            ref count,
            top,
            "Unexplored",
            context.GetTexture(
                context.GetPlayerFactionTheme()?.GalaxyBackground?.UnexploredPlanetIconPath
            )
        );
        if (
            mode
            is GalacticInformationFilterMode.IdleFleets
                or GalacticInformationFilterMode.FleetsEnroute
                or GalacticInformationFilterMode.FleetWaypoints
                or GalacticInformationFilterMode.IdlePersonnel
                or GalacticInformationFilterMode.ActivePersonnel
        )
            AddLegendKey(
                rows,
                ref count,
                top,
                "Mixed presence",
                context.GetTexture(
                    context.GetPlayerFactionTheme()?.GalaxyBackground?.PlanetIcons?.Mixed
                )
            );
        return count;
    }

    /// <summary>
    /// Places one marker-color key in the compact two-column list.
    /// </summary>
    /// <param name="rows">The rows receiving the key.</param>
    /// <param name="count">The number of keys already placed.</param>
    /// <param name="top">The first key row's vertical position.</param>
    /// <param name="label">The key's label.</param>
    /// <param name="texture">The actual map marker artwork.</param>
    private static void AddLegendKey(
        List<GalacticInformationLegendRowRenderData> rows,
        ref int count,
        int top,
        string label,
        Texture2D texture
    )
    {
        int width = (_legendWidth - 2 * _legendPadding) / 2;
        rows.Add(
            new GalacticInformationLegendRowRenderData(
                new RectInt(
                    _legendPadding + (count % 2) * width,
                    top + (count / 2) * _legendRowHeight,
                    width,
                    _legendRowHeight
                ),
                texture,
                label
            )
        );
        count++;
    }

    /// <summary>
    /// Projects configured categories and their nested filter submenus.
    /// </summary>
    /// <param name="theme">The active galactic-information theme.</param>
    /// <param name="state">The current interaction state.</param>
    /// <param name="highlightColor">The active faction highlight color.</param>
    /// <param name="checkMarkTexture">The shared selected-state check-mark texture.</param>
    /// <param name="context">The current strategy UI context.</param>
    /// <returns>The projected category presentations.</returns>
    private static List<GalacticInformationCategoryRenderData> ProjectCategories(
        GalacticInformationDisplayTheme theme,
        GalacticInformationDisplayState state,
        Color highlightColor,
        Texture2D checkMarkTexture,
        UIContext context
    )
    {
        List<GalacticInformationCategoryRenderData> categories =
            new List<GalacticInformationCategoryRenderData>(theme.Categories.Count);
        for (int i = 0; i < theme.Categories.Count; i++)
        {
            categories.Add(
                ProjectCategory(
                    theme,
                    theme.Categories[i],
                    i,
                    state,
                    highlightColor,
                    checkMarkTexture,
                    context
                )
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
    /// <param name="checkMarkTexture">The shared selected-state check-mark texture.</param>
    /// <param name="context">The current strategy UI context.</param>
    /// <returns>The immutable category presentation.</returns>
    private static GalacticInformationCategoryRenderData ProjectCategory(
        GalacticInformationDisplayTheme theme,
        GalacticInformationCategoryTheme category,
        int categoryIndex,
        GalacticInformationDisplayState state,
        Color highlightColor,
        Texture2D checkMarkTexture,
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
                state.SelectedFilterMode,
                highlightColor,
                checkMarkTexture,
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
    /// <param name="selectedFilterMode">The filter currently applied to the map.</param>
    /// <param name="highlightColor">The active faction highlight color.</param>
    /// <param name="checkMarkTexture">The shared selected-state check-mark texture.</param>
    /// <param name="context">The current strategy UI context.</param>
    /// <returns>The immutable submenu presentation, or null when no layout is configured.</returns>
    private static GalacticInformationSubmenuRenderData ProjectSubmenu(
        GalacticInformationDisplayTheme theme,
        GalacticInformationCategoryTheme category,
        bool visible,
        int hoveredFilterIndex,
        GalacticInformationFilterMode selectedFilterMode,
        Color highlightColor,
        Texture2D checkMarkTexture,
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
                    category.Filters[i]?.Mode == selectedFilterMode,
                    highlightColor,
                    checkMarkTexture,
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
    /// <param name="selected">Whether the filter is currently applied to the map.</param>
    /// <param name="highlightColor">The active faction highlight color.</param>
    /// <param name="checkMarkTexture">The shared selected-state check-mark texture.</param>
    /// <param name="context">The current strategy UI context.</param>
    /// <returns>The immutable filter-row presentation.</returns>
    private static GalacticInformationFilterRenderData ProjectFilter(
        GalacticInformationDisplayTheme theme,
        GalacticInformationFilterTheme filter,
        bool hovered,
        bool selected,
        Color highlightColor,
        Texture2D checkMarkTexture,
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
                default,
                default
            );
        }

        return new GalacticInformationFilterRenderData(
            filter.Mode,
            true,
            ToRect(row),
            new GalacticInformationImageRenderData(
                selected ? checkMarkTexture : null,
                new RectInt(row.X + icon.X - icon.Width, row.Y + icon.Y, icon.Width, icon.Height)
            ),
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
            new GalacticInformationImageRenderData(
                null,
                new RectInt(
                    row.X - theme.MenuIconSourceLayout.Width,
                    row.Y,
                    theme.MenuIconSourceLayout.Width,
                    theme.MenuIconSourceLayout.Height
                )
            ),
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
