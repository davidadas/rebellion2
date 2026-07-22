using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Factions;
using Rebellion.Game.Messages;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Performs window-level actions requested by the Messages feature.
/// </summary>
public interface IMessagesWindowActions
{
    /// <summary>
    /// Opens the strategy location represented by a message target.
    /// </summary>
    /// <param name="targetInstanceId">The preferred target instance identifier.</param>
    /// <param name="secondaryTargetInstanceId">The fallback target instance identifier.</param>
    /// <param name="locationInstanceId">The event location instance identifier.</param>
    /// <returns>True when a target location was opened.</returns>
    bool OpenMessageTarget(
        string targetInstanceId,
        string secondaryTargetInstanceId,
        string locationInstanceId
    );
}

/// <summary>
/// Owns per-window Messages state, domain mutations, audio policy, and navigation requests.
/// </summary>
public sealed class MessagesWindowController
{
    private readonly Action<UIWindow> closeWindow;
    private readonly Func<Vector2Int> getWindowPosition;
    private readonly Func<UIContext> getUIContext;
    private readonly Action markDirty;
    private readonly Action<string> playSfx;
    private readonly Dictionary<MessagesWindowView, MessagesWindowSession> sessions =
        new Dictionary<MessagesWindowView, MessagesWindowSession>();
    private readonly StrategyWindowLayerView windowLayer;
    private readonly UIWindowManager windowManager;
    private IMessagesWindowActions actions;

    /// <summary>
    /// Creates the Messages feature controller with current presentation-resource access.
    /// </summary>
    /// <param name="playSfx">Plays a strategy sound effect path.</param>
    /// <param name="getUIContext">Resolves the current theme and texture context.</param>
    /// <param name="windowLayer">Provides the authored Messages prefab and modal layer.</param>
    /// <param name="windowManager">Owns strategy-window creation, focus, and registration.</param>
    /// <param name="getWindowPosition">Returns the authored Messages placement.</param>
    /// <param name="closeWindow">Closes a registered strategy window.</param>
    /// <param name="markDirty">Invalidates strategy presentation after window changes.</param>
    public MessagesWindowController(
        Action<string> playSfx,
        Func<UIContext> getUIContext,
        StrategyWindowLayerView windowLayer,
        UIWindowManager windowManager,
        Func<Vector2Int> getWindowPosition,
        Action<UIWindow> closeWindow,
        Action markDirty
    )
    {
        this.playSfx = playSfx ?? throw new ArgumentNullException(nameof(playSfx));
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
    /// Connects the controller to window-level strategy actions.
    /// </summary>
    /// <param name="actions">The feature-specific Messages actions.</param>
    public void Initialize(IMessagesWindowActions actions)
    {
        this.actions = actions ?? throw new ArgumentNullException(nameof(actions));
    }

    /// <summary>
    /// Subscribes the controller to a Messages view and creates its session exactly once.
    /// </summary>
    /// <param name="view">The Messages view to bind.</param>
    public void BindWindow(MessagesWindowView view)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));

        EnsureInitialized();
        if (sessions.ContainsKey(view))
            return;

        UIWindow window = view.GetComponent<UIWindow>();
        if (window == null)
            throw new MissingReferenceException($"{view.name} is missing UIWindow.");

        MessagesWindowSession session = new MessagesWindowSession(window);
        RefreshSession(session);
        sessions.Add(view, session);
        view.ChatRequested += HandleChatRequested;
        view.CloseRequested += HandleCloseRequested;
        view.ContextRequested += HandleContextRequested;
        view.ControlPressed += HandleControlPressed;
        view.Destroyed += HandleViewDestroyed;
        view.DisplayRequested += HandleDisplayRequested;
        view.IndexRequested += HandleIndexRequested;
        view.MessageNextRequested += HandleMessageNextRequested;
        view.MessagePreviousRequested += HandleMessagePreviousRequested;
        view.MessageRemovalRequested += HandleMessageRemovalRequested;
        view.MessageRowActivated += HandleMessageRowActivated;
        view.MessageRowSelected += HandleMessageRowSelected;
        view.MessageSelectAllRequested += HandleMessageSelectAllRequested;
        view.MessageTargetRequested += HandleMessageTargetRequested;
        view.NotificationToggleRequested += HandleNotificationToggleRequested;
        view.TabRequested += HandleTabRequested;
    }

    /// <summary>
    /// Selects a Messages tab and resets panel and selection state for one window session.
    /// </summary>
    /// <param name="view">The Messages view whose session should change.</param>
    /// <param name="tab">The semantic Messages tab.</param>
    public void OpenTab(MessagesWindowView view, MessagesTab tab)
    {
        MessagesWindowSession session = GetSession(view);
        session.SelectTab(MessagesTabCatalog.Clamp((int)tab));
        RefreshSession(session);
    }

    /// <summary>
    /// Gets whether a Messages window is currently registered.
    /// </summary>
    public bool IsOpen => FindWindow() != null;

    /// <summary>
    /// Opens or focuses the Messages window on a requested tab.
    /// </summary>
    /// <param name="initialTab">The semantic Messages tab to display.</param>
    public void Open(MessagesTab initialTab)
    {
        UIWindow existing = FindWindow();
        if (existing != null)
        {
            if (windowManager.TryGetWindowView(existing, out MessagesWindowView existingView))
                OpenTab(existingView, initialTab);

            windowManager.Focus(existing);
            markDirty();
            return;
        }

        Vector2Int position = getWindowPosition();
        windowManager.CreateWindow(
            windowLayer.MessagesWindowPrefab,
            windowLayer.GetWindowParent(true),
            "MessagesWindow",
            position.x,
            position.y,
            windowLayer.GetWindowSize(windowLayer.MessagesWindowPrefab),
            true,
            true,
            false,
            false,
            out MessagesWindowView view
        );
        BindWindow(view);
        OpenTab(view, initialTab);
        markDirty();
    }

    /// <summary>
    /// Opens the Messages window directly on one delivered message detail.
    /// </summary>
    /// <param name="message">The delivered message to display.</param>
    /// <param name="tab">The message category containing the message.</param>
    public void OpenDetail(Message message, MessagesTab tab)
    {
        if (message == null)
            return;

        Open(tab);
        UIWindow window = FindWindow();
        if (
            window == null
            || !windowManager.TryGetWindowView(window, out MessagesWindowView view)
            || !TryGetSession(view, out MessagesWindowSession session)
        )
            return;

        RefreshSession(session);
        Message currentMessage = GetMessage(session.Messages, message.InstanceID);
        if (currentMessage == null)
            return;

        session.SelectOnly(currentMessage);
        MarkMessageRead(currentMessage);
        session.ShowDetail();
        PlayMessageDetailAudio(currentMessage);
        windowManager.Focus(window);
        markDirty();
    }

    /// <summary>
    /// Renders every registered Messages window.
    /// </summary>
    public void RenderWindows()
    {
        foreach (UIWindow window in windowManager.Windows)
        {
            if (windowManager.TryGetWindowView(window, out MessagesWindowView view))
                RenderWindow(view, window);
        }
    }

    /// <summary>
    /// Projects current message state and renders a Messages window.
    /// </summary>
    /// <param name="view">The destination Messages view.</param>
    /// <param name="window">The window shell supplying position and layout state.</param>
    public void RenderWindow(MessagesWindowView view, UIWindow window)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));
        if (window == null)
            throw new ArgumentNullException(nameof(window));

        MessagesWindowSession session = GetSession(view);
        UIContext uiContext = GetRequiredUIContext();
        Faction playerFaction = uiContext.Game?.GetPlayerFaction();
        IReadOnlyList<Message> messages = session.Messages;
        Message selectedMessage = session.GetSelectedMessage();
        view.Render(
            MessagesWindowProjector.Project(
                uiContext,
                messages,
                session.ActiveTab,
                session.DetailVisible,
                session.SelectedMessageId,
                session.GetSelectedMessageIDs(),
                IsMessageNotificationEnabled(playerFaction, session.ActiveTab),
                HasNavigationTarget(selectedMessage),
                window.X,
                window.Y
            )
        );
    }

    /// <summary>
    /// Reconciles every open Messages session with the current player message collections.
    /// </summary>
    public void ReconcileWindows()
    {
        foreach (MessagesWindowSession session in sessions.Values)
            RefreshSession(session);
    }

    /// <summary>
    /// Gets the active tab stored in one controller-owned Messages session.
    /// </summary>
    /// <param name="view">The Messages view whose session should be inspected.</param>
    /// <returns>The active semantic tab, or the aggregate tab when no session exists.</returns>
    internal MessagesTab GetActiveTab(MessagesWindowView view)
    {
        return TryGetSession(view, out MessagesWindowSession session)
            ? session.ActiveTab
            : MessagesTab.All;
    }

    /// <summary>
    /// Gets whether one controller-owned Messages session displays message detail.
    /// </summary>
    /// <param name="view">The Messages view whose session should be inspected.</param>
    /// <returns>True when the detail panel is active.</returns>
    internal bool IsDetailVisible(MessagesWindowView view)
    {
        return TryGetSession(view, out MessagesWindowSession session) && session.DetailVisible;
    }

    /// <summary>
    /// Gets the primary selected message identifier in one controller-owned Messages session.
    /// </summary>
    /// <param name="view">The Messages view whose session should be inspected.</param>
    /// <returns>The selected source message identifier, or null when none is selected.</returns>
    internal string GetSelectedMessageID(MessagesWindowView view)
    {
        return TryGetSession(view, out MessagesWindowSession session)
            ? session.SelectedMessageId
            : null;
    }

    /// <summary>
    /// Returns message detail audio in playback order.
    /// </summary>
    /// <param name="message">The source message.</param>
    /// <returns>The non-empty voice paths associated with the message.</returns>
    internal static IReadOnlyList<string> GetDetailAudioPaths(Message message)
    {
        if (message == null)
            return Array.Empty<string>();

        List<string> paths = new List<string>();
        AddDetailAudioPath(paths, message.MessageVoicePath);
        AddDetailAudioPath(paths, message.OfficerVoicePath);
        return paths;
    }

    /// <summary>
    /// Removes messages selected by stable identifier from their faction buckets.
    /// </summary>
    /// <param name="faction">The faction that owns the messages.</param>
    /// <param name="selectedMessageIds">The selected source message identifiers.</param>
    /// <returns>True when at least one message was removed.</returns>
    internal static bool RemoveSelectedMessages(
        Faction faction,
        IReadOnlyCollection<string> selectedMessageIds
    )
    {
        if (faction == null || selectedMessageIds == null || selectedMessageIds.Count == 0)
            return false;

        HashSet<string> selectedIds = new HashSet<string>(
            selectedMessageIds.Where(id => !string.IsNullOrEmpty(id)),
            StringComparer.Ordinal
        );
        int removedCount = 0;

        foreach (List<Message> bucket in faction.Messages.Values)
            removedCount += bucket.RemoveAll(message => selectedIds.Contains(message.InstanceID));

        return removedCount > 0;
    }

    /// <summary>
    /// Marks a message as read when one is present.
    /// </summary>
    /// <param name="message">The message whose read state should change.</param>
    internal static void MarkMessageRead(Message message)
    {
        if (message != null)
            message.Read = true;
    }

    /// <summary>
    /// Plays the detail audio assigned to a message.
    /// </summary>
    /// <param name="message">The message whose detail was presented.</param>
    internal void PlayMessageDetailAudio(Message message)
    {
        foreach (string path in GetDetailAudioPaths(message))
            playSfx(path);
    }

    /// <summary>
    /// Requests navigation to a message target when the message supplies one.
    /// </summary>
    /// <param name="message">The message whose target should open.</param>
    /// <returns>True when navigation was requested and completed.</returns>
    internal bool OpenMessageTarget(Message message)
    {
        if (!HasNavigationTarget(message))
            return false;

        EnsureInitialized();
        return actions.OpenMessageTarget(
            message.NavigationTargetInstanceID,
            message.NavigationSecondaryTargetInstanceID,
            message.EventLocationInstanceID
        );
    }

    /// <summary>
    /// Returns whether a message identifies a strategy navigation destination.
    /// </summary>
    /// <param name="message">The message to inspect.</param>
    /// <returns>True when at least one navigation identifier is present.</returns>
    internal static bool HasNavigationTarget(Message message)
    {
        return message != null
            && (
                !string.IsNullOrEmpty(message.NavigationTargetInstanceID)
                || !string.IsNullOrEmpty(message.NavigationSecondaryTargetInstanceID)
                || !string.IsNullOrEmpty(message.EventLocationInstanceID)
            );
    }

    /// <summary>
    /// Returns messages for a semantic tab without changing their stored ordering.
    /// </summary>
    /// <param name="faction">The faction that owns the messages.</param>
    /// <param name="tab">The semantic Messages tab.</param>
    /// <returns>The messages represented by the requested tab.</returns>
    internal static List<Message> GetRows(Faction faction, MessagesTab tab)
    {
        if (faction == null)
            return new List<Message>();

        if (tab == MessagesTab.All)
            return faction.Messages.SelectMany(entry => entry.Value).ToList();

        MessageType? type = MessagesTabCatalog.GetMessageType(tab);
        if (!type.HasValue || !faction.Messages.TryGetValue(type.Value, out List<Message> messages))
            return new List<Message>();

        return messages;
    }

    /// <summary>
    /// Returns whether notifications are enabled for a semantic Messages tab.
    /// </summary>
    /// <param name="faction">The faction that owns the notification settings.</param>
    /// <param name="tab">The semantic Messages tab.</param>
    /// <returns>True when the tab is configured to notify.</returns>
    internal static bool IsMessageNotificationEnabled(Faction faction, MessagesTab tab)
    {
        if (faction == null)
            return false;

        MessageType? messageType = MessagesTabCatalog.GetMessageType(tab);
        return messageType.HasValue
            ? faction.IsAdvisorMessageNotificationEnabled(messageType.Value)
            : faction.AdvisorMessageNotificationsEnabled;
    }

    /// <summary>
    /// Toggles notifications for a semantic Messages tab.
    /// </summary>
    /// <param name="faction">The faction that owns the notification settings.</param>
    /// <param name="tab">The semantic Messages tab.</param>
    internal static void ToggleMessageNotification(Faction faction, MessagesTab tab)
    {
        if (faction == null)
            return;

        MessageType? messageType = MessagesTabCatalog.GetMessageType(tab);
        if (messageType.HasValue)
            faction.ToggleAdvisorMessageNotification(messageType.Value);
        else
            faction.ToggleAllAdvisorMessageNotifications();
    }

    /// <summary>
    /// Appends a non-empty audio path.
    /// </summary>
    /// <param name="paths">The ordered audio path collection.</param>
    /// <param name="path">The candidate path.</param>
    private static void AddDetailAudioPath(List<string> paths, string path)
    {
        if (!string.IsNullOrEmpty(path))
            paths.Add(path);
    }

    /// <summary>
    /// Returns a message by stable identifier when present.
    /// </summary>
    /// <param name="messages">The active tab's messages.</param>
    /// <param name="messageId">The source message identifier.</param>
    /// <returns>The matching message, or null when the identifier is unavailable.</returns>
    private static Message GetMessage(IReadOnlyList<Message> messages, string messageId)
    {
        return messages == null || string.IsNullOrEmpty(messageId)
            ? null
            : messages.FirstOrDefault(message => message?.InstanceID == messageId);
    }

    /// <summary>
    /// Routes the chat command to the semantic chat tab in the requesting session.
    /// </summary>
    /// <param name="view">The requesting Messages view.</param>
    private void HandleChatRequested(MessagesWindowView view)
    {
        if (!TryGetSession(view, out MessagesWindowSession session))
            return;

        FocusWindow(view);
        session.SelectTab(MessagesTab.Chat);
        RequestRender();
    }

    /// <summary>
    /// Focuses and closes the window associated with a view request.
    /// </summary>
    /// <param name="view">The requesting Messages view.</param>
    private void HandleCloseRequested(MessagesWindowView view)
    {
        FocusWindow(view);
        if (TryGetSession(view, out MessagesWindowSession session))
            closeWindow(session.Window);
    }

    /// <summary>
    /// Routes a row context-menu gesture to the strategy input owner.
    /// </summary>
    /// <param name="view">The requesting Messages view.</param>
    /// <param name="eventData">The pointer event that requested the context menu.</param>
    private void HandleContextRequested(MessagesWindowView view, PointerEventData eventData)
    {
        if (TryGetSession(view, out MessagesWindowSession session))
            session.Window.RequestContext(eventData);
    }

    /// <summary>
    /// Plays the shared command-control sound.
    /// </summary>
    private void HandleControlPressed()
    {
        playSfx(StrategyUISoundPaths.ControlPress);
    }

    /// <summary>
    /// Opens the selected message in the detail panel and plays its assigned audio.
    /// </summary>
    /// <param name="view">The requesting Messages view.</param>
    private void HandleDisplayRequested(MessagesWindowView view)
    {
        if (!TryGetSession(view, out MessagesWindowSession session))
            return;

        FocusWindow(view);
        IReadOnlyList<Message> messages = session.Messages;
        if (messages.Count == 0)
            return;

        if (session.GetSelectedMessage() == null)
            session.SelectOnly(messages[messages.Count - 1]);

        Message message = session.GetSelectedMessage();
        MarkMessageRead(message);
        session.ShowDetail();
        PlayMessageDetailAudio(message);
        RequestRender();
    }

    /// <summary>
    /// Returns the requesting session to the message index.
    /// </summary>
    /// <param name="view">The requesting Messages view.</param>
    private void HandleIndexRequested(MessagesWindowView view)
    {
        if (!TryGetSession(view, out MessagesWindowSession session))
            return;

        FocusWindow(view);
        session.HideDetail();
        RequestRender();
    }

    /// <summary>
    /// Moves selection toward newer source indexes.
    /// </summary>
    /// <param name="view">The requesting Messages view.</param>
    private void HandleMessageNextRequested(MessagesWindowView view)
    {
        MoveSelection(view, 1);
    }

    /// <summary>
    /// Moves selection toward older source indexes.
    /// </summary>
    /// <param name="view">The requesting Messages view.</param>
    private void HandleMessagePreviousRequested(MessagesWindowView view)
    {
        MoveSelection(view, -1);
    }

    /// <summary>
    /// Removes selected messages and resets incompatible session state after success.
    /// </summary>
    /// <param name="view">The requesting Messages view.</param>
    private void HandleMessageRemovalRequested(MessagesWindowView view)
    {
        if (!TryGetSession(view, out MessagesWindowSession session))
            return;

        FocusWindow(view);
        Faction playerFaction = GetPlayerFaction();
        if (RemoveSelectedMessages(playerFaction, session.GetSelectedMessageIDs()))
        {
            session.ClearSelection();
            session.HideDetail();
            RefreshSession(session);
        }

        RequestRender();
    }

    /// <summary>
    /// Selects and opens an activated row in the detail panel.
    /// </summary>
    /// <param name="view">The requesting Messages view.</param>
    /// <param name="messageId">The activated source message identifier.</param>
    private void HandleMessageRowActivated(MessagesWindowView view, string messageId)
    {
        if (!TryGetSession(view, out MessagesWindowSession session))
            return;

        FocusWindow(view);
        Message message = GetMessage(session.Messages, messageId);
        if (message == null)
            return;

        session.SelectOnly(message);
        MarkMessageRead(message);
        session.ShowDetail();
        PlayMessageDetailAudio(message);
        RequestRender();
    }

    /// <summary>
    /// Selects a row and records its read state.
    /// </summary>
    /// <param name="view">The requesting Messages view.</param>
    /// <param name="messageId">The selected source message identifier.</param>
    private void HandleMessageRowSelected(MessagesWindowView view, string messageId)
    {
        if (!TryGetSession(view, out MessagesWindowSession session))
            return;

        FocusWindow(view);
        Message message = GetMessage(session.Messages, messageId);
        if (message == null)
            return;

        session.SelectOnly(message);
        MarkMessageRead(message);
        RequestRender();
    }

    /// <summary>
    /// Selects every message in the requesting session's active tab.
    /// </summary>
    /// <param name="view">The requesting Messages view.</param>
    private void HandleMessageSelectAllRequested(MessagesWindowView view)
    {
        if (!TryGetSession(view, out MessagesWindowSession session))
            return;

        FocusWindow(view);
        session.SelectAll();
        RequestRender();
    }

    /// <summary>
    /// Opens the strategy destination associated with the selected message.
    /// </summary>
    /// <param name="view">The requesting Messages view.</param>
    private void HandleMessageTargetRequested(MessagesWindowView view)
    {
        if (!TryGetSession(view, out MessagesWindowSession session))
            return;

        Message message = session.GetSelectedMessage();
        if (!HasNavigationTarget(message))
            return;

        FocusWindow(view);
        OpenMessageTarget(message);
    }

    /// <summary>
    /// Toggles notification policy for the active Messages tab.
    /// </summary>
    /// <param name="view">The requesting Messages view.</param>
    private void HandleNotificationToggleRequested(MessagesWindowView view)
    {
        if (!TryGetSession(view, out MessagesWindowSession session))
            return;

        FocusWindow(view);
        ToggleMessageNotification(GetPlayerFaction(), session.ActiveTab);
        RequestRender();
    }

    /// <summary>
    /// Applies an authored tab gesture to the requesting session.
    /// </summary>
    /// <param name="view">The requesting Messages view.</param>
    /// <param name="tab">The requested semantic Messages tab.</param>
    private void HandleTabRequested(MessagesWindowView view, MessagesTab tab)
    {
        if (!TryGetSession(view, out MessagesWindowSession session))
            return;

        FocusWindow(view);
        session.SelectTab(tab);
        RequestRender();
    }

    /// <summary>
    /// Unsubscribes a destroyed Messages view and releases its controller-owned session.
    /// </summary>
    /// <param name="view">The destroyed Messages view.</param>
    private void HandleViewDestroyed(MessagesWindowView view)
    {
        if (ReferenceEquals(view, null) || !sessions.Remove(view))
            return;

        view.ChatRequested -= HandleChatRequested;
        view.CloseRequested -= HandleCloseRequested;
        view.ContextRequested -= HandleContextRequested;
        view.ControlPressed -= HandleControlPressed;
        view.Destroyed -= HandleViewDestroyed;
        view.DisplayRequested -= HandleDisplayRequested;
        view.IndexRequested -= HandleIndexRequested;
        view.MessageNextRequested -= HandleMessageNextRequested;
        view.MessagePreviousRequested -= HandleMessagePreviousRequested;
        view.MessageRemovalRequested -= HandleMessageRemovalRequested;
        view.MessageRowActivated -= HandleMessageRowActivated;
        view.MessageRowSelected -= HandleMessageRowSelected;
        view.MessageSelectAllRequested -= HandleMessageSelectAllRequested;
        view.MessageTargetRequested -= HandleMessageTargetRequested;
        view.NotificationToggleRequested -= HandleNotificationToggleRequested;
        view.TabRequested -= HandleTabRequested;
    }

    /// <summary>
    /// Moves one session's primary selection and applies detail read and audio transitions.
    /// </summary>
    /// <param name="view">The requesting Messages view.</param>
    /// <param name="direction">The signed source-index offset.</param>
    private void MoveSelection(MessagesWindowView view, int direction)
    {
        if (!TryGetSession(view, out MessagesWindowSession session))
            return;

        FocusWindow(view);
        bool moved = session.MoveSelection(direction);
        Message message = session.GetSelectedMessage();
        MarkMessageRead(message);
        if (session.DetailVisible && moved)
            PlayMessageDetailAudio(message);
        RequestRender();
    }

    /// <summary>
    /// Gives one Messages window focus through its lifecycle owner.
    /// </summary>
    /// <param name="view">The Messages view whose window should receive focus.</param>
    private void FocusWindow(MessagesWindowView view)
    {
        if (TryGetSession(view, out MessagesWindowSession session))
            session.Window.RequestFocus();
    }

    /// <summary>
    /// Requests a new strategy render after controller-owned state changes.
    /// </summary>
    private void RequestRender()
    {
        markDirty();
    }

    /// <summary>
    /// Returns the current Messages presentation context.
    /// </summary>
    /// <returns>The current theme and texture context.</returns>
    private UIContext GetRequiredUIContext()
    {
        return getUIContext()
            ?? throw new InvalidOperationException(
                "MessagesWindowController requires an active UIContext before rendering."
            );
    }

    /// <summary>
    /// Gets the active player faction from the current game context.
    /// </summary>
    /// <returns>The active player faction, or null when no game is available.</returns>
    private Faction GetPlayerFaction()
    {
        return GetRequiredUIContext().Game?.GetPlayerFaction();
    }

    /// <summary>
    /// Refreshes one session's ordered messages and reconciles its identity-backed selection.
    /// </summary>
    /// <param name="session">The Messages session to refresh.</param>
    private void RefreshSession(MessagesWindowSession session)
    {
        if (session == null)
            throw new ArgumentNullException(nameof(session));

        session.Reconcile(GetRows(GetPlayerFaction(), session.ActiveTab));
    }

    /// <summary>
    /// Returns the controller-owned session for an initialized Messages view.
    /// </summary>
    /// <param name="view">The initialized Messages view.</param>
    /// <returns>The session owned by the view.</returns>
    private MessagesWindowSession GetSession(MessagesWindowView view)
    {
        if (view == null)
            throw new ArgumentNullException(nameof(view));
        if (sessions.TryGetValue(view, out MessagesWindowSession session))
            return session;

        throw new InvalidOperationException($"{view.name} has not been initialized.");
    }

    /// <summary>
    /// Gets the controller-owned session for one Messages view.
    /// </summary>
    /// <param name="view">The Messages view whose session should be resolved.</param>
    /// <param name="session">The resolved session.</param>
    /// <returns>True when a session exists.</returns>
    private bool TryGetSession(MessagesWindowView view, out MessagesWindowSession session)
    {
        if (view == null)
        {
            session = null;
            return false;
        }

        return sessions.TryGetValue(view, out session);
    }

    /// <summary>
    /// Finds the registered Messages window.
    /// </summary>
    /// <returns>The registered window, or null when Messages is closed.</returns>
    private UIWindow FindWindow()
    {
        foreach (UIWindow window in windowManager.Windows)
        {
            if (windowManager.TryGetWindowView(window, out MessagesWindowView _))
                return window;
        }

        return null;
    }

    /// <summary>
    /// Verifies that window actions were connected before use.
    /// </summary>
    private void EnsureInitialized()
    {
        if (actions == null)
            throw new InvalidOperationException(
                "MessagesWindowController.Initialize must be called first."
            );
    }
}
