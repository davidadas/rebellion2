using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.SceneGraph;
using UnityEngine;

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

    /// <summary>
    /// Creates the confirmation feature controller.
    /// </summary>
    /// <param name="getUIContext">Returns the current strategy presentation context.</param>
    /// <param name="playSfx">Plays a configured strategy sound path.</param>
    /// <param name="windowLayer">Provides the authored confirmation prefab and modal layer.</param>
    /// <param name="windowManager">Owns strategy-window creation and registration.</param>
    /// <param name="getWindowPosition">Returns the authored confirmation placement.</param>
    /// <param name="closeWindow">Closes a registered strategy window.</param>
    /// <param name="markDirty">Invalidates strategy presentation after window changes.</param>
    public ConfirmDialogWindowController(
        Func<UIContext> getUIContext,
        Action<string> playSfx,
        StrategyWindowLayerView windowLayer,
        UIWindowManager windowManager,
        Func<Vector2Int> getWindowPosition,
        Action<UIWindow> closeWindow,
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
        this.closeWindow = closeWindow ?? throw new ArgumentNullException(nameof(closeWindow));
        this.markDirty = markDirty ?? throw new ArgumentNullException(nameof(markDirty));
    }

    /// <summary>
    /// Subscribes the controller to one confirmation view exactly once.
    /// </summary>
    /// <param name="view">The confirmation view to bind.</param>
    public void BindWindow(ConfirmDialogWindowView view)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));
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
    /// <param name="confirmedAction">The action to invoke after confirmation.</param>
    /// <returns>True when a valid session was created.</returns>
    public bool TryInitializeScrapConfirmWindow(
        ConfirmDialogWindowView view,
        UIWindow sourceWindow,
        IReadOnlyList<ISceneNode> sourceItems,
        Action confirmedAction
    )
    {
        List<ISceneNode> items = CopyItems(sourceItems);
        UIWindow dialogWindow = view == null ? null : view.GetComponent<UIWindow>();
        if (
            view == null
            || dialogWindow == null
            || sourceWindow == null
            || items.Count == 0
            || confirmedAction == null
        )
            return false;

        SetSession(
            view,
            new ConfirmDialogSession(
                dialogWindow,
                ConfirmDialogKind.Scrap,
                items,
                -1,
                confirmedAction
            )
        );
        PlayPromptSound(ConfirmDialogKind.Scrap);
        return true;
    }

    /// <summary>
    /// Starts a retirement confirmation session for a personnel selection.
    /// </summary>
    /// <param name="view">The destination confirmation view.</param>
    /// <param name="sourceWindow">The originating strategy window.</param>
    /// <param name="sourceItems">The personnel selected for retirement.</param>
    /// <param name="confirmedAction">The action to invoke after confirmation.</param>
    /// <returns>True when a valid session was created.</returns>
    public bool TryInitializeRetireConfirmWindow(
        ConfirmDialogWindowView view,
        UIWindow sourceWindow,
        IReadOnlyList<ISceneNode> sourceItems,
        Action confirmedAction
    )
    {
        List<ISceneNode> items = CopyItems(sourceItems);
        UIWindow dialogWindow = view == null ? null : view.GetComponent<UIWindow>();
        if (
            view == null
            || dialogWindow == null
            || sourceWindow == null
            || items.Count == 0
            || confirmedAction == null
        )
            return false;

        SetSession(
            view,
            new ConfirmDialogSession(
                dialogWindow,
                ConfirmDialogKind.Retire,
                items,
                -1,
                confirmedAction
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
    /// <param name="confirmedAction">The action to invoke after confirmation.</param>
    /// <returns>True when a valid session was created.</returns>
    public bool TryInitializeStopConstructionConfirmWindow(
        ConfirmDialogWindowView view,
        UIWindow sourceWindow,
        IReadOnlyList<ISceneNode> sourceItems,
        Action confirmedAction
    )
    {
        List<ISceneNode> items = CopyItems(sourceItems);
        UIWindow dialogWindow = view == null ? null : view.GetComponent<UIWindow>();
        if (
            view == null
            || dialogWindow == null
            || sourceWindow == null
            || items.Count == 0
            || confirmedAction == null
        )
            return false;

        SetSession(
            view,
            new ConfirmDialogSession(
                dialogWindow,
                ConfirmDialogKind.StopConstruction,
                items,
                -1,
                confirmedAction
            )
        );
        PlayPromptSound(ConfirmDialogKind.StopConstruction);
        return true;
    }

    /// <summary>
    /// Starts a movement confirmation session for a unit selection.
    /// </summary>
    /// <param name="view">The destination confirmation view.</param>
    /// <param name="sourceWindow">The originating strategy window.</param>
    /// <param name="sourceItems">The units selected for movement.</param>
    /// <param name="transitTimeInDays">The displayed transit duration.</param>
    /// <param name="confirmedAction">The action to invoke after confirmation.</param>
    /// <returns>True when a valid session was created.</returns>
    public bool TryInitializeMoveConfirmWindow(
        ConfirmDialogWindowView view,
        UIWindow sourceWindow,
        IReadOnlyList<ISceneNode> sourceItems,
        int transitTimeInDays,
        Action confirmedAction
    )
    {
        List<ISceneNode> items = CopyItems(sourceItems);
        UIWindow dialogWindow = view == null ? null : view.GetComponent<UIWindow>();
        if (
            view == null
            || dialogWindow == null
            || sourceWindow == null
            || items.Count == 0
            || confirmedAction == null
        )
            return false;

        SetSession(
            view,
            new ConfirmDialogSession(
                dialogWindow,
                ConfirmDialogKind.Move,
                items,
                transitTimeInDays,
                confirmedAction
            )
        );
        return true;
    }

    /// <summary>
    /// Opens scrap confirmation for a resolved non-empty selection.
    /// </summary>
    /// <param name="sourceWindow">The originating strategy window.</param>
    /// <param name="sourceItems">The units selected for scrapping.</param>
    /// <param name="confirmedAction">The action to invoke after confirmation.</param>
    public void OpenScrap(
        UIWindow sourceWindow,
        IReadOnlyList<ISceneNode> sourceItems,
        Action confirmedAction
    )
    {
        ConfirmDialogWindowView view = CreateWindow(out UIWindow window);
        if (!TryInitializeScrapConfirmWindow(view, sourceWindow, sourceItems, confirmedAction))
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
    /// <param name="confirmedAction">The action to invoke after confirmation.</param>
    public void OpenStopConstruction(
        UIWindow sourceWindow,
        IReadOnlyList<ISceneNode> sourceItems,
        Action confirmedAction
    )
    {
        ConfirmDialogWindowView view = CreateWindow(out UIWindow window);
        if (
            !TryInitializeStopConstructionConfirmWindow(
                view,
                sourceWindow,
                sourceItems,
                confirmedAction
            )
        )
        {
            windowManager.DestroyWindow(window);
            return;
        }

        markDirty();
    }

    /// <summary>
    /// Opens retirement confirmation for a personnel selection.
    /// </summary>
    /// <param name="sourceWindow">The originating strategy window.</param>
    /// <param name="sourceItems">The personnel selected for retirement.</param>
    /// <param name="confirmedAction">The action to invoke after confirmation.</param>
    public void OpenRetire(
        UIWindow sourceWindow,
        IReadOnlyList<ISceneNode> sourceItems,
        Action confirmedAction
    )
    {
        ConfirmDialogWindowView view = CreateWindow(out UIWindow window);
        if (!TryInitializeRetireConfirmWindow(view, sourceWindow, sourceItems, confirmedAction))
        {
            windowManager.DestroyWindow(window);
            return;
        }

        markDirty();
    }

    /// <summary>
    /// Opens movement confirmation for a unit selection.
    /// </summary>
    /// <param name="sourceWindow">The originating strategy window.</param>
    /// <param name="sourceItems">The units selected for movement.</param>
    /// <param name="transitTimeInDays">The displayed transit duration.</param>
    /// <param name="confirmedAction">The action to invoke after confirmation.</param>
    public void OpenMove(
        UIWindow sourceWindow,
        IReadOnlyList<ISceneNode> sourceItems,
        int transitTimeInDays,
        Action confirmedAction
    )
    {
        ConfirmDialogWindowView view = CreateWindow(out UIWindow window);
        if (
            !TryInitializeMoveConfirmWindow(
                view,
                sourceWindow,
                sourceItems,
                transitTimeInDays,
                confirmedAction
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

        sessions.Remove(view);
        try
        {
            if (confirmed)
                session.ConfirmedAction();
        }
        finally
        {
            closeWindow(session.DialogWindow);
        }
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
        /// <param name="kind">The confirmed action kind.</param>
        /// <param name="items">The selected scene nodes.</param>
        /// <param name="transitTimeInDays">The displayed movement duration.</param>
        /// <param name="confirmedAction">The action invoked after confirmation.</param>
        public ConfirmDialogSession(
            UIWindow dialogWindow,
            ConfirmDialogKind kind,
            IReadOnlyList<ISceneNode> items,
            int transitTimeInDays,
            Action confirmedAction
        )
        {
            DialogWindow = dialogWindow ?? throw new ArgumentNullException(nameof(dialogWindow));
            Kind = kind;
            Items = new List<ISceneNode>(
                items ?? throw new ArgumentNullException(nameof(items))
            ).AsReadOnly();
            TransitTimeInDays = transitTimeInDays;
            ConfirmedAction =
                confirmedAction ?? throw new ArgumentNullException(nameof(confirmedAction));
        }

        public UIWindow DialogWindow { get; }

        public ConfirmDialogKind Kind { get; }

        public IReadOnlyList<ISceneNode> Items { get; }

        public Action ConfirmedAction { get; }

        public int TransitTimeInDays { get; }
    }
}
