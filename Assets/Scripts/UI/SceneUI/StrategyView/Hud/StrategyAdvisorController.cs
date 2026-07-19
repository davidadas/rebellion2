using System;
using System.Collections.Generic;
using Rebellion.Game.Factions;
using Rebellion.Game.Messages;
using Rebellion.Game.Units;
using UnityEngine;

/// <summary>
/// Owns advisor notification priority, expiry, cooldown, presentation projection, audio, and input routing.
/// </summary>
public sealed class StrategyAdvisorController : IContextMenuReceiver
{
    private readonly Func<Faction> getPlayerFaction;
    private readonly Func<string, Texture2D> getTexture;
    private readonly Action<string> playSfx;
    private readonly Dictionary<int, StrategyAdvisorNotificationTheme> pendingNotifications =
        new Dictionary<int, StrategyAdvisorNotificationTheme>();
    private readonly Dictionary<int, int> pendingExpirationTicks = new Dictionary<int, int>();
    private readonly Dictionary<int, int> nextAllowedTicks = new Dictionary<int, int>();
    private readonly List<StrategyAdvisorNotificationTheme> notificationsByPriority =
        new List<StrategyAdvisorNotificationTheme>();

    private IStrategyHudActions actions;
    private StrategyAdvisorTheme theme;
    private StrategyAdvisorView view;

    /// <summary>
    /// Creates an advisor controller with faction, texture, and audio dependencies.
    /// </summary>
    /// <param name="getPlayerFaction">Resolves the current player faction.</param>
    /// <param name="getTexture">Resolves a texture from a configured resource path.</param>
    /// <param name="playSfx">Plays a strategy sound-effect path.</param>
    public StrategyAdvisorController(
        Func<Faction> getPlayerFaction,
        Func<string, Texture2D> getTexture,
        Action<string> playSfx
    )
    {
        this.getPlayerFaction =
            getPlayerFaction ?? throw new ArgumentNullException(nameof(getPlayerFaction));
        this.getTexture = getTexture ?? throw new ArgumentNullException(nameof(getTexture));
        this.playSfx = playSfx ?? throw new ArgumentNullException(nameof(playSfx));
    }

    /// <summary>
    /// Connects advisor commands to strategy-screen actions.
    /// </summary>
    /// <param name="actions">The strategy-screen action boundary.</param>
    public void Initialize(IStrategyHudActions actions)
    {
        this.actions = actions ?? throw new ArgumentNullException(nameof(actions));
    }

    /// <summary>
    /// Subscribes the controller to one authored advisor view exactly once.
    /// </summary>
    /// <param name="nextView">The authored advisor view.</param>
    public void BindView(StrategyAdvisorView nextView)
    {
        if (nextView == null)
            throw new ArgumentNullException(nameof(nextView));

        EnsureInitialized();
        if (view == nextView)
            return;

        ReleaseView();
        view = nextView;
        view.Destroyed += HandleViewDestroyed;
        view.DroidClicked += HandleDroidClicked;
        view.DroidContextRequested += HandleDroidContextRequested;
        view.PlaybackStarted += HandlePlaybackStarted;
        view.ProtocolContextRequested += HandleProtocolContextRequested;
        view.Render(CreateViewData(theme));
    }

    /// <summary>
    /// Releases the controller from the supplied view when it is currently bound.
    /// </summary>
    /// <param name="boundView">The advisor view expected to be bound.</param>
    public void UnbindView(StrategyAdvisorView boundView)
    {
        if (!ReferenceEquals(view, boundView))
            return;

        ReleaseView();
    }

    /// <summary>
    /// Applies a changed advisor theme and resets notification and playback state from the old theme.
    /// </summary>
    /// <param name="nextTheme">The active faction advisor theme.</param>
    public void Render(StrategyAdvisorTheme nextTheme)
    {
        if (ReferenceEquals(theme, nextTheme))
            return;

        ClearNotificationState();
        theme = nextTheme;
        if (theme != null)
        {
            for (int i = 0; i < theme.Notifications.Count; i++)
            {
                StrategyAdvisorNotificationTheme notification = theme.Notifications[i];
                if (notification == null)
                    throw new InvalidOperationException($"Advisor notification theme {i} is null.");
                notificationsByPriority.Add(notification);
            }

            notificationsByPriority.Sort((left, right) => left.TableID.CompareTo(right.TableID));
        }

        GetRequiredView().Render(CreateViewData(theme));
    }

    /// <summary>
    /// Queues or replaces a pending notification derived from a delivered message.
    /// </summary>
    /// <param name="message">The delivered message.</param>
    /// <param name="currentTick">The current game tick.</param>
    /// <param name="notificationEnabled">Whether its message category permits notification.</param>
    public void Notify(Message message, int currentTick, bool notificationEnabled)
    {
        if (message == null || theme == null || !notificationEnabled)
            return;

        int code = GetNotificationCode(theme, message);
        StrategyAdvisorNotificationTheme notification = theme.GetNotification(
            code,
            out int lifetimeTicks
        );
        if (notification == null)
            return;

        pendingNotifications[notification.TableID] = notification;
        pendingExpirationTicks[notification.TableID] = currentTick + lifetimeTicks;
    }

    /// <summary>
    /// Consumes the highest-priority eligible pending notification for the current tick.
    /// </summary>
    /// <param name="currentTick">The current game tick.</param>
    /// <param name="announcementsEnabled">Whether gated protocol announcements may play.</param>
    public void ProcessPending(int currentTick, bool announcementsEnabled)
    {
        if (theme == null)
            return;

        StrategyAdvisorView targetView = GetRequiredView();
        for (int i = 0; i < notificationsByPriority.Count; i++)
        {
            StrategyAdvisorNotificationTheme priority = notificationsByPriority[i];
            if (
                !pendingNotifications.TryGetValue(
                    priority.TableID,
                    out StrategyAdvisorNotificationTheme notification
                )
            )
                continue;

            pendingNotifications.Remove(priority.TableID);
            int expirationTick = pendingExpirationTicks[priority.TableID];
            pendingExpirationTicks.Remove(priority.TableID);
            if (expirationTick < currentTick)
                continue;

            int nextAllowedTick = nextAllowedTicks.TryGetValue(priority.TableID, out int tick)
                ? tick
                : 0;
            if (nextAllowedTick >= currentTick)
                continue;

            nextAllowedTicks[priority.TableID] = currentTick + theme.RepeatCooldownTicks;
            targetView.EnqueuePlaybacks(CreatePlaybackBatch(notification, announcementsEnabled));
            break;
        }
    }

    /// <summary>
    /// Resolves the notification code for a message, honoring subject-specific mappings.
    /// </summary>
    /// <param name="advisorTheme">The active advisor theme.</param>
    /// <param name="message">The delivered message.</param>
    /// <returns>The notification code mapped by the message.</returns>
    internal static int GetNotificationCode(StrategyAdvisorTheme advisorTheme, Message message)
    {
        if (advisorTheme == null)
            throw new ArgumentNullException(nameof(advisorTheme));
        if (message == null)
            throw new ArgumentNullException(nameof(message));

        if (message.AdvisorSubjectNotification == AdvisorSubjectNotification.None)
            return message.AdvisorNotificationCode;

        return advisorTheme.GetSubjectNotificationCode(
            message.AdvisorSubjectTypeID,
            message.AdvisorSubjectNotification
        );
    }

    /// <summary>
    /// Builds the protocol advisor command menu in authored display order.
    /// </summary>
    /// <param name="faction">The active player faction, or null when unavailable.</param>
    /// <returns>The advisor command presentation.</returns>
    internal static IReadOnlyList<StrategyMenuCommand> BuildCommandMenu(Faction faction)
    {
        return new List<StrategyMenuCommand>
        {
            new StrategyMenuCommand(
                StrategyContextMenuActions.AdvisorBuildShips,
                "Build Ships",
                faction != null
            ),
            new StrategyMenuCommand(
                StrategyContextMenuActions.AdvisorBuildTroops,
                "Build Troops",
                faction != null
            ),
            new StrategyMenuCommand(
                StrategyContextMenuActions.AdvisorBuildFacilities,
                "Build Facilities",
                faction != null
            ),
            new StrategyMenuCommand(
                StrategyContextMenuActions.AdvisorGalaxyOverview,
                "Galaxy Overview",
                faction != null
            ),
            new StrategyMenuCommand(
                StrategyContextMenuActions.AdvisorObjectives,
                "Objectives",
                faction != null
            ),
            CreateToggleCommand(
                StrategyContextMenuActions.AdvisorTranslateCounterpart,
                "Translate Counterpart",
                faction != null,
                faction?.TranslateCounterpart == true
            ),
            CreateToggleCommand(
                StrategyContextMenuActions.AdvisorAgentAdvice,
                "Agent Advice",
                faction != null,
                faction?.AgentAdvice == true
            ),
        };
    }

    /// <summary>
    /// Builds the droid advisor notification menu in authored display order.
    /// </summary>
    /// <param name="faction">The active player faction, or null when unavailable.</param>
    /// <returns>The advisor notification presentation.</returns>
    internal static IReadOnlyList<StrategyMenuCommand> BuildNotificationMenu(Faction faction)
    {
        List<StrategyMenuCommand> alerts = new List<StrategyMenuCommand>();
        AddMessageCommand(
            alerts,
            faction,
            StrategyContextMenuActions.AdvisorLoyaltyMessages,
            "Loyalty",
            MessageType.PopularSupport
        );
        AddMessageCommand(
            alerts,
            faction,
            StrategyContextMenuActions.AdvisorFleetMessages,
            "Fleets",
            MessageType.Fleet
        );
        AddMessageCommand(
            alerts,
            faction,
            StrategyContextMenuActions.AdvisorMissionMessages,
            "Mission",
            MessageType.Mission
        );
        AddMessageCommand(
            alerts,
            faction,
            StrategyContextMenuActions.AdvisorResourceMessages,
            "Resources",
            MessageType.Resource
        );
        AddMessageCommand(
            alerts,
            faction,
            StrategyContextMenuActions.AdvisorManufacturingMessages,
            "Manufacturing",
            MessageType.Manufacturing
        );
        AddMessageCommand(
            alerts,
            faction,
            StrategyContextMenuActions.AdvisorDefenseMessages,
            "Defense",
            MessageType.Defense
        );
        AddMessageCommand(
            alerts,
            faction,
            StrategyContextMenuActions.AdvisorConflictMessages,
            "Conflict",
            MessageType.Conflict
        );
        AddMessageCommand(
            alerts,
            faction,
            StrategyContextMenuActions.AdvisorAdviceMessages,
            "Advice",
            MessageType.Advice
        );
        AddMessageCommand(
            alerts,
            faction,
            StrategyContextMenuActions.AdvisorChatMessages,
            "Chat",
            MessageType.Chat
        );
        return new List<StrategyMenuCommand>
        {
            new StrategyMenuCommand(
                StrategyContextMenuActions.AdvisorMessages,
                "Messages",
                faction != null
            ),
            new StrategyMenuCommand("Message Alerts", faction != null, alerts),
        };
    }

    /// <summary>
    /// Executes one command selected from an advisor-owned context-menu request.
    /// </summary>
    /// <param name="request">The completed advisor request.</param>
    /// <param name="command">The selected advisor command.</param>
    public void OnContextMenuCommandSelected(
        ContextMenuRequest request,
        IContextMenuCommand command
    )
    {
        if (
            request?.Source is not AdvisorContextMenuSource source
            || command is not StrategyMenuCommand menuCommand
            || !menuCommand.Enabled
        )
            return;

        if (TryGetManufacturingType(menuCommand.Action, out ManufacturingType manufacturingType))
        {
            actions.BeginAdvisorConstruction(manufacturingType, source.SourceX, source.SourceY);
            return;
        }

        switch (menuCommand.Action)
        {
            case StrategyContextMenuActions.AdvisorGalaxyOverview:
                actions.OpenAdvisorReport(AdvisorReportMode.GalaxyOverview);
                return;
            case StrategyContextMenuActions.AdvisorObjectives:
                actions.OpenAdvisorReport(AdvisorReportMode.Objectives);
                return;
            case StrategyContextMenuActions.AdvisorMessages:
                actions.OpenMessagesTab(MessagesTab.All);
                return;
        }

        Faction faction = getPlayerFaction();
        if (faction == null)
            return;

        if (TryGetMessageType(menuCommand.Action, out MessageType messageType))
        {
            faction.ToggleAdvisorMessageNotification(messageType);
            return;
        }

        switch (menuCommand.Action)
        {
            case StrategyContextMenuActions.AdvisorTranslateCounterpart:
                faction.TranslateCounterpart = !faction.TranslateCounterpart;
                break;
            case StrategyContextMenuActions.AdvisorAgentAdvice:
                faction.AgentAdvice = !faction.AgentAdvice;
                break;
        }
    }

    /// <summary>
    /// Handles cancellation of an advisor-owned context-menu request.
    /// </summary>
    /// <param name="request">The canceled advisor request.</param>
    public void OnContextMenuCancelled(ContextMenuRequest request) { }

    /// <summary>
    /// Creates immutable advisor idle presentation from a configured theme.
    /// </summary>
    /// <param name="advisorTheme">The configured advisor theme.</param>
    /// <returns>The complete idle presentation snapshot.</returns>
    private StrategyAdvisorViewData CreateViewData(StrategyAdvisorTheme advisorTheme)
    {
        if (advisorTheme == null)
            return new StrategyAdvisorViewData(false, null, null, null, null, 0f);

        return new StrategyAdvisorViewData(
            true,
            ResolveTexture(advisorTheme.GetFramePath(advisorTheme.ProtocolIdleBitmapID, 0, false)),
            ResolveTexture(advisorTheme.GetFramePath(advisorTheme.DroidIdleBitmapID, 0, true)),
            ToRect(advisorTheme.ProtocolSourceLayout),
            ToRect(advisorTheme.DroidSourceLayout),
            advisorTheme.FrameIntervalSeconds
        );
    }

    /// <summary>
    /// Projects droid and optional protocol animations in their established playback order.
    /// </summary>
    /// <param name="notification">The selected advisor notification.</param>
    /// <param name="announcementsEnabled">Whether gated protocol announcements may play.</param>
    /// <returns>The ordered immutable playback batch.</returns>
    private IReadOnlyList<StrategyAdvisorAnimationViewData> CreatePlaybackBatch(
        StrategyAdvisorNotificationTheme notification,
        bool announcementsEnabled
    )
    {
        List<StrategyAdvisorAnimationViewData> playbacks =
            new List<StrategyAdvisorAnimationViewData>();
        AddPlayback(playbacks, notification.Droid, true);
        if (notification.Protocol?.RequiresAnnouncementsEnabled != true || announcementsEnabled)
            AddPlayback(playbacks, notification.Protocol, false);

        return playbacks;
    }

    /// <summary>
    /// Projects one valid configured animation into an ordered texture sequence.
    /// </summary>
    /// <param name="playbacks">The destination playback batch.</param>
    /// <param name="animation">The configured animation.</param>
    /// <param name="usesDroid">Whether the droid image presents the animation.</param>
    private void AddPlayback(
        ICollection<StrategyAdvisorAnimationViewData> playbacks,
        StrategyAdvisorAnimationTheme animation,
        bool usesDroid
    )
    {
        if (animation == null || animation.FrameCount <= 0)
            return;

        Texture2D[] frames = new Texture2D[animation.FrameCount];
        for (int frameIndex = 0; frameIndex < animation.FrameCount; frameIndex++)
        {
            frames[frameIndex] = ResolveTexture(
                theme.GetFramePath(animation.BitmapID, frameIndex, usesDroid)
            );
        }

        playbacks.Add(
            new StrategyAdvisorAnimationViewData(
                frames,
                usesDroid,
                animation.WaveID == 0 ? null : theme.GetAudioPath(animation.WaveID)
            )
        );
    }

    /// <summary>
    /// Routes a droid click to the messages index.
    /// </summary>
    private void HandleDroidClicked()
    {
        actions.OpenMessagesTab(MessagesTab.All);
    }

    /// <summary>
    /// Routes a droid context request to the notification menu.
    /// </summary>
    /// <param name="sourceX">The source-space horizontal pointer coordinate.</param>
    /// <param name="sourceY">The source-space vertical pointer coordinate.</param>
    private void HandleDroidContextRequested(int sourceX, int sourceY)
    {
        IReadOnlyList<StrategyMenuCommand> commands = BuildNotificationMenu(getPlayerFaction());
        actions.OpenAdvisorNotificationContextMenu(
            CreateContextMenuRequest(commands, sourceX, sourceY),
            sourceX,
            sourceY
        );
    }

    /// <summary>
    /// Requests the configured animation audio when local playback starts.
    /// </summary>
    /// <param name="animation">The animation that began playback.</param>
    private void HandlePlaybackStarted(StrategyAdvisorAnimationViewData animation)
    {
        if (!string.IsNullOrEmpty(animation?.AudioPath))
            playSfx(animation.AudioPath);
    }

    /// <summary>
    /// Routes a protocol advisor context request to the command menu.
    /// </summary>
    /// <param name="sourceX">The source-space horizontal pointer coordinate.</param>
    /// <param name="sourceY">The source-space vertical pointer coordinate.</param>
    private void HandleProtocolContextRequested(int sourceX, int sourceY)
    {
        IReadOnlyList<StrategyMenuCommand> commands = BuildCommandMenu(getPlayerFaction());
        actions.OpenAdvisorCommandContextMenu(
            CreateContextMenuRequest(commands, sourceX, sourceY),
            sourceX,
            sourceY
        );
    }

    /// <summary>
    /// Creates one advisor-owned context-menu request at its source position.
    /// </summary>
    /// <param name="commands">The ordered advisor commands.</param>
    /// <param name="sourceX">The source-space horizontal pointer coordinate.</param>
    /// <param name="sourceY">The source-space vertical pointer coordinate.</param>
    /// <returns>The completed context-menu request.</returns>
    private ContextMenuRequest CreateContextMenuRequest(
        IReadOnlyList<StrategyMenuCommand> commands,
        int sourceX,
        int sourceY
    )
    {
        return new ContextMenuRequest(
            new AdvisorContextMenuSource(sourceX, sourceY),
            commands,
            this
        );
    }

    /// <summary>
    /// Creates one advisor toggle command with a selection check mark.
    /// </summary>
    /// <param name="action">The semantic action identifier.</param>
    /// <param name="text">The displayed command label.</param>
    /// <param name="enabled">Whether the command may be selected.</param>
    /// <param name="selected">Whether the persisted option is selected.</param>
    /// <returns>The completed advisor command.</returns>
    private static StrategyMenuCommand CreateToggleCommand(
        int action,
        string text,
        bool enabled,
        bool selected
    )
    {
        return new StrategyMenuCommand(
            action,
            text,
            enabled,
            selected ? StrategyContextMenuIconKeys.CheckMark : StrategyContextMenuIconKeys.None,
            usesIconColumn: true
        );
    }

    /// <summary>
    /// Adds one persisted advisor message-category toggle.
    /// </summary>
    /// <param name="commands">The destination command collection.</param>
    /// <param name="faction">The active player faction, or null when unavailable.</param>
    /// <param name="action">The semantic action identifier.</param>
    /// <param name="text">The displayed command label.</param>
    /// <param name="messageType">The persisted message category.</param>
    private static void AddMessageCommand(
        ICollection<StrategyMenuCommand> commands,
        Faction faction,
        int action,
        string text,
        MessageType messageType
    )
    {
        commands.Add(
            CreateToggleCommand(
                action,
                text,
                faction != null,
                faction?.IsAdvisorMessageNotificationEnabled(messageType) == true
            )
        );
    }

    /// <summary>
    /// Maps an advisor notification action to its persisted message category.
    /// </summary>
    /// <param name="action">The semantic action identifier.</param>
    /// <param name="messageType">Receives the mapped message category.</param>
    /// <returns>True when the action represents a message-category toggle.</returns>
    private static bool TryGetMessageType(int action, out MessageType messageType)
    {
        switch (action)
        {
            case StrategyContextMenuActions.AdvisorLoyaltyMessages:
                messageType = MessageType.PopularSupport;
                return true;
            case StrategyContextMenuActions.AdvisorFleetMessages:
                messageType = MessageType.Fleet;
                return true;
            case StrategyContextMenuActions.AdvisorMissionMessages:
                messageType = MessageType.Mission;
                return true;
            case StrategyContextMenuActions.AdvisorResourceMessages:
                messageType = MessageType.Resource;
                return true;
            case StrategyContextMenuActions.AdvisorManufacturingMessages:
                messageType = MessageType.Manufacturing;
                return true;
            case StrategyContextMenuActions.AdvisorDefenseMessages:
                messageType = MessageType.Defense;
                return true;
            case StrategyContextMenuActions.AdvisorConflictMessages:
                messageType = MessageType.Conflict;
                return true;
            case StrategyContextMenuActions.AdvisorChatMessages:
                messageType = MessageType.Chat;
                return true;
            case StrategyContextMenuActions.AdvisorAdviceMessages:
                messageType = MessageType.Advice;
                return true;
            default:
                messageType = default;
                return false;
        }
    }

    /// <summary>
    /// Maps an advisor construction action to its manufacturing category.
    /// </summary>
    /// <param name="action">The semantic action identifier.</param>
    /// <param name="manufacturingType">Receives the mapped manufacturing category.</param>
    /// <returns>True when the action represents advisor-directed construction.</returns>
    private static bool TryGetManufacturingType(int action, out ManufacturingType manufacturingType)
    {
        switch (action)
        {
            case StrategyContextMenuActions.AdvisorBuildShips:
                manufacturingType = ManufacturingType.Ship;
                return true;
            case StrategyContextMenuActions.AdvisorBuildTroops:
                manufacturingType = ManufacturingType.Troop;
                return true;
            case StrategyContextMenuActions.AdvisorBuildFacilities:
                manufacturingType = ManufacturingType.Building;
                return true;
            default:
                manufacturingType = ManufacturingType.None;
                return false;
        }
    }

    /// <summary>
    /// Releases subscriptions when the bound authored advisor view is destroyed.
    /// </summary>
    /// <param name="destroyedView">The destroyed advisor view.</param>
    private void HandleViewDestroyed(StrategyAdvisorView destroyedView)
    {
        if (!ReferenceEquals(view, destroyedView))
            return;

        ReleaseView();
    }

    /// <summary>
    /// Releases subscriptions from the currently bound advisor view.
    /// </summary>
    private void ReleaseView()
    {
        if (ReferenceEquals(view, null))
            return;

        view.Destroyed -= HandleViewDestroyed;
        view.DroidClicked -= HandleDroidClicked;
        view.DroidContextRequested -= HandleDroidContextRequested;
        view.PlaybackStarted -= HandlePlaybackStarted;
        view.ProtocolContextRequested -= HandleProtocolContextRequested;
        view = null;
    }

    /// <summary>
    /// Resolves one non-empty configured texture path.
    /// </summary>
    /// <param name="path">The configured resource path.</param>
    /// <returns>The resolved texture, or null for an empty path.</returns>
    private Texture2D ResolveTexture(string path)
    {
        return string.IsNullOrEmpty(path) ? null : getTexture(path);
    }

    /// <summary>
    /// Clears pending notification, expiry, cooldown, and priority state for a theme change.
    /// </summary>
    private void ClearNotificationState()
    {
        pendingNotifications.Clear();
        pendingExpirationTicks.Clear();
        nextAllowedTicks.Clear();
        notificationsByPriority.Clear();
    }

    /// <summary>
    /// Verifies strategy-screen action routing is available.
    /// </summary>
    private void EnsureInitialized()
    {
        if (actions == null)
            throw new InvalidOperationException(
                $"{nameof(StrategyAdvisorController)} must be initialized before use."
            );
    }

    /// <summary>
    /// Gets the bound advisor view and rejects incomplete HUD composition.
    /// </summary>
    /// <returns>The bound authored advisor view.</returns>
    private StrategyAdvisorView GetRequiredView()
    {
        EnsureInitialized();
        return view
            ?? throw new InvalidOperationException(
                $"{nameof(StrategyAdvisorController)} must bind a view before rendering."
            );
    }

    /// <summary>
    /// Converts optional serialized source-space layout to immutable bounds.
    /// </summary>
    /// <param name="layout">The optional serialized layout.</param>
    /// <returns>The equivalent immutable bounds, or null.</returns>
    private static RectInt? ToRect(SourceRectLayout layout)
    {
        return layout == null ? null : new RectInt(layout.X, layout.Y, layout.Width, layout.Height);
    }

    /// <summary>
    /// Preserves the pointer position for one advisor-owned context-menu request.
    /// </summary>
    private sealed class AdvisorContextMenuSource
    {
        /// <summary>
        /// Creates advisor request state at one source-space position.
        /// </summary>
        /// <param name="sourceX">The source-space horizontal pointer coordinate.</param>
        /// <param name="sourceY">The source-space vertical pointer coordinate.</param>
        public AdvisorContextMenuSource(int sourceX, int sourceY)
        {
            SourceX = sourceX;
            SourceY = sourceY;
        }

        public int SourceX { get; }

        public int SourceY { get; }
    }
}
