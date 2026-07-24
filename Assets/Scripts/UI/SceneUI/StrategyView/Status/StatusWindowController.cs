using System;
using System.Collections.Generic;
using Rebellion.Game.Galaxy;
using Rebellion.SceneGraph;
using UnityEngine;

/// <summary>
/// Performs window-level actions requested by the status feature.
/// </summary>
public interface IStatusWindowActions
{
    /// <summary>
    /// Opens Encyclopedia information for a status target.
    /// </summary>
    /// <param name="target">The status target whose information should open.</param>
    void OpenStatusInfo(StrategyStatusTarget target);
}

/// <summary>
/// Owns status sessions, domain projection, themed assets, and semantic action routing.
/// </summary>
public sealed class StatusWindowController
{
    private readonly HashSet<StatusWindowView> boundViews = new HashSet<StatusWindowView>();
    private readonly Action<UIWindow> closeWindow;
    private readonly Func<Vector2Int> getWindowPosition;
    private readonly Func<IReadOnlyList<GalaxyMapSector>> getSectors;
    private readonly Func<UIContext> getUIContext;
    private readonly Func<string, ISceneNode> findVisibleNode;
    private readonly Action markDirty;
    private readonly Action<string> playSfx;
    private readonly Dictionary<StatusWindowView, StatusWindowSession> sessions =
        new Dictionary<StatusWindowView, StatusWindowSession>();
    private readonly StrategyWindowLayerView windowLayer;
    private readonly UIWindowManager windowManager;
    private IStatusWindowActions actions;

    /// <summary>
    /// Creates a status-window controller with its presentation dependency.
    /// </summary>
    /// <param name="getUIContext">Returns the current strategy presentation context.</param>
    /// <param name="windowLayer">Provides the authored status prefab and modal layer.</param>
    /// <param name="windowManager">Owns strategy-window creation and registration.</param>
    /// <param name="getSectors">Returns the visible sectors in presentation order.</param>
    /// <param name="findVisibleNode">Resolves a node from the visible galaxy snapshot.</param>
    /// <param name="playSfx">Plays a strategy UI sound effect.</param>
    /// <param name="getWindowPosition">Returns the authored status-window placement.</param>
    /// <param name="closeWindow">Closes a registered strategy window.</param>
    /// <param name="markDirty">Invalidates strategy presentation after window changes.</param>
    public StatusWindowController(
        Func<UIContext> getUIContext,
        StrategyWindowLayerView windowLayer,
        UIWindowManager windowManager,
        Func<IReadOnlyList<GalaxyMapSector>> getSectors,
        Func<string, ISceneNode> findVisibleNode,
        Action<string> playSfx,
        Func<Vector2Int> getWindowPosition,
        Action<UIWindow> closeWindow,
        Action markDirty
    )
    {
        this.getUIContext = getUIContext ?? throw new ArgumentNullException(nameof(getUIContext));
        this.windowLayer = windowLayer ?? throw new ArgumentNullException(nameof(windowLayer));
        this.windowManager =
            windowManager ?? throw new ArgumentNullException(nameof(windowManager));
        this.getSectors = getSectors ?? throw new ArgumentNullException(nameof(getSectors));
        this.findVisibleNode =
            findVisibleNode ?? throw new ArgumentNullException(nameof(findVisibleNode));
        this.playSfx = playSfx ?? throw new ArgumentNullException(nameof(playSfx));
        this.getWindowPosition =
            getWindowPosition ?? throw new ArgumentNullException(nameof(getWindowPosition));
        this.closeWindow = closeWindow ?? throw new ArgumentNullException(nameof(closeWindow));
        this.markDirty = markDirty ?? throw new ArgumentNullException(nameof(markDirty));
    }

    /// <summary>
    /// Supplies owning window actions after the strategy controller graph is constructed.
    /// </summary>
    /// <param name="windowActions">The feature-specific status actions.</param>
    public void Initialize(IStatusWindowActions windowActions)
    {
        actions = windowActions ?? throw new ArgumentNullException(nameof(windowActions));
    }

    /// <summary>
    /// Starts a status session for one authored view.
    /// </summary>
    /// <param name="view">The destination status view.</param>
    /// <param name="target">The entity or manufacturing summary to present.</param>
    /// <param name="infoDisabled">Whether Encyclopedia navigation is unavailable.</param>
    /// <returns>True when a valid status session was created.</returns>
    public bool TryInitializeWindow(
        StatusWindowView view,
        StrategyStatusTarget target,
        bool infoDisabled
    )
    {
        UIWindow window = view == null ? null : view.GetComponent<UIWindow>();
        if (view == null || window == null || target == null)
            return false;

        BindWindow(view);
        sessions[view] = new StatusWindowSession(window, target, infoDisabled, findVisibleNode);
        return true;
    }

    /// <summary>
    /// Replaces the current status window with a resolved target.
    /// </summary>
    /// <param name="target">The entity or manufacturing summary to present.</param>
    /// <param name="infoDisabled">Whether Encyclopedia navigation is unavailable.</param>
    /// <returns>True when the target initialized a status window.</returns>
    public bool Open(StrategyStatusTarget target, bool infoDisabled = false)
    {
        if (target == null)
            return false;

        UIWindow existing = FindWindow();
        if (existing != null && windowManager.TryGetWindowView(existing, out StatusWindowView _))
        {
            closeWindow(existing);
        }

        Vector2Int position = getWindowPosition();
        UIWindow window = windowManager.CreateWindow(
            windowLayer.StatusWindowPrefab,
            windowLayer.GetWindowParent(true),
            $"StatusWindow-{target.Item?.GetDisplayName() ?? target.Planet?.Planet?.GetDisplayName() ?? target.ManufacturingType?.ToString() ?? "UnknownTarget"}",
            position.x,
            position.y,
            windowLayer.GetWindowSize(windowLayer.StatusWindowPrefab),
            true,
            true,
            false,
            false,
            out StatusWindowView view
        );
        if (!TryInitializeWindow(view, target, infoDisabled))
        {
            windowManager.DestroyWindow(window);
            return false;
        }

        markDirty();
        return true;
    }

    /// <summary>
    /// Renders every registered status window.
    /// </summary>
    public void RenderWindows()
    {
        foreach (UIWindow window in windowManager.Windows)
        {
            if (windowManager.TryGetWindowView(window, out StatusWindowView view))
                RenderWindow(view, window);
        }
    }

    /// <summary>
    /// Rebinds status targets to the refreshed visible galaxy snapshot.
    /// </summary>
    /// <param name="sectors">The refreshed visible sectors.</param>
    public void ReconcileWindows(IReadOnlyList<GalaxyMapSector> sectors)
    {
        if (sectors == null)
            return;

        List<UIWindow> invalidWindows = new List<UIWindow>();
        foreach (StatusWindowSession session in sessions.Values)
        {
            if (!session.Reconcile(sectors))
                invalidWindows.Add(session.Window);
        }

        foreach (UIWindow window in invalidWindows)
            closeWindow(window);
    }

    /// <summary>
    /// Subscribes the controller to one status view exactly once.
    /// </summary>
    /// <param name="view">The view to bind.</param>
    public void BindWindow(StatusWindowView view)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));
        EnsureInitialized();
        if (!boundViews.Add(view))
            return;

        view.CloseRequested += HandleCloseRequested;
        view.Destroyed += HandleViewDestroyed;
        view.InfoRequested += HandleInfoRequested;
    }

    /// <summary>
    /// Projects current domain state and renders one status window.
    /// </summary>
    /// <param name="view">The destination status view.</param>
    /// <param name="window">The window shell supplying source-space position.</param>
    public void RenderWindow(StatusWindowView view, UIWindow window)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));
        if (window == null)
            throw new ArgumentNullException(nameof(window));

        StatusWindowSession session = GetSession(view);

        UIContext uiContext = GetUIContext();
        IReadOnlyList<GalaxyMapSector> sectors =
            getSectors() ?? throw new InvalidOperationException("Status sectors are unavailable.");
        StrategyStatusInfo info = new StrategyStatusInfoBuilder(
            uiContext.Game,
            sectors,
            findVisibleNode
        ).Build(session.Target);
        if (info == null)
            return;

        StatusWindowTheme theme = uiContext.GetTheme(info.OwnerFactionId)?.StrategyWindows?.Status;
        view.Render(
            new StatusWindowRenderData(
                window.X,
                window.Y,
                uiContext.GetTexture(theme?.BackgroundImagePath),
                info.CenterImage,
                session.InfoDisabled,
                info.Header,
                ResolveImageTextures(uiContext, theme, info),
                info.Label,
                CreateRows(info.Rows)
            )
        );
    }

    /// <summary>
    /// Copies projected status values into presentation-only row data.
    /// </summary>
    /// <param name="rows">The projected status values.</param>
    /// <returns>The immutable presentation rows.</returns>
    private static IReadOnlyList<StatusWindowRowRenderData> CreateRows(
        IReadOnlyList<StrategyStatusRow> rows
    )
    {
        List<StatusWindowRowRenderData> result = new List<StatusWindowRowRenderData>();
        foreach (StrategyStatusRow row in rows ?? Array.Empty<StrategyStatusRow>())
            result.Add(new StatusWindowRowRenderData(row?.Left, row?.Right));
        return result.AsReadOnly();
    }

    /// <summary>
    /// Resolves the complete status-image stack in its authored draw order.
    /// </summary>
    /// <param name="uiContext">The current presentation context.</param>
    /// <param name="theme">The applicable status-window theme.</param>
    /// <param name="info">The projected status information.</param>
    /// <returns>The resolved image stack.</returns>
    private static IReadOnlyList<Texture2D> ResolveImageTextures(
        UIContext uiContext,
        StatusWindowTheme theme,
        StrategyStatusInfo info
    )
    {
        List<Texture2D> textures = new List<Texture2D>();
        foreach (StatusWindowImage image in info.Images)
            textures.Add(uiContext.GetTexture(GetImageThemePath(theme, image)));
        foreach (ISceneNode item in info.ImageItems)
            textures.Add(GetPrimaryImageTexture(uiContext, item));
        foreach (ISceneNode item in info.OverlayImageItems)
            textures.Add(uiContext.GetEntityCapturedOverlayTexture(item));
        return textures.AsReadOnly();
    }

    /// <summary>
    /// Resolves a primary entity image with planet-specific handling.
    /// </summary>
    /// <param name="uiContext">The current presentation context.</param>
    /// <param name="item">The represented scene node.</param>
    /// <returns>The resolved entity image, or null.</returns>
    private static Texture2D GetPrimaryImageTexture(UIContext uiContext, ISceneNode item)
    {
        return item is Planet planet
            ? uiContext.GetPlanetTexture(planet)
            : uiContext.GetEntityTexture(item, false);
    }

    /// <summary>
    /// Resolves a themed status-image role to its configured resource path.
    /// </summary>
    /// <param name="theme">The applicable status-window theme.</param>
    /// <param name="image">The requested image role.</param>
    /// <returns>The configured resource path, or null.</returns>
    private static string GetImageThemePath(StatusWindowTheme theme, StatusWindowImage image)
    {
        return image switch
        {
            StatusWindowImage.FleetBanner => theme?.FleetBannerImagePath,
            StatusWindowImage.FleetBannerEnroute => theme?.FleetBannerEnrouteImagePath,
            StatusWindowImage.FleetBannerDamaged => theme?.FleetBannerDamagedImagePath,
            StatusWindowImage.Shipyard => theme?.ShipyardImagePath,
            StatusWindowImage.Construction => theme?.ConstructionImagePath,
            StatusWindowImage.Training => theme?.TrainingImagePath,
            _ => null,
        };
    }

    /// <summary>
    /// Routes a semantic Encyclopedia request for the requesting view's status target.
    /// </summary>
    /// <param name="view">The requesting status view.</param>
    private void HandleInfoRequested(StatusWindowView view)
    {
        if (sessions.TryGetValue(view, out StatusWindowSession session) && !session.InfoDisabled)
        {
            playSfx(StrategyUISoundPaths.ControlPress);
            actions.OpenStatusInfo(session.Target);
        }
    }

    /// <summary>
    /// Routes a semantic close request to the owning window controller.
    /// </summary>
    /// <param name="view">The requesting status view.</param>
    private void HandleCloseRequested(StatusWindowView view)
    {
        if (sessions.TryGetValue(view, out StatusWindowSession session))
        {
            playSfx(StrategyUISoundPaths.ControlPress);
            closeWindow(session.Window);
        }
    }

    /// <summary>
    /// Releases subscriptions and session state for a destroyed status view.
    /// </summary>
    /// <param name="view">The destroyed status view.</param>
    private void HandleViewDestroyed(StatusWindowView view)
    {
        if (ReferenceEquals(view, null) || !boundViews.Remove(view))
            return;

        view.CloseRequested -= HandleCloseRequested;
        view.Destroyed -= HandleViewDestroyed;
        view.InfoRequested -= HandleInfoRequested;
        sessions.Remove(view);
    }

    /// <summary>
    /// Verifies that action routing is available before a view is bound.
    /// </summary>
    private void EnsureInitialized()
    {
        if (actions == null)
            throw new InvalidOperationException(
                $"{nameof(StatusWindowController)} must be initialized before use."
            );
    }

    /// <summary>
    /// Returns the controller-owned session for an initialized status view.
    /// </summary>
    /// <param name="view">The initialized status view.</param>
    /// <returns>The session owned by the view.</returns>
    private StatusWindowSession GetSession(StatusWindowView view)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));
        if (sessions.TryGetValue(view, out StatusWindowSession session))
            return session;

        throw new InvalidOperationException($"{view.name} has not been initialized.");
    }

    /// <summary>
    /// Finds the registered status window.
    /// </summary>
    /// <returns>The registered window, or null when status is closed.</returns>
    private UIWindow FindWindow()
    {
        foreach (UIWindow window in windowManager.Windows)
        {
            if (windowManager.TryGetWindowView(window, out StatusWindowView _))
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
}
