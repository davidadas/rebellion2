using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using UnityEngine;

/// <summary>
/// Performs window-level actions requested by the strategy confirmation feature.
/// </summary>
public interface IConfirmDialogWindowActions
{
    /// <summary>
    /// Clears the source selection and rebuilds strategy state after an accepted mutation.
    /// </summary>
    /// <param name="sourceWindow">The window that originated the confirmed action.</param>
    void RefreshAfterConfirmedAction(UIWindow sourceWindow);
}

/// <summary>
/// Owns confirmation sessions, presentation projection, audio, and semantic choice routing.
/// </summary>
public sealed class ConfirmDialogWindowController
{
    private const string _moveTransitLabel = "Transit Time in Days";
    private const string _retirePrompt = "Retire these personnel?";
    private const string _scrapPrompt = "Scrap these units?";
    private const string _stopConstructionPrompt =
        "Are you sure you want to stop construction of the following?";

    private readonly StrategyConfirmActionController actionController;
    private readonly HashSet<ConfirmDialogWindowView> boundViews =
        new HashSet<ConfirmDialogWindowView>();
    private readonly Action<UIWindow> closeWindow;
    private readonly Func<Vector2Int> getWindowPosition;
    private readonly Func<UIContext> getUIContext;
    private readonly Action markDirty;
    private readonly Action<string> playSfx;
    private readonly Dictionary<ConfirmDialogWindowView, ConfirmDialogSession> sessions =
        new Dictionary<ConfirmDialogWindowView, ConfirmDialogSession>();
    private readonly StrategyWindowLayerView windowLayer;
    private readonly UIWindowManager windowManager;
    private IConfirmDialogWindowActions actions;

    /// <summary>
    /// Creates the confirmation feature controller.
    /// </summary>
    /// <param name="actionController">Executes accepted game mutations.</param>
    /// <param name="getUIContext">Returns the current strategy presentation context.</param>
    /// <param name="playSfx">Plays a configured strategy sound path.</param>
    /// <param name="windowLayer">Provides the authored confirmation prefab and modal layer.</param>
    /// <param name="windowManager">Owns strategy-window creation and registration.</param>
    /// <param name="getWindowPosition">Returns the authored confirmation placement.</param>
    /// <param name="closeWindow">Closes a registered strategy window.</param>
    /// <param name="markDirty">Invalidates strategy presentation after window changes.</param>
    public ConfirmDialogWindowController(
        StrategyConfirmActionController actionController,
        Func<UIContext> getUIContext,
        Action<string> playSfx,
        StrategyWindowLayerView windowLayer,
        UIWindowManager windowManager,
        Func<Vector2Int> getWindowPosition,
        Action<UIWindow> closeWindow,
        Action markDirty
    )
    {
        this.actionController =
            actionController ?? throw new ArgumentNullException(nameof(actionController));
        this.getUIContext = getUIContext ?? throw new ArgumentNullException(nameof(getUIContext));
        this.playSfx = playSfx ?? throw new ArgumentNullException(nameof(playSfx));
        this.windowLayer = windowLayer ?? throw new ArgumentNullException(nameof(windowLayer));
        this.windowManager =
            windowManager ?? throw new ArgumentNullException(nameof(windowManager));
        this.getWindowPosition =
            getWindowPosition ?? throw new ArgumentNullException(nameof(getWindowPosition));
        this.closeWindow = closeWindow ?? throw new ArgumentNullException(nameof(closeWindow));
        this.markDirty = markDirty ?? throw new ArgumentNullException(nameof(markDirty));
    }

    /// <summary>
    /// Supplies owning window actions after the strategy controller graph is constructed.
    /// </summary>
    /// <param name="windowActions">The feature-specific confirmation actions.</param>
    public void Initialize(IConfirmDialogWindowActions windowActions)
    {
        actions = windowActions ?? throw new ArgumentNullException(nameof(windowActions));
    }

    /// <summary>
    /// Subscribes the controller to one confirmation view exactly once.
    /// </summary>
    /// <param name="view">The confirmation view to bind.</param>
    public void BindWindow(ConfirmDialogWindowView view)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));
        EnsureInitialized();
        if (!boundViews.Add(view))
            return;

        view.ChoiceRequested += HandleChoiceRequested;
        view.Destroyed += HandleViewDestroyed;
    }

    /// <summary>
    /// Starts a scrap confirmation session for a non-empty selection.
    /// </summary>
    /// <param name="view">The destination confirmation view.</param>
    /// <param name="sourceWindow">The originating strategy window.</param>
    /// <param name="sourceItems">The units selected for scrapping.</param>
    /// <returns>True when a valid session was created.</returns>
    public bool TryInitializeScrapConfirmWindow(
        ConfirmDialogWindowView view,
        UIWindow sourceWindow,
        IReadOnlyList<ISceneNode> sourceItems
    )
    {
        List<ISceneNode> items = CopyItems(sourceItems);
        UIWindow dialogWindow = view == null ? null : view.GetComponent<UIWindow>();
        if (view == null || dialogWindow == null || sourceWindow == null || items.Count == 0)
            return false;

        SetSession(
            view,
            new ConfirmDialogSession(
                dialogWindow,
                sourceWindow,
                ConfirmDialogKind.Scrap,
                items,
                null,
                -1
            )
        );
        PlayPromptSound(ConfirmDialogKind.Scrap);
        return true;
    }

    /// <summary>
    /// Starts a retirement confirmation session for an eligible personnel selection.
    /// </summary>
    /// <param name="view">The destination confirmation view.</param>
    /// <param name="sourceWindow">The originating strategy window.</param>
    /// <param name="sourceItems">The personnel selected for retirement.</param>
    /// <param name="playerFactionId">The player faction identifier.</param>
    /// <returns>True when a valid session was created.</returns>
    public bool TryInitializeRetireConfirmWindow(
        ConfirmDialogWindowView view,
        UIWindow sourceWindow,
        IReadOnlyList<ISceneNode> sourceItems,
        string playerFactionId
    )
    {
        List<ISceneNode> items = CopyItems(sourceItems);
        UIWindow dialogWindow = view == null ? null : view.GetComponent<UIWindow>();
        if (
            view == null
            || dialogWindow == null
            || sourceWindow == null
            || !StrategyContextMenuAvailability.CanRetireFleet(items, playerFactionId)
        )
            return false;

        SetSession(
            view,
            new ConfirmDialogSession(
                dialogWindow,
                sourceWindow,
                ConfirmDialogKind.Retire,
                items,
                null,
                -1
            )
        );
        PlayPromptSound(ConfirmDialogKind.Retire);
        return true;
    }

    /// <summary>
    /// Starts a stop-construction confirmation session for queued items.
    /// </summary>
    /// <param name="view">The destination confirmation view.</param>
    /// <param name="sourceWindow">The originating strategy window.</param>
    /// <param name="sourceItems">The queued items selected for cancellation.</param>
    /// <returns>True when a valid session was created.</returns>
    public bool TryInitializeStopConstructionConfirmWindow(
        ConfirmDialogWindowView view,
        UIWindow sourceWindow,
        IReadOnlyList<ISceneNode> sourceItems
    )
    {
        List<ISceneNode> items = CopyItems(sourceItems);
        UIWindow dialogWindow = view == null ? null : view.GetComponent<UIWindow>();
        if (
            view == null
            || dialogWindow == null
            || sourceWindow == null
            || items.Count == 0
            || items.Any(item =>
                item is not IManufacturable { ManufacturingStatus: ManufacturingStatus.Building }
            )
        )
            return false;

        SetSession(
            view,
            new ConfirmDialogSession(
                dialogWindow,
                sourceWindow,
                ConfirmDialogKind.StopConstruction,
                items,
                null,
                -1
            )
        );
        PlayPromptSound(ConfirmDialogKind.StopConstruction);
        return true;
    }

    /// <summary>
    /// Starts a movement confirmation session for an eligible unit selection.
    /// </summary>
    /// <param name="view">The destination confirmation view.</param>
    /// <param name="sourceWindow">The originating strategy window.</param>
    /// <param name="target">The selected movement destination.</param>
    /// <param name="sourceItems">The units selected for movement.</param>
    /// <param name="playerFactionId">The player faction identifier.</param>
    /// <returns>True when a valid session was created.</returns>
    public bool TryInitializeMoveConfirmWindow(
        ConfirmDialogWindowView view,
        UIWindow sourceWindow,
        StrategyMissionTarget target,
        IReadOnlyList<ISceneNode> sourceItems,
        string playerFactionId
    )
    {
        List<ISceneNode> items = CopyItems(sourceItems);
        UIWindow dialogWindow = view == null ? null : view.GetComponent<UIWindow>();
        if (
            view == null
            || dialogWindow == null
            || sourceWindow == null
            || !StrategyContextMenuAvailability.CanMoveItems(items, playerFactionId)
        )
            return false;

        SetSession(
            view,
            new ConfirmDialogSession(
                dialogWindow,
                sourceWindow,
                ConfirmDialogKind.Move,
                items,
                target,
                actionController.GetMoveTransitTimeInDays(items, target)
            )
        );
        return true;
    }

    /// <summary>
    /// Opens scrap confirmation for a resolved non-empty selection.
    /// </summary>
    /// <param name="sourceWindow">The originating strategy window.</param>
    /// <param name="sourceItems">The units selected for scrapping.</param>
    public void OpenScrap(UIWindow sourceWindow, IReadOnlyList<ISceneNode> sourceItems)
    {
        ConfirmDialogWindowView view = CreateWindow(out UIWindow window);
        if (!TryInitializeScrapConfirmWindow(view, sourceWindow, sourceItems))
        {
            windowManager.DestroyWindow(window);
            return;
        }

        markDirty();
    }

    /// <summary>
    /// Opens stop-construction confirmation for queued items.
    /// </summary>
    /// <param name="sourceWindow">The originating strategy window.</param>
    /// <param name="sourceItems">The queued items selected for cancellation.</param>
    public void OpenStopConstruction(UIWindow sourceWindow, IReadOnlyList<ISceneNode> sourceItems)
    {
        ConfirmDialogWindowView view = CreateWindow(out UIWindow window);
        if (!TryInitializeStopConstructionConfirmWindow(view, sourceWindow, sourceItems))
        {
            windowManager.DestroyWindow(window);
            return;
        }

        markDirty();
    }

    /// <summary>
    /// Opens retirement confirmation for an eligible personnel selection.
    /// </summary>
    /// <param name="sourceWindow">The originating strategy window.</param>
    /// <param name="sourceItems">The personnel selected for retirement.</param>
    /// <param name="playerFactionId">The player faction identifier.</param>
    public void OpenRetire(
        UIWindow sourceWindow,
        IReadOnlyList<ISceneNode> sourceItems,
        string playerFactionId
    )
    {
        ConfirmDialogWindowView view = CreateWindow(out UIWindow window);
        if (!TryInitializeRetireConfirmWindow(view, sourceWindow, sourceItems, playerFactionId))
        {
            windowManager.DestroyWindow(window);
            return;
        }

        markDirty();
    }

    /// <summary>
    /// Opens movement confirmation for an eligible unit selection.
    /// </summary>
    /// <param name="sourceWindow">The originating strategy window.</param>
    /// <param name="target">The selected movement destination.</param>
    /// <param name="sourceItems">The units selected for movement.</param>
    /// <param name="playerFactionId">The player faction identifier.</param>
    public void OpenMove(
        UIWindow sourceWindow,
        StrategyMissionTarget target,
        IReadOnlyList<ISceneNode> sourceItems,
        string playerFactionId
    )
    {
        ConfirmDialogWindowView view = CreateWindow(out UIWindow window);
        if (
            !TryInitializeMoveConfirmWindow(
                view,
                sourceWindow,
                target,
                sourceItems,
                playerFactionId
            )
        )
        {
            windowManager.DestroyWindow(window);
            return;
        }

        markDirty();
    }

    /// <summary>
    /// Renders every registered confirmation window.
    /// </summary>
    public void RenderWindows()
    {
        foreach (UIWindow window in windowManager.Windows)
        {
            if (windowManager.TryGetWindowView(window, out ConfirmDialogWindowView view))
                RenderWindow(view, window);
        }
    }

    /// <summary>
    /// Projects and renders the active session for one confirmation window.
    /// </summary>
    /// <param name="view">The destination confirmation view.</param>
    /// <param name="window">The window shell supplying source-space position.</param>
    public void RenderWindow(ConfirmDialogWindowView view, UIWindow window)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));
        if (window == null)
            throw new ArgumentNullException(nameof(window));

        ConfirmDialogSession session = GetSession(view);

        UIContext uiContext = GetUIContext();
        ConfirmDialogTheme theme = uiContext.GetPlayerFactionTheme()?.ConfirmDialogTheme;
        view.Render(
            new ConfirmDialogWindowRenderData(
                window.X,
                window.Y,
                uiContext.GetTexture(theme?.BackgroundImagePath),
                uiContext.GetTexture(GetTitleImagePath(theme, session.Kind)),
                BuildLines(session)
            )
        );
    }

    /// <summary>
    /// Stores one session after binding its destination view.
    /// </summary>
    /// <param name="view">The destination confirmation view.</param>
    /// <param name="session">The session to store.</param>
    private void SetSession(ConfirmDialogWindowView view, ConfirmDialogSession session)
    {
        BindWindow(view);
        sessions[view] = session;
    }

    /// <summary>
    /// Creates one authored confirmation window at its configured placement.
    /// </summary>
    /// <param name="window">Receives the created window shell.</param>
    /// <returns>The created confirmation view.</returns>
    private ConfirmDialogWindowView CreateWindow(out UIWindow window)
    {
        Vector2Int position = getWindowPosition();
        window = windowManager.CreateWindow(
            windowLayer.ConfirmDialogWindowPrefab,
            windowLayer.GetWindowParent(true),
            "ConfirmDialogWindow",
            position.x,
            position.y,
            windowLayer.GetWindowSize(windowLayer.ConfirmDialogWindowPrefab),
            true,
            true,
            false,
            false,
            out ConfirmDialogWindowView view
        );
        return view;
    }

    /// <summary>
    /// Executes or rejects a semantic confirmation choice, then closes the dialog.
    /// </summary>
    /// <param name="view">The requesting confirmation view.</param>
    /// <param name="confirmed">Whether the user accepted the action.</param>
    private void HandleChoiceRequested(ConfirmDialogWindowView view, bool confirmed)
    {
        if (!sessions.TryGetValue(view, out ConfirmDialogSession session))
            return;

        bool mutated = confirmed && ExecuteConfirmedAction(session);
        if (mutated)
            actions.RefreshAfterConfirmedAction(session.SourceWindow);

        closeWindow(session.DialogWindow);
    }

    /// <summary>
    /// Executes the mutation represented by one accepted confirmation session.
    /// </summary>
    /// <param name="session">The accepted confirmation session.</param>
    /// <returns>True when the action mutated or queued game state.</returns>
    private bool ExecuteConfirmedAction(ConfirmDialogSession session)
    {
        return session.Kind switch
        {
            ConfirmDialogKind.Scrap => actionController.ExecuteScrap(session.Items),
            ConfirmDialogKind.Retire => actionController.ExecuteRetire(session.Items),
            ConfirmDialogKind.StopConstruction => actionController.ExecuteStopConstruction(
                session.Items
            ),
            ConfirmDialogKind.Move => actionController.TryExecuteMove(
                session.MoveTarget,
                session.Items,
                GetUIContext().GetPlayerFactionInstanceID()
            ),
            _ => false,
        };
    }

    /// <summary>
    /// Builds the ordered prompt and selection lines for one session.
    /// </summary>
    /// <param name="session">The session to project.</param>
    /// <returns>The ordered dialog lines.</returns>
    private static IReadOnlyList<string> BuildLines(ConfirmDialogSession session)
    {
        List<string> lines = new List<string> { GetPrompt(session) };
        lines.AddRange(session.Items.Select(item => item?.GetDisplayName() ?? string.Empty));
        return lines;
    }

    /// <summary>
    /// Gets the first prompt line for one confirmation session.
    /// </summary>
    /// <param name="session">The session to project.</param>
    /// <returns>The prompt line.</returns>
    private static string GetPrompt(ConfirmDialogSession session)
    {
        return session.Kind switch
        {
            ConfirmDialogKind.Scrap => _scrapPrompt,
            ConfirmDialogKind.Retire => _retirePrompt,
            ConfirmDialogKind.StopConstruction => _stopConstructionPrompt,
            ConfirmDialogKind.Move when session.TransitTimeInDays >= 0 =>
                $"{_moveTransitLabel} {session.TransitTimeInDays}",
            _ => _moveTransitLabel,
        };
    }

    /// <summary>
    /// Gets the configured title image path for one confirmation kind.
    /// </summary>
    /// <param name="theme">The current confirmation theme.</param>
    /// <param name="kind">The confirmed action kind.</param>
    /// <returns>The configured title image path.</returns>
    private static string GetTitleImagePath(ConfirmDialogTheme theme, ConfirmDialogKind kind)
    {
        return kind switch
        {
            ConfirmDialogKind.Scrap => theme?.ScrapTitleImagePath,
            ConfirmDialogKind.Retire => theme?.RetireTitleImagePath,
            ConfirmDialogKind.StopConstruction => theme?.StopConstructionTitleImagePath,
            _ => theme?.MoveTitleImagePath,
        };
    }

    /// <summary>
    /// Plays the configured prompt cue for destructive confirmation sessions.
    /// </summary>
    /// <param name="kind">The confirmation kind whose prompt cue should play.</param>
    private void PlayPromptSound(ConfirmDialogKind kind)
    {
        ConfirmDialogTheme theme = GetUIContext().GetPlayerFactionTheme()?.ConfirmDialogTheme;
        string path =
            kind == ConfirmDialogKind.StopConstruction
                ? theme?.StopConstructionSoundPath
                : theme?.ScrapRetireSoundPath;
        if (!string.IsNullOrEmpty(path))
            playSfx(path);
    }

    /// <summary>
    /// Removes subscriptions and session state for a destroyed confirmation view.
    /// </summary>
    /// <param name="view">The destroyed confirmation view.</param>
    private void HandleViewDestroyed(ConfirmDialogWindowView view)
    {
        if (ReferenceEquals(view, null) || !boundViews.Remove(view))
            return;

        view.ChoiceRequested -= HandleChoiceRequested;
        view.Destroyed -= HandleViewDestroyed;
        sessions.Remove(view);
    }

    /// <summary>
    /// Copies the selected scene nodes into stable session storage.
    /// </summary>
    /// <param name="items">The selected scene nodes.</param>
    /// <returns>A stable item list.</returns>
    private static List<ISceneNode> CopyItems(IReadOnlyList<ISceneNode> items)
    {
        return items?.ToList() ?? new List<ISceneNode>();
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
    /// Verifies that action routing is available before a view is bound.
    /// </summary>
    private void EnsureInitialized()
    {
        if (actions == null)
            throw new InvalidOperationException(
                $"{nameof(ConfirmDialogWindowController)} must be initialized before use."
            );
    }

    /// <summary>
    /// Returns the controller-owned session for an initialized confirmation view.
    /// </summary>
    /// <param name="view">The initialized confirmation view.</param>
    /// <returns>The session owned by the view.</returns>
    private ConfirmDialogSession GetSession(ConfirmDialogWindowView view)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));
        if (sessions.TryGetValue(view, out ConfirmDialogSession session))
            return session;

        throw new InvalidOperationException($"{view.name} has not been initialized.");
    }

    /// <summary>
    /// Stores the domain state owned by one live confirmation window.
    /// </summary>
    private sealed class ConfirmDialogSession
    {
        /// <summary>
        /// Creates one immutable confirmation session.
        /// </summary>
        /// <param name="dialogWindow">The owning confirmation window.</param>
        /// <param name="sourceWindow">The originating strategy window.</param>
        /// <param name="kind">The confirmed action kind.</param>
        /// <param name="items">The selected scene nodes.</param>
        /// <param name="moveTarget">The optional movement destination.</param>
        /// <param name="transitTimeInDays">The displayed movement duration.</param>
        public ConfirmDialogSession(
            UIWindow dialogWindow,
            UIWindow sourceWindow,
            ConfirmDialogKind kind,
            IReadOnlyList<ISceneNode> items,
            StrategyMissionTarget moveTarget,
            int transitTimeInDays
        )
        {
            DialogWindow = dialogWindow ?? throw new ArgumentNullException(nameof(dialogWindow));
            SourceWindow = sourceWindow ?? throw new ArgumentNullException(nameof(sourceWindow));
            Kind = kind;
            Items = new List<ISceneNode>(
                items ?? throw new ArgumentNullException(nameof(items))
            ).AsReadOnly();
            MoveTarget = moveTarget;
            TransitTimeInDays = transitTimeInDays;
        }

        /// <summary>
        /// Gets the owning confirmation window.
        /// </summary>
        public UIWindow DialogWindow { get; }

        /// <summary>
        /// Gets the source window.
        /// </summary>
        public UIWindow SourceWindow { get; }

        /// <summary>
        /// Gets the kind.
        /// </summary>
        public ConfirmDialogKind Kind { get; }

        /// <summary>
        /// Gets the items.
        /// </summary>
        public IReadOnlyList<ISceneNode> Items { get; }

        /// <summary>
        /// Gets the move target.
        /// </summary>
        public StrategyMissionTarget MoveTarget { get; }

        /// <summary>
        /// Gets the transit time in days.
        /// </summary>
        public int TransitTimeInDays { get; }
    }
}
