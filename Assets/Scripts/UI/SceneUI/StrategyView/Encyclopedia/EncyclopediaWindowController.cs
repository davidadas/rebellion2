using System;
using System.Collections.Generic;
using Rebellion.Game.Encyclopedia;
using Rebellion.Game.Missions;
using Rebellion.Game.Research;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Owns every Encyclopedia window session, transition, projection, and window request.
/// </summary>
public sealed class EncyclopediaWindowController
{
    private const string _fleetEntryTypeId = "FLEET";

    private readonly Dictionary<EncyclopediaWindowView, EncyclopediaWindowSession> sessions =
        new Dictionary<EncyclopediaWindowView, EncyclopediaWindowSession>();
    private readonly Func<Vector2Int> getWindowPosition;
    private readonly Func<UIContext> getUIContext;
    private readonly Action markDirty;
    private readonly Action<string> playSfx;
    private readonly StrategyWindowLayerView windowLayer;
    private readonly UIWindowManager windowManager;

    /// <summary>
    /// Creates the Encyclopedia feature controller.
    /// </summary>
    /// <param name="getUIContext">Returns the current strategy UI context.</param>
    /// <param name="playSfx">Plays a strategy sound-effect path.</param>
    /// <param name="windowLayer">Provides the authored Encyclopedia prefab and modal layer.</param>
    /// <param name="windowManager">Owns strategy-window creation, focus, and registration.</param>
    /// <param name="getWindowPosition">Returns the authored Encyclopedia placement.</param>
    /// <param name="markDirty">Invalidates strategy presentation after window changes.</param>
    public EncyclopediaWindowController(
        Func<UIContext> getUIContext,
        Action<string> playSfx,
        StrategyWindowLayerView windowLayer,
        UIWindowManager windowManager,
        Func<Vector2Int> getWindowPosition,
        Action markDirty
    )
    {
        this.getUIContext = getUIContext ?? throw new ArgumentNullException(nameof(getUIContext));
        this.playSfx = playSfx ?? throw new ArgumentNullException(nameof(playSfx));
        this.windowLayer = windowLayer ?? throw new ArgumentNullException(nameof(windowLayer));
        this.windowManager =
            windowManager ?? throw new ArgumentNullException(nameof(windowManager));
        this.getWindowPosition =
            getWindowPosition ?? throw new ArgumentNullException(nameof(getWindowPosition));
        this.markDirty = markDirty ?? throw new ArgumentNullException(nameof(markDirty));
    }

    /// <summary>
    /// Creates and binds the controller session for one Encyclopedia window.
    /// </summary>
    /// <param name="view">The Encyclopedia view to bind.</param>
    public void BindWindow(EncyclopediaWindowView view)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));

        if (sessions.ContainsKey(view))
            return;

        UIWindow window = view.GetComponent<UIWindow>();
        if (window == null)
            throw new MissingReferenceException($"{view.name} is missing UIWindow.");

        EncyclopediaWindowSession session = new EncyclopediaWindowSession(window);
        RefreshSession(session);
        sessions.Add(view, session);
        view.CommandRequested += HandleCommandRequested;
        view.ContextRequested += HandleContextRequested;
        view.Destroyed += HandleViewDestroyed;
        view.FocusRequested += HandleFocusRequested;
        view.NextRequested += HandleNextRequested;
        view.PreviousRequested += HandlePreviousRequested;
        view.RowActivated += HandleRowActivated;
        view.RowSelected += HandleRowSelected;
        view.SearchTextChanged += HandleSearchTextChanged;
        view.TabSelected += HandleTabSelected;
    }

    /// <summary>
    /// Opens or focuses the Encyclopedia at its current session state.
    /// </summary>
    public void Open()
    {
        UIWindow existing = FindWindow();
        if (existing != null)
        {
            windowManager.Focus(existing);
            return;
        }

        EncyclopediaWindowView view = CreateWindow();
        BindWindow(view);
        markDirty();
    }

    /// <summary>
    /// Opens or focuses the Encyclopedia on one scene-graph entry.
    /// </summary>
    /// <param name="target">The scene-graph entry to display.</param>
    public void Open(ISceneNode target)
    {
        if (target == null)
        {
            Open();
            return;
        }

        UIWindow existing = FindWindow();
        if (existing != null)
        {
            if (windowManager.TryGetWindowView(existing, out EncyclopediaWindowView existingView))
                RequestEntry(existingView, target);
            windowManager.Focus(existing);
            markDirty();
            return;
        }

        EncyclopediaWindowView view = CreateWindow();
        BindWindow(view);
        RequestEntry(view, target);
        markDirty();
    }

    /// <summary>
    /// Renders every registered Encyclopedia window.
    /// </summary>
    public void RenderWindows()
    {
        foreach (UIWindow window in windowManager.Windows)
        {
            if (windowManager.TryGetWindowView(window, out EncyclopediaWindowView view))
                RenderWindow(view, window);
        }
    }

    /// <summary>
    /// Gets a read-only snapshot of one bound Encyclopedia session.
    /// </summary>
    /// <param name="view">The Encyclopedia view whose session is requested.</param>
    /// <returns>The current Encyclopedia session state.</returns>
    internal EncyclopediaWindowState GetState(EncyclopediaWindowView view)
    {
        return GetSession(view).State;
    }

    /// <summary>
    /// Projects the current Encyclopedia state and renders its view.
    /// </summary>
    /// <param name="view">The destination Encyclopedia view.</param>
    /// <param name="window">The window shell supplying position, size, and focus state.</param>
    public void RenderWindow(EncyclopediaWindowView view, UIWindow window)
    {
        if (window == null)
            throw new ArgumentNullException(nameof(window));

        EncyclopediaWindowSession session = GetSession(view);
        UIContext uiContext = GetRequiredUIContext();
        view.Render(
            EncyclopediaWindowProjector.CreateRenderData(
                uiContext,
                window,
                uiContext.GetPlayerFactionTheme()?.UseUpperButtonLayout == true,
                session
            )
        );
    }

    /// <summary>
    /// Opens the Encyclopedia entry represented by a strategy scene node.
    /// </summary>
    /// <param name="view">The Encyclopedia view that should display the entry.</param>
    /// <param name="target">The strategy node represented by the entry.</param>
    public void RequestEntry(EncyclopediaWindowView view, ISceneNode target)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));
        if (target == null)
            return;

        UIContext uiContext = GetRequiredUIContext();
        List<EncyclopediaEntry> entries = uiContext.EncyclopediaCatalog.GetRows(
            null,
            uiContext.GetPlayerFactionInstanceID()
        );
        int entryIndex = FindEntryIndex(entries, GetEntryTypeID(target));
        EncyclopediaWindowSession session = GetSession(view);
        session.OpenEntry(
            entryIndex >= 0 && entryIndex < entries.Count ? entries[entryIndex] : null
        );
        RefreshSession(session);
        markDirty();
    }

    /// <summary>
    /// Finds an Encyclopedia entry index by exact type identifier.
    /// </summary>
    /// <param name="entries">The entries to inspect.</param>
    /// <param name="typeId">The requested entry type identifier.</param>
    /// <returns>The matching entry index, or -1 when none exists.</returns>
    internal static int FindEntryIndex(IReadOnlyList<EncyclopediaEntry> entries, string typeId)
    {
        if (entries == null || string.IsNullOrEmpty(typeId))
            return -1;

        for (int i = 0; i < entries.Count; i++)
        {
            if (string.Equals(entries[i]?.TypeID, typeId, StringComparison.Ordinal))
                return i;
        }

        return -1;
    }

    /// <summary>
    /// Resolves the Encyclopedia type identifier represented by a strategy scene node.
    /// </summary>
    /// <param name="target">The strategy node to translate.</param>
    /// <returns>The catalog type identifier associated with the node.</returns>
    internal static string GetEntryTypeID(ISceneNode target)
    {
        if (target == null)
            return null;

        if (target is ResearchMission researchMission)
        {
            return researchMission.Discipline switch
            {
                ResearchDiscipline.ShipDesign => MissionIconKeys.ResearchShipDesign,
                ResearchDiscipline.FacilityDesign => MissionIconKeys.ResearchFacilityDesign,
                ResearchDiscipline.TroopTraining => MissionIconKeys.ResearchTroopTraining,
                _ => target.GetTypeID(),
            };
        }

        if (target is Fleet)
            return _fleetEntryTypeId;

        return target.GetTypeID();
    }

    /// <summary>
    /// Applies one semantic Encyclopedia command.
    /// </summary>
    /// <param name="view">The Encyclopedia view that emitted the command.</param>
    /// <param name="command">The semantic command selected by the user.</param>
    private void HandleCommandRequested(
        EncyclopediaWindowView view,
        EncyclopediaWindowCommand command
    )
    {
        EncyclopediaWindowSession session = GetSession(view);
        playSfx(StrategyUISoundPaths.ControlPress);
        switch (command)
        {
            case EncyclopediaWindowCommand.Close:
                session.Window.RequestButton(StrategyWindowButtonActions.CloseWindow);
                return;
            case EncyclopediaWindowCommand.ShowTopic:
                session.ShowTopic();
                break;
            case EncyclopediaWindowCommand.ShowIndex:
                session.ShowIndex();
                break;
            default:
                return;
        }

        markDirty();
    }

    /// <summary>
    /// Routes an index-row context request to the strategy input owner.
    /// </summary>
    /// <param name="view">The Encyclopedia view requesting a context menu.</param>
    /// <param name="entryTypeId">The catalog entry that received the context gesture.</param>
    /// <param name="eventData">The pointer event that requested the context menu.</param>
    private void HandleContextRequested(
        EncyclopediaWindowView view,
        string entryTypeId,
        PointerEventData eventData
    )
    {
        GetSession(view).Window.RequestContext(eventData);
    }

    /// <summary>
    /// Routes a focus request to the strategy window owner.
    /// </summary>
    /// <param name="view">The Encyclopedia view requesting focus.</param>
    private void HandleFocusRequested(EncyclopediaWindowView view)
    {
        GetSession(view).Window.RequestFocus();
    }

    /// <summary>
    /// Moves selection to the next projected entry when available.
    /// </summary>
    /// <param name="view">The Encyclopedia view requesting navigation.</param>
    private void HandleNextRequested(EncyclopediaWindowView view)
    {
        if (GetSession(view).MoveSelection(1))
            markDirty();
    }

    /// <summary>
    /// Moves selection to the previous projected entry when available.
    /// </summary>
    /// <param name="view">The Encyclopedia view requesting navigation.</param>
    private void HandlePreviousRequested(EncyclopediaWindowView view)
    {
        if (GetSession(view).MoveSelection(-1))
            markDirty();
    }

    /// <summary>
    /// Selects and opens one projected Encyclopedia entry.
    /// </summary>
    /// <param name="view">The Encyclopedia view that emitted the activation.</param>
    /// <param name="entryTypeId">The activated catalog entry identifier.</param>
    private void HandleRowActivated(EncyclopediaWindowView view, string entryTypeId)
    {
        GetSession(view).ActivateRow(entryTypeId);
        markDirty();
    }

    /// <summary>
    /// Selects one projected Encyclopedia entry.
    /// </summary>
    /// <param name="view">The Encyclopedia view that emitted the selection.</param>
    /// <param name="entryTypeId">The selected catalog entry identifier.</param>
    private void HandleRowSelected(EncyclopediaWindowView view, string entryTypeId)
    {
        GetSession(view).SelectRow(entryTypeId);
        markDirty();
    }

    /// <summary>
    /// Applies a new entry-name filter and returns the session to its index.
    /// </summary>
    /// <param name="view">The Encyclopedia view that emitted the filter.</param>
    /// <param name="searchText">The new case-insensitive entry-name filter.</param>
    private void HandleSearchTextChanged(EncyclopediaWindowView view, string searchText)
    {
        EncyclopediaWindowSession session = GetSession(view);
        session.SetSearchText(searchText);
        RefreshSession(session);
        markDirty();
    }

    /// <summary>
    /// Applies a semantic database-tab selection.
    /// </summary>
    /// <param name="view">The Encyclopedia view that emitted the selection.</param>
    /// <param name="tab">The selected semantic database tab.</param>
    private void HandleTabSelected(EncyclopediaWindowView view, EncyclopediaWindowTab tab)
    {
        EncyclopediaWindowSession session = GetSession(view);
        session.SelectTab(tab);
        RefreshSession(session);
        markDirty();
    }

    /// <summary>
    /// Refreshes one session's visible catalog entries from its active tab and search state.
    /// </summary>
    /// <param name="session">The Encyclopedia session to refresh.</param>
    private void RefreshSession(EncyclopediaWindowSession session)
    {
        if (session == null)
            throw new ArgumentNullException(nameof(session));

        UIContext uiContext = GetRequiredUIContext();
        session.SetProjectedEntries(
            EncyclopediaWindowProjector.GetVisibleEntries(
                uiContext.EncyclopediaCatalog,
                uiContext.GetPlayerFactionInstanceID(),
                session.State
            )
        );
    }

    /// <summary>
    /// Releases controller subscriptions and state for a destroyed Encyclopedia view.
    /// </summary>
    /// <param name="view">The destroyed view.</param>
    private void HandleViewDestroyed(EncyclopediaWindowView view)
    {
        if (ReferenceEquals(view, null) || !sessions.Remove(view))
            return;

        view.CommandRequested -= HandleCommandRequested;
        view.ContextRequested -= HandleContextRequested;
        view.Destroyed -= HandleViewDestroyed;
        view.FocusRequested -= HandleFocusRequested;
        view.NextRequested -= HandleNextRequested;
        view.PreviousRequested -= HandlePreviousRequested;
        view.RowActivated -= HandleRowActivated;
        view.RowSelected -= HandleRowSelected;
        view.SearchTextChanged -= HandleSearchTextChanged;
        view.TabSelected -= HandleTabSelected;
    }

    /// <summary>
    /// Gets the controller session associated with one Encyclopedia view.
    /// </summary>
    /// <param name="view">The bound Encyclopedia view.</param>
    /// <returns>The session owned by the supplied view.</returns>
    private EncyclopediaWindowSession GetSession(EncyclopediaWindowView view)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));
        if (sessions.TryGetValue(view, out EncyclopediaWindowSession session))
            return session;

        throw new InvalidOperationException(
            "The Encyclopedia view has not been bound to a session."
        );
    }

    /// <summary>
    /// Creates one authored Encyclopedia window at its configured placement.
    /// </summary>
    /// <returns>The created Encyclopedia view.</returns>
    private EncyclopediaWindowView CreateWindow()
    {
        Vector2Int position = getWindowPosition();
        windowManager.CreateWindow(
            windowLayer.EncyclopediaWindowPrefab,
            windowLayer.GetWindowParent(true),
            "EncyclopediaWindow",
            position.x,
            position.y,
            windowLayer.GetWindowSize(windowLayer.EncyclopediaWindowPrefab),
            true,
            true,
            false,
            false,
            out EncyclopediaWindowView view
        );
        return view;
    }

    /// <summary>
    /// Finds the registered Encyclopedia window.
    /// </summary>
    /// <returns>The registered window, or null when Encyclopedia is closed.</returns>
    private UIWindow FindWindow()
    {
        foreach (UIWindow window in windowManager.Windows)
        {
            if (windowManager.TryGetWindowView(window, out EncyclopediaWindowView _))
                return window;
        }

        return null;
    }

    /// <summary>
    /// Returns the current UI context or fails when strategy composition is incomplete.
    /// </summary>
    /// <returns>The current strategy UI context.</returns>
    private UIContext GetRequiredUIContext()
    {
        return getUIContext()
            ?? throw new InvalidOperationException("Encyclopedia UI context is unavailable.");
    }
}

/// <summary>
/// Owns the mutable interaction and projection state for one Encyclopedia window.
/// </summary>
internal sealed class EncyclopediaWindowSession
{
    private EncyclopediaWindowTab activeTab = EncyclopediaWindowTab.AllDatabases;
    private bool panel;
    private IReadOnlyList<EncyclopediaEntry> projectedEntries = Array.Empty<EncyclopediaEntry>();
    private int selectedIndex = -1;
    private string selectedTypeId;
    private string searchText = string.Empty;

    /// <summary>
    /// Creates one Encyclopedia window session.
    /// </summary>
    /// <param name="window">The owning Encyclopedia window.</param>
    public EncyclopediaWindowSession(UIWindow window)
    {
        Window = window ?? throw new ArgumentNullException(nameof(window));
    }

    public EncyclopediaWindowTab ActiveTab => activeTab;

    public bool Panel => panel;

    public IReadOnlyList<EncyclopediaEntry> ProjectedEntries => projectedEntries;

    public string SearchText => searchText;

    public int SelectedIndex => selectedIndex;

    public string SelectedTypeId => selectedTypeId;

    public UIWindow Window { get; }

    public EncyclopediaWindowState State =>
        new EncyclopediaWindowState(panel, activeTab, selectedIndex, searchText);

    /// <summary>
    /// Replaces the current entry projection and reconciles selection and panel state.
    /// </summary>
    /// <param name="entries">The projected entries in visible order.</param>
    public void SetProjectedEntries(IReadOnlyList<EncyclopediaEntry> entries)
    {
        projectedEntries = EncyclopediaWindowRenderData.Copy(entries);
        if (projectedEntries.Count == 0)
        {
            panel = false;
            ClearSelection();
            return;
        }

        selectedIndex = FindEntryIndex(projectedEntries, selectedTypeId);
        if (selectedIndex < 0)
            selectedTypeId = null;
        if (panel && selectedIndex < 0)
            SelectEntry(projectedEntries[0]);
    }

    /// <summary>
    /// Resets the session to one requested entry from the complete database.
    /// </summary>
    /// <param name="entry">The requested entry, or null when unavailable.</param>
    public void OpenEntry(EncyclopediaEntry entry)
    {
        activeTab = EncyclopediaWindowTab.AllDatabases;
        searchText = string.Empty;
        SelectEntry(entry);
        panel = entry != null;
    }

    /// <summary>
    /// Applies a new entry-name filter and returns to the index panel.
    /// </summary>
    /// <param name="value">The new entry-name filter.</param>
    public void SetSearchText(string value)
    {
        searchText = value ?? string.Empty;
        ClearSelection();
        panel = false;
    }

    /// <summary>
    /// Selects a semantic database tab and clears incompatible state when it changed.
    /// </summary>
    /// <param name="tab">The selected semantic database tab.</param>
    public void SelectTab(EncyclopediaWindowTab tab)
    {
        if (tab == activeTab)
            return;

        activeTab = tab;
        ClearSelection();
        panel = false;
        searchText = string.Empty;
    }

    /// <summary>
    /// Selects one visible index row.
    /// </summary>
    /// <param name="entryTypeId">The catalog entry identifier.</param>
    public void SelectRow(string entryTypeId)
    {
        int rowIndex = FindEntryIndex(projectedEntries, entryTypeId);
        SelectEntry(rowIndex >= 0 ? projectedEntries[rowIndex] : null);
    }

    /// <summary>
    /// Selects one visible row and opens its topic panel.
    /// </summary>
    /// <param name="entryTypeId">The catalog entry identifier.</param>
    public void ActivateRow(string entryTypeId)
    {
        SelectRow(entryTypeId);
        panel = selectedIndex >= 0;
    }

    /// <summary>
    /// Displays the current topic panel.
    /// </summary>
    public void ShowTopic()
    {
        panel = true;
    }

    /// <summary>
    /// Displays the database index panel.
    /// </summary>
    public void ShowIndex()
    {
        panel = false;
    }

    /// <summary>
    /// Moves primary selection within the current projected entry range.
    /// </summary>
    /// <param name="direction">The signed selection offset.</param>
    /// <returns>True when primary selection changed.</returns>
    public bool MoveSelection(int direction)
    {
        int nextIndex = SelectableListSelection.GetMovedIndex(
            selectedIndex,
            projectedEntries.Count,
            direction
        );
        if (nextIndex == selectedIndex)
            return false;

        SelectEntry(nextIndex >= 0 ? projectedEntries[nextIndex] : null);
        return true;
    }

    /// <summary>
    /// Selects one projected catalog entry and derives its current visible index.
    /// </summary>
    /// <param name="entry">The entry to select.</param>
    private void SelectEntry(EncyclopediaEntry entry)
    {
        selectedTypeId = entry?.TypeID;
        selectedIndex = FindEntryIndex(projectedEntries, selectedTypeId);
    }

    /// <summary>
    /// Clears the selected catalog entry.
    /// </summary>
    private void ClearSelection()
    {
        selectedIndex = -1;
        selectedTypeId = null;
    }

    /// <summary>
    /// Finds one catalog identifier in a projected entry collection.
    /// </summary>
    /// <param name="entries">The projected entries.</param>
    /// <param name="typeId">The catalog entry identifier.</param>
    /// <returns>The visible entry index, or negative one.</returns>
    private static int FindEntryIndex(IReadOnlyList<EncyclopediaEntry> entries, string typeId)
    {
        if (entries == null || string.IsNullOrEmpty(typeId))
            return -1;

        for (int index = 0; index < entries.Count; index++)
        {
            if (string.Equals(entries[index]?.TypeID, typeId, StringComparison.Ordinal))
                return index;
        }

        return -1;
    }
}
