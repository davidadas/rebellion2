using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Owns every Finder window session, transition, projection, and navigation request.
/// </summary>
public sealed class FinderWindowController
{
    private readonly Action<UIWindow> closeWindow;
    private readonly Func<IReadOnlyList<GalaxyMapSector>> getSectors;
    private readonly Dictionary<FinderWindowView, FinderWindowSession> sessions =
        new Dictionary<FinderWindowView, FinderWindowSession>();
    private readonly Func<Vector2Int> getWindowPosition;
    private readonly Func<UIContext> getUIContext;
    private readonly Action markDirty;
    private readonly Func<FinderMode, FinderWindowRow, bool> openTarget;
    private readonly Action<string> playSfx;
    private readonly StrategyWindowLayerView windowLayer;
    private readonly UIWindowManager windowManager;

    /// <summary>
    /// Creates a Finder feature controller.
    /// </summary>
    /// <param name="getUIContext">Returns the current strategy UI context.</param>
    /// <param name="playSfx">Plays one strategy sound-effect path.</param>
    /// <param name="windowLayer">Provides the authored Finder prefab and modal layer.</param>
    /// <param name="windowManager">Owns strategy-window creation, focus, and registration.</param>
    /// <param name="getSectors">Returns the visible sectors in presentation order.</param>
    /// <param name="getWindowPosition">Returns the authored Finder placement.</param>
    /// <param name="openTarget">Opens the destination represented by a Finder result.</param>
    /// <param name="closeWindow">Closes a registered strategy window.</param>
    /// <param name="markDirty">Invalidates strategy presentation after window changes.</param>
    public FinderWindowController(
        Func<UIContext> getUIContext,
        Action<string> playSfx,
        StrategyWindowLayerView windowLayer,
        UIWindowManager windowManager,
        Func<IReadOnlyList<GalaxyMapSector>> getSectors,
        Func<Vector2Int> getWindowPosition,
        Func<FinderMode, FinderWindowRow, bool> openTarget,
        Action<UIWindow> closeWindow,
        Action markDirty
    )
    {
        this.getUIContext = getUIContext ?? throw new ArgumentNullException(nameof(getUIContext));
        this.playSfx = playSfx ?? throw new ArgumentNullException(nameof(playSfx));
        this.windowLayer = windowLayer ?? throw new ArgumentNullException(nameof(windowLayer));
        this.windowManager =
            windowManager ?? throw new ArgumentNullException(nameof(windowManager));
        this.getSectors = getSectors ?? throw new ArgumentNullException(nameof(getSectors));
        this.getWindowPosition =
            getWindowPosition ?? throw new ArgumentNullException(nameof(getWindowPosition));
        this.openTarget = openTarget ?? throw new ArgumentNullException(nameof(openTarget));
        this.closeWindow = closeWindow ?? throw new ArgumentNullException(nameof(closeWindow));
        this.markDirty = markDirty ?? throw new ArgumentNullException(nameof(markDirty));
    }

    /// <summary>
    /// Creates and binds the controller session for one Finder window.
    /// </summary>
    /// <param name="view">The Finder view to bind.</param>
    /// <param name="mode">The Finder category represented by the window.</param>
    public void BindWindow(FinderWindowView view, FinderMode mode)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));

        if (sessions.TryGetValue(view, out FinderWindowSession existingSession))
        {
            if (existingSession.Mode != mode)
            {
                throw new InvalidOperationException(
                    "A bound Finder window cannot change its represented category."
                );
            }

            return;
        }

        UIWindow window = view.GetComponent<UIWindow>();
        if (window == null)
            throw new MissingReferenceException($"{view.name} is missing UIWindow.");

        FinderWindowSession session = new FinderWindowSession(window, mode);
        RefreshSession(session);
        sessions.Add(view, session);
        view.CommandRequested += HandleCommandRequested;
        view.ContextRequested += HandleContextRequested;
        view.Destroyed += HandleViewDestroyed;
        view.FocusRequested += HandleFocusRequested;
        view.RowActivated += HandleRowActivated;
        view.RowSelected += HandleRowSelected;
        view.SearchTextChanged += HandleSearchTextChanged;
        view.TabSelected += HandleTabSelected;
    }

    /// <summary>
    /// Opens or focuses the Finder for a requested category.
    /// </summary>
    /// <param name="mode">The Finder category to display.</param>
    public void Open(FinderMode mode)
    {
        UIWindow existing = FindWindow(mode);
        if (existing != null)
        {
            windowManager.Focus(existing);
            return;
        }

        Vector2Int position = getWindowPosition();
        windowManager.CreateWindow(
            windowLayer.FinderWindowPrefab,
            windowLayer.GetWindowParent(true),
            $"FinderWindow-{mode}",
            position.x,
            position.y,
            windowLayer.GetWindowSize(windowLayer.FinderWindowPrefab),
            true,
            true,
            false,
            false,
            out FinderWindowView view
        );
        BindWindow(view, mode);
        markDirty();
    }

    /// <summary>
    /// Renders every registered Finder window.
    /// </summary>
    public void RenderWindows()
    {
        foreach (UIWindow window in windowManager.Windows)
        {
            if (windowManager.TryGetWindowView(window, out FinderWindowView view))
                RenderWindow(view, window);
        }
    }

    /// <summary>
    /// Gets the Finder category owned by one bound window session.
    /// </summary>
    /// <param name="view">The Finder view whose category is requested.</param>
    /// <returns>The category represented by the bound Finder window.</returns>
    public FinderMode GetMode(FinderWindowView view)
    {
        return GetSession(view).Mode;
    }

    /// <summary>
    /// Gets a read-only snapshot of one bound Finder session.
    /// </summary>
    /// <param name="view">The Finder view whose session is requested.</param>
    /// <returns>The current Finder session state.</returns>
    internal FinderWindowState GetState(FinderWindowView view)
    {
        return GetSession(view).State;
    }

    /// <summary>
    /// Projects current strategy state and renders one Finder window.
    /// </summary>
    /// <param name="view">The destination Finder view.</param>
    /// <param name="window">The window shell supplying placement and focus state.</param>
    public void RenderWindow(FinderWindowView view, UIWindow window)
    {
        if (window == null)
            throw new ArgumentNullException(nameof(window));

        UIContext uiContext = GetRequiredUIContext();
        FinderWindowSession session = GetSession(view);

        view.Render(
            FinderWindowProjector.CreateRenderData(
                uiContext,
                window,
                uiContext.GetPlayerFactionTheme()?.UseUpperButtonLayout == true,
                session,
                session.ProjectedTabs
            )
        );
    }

    /// <summary>
    /// Reconciles every open Finder session with the current visible strategy snapshot.
    /// </summary>
    public void ReconcileWindows()
    {
        foreach (FinderWindowSession session in sessions.Values)
            RefreshSession(session);
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

    /// <summary>
    /// Applies one semantic Finder command to its controller-owned session or window action.
    /// </summary>
    /// <param name="view">The Finder view that emitted the command.</param>
    /// <param name="command">The semantic command selected by the user.</param>
    private void HandleCommandRequested(FinderWindowView view, FinderWindowCommand command)
    {
        FinderWindowSession session = GetSession(view);
        playSfx(StrategyUISoundPaths.ControlPress);
        switch (command)
        {
            case FinderWindowCommand.Close:
                session.Window.RequestButton(StrategyWindowButtonActions.CloseWindow);
                return;
            case FinderWindowCommand.Target:
                OpenSelectedRow(session);
                return;
            case FinderWindowCommand.ShowShips:
            case FinderWindowCommand.ShowSpecialForces:
                session.SelectPanel(true);
                break;
            case FinderWindowCommand.ShowFleets:
            case FinderWindowCommand.ShowPersonnel:
                session.SelectPanel(false);
                break;
            default:
                return;
        }

        RefreshSession(session);
        markDirty();
    }

    /// <summary>
    /// Routes a context-menu request to the strategy input owner.
    /// </summary>
    /// <param name="view">The requesting Finder view.</param>
    /// <param name="rowId">The stable row that received the context gesture.</param>
    /// <param name="eventData">The pointer event that requested the context menu.</param>
    private void HandleContextRequested(
        FinderWindowView view,
        string rowId,
        PointerEventData eventData
    )
    {
        GetSession(view).Window.RequestContext(eventData);
    }

    /// <summary>
    /// Routes a focus request to the strategy window owner.
    /// </summary>
    /// <param name="view">The requesting Finder view.</param>
    private void HandleFocusRequested(FinderWindowView view)
    {
        GetSession(view).Window.RequestFocus();
    }

    /// <summary>
    /// Selects and opens one projected Finder result.
    /// </summary>
    /// <param name="view">The Finder view that emitted the activation.</param>
    /// <param name="rowId">The activated row identifier.</param>
    private void HandleRowActivated(FinderWindowView view, string rowId)
    {
        FinderWindowSession session = GetSession(view);
        session.SelectRow(rowId);
        OpenSelectedRow(session);
    }

    /// <summary>
    /// Selects one projected Finder result and invalidates presentation.
    /// </summary>
    /// <param name="view">The Finder view that emitted the selection.</param>
    /// <param name="rowId">The selected row identifier.</param>
    private void HandleRowSelected(FinderWindowView view, string rowId)
    {
        GetSession(view).SelectRow(rowId);
        markDirty();
    }

    /// <summary>
    /// Applies a new name filter and clears the incompatible row selection.
    /// </summary>
    /// <param name="view">The Finder view that emitted the filter.</param>
    /// <param name="searchText">The new case-insensitive name filter.</param>
    private void HandleSearchTextChanged(FinderWindowView view, string searchText)
    {
        FinderWindowSession session = GetSession(view);
        session.SetSearchText(searchText);
        RefreshSession(session);
        markDirty();
    }

    /// <summary>
    /// Applies an authored tab selection and clears incompatible session state.
    /// </summary>
    /// <param name="view">The Finder view that emitted the tab selection.</param>
    /// <param name="tabIndex">The selected authored tab slot.</param>
    private void HandleTabSelected(FinderWindowView view, int tabIndex)
    {
        FinderWindowSession session = GetSession(view);
        session.SelectTab(tabIndex);
        RefreshSession(session);
        markDirty();
    }

    /// <summary>
    /// Refreshes one session's tabs and filtered rows from the current strategy snapshot.
    /// </summary>
    /// <param name="session">The Finder session to refresh.</param>
    private void RefreshSession(FinderWindowSession session)
    {
        if (session == null)
            throw new ArgumentNullException(nameof(session));

        UIContext uiContext = GetRequiredUIContext();
        IReadOnlyList<GalaxyMapSector> sectors =
            getSectors() ?? throw new InvalidOperationException("Finder sectors are unavailable.");
        FinderWindowRowBuilder builder = new FinderWindowRowBuilder(
            sectors,
            uiContext.Game?.GetFactions(),
            uiContext.GetPlayerFactionInstanceID()
        );
        List<FinderWindowTab> tabs = builder.GetTabs(session.Mode);
        session.ReconcileTabCount(tabs.Count);
        FinderWindowTab activeTab = GetItem(tabs, session.ActiveTab);
        List<FinderWindowRow> rows = FinderWindowProjector.FilterRows(
            builder.GetRows(session.Mode, session.Panel, activeTab),
            session.SearchText
        );
        session.SetProjection(tabs, rows);
    }

    /// <summary>
    /// Opens the selected projected row and refreshes strategy presentation.
    /// </summary>
    /// <param name="session">The session that owns the current selection and projection.</param>
    private void OpenSelectedRow(FinderWindowSession session)
    {
        FinderWindowRow row = session.SelectedRow;
        if (row == null)
            return;

        if (openTarget(session.Mode, row))
            closeWindow(session.Window);
        markDirty();
    }

    /// <summary>
    /// Releases controller subscriptions and state for a destroyed Finder view.
    /// </summary>
    /// <param name="view">The destroyed Finder view.</param>
    private void HandleViewDestroyed(FinderWindowView view)
    {
        if (ReferenceEquals(view, null) || !sessions.Remove(view))
            return;

        view.CommandRequested -= HandleCommandRequested;
        view.ContextRequested -= HandleContextRequested;
        view.Destroyed -= HandleViewDestroyed;
        view.FocusRequested -= HandleFocusRequested;
        view.RowActivated -= HandleRowActivated;
        view.RowSelected -= HandleRowSelected;
        view.SearchTextChanged -= HandleSearchTextChanged;
        view.TabSelected -= HandleTabSelected;
    }

    /// <summary>
    /// Gets the controller session associated with one Finder view.
    /// </summary>
    /// <param name="view">The bound Finder view.</param>
    /// <returns>The session owned by the supplied view.</returns>
    private FinderWindowSession GetSession(FinderWindowView view)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));
        if (sessions.TryGetValue(view, out FinderWindowSession session))
            return session;

        throw new InvalidOperationException("The Finder view has not been bound to a session.");
    }

    /// <summary>
    /// Finds the registered Finder window for one category.
    /// </summary>
    /// <param name="mode">The Finder category to locate.</param>
    /// <returns>The registered window, or null when the category is closed.</returns>
    private UIWindow FindWindow(FinderMode mode)
    {
        foreach (UIWindow window in windowManager.Windows)
        {
            if (
                windowManager.TryGetWindowView(window, out FinderWindowView view)
                && GetMode(view) == mode
            )
            {
                return window;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the current UI context or rejects incomplete composition.
    /// </summary>
    /// <returns>The current strategy UI context.</returns>
    private UIContext GetRequiredUIContext()
    {
        return getUIContext()
            ?? throw new InvalidOperationException("Finder UI context is unavailable.");
    }
}

/// <summary>
/// Owns the mutable interaction and projection state for one Finder window.
/// </summary>
internal sealed class FinderWindowSession
{
    private int activeTab;
    private bool panel;
    private int selectedIndex = -1;
    private string selectedRowId;
    private string searchText = string.Empty;

    /// <summary>
    /// Creates the default session for one Finder category.
    /// </summary>
    /// <param name="window">The owning Finder window.</param>
    /// <param name="mode">The Finder category represented by the session.</param>
    public FinderWindowSession(UIWindow window, FinderMode mode)
    {
        Window = window ?? throw new ArgumentNullException(nameof(window));
        Mode = mode;
        ProjectedTabs = Array.Empty<FinderWindowTab>();
        ProjectedRows = Array.Empty<FinderWindowRow>();
    }

    public int ActiveTab => activeTab;

    public FinderMode Mode { get; }

    public bool Panel => panel;

    public IReadOnlyList<FinderWindowTab> ProjectedTabs { get; private set; }

    public IReadOnlyList<FinderWindowRow> ProjectedRows { get; private set; }

    public string SearchText => searchText;

    public int SelectedIndex => selectedIndex;

    public FinderWindowRow SelectedRow =>
        selectedIndex >= 0 && selectedIndex < ProjectedRows.Count
            ? ProjectedRows[selectedIndex]
            : null;

    public FinderWindowState State =>
        new FinderWindowState(Mode, panel, activeTab, selectedIndex, searchText);

    public UIWindow Window { get; }

    /// <summary>
    /// Reconciles the selected tab against the current semantic tab catalog.
    /// </summary>
    /// <param name="count">The number of projected tabs.</param>
    public void ReconcileTabCount(int count)
    {
        activeTab = ClampIndex(activeTab, count, true);
    }

    /// <summary>
    /// Replaces the current tab and row projections and reconciles row selection.
    /// </summary>
    /// <param name="tabs">The semantic tabs in display order.</param>
    /// <param name="rows">The projected rows in visible order.</param>
    public void SetProjection(
        IReadOnlyList<FinderWindowTab> tabs,
        IReadOnlyList<FinderWindowRow> rows
    )
    {
        ProjectedTabs = Copy(tabs);
        ProjectedRows = FinderWindowRenderData.Copy(rows);
        selectedIndex = FindRowIndex(ProjectedRows, selectedRowId);
        if (selectedIndex < 0)
            selectedRowId = null;
    }

    /// <summary>
    /// Applies a new case-insensitive row-name filter.
    /// </summary>
    /// <param name="value">The new filter value.</param>
    public void SetSearchText(string value)
    {
        searchText = value ?? string.Empty;
        ClearSelection();
    }

    /// <summary>
    /// Selects one alternate Finder panel and resets incompatible state.
    /// </summary>
    /// <param name="value">Whether the alternate panel is active.</param>
    public void SelectPanel(bool value)
    {
        panel = value;
        ClearSelection();
        searchText = string.Empty;
    }

    /// <summary>
    /// Selects one visible result row.
    /// </summary>
    /// <param name="rowId">The stable row identifier.</param>
    public void SelectRow(string rowId)
    {
        selectedIndex = FindRowIndex(ProjectedRows, rowId);
        selectedRowId = selectedIndex >= 0 ? ProjectedRows[selectedIndex].Identity : null;
    }

    /// <summary>
    /// Selects one authored tab and resets state only when the tab changed.
    /// </summary>
    /// <param name="tabIndex">The selected authored tab slot.</param>
    public void SelectTab(int tabIndex)
    {
        if (tabIndex == activeTab)
            return;

        activeTab = tabIndex;
        ClearSelection();
        searchText = string.Empty;
    }

    /// <summary>
    /// Clears the current row selection.
    /// </summary>
    private void ClearSelection()
    {
        selectedIndex = -1;
        selectedRowId = null;
    }

    /// <summary>
    /// Finds one stable row identifier in a projected result collection.
    /// </summary>
    /// <param name="rows">The projected rows.</param>
    /// <param name="rowId">The row identifier.</param>
    /// <returns>The visible row index, or negative one.</returns>
    private static int FindRowIndex(IReadOnlyList<FinderWindowRow> rows, string rowId)
    {
        if (rows == null || string.IsNullOrEmpty(rowId))
            return -1;

        for (int index = 0; index < rows.Count; index++)
        {
            if (string.Equals(rows[index]?.Identity, rowId, StringComparison.Ordinal))
                return index;
        }

        return -1;
    }

    /// <summary>
    /// Copies an optional source list into immutable session storage.
    /// </summary>
    /// <typeparam name="T">The source item type.</typeparam>
    /// <param name="items">The source items.</param>
    /// <returns>The copied read-only list.</returns>
    private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> items)
    {
        return new List<T>(items ?? Array.Empty<T>()).AsReadOnly();
    }

    /// <summary>
    /// Clamps an index against a projected item count.
    /// </summary>
    /// <param name="index">The current index.</param>
    /// <param name="count">The projected item count.</param>
    /// <param name="selectFirstWhenEmpty">Whether an empty selection should choose the first item.</param>
    /// <returns>The reconciled index.</returns>
    private static int ClampIndex(int index, int count, bool selectFirstWhenEmpty)
    {
        if (count <= 0)
            return -1;
        if (index >= count)
            return count - 1;
        if (index < 0 && selectFirstWhenEmpty)
            return 0;

        return index;
    }
}
