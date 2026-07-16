using System;
using System.Collections.Generic;
using Rebellion.SceneGraph;
using UnityEngine;

/// <summary>
/// Owns advisor-report projection, themed asset resolution, and window action routing.
/// </summary>
public sealed class AdvisorReportWindowController
{
    private const string _galaxyOverviewTitle = "Galaxy Overview";
    private const string _objectivesTitle = "Objectives";

    private readonly Action<UIWindow> closeWindow;
    private readonly Func<Vector2Int> getWindowPosition;
    private readonly Func<UIContext> getUIContext;
    private readonly Action markDirty;
    private readonly Dictionary<AdvisorReportWindowView, AdvisorReportWindowSession> sessions =
        new Dictionary<AdvisorReportWindowView, AdvisorReportWindowSession>();
    private readonly StrategyWindowLayerView windowLayer;
    private readonly UIWindowManager windowManager;

    /// <summary>
    /// Creates an advisor-report controller with its presentation dependency.
    /// </summary>
    /// <param name="getUIContext">Returns the current strategy presentation context.</param>
    /// <param name="windowLayer">Provides the authored advisor-report prefab and window layer.</param>
    /// <param name="windowManager">Owns strategy-window creation and focus.</param>
    /// <param name="getWindowPosition">Returns the authored advisor-report placement.</param>
    /// <param name="closeWindow">Closes a registered strategy window.</param>
    /// <param name="markDirty">Invalidates strategy presentation after window changes.</param>
    public AdvisorReportWindowController(
        Func<UIContext> getUIContext,
        StrategyWindowLayerView windowLayer,
        UIWindowManager windowManager,
        Func<Vector2Int> getWindowPosition,
        Action<UIWindow> closeWindow,
        Action markDirty
    )
    {
        this.getUIContext = getUIContext ?? throw new ArgumentNullException(nameof(getUIContext));
        this.windowLayer = windowLayer ?? throw new ArgumentNullException(nameof(windowLayer));
        this.windowManager =
            windowManager ?? throw new ArgumentNullException(nameof(windowManager));
        this.getWindowPosition =
            getWindowPosition ?? throw new ArgumentNullException(nameof(getWindowPosition));
        this.closeWindow = closeWindow ?? throw new ArgumentNullException(nameof(closeWindow));
        this.markDirty = markDirty ?? throw new ArgumentNullException(nameof(markDirty));
    }

    /// <summary>
    /// Subscribes the controller to one advisor-report view exactly once.
    /// </summary>
    /// <param name="view">The view to bind.</param>
    /// <param name="mode">The report mode owned by this window session.</param>
    public void BindWindow(AdvisorReportWindowView view, AdvisorReportMode mode)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));
        UIWindow window = view.GetComponent<UIWindow>();
        if (window == null)
            throw new MissingReferenceException($"{view.name} is missing UIWindow.");
        if (sessions.TryGetValue(view, out AdvisorReportWindowSession session))
        {
            session.SelectMode(mode);
            return;
        }

        sessions.Add(view, new AdvisorReportWindowSession(window, mode));
        view.CloseRequested += HandleCloseRequested;
        view.Destroyed += HandleViewDestroyed;
    }

    /// <summary>
    /// Opens or focuses the advisor report for a requested mode.
    /// </summary>
    /// <param name="mode">The report mode to display.</param>
    public void Open(AdvisorReportMode mode)
    {
        UIWindow existing = FindWindow();
        if (existing != null)
        {
            if (windowManager.TryGetWindowView(existing, out AdvisorReportWindowView existingView))
                BindWindow(existingView, mode);
            windowManager.Focus(existing);
            markDirty();
            return;
        }

        Vector2Int position = getWindowPosition();
        windowManager.CreateWindow(
            windowLayer.AdvisorReportWindowPrefab,
            windowLayer.GetWindowParent(true),
            $"AdvisorReportWindow-{mode}",
            position.x,
            position.y,
            windowLayer.GetWindowSize(windowLayer.AdvisorReportWindowPrefab),
            true,
            true,
            false,
            false,
            out AdvisorReportWindowView view
        );
        BindWindow(view, mode);
        markDirty();
    }

    /// <summary>
    /// Renders every registered advisor-report window.
    /// </summary>
    public void RenderWindows()
    {
        foreach (UIWindow window in windowManager.Windows)
        {
            if (windowManager.TryGetWindowView(window, out AdvisorReportWindowView view))
                RenderWindow(view, window);
        }
    }

    /// <summary>
    /// Gets the report mode owned by one bound window session.
    /// </summary>
    /// <param name="view">The advisor-report view whose mode is requested.</param>
    /// <returns>The mode owned by the bound window.</returns>
    internal AdvisorReportMode GetMode(AdvisorReportWindowView view)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));
        if (!sessions.TryGetValue(view, out AdvisorReportWindowSession session))
            throw new InvalidOperationException("The advisor-report view is not bound.");

        return session.Mode;
    }

    /// <summary>
    /// Projects current game state and renders an advisor-report window.
    /// </summary>
    /// <param name="view">The destination advisor-report view.</param>
    /// <param name="window">The window shell supplying position.</param>
    public void RenderWindow(AdvisorReportWindowView view, UIWindow window)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));
        if (window == null)
            throw new ArgumentNullException(nameof(window));

        AdvisorReportMode mode = GetMode(view);
        UIContext uiContext = GetUIContext();
        AdvisorReportWindowTheme theme = uiContext
            .GetPlayerFactionTheme()
            ?.StrategyWindows?.AdvisorReport;
        IReadOnlyList<AdvisorReportRow> rows = AdvisorReportBuilder.Build(
            uiContext.Game,
            uiContext.Game?.GetPlayerFaction(),
            theme,
            mode
        );
        view.Render(
            new AdvisorReportWindowRenderData(
                window.X,
                window.Y,
                mode,
                uiContext.GetTexture(theme?.BackgroundImagePath),
                uiContext.GetTexture(theme?.GalaxyImagePath),
                GetTitle(mode),
                CreateRows(uiContext, rows)
            )
        );
    }

    /// <summary>
    /// Converts domain report rows into presentation-only rows.
    /// </summary>
    /// <param name="uiContext">The presentation context used to resolve row textures.</param>
    /// <param name="rows">The projected domain report rows.</param>
    /// <returns>Presentation rows in the same order.</returns>
    internal static IReadOnlyList<AdvisorReportRowRenderData> CreateRows(
        UIContext uiContext,
        IReadOnlyList<AdvisorReportRow> rows
    )
    {
        if (uiContext == null)
            throw new ArgumentNullException(nameof(uiContext));
        if (rows == null)
            throw new ArgumentNullException(nameof(rows));

        List<AdvisorReportRowRenderData> renderRows = new List<AdvisorReportRowRenderData>(
            rows.Count
        );

        foreach (AdvisorReportRow row in rows)
        {
            if (row == null)
                throw new ArgumentException("Rows cannot contain null.", nameof(rows));

            renderRows.Add(
                new AdvisorReportRowRenderData(
                    GetRowTexture(uiContext, row),
                    row.PrimaryText,
                    row.SecondaryText
                )
            );
        }

        return renderRows.AsReadOnly();
    }

    /// <summary>
    /// Returns the title associated with a report mode.
    /// </summary>
    /// <param name="mode">The report mode.</param>
    /// <returns>The displayed report title.</returns>
    internal static string GetTitle(AdvisorReportMode mode)
    {
        return mode switch
        {
            AdvisorReportMode.GalaxyOverview => _galaxyOverviewTitle,
            AdvisorReportMode.Objectives => _objectivesTitle,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
        };
    }

    /// <summary>
    /// Resolves an entity or configured image path for one report row.
    /// </summary>
    /// <param name="uiContext">The presentation context used to resolve the texture.</param>
    /// <param name="row">The report row.</param>
    /// <returns>The resolved row texture, or null.</returns>
    private static Texture2D GetRowTexture(UIContext uiContext, AdvisorReportRow row)
    {
        ISceneNode item = row.Item;
        return item != null
            ? uiContext.GetEntityTexture(item, true)
            : uiContext.GetTexture(row.ImagePath);
    }

    /// <summary>
    /// Routes a semantic close request to the owning window controller.
    /// </summary>
    /// <param name="view">The requesting advisor-report view.</param>
    private void HandleCloseRequested(AdvisorReportWindowView view)
    {
        if (sessions.TryGetValue(view, out AdvisorReportWindowSession session))
            closeWindow(session.Window);
    }

    /// <summary>
    /// Releases subscriptions and state for a destroyed advisor-report view.
    /// </summary>
    /// <param name="view">The destroyed advisor-report view.</param>
    private void HandleViewDestroyed(AdvisorReportWindowView view)
    {
        if (ReferenceEquals(view, null) || !sessions.Remove(view))
            return;

        view.CloseRequested -= HandleCloseRequested;
        view.Destroyed -= HandleViewDestroyed;
    }

    /// <summary>
    /// Finds the registered advisor-report window.
    /// </summary>
    /// <returns>The registered window, or null when the report is closed.</returns>
    private UIWindow FindWindow()
    {
        foreach (UIWindow window in windowManager.Windows)
        {
            if (windowManager.TryGetWindowView(window, out AdvisorReportWindowView _))
                return window;
        }

        return null;
    }

    /// <summary>
    /// Gets the current presentation context and rejects incomplete composition.
    /// </summary>
    /// <returns>The current strategy presentation context.</returns>
    private UIContext GetUIContext()
    {
        return getUIContext()
            ?? throw new InvalidOperationException("The strategy UI context is unavailable.");
    }

    /// <summary>
    /// Contains state owned by one advisor-report window.
    /// </summary>
    private sealed class AdvisorReportWindowSession
    {
        /// <summary>
        /// Creates one advisor-report session.
        /// </summary>
        /// <param name="window">The owning advisor-report window.</param>
        /// <param name="mode">The displayed report mode.</param>
        public AdvisorReportWindowSession(UIWindow window, AdvisorReportMode mode)
        {
            Window = window ?? throw new ArgumentNullException(nameof(window));
            Mode = mode;
        }

        /// <summary>
        /// Gets the displayed report mode.
        /// </summary>
        public AdvisorReportMode Mode { get; private set; }

        /// <summary>
        /// Gets the owning advisor-report window.
        /// </summary>
        public UIWindow Window { get; }

        /// <summary>
        /// Selects the report mode displayed by this session.
        /// </summary>
        /// <param name="mode">The requested report mode.</param>
        public void SelectMode(AdvisorReportMode mode)
        {
            Mode = mode;
        }
    }
}
