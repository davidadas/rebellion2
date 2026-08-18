using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Advisor;
using Rebellion.Game.Factions;
using Rebellion.Game.Messages;
using Rebellion.Game.Results;
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
    private Action playbackCompleted;
    private Action playbackStarted;

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
        view.PlaybackCompleted += HandlePlaybackCompleted;
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
    /// Applies a changed advisor theme or refreshes asynchronously loaded idle frames.
    /// </summary>
    /// <param name="nextTheme">The active faction advisor theme.</param>
    public void Render(StrategyAdvisorTheme nextTheme)
    {
        if (ReferenceEquals(theme, nextTheme))
        {
            if (theme != null)
            {
                StrategyAdvisorViewData data = CreateViewData(theme);
                GetRequiredView()
                    .RefreshIdleFrames(data.ProtocolIdleTexture, data.DroidIdleTexture);
            }

            return;
        }

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
    /// <param name="delivery">The delivered message and transient presentation request.</param>
    /// <param name="currentTick">The current game tick.</param>
    /// <param name="notificationEnabled">Whether its message category permits notification.</param>
    public void Notify(MessageDeliveredResult delivery, int currentTick, bool notificationEnabled)
    {
        if (delivery?.Message == null || theme == null || !notificationEnabled)
            return;

        StrategyAdvisorNotificationTheme notification = ResolveNotification(
            delivery,
            out int lifetimeTicks
        );
        if (notification == null)
            return;

        if (
            notification.TableID < 0
            && notificationsByPriority.TrueForAll(candidate => candidate.TableID >= 0)
        )
        {
            notificationsByPriority.Add(notification);
            notificationsByPriority.Sort((left, right) => left.TableID.CompareTo(right.TableID));
        }

        pendingNotifications[notification.TableID] = notification;
        pendingExpirationTicks[notification.TableID] = currentTick + lifetimeTicks;
    }

    private StrategyAdvisorNotificationTheme ResolveNotification(
        MessageDeliveredResult delivery,
        out int lifetimeTicks
    )
    {
        AdvisorNotification authored = delivery.AdvisorNotification;
        StrategyAdvisorNotificationTheme preset = null;
        lifetimeTicks = 0;
        if (authored?.Preset.HasValue != false)
        {
            int code = GetNotificationCode(theme, delivery);
            preset = theme.GetNotification(code, out lifetimeTicks);
        }
        if (authored?.HasOverrides != true)
            return preset;

        lifetimeTicks = authored.LifetimeTicks ?? lifetimeTicks;
        if (lifetimeTicks <= 0)
            lifetimeTicks = 1;
        return new StrategyAdvisorNotificationTheme
        {
            TableID = preset?.TableID ?? -1,
            Droid = MergeAnimation(preset?.Droid, authored.Droid),
            Protocol = MergeAnimation(preset?.Protocol, authored.Protocol),
        };
    }

    private static StrategyAdvisorAnimationTheme MergeAnimation(
        StrategyAdvisorAnimationTheme preset,
        AdvisorAnimation authored
    )
    {
        if (authored == null)
            return preset;

        return new StrategyAdvisorAnimationTheme
        {
            Animation = authored.Animation ?? preset?.Animation,
            AnimationPath = authored.AnimationPath ?? preset?.AnimationPath,
            FrameCount = authored.FrameCount ?? preset?.FrameCount ?? 0,
            Audio = authored.Audio ?? preset?.Audio,
            AudioPath = authored.AudioPath ?? preset?.AudioPath,
            DelayBeforeSeconds = authored.DelayBeforeSeconds ?? preset?.DelayBeforeSeconds ?? 0f,
            RequiresAnnouncementsEnabled =
                authored.RequiresAnnouncementsEnabled
                ?? preset?.RequiresAnnouncementsEnabled
                ?? false,
        };
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

            int expirationTick = pendingExpirationTicks[priority.TableID];
            if (expirationTick < currentTick)
            {
                pendingNotifications.Remove(priority.TableID);
                pendingExpirationTicks.Remove(priority.TableID);
                continue;
            }

            int nextAllowedTick = nextAllowedTicks.TryGetValue(priority.TableID, out int tick)
                ? tick
                : int.MinValue;
            if (nextAllowedTick > currentTick)
                continue;

            if (
                !TryCreatePlaybackBatch(
                    notification,
                    announcementsEnabled,
                    out IReadOnlyList<StrategyAdvisorAnimationViewData> playbackBatch
                )
            )
                return;

            pendingNotifications.Remove(priority.TableID);
            pendingExpirationTicks.Remove(priority.TableID);
            nextAllowedTicks[priority.TableID] = currentTick + theme.RepeatCooldownTicks;
            targetView.EnqueuePlaybacks(playbackBatch);
            break;
        }
    }

    /// <summary>
    /// Immediately plays the authored response for an order rejected during transit.
    /// </summary>
    public void PlayInTransitOrderRejected()
    {
        List<StrategyAdvisorAnimationViewData> playbacks =
            new List<StrategyAdvisorAnimationViewData>();
        if (!TryAddPlayback(playbacks, theme?.InTransitOrderRejected, false))
            return;

        StrategyAdvisorAnimationViewData playback = playbacks.FirstOrDefault();
        if (playback != null)
            ReplaceAnimation(playback, null, null);
    }

    /// <summary>
    /// Cancels current protocol-advisor playback and replaces it with one resolved animation.
    /// </summary>
    /// <param name="animation">The resolved animation presentation.</param>
    /// <param name="started">Invoked when playback starts after its configured delay.</param>
    /// <param name="completed">Invoked after playback completes.</param>
    public void ReplaceAnimation(
        StrategyAdvisorAnimationViewData animation,
        Action started,
        Action completed
    )
    {
        StrategyAdvisorView targetView = GetRequiredView();
        playbackStarted = null;
        playbackCompleted = null;
        targetView.CancelPlayback();

        if (animation == null || animation.Frames.Count == 0)
        {
            completed?.Invoke();
            return;
        }

        playbackStarted = started;
        playbackCompleted = completed;
        targetView.EnqueuePlaybacks(new[] { animation });
    }

    /// <summary>
    /// Cancels the active advisor animation without invoking its completion callback.
    /// </summary>
    public void CancelAnimation()
    {
        playbackStarted = null;
        playbackCompleted = null;
        GetRequiredView().CancelPlayback();
    }

    /// <summary>
    /// Pauses the active advisor animation without discarding its position.
    /// </summary>
    public void PauseAnimation()
    {
        GetRequiredView().PausePlayback();
    }

    /// <summary>
    /// Resumes the active advisor animation from its paused position.
    /// </summary>
    public void ResumeAnimation()
    {
        GetRequiredView().ResumePlayback();
    }

    /// <summary>
    /// Invokes and clears the callback associated with completed generic playback.
    /// </summary>
    private void HandlePlaybackCompleted()
    {
        playbackStarted = null;
        Action completed = playbackCompleted;
        playbackCompleted = null;
        completed?.Invoke();
    }

    /// <summary>
    /// Clears pending and active advisor presentation from a replaced game session.
    /// </summary>
    public void ResetSession()
    {
        pendingNotifications.Clear();
        pendingExpirationTicks.Clear();
        nextAllowedTicks.Clear();
        playbackStarted = null;
        playbackCompleted = null;
        view?.ResetPlayback();
    }

    /// <summary>
    /// Resolves the notification code for a message, honoring subject-specific mappings.
    /// </summary>
    /// <param name="advisorTheme">The active advisor theme.</param>
    /// <param name="delivery">The delivered message and transient presentation request.</param>
    /// <returns>The notification code mapped by the message.</returns>
    internal static int GetNotificationCode(
        StrategyAdvisorTheme advisorTheme,
        MessageDeliveredResult delivery
    )
    {
        if (advisorTheme == null)
            throw new ArgumentNullException(nameof(advisorTheme));
        if (delivery == null)
            throw new ArgumentNullException(nameof(delivery));

        if (delivery.AdvisorSubjectNotification == AdvisorSubjectNotification.None)
            return (int)delivery.NotificationType;

        return advisorTheme.GetSubjectNotificationCode(
            delivery.AdvisorSubjectTypeID,
            delivery.AdvisorSubjectNotification
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
                StrategyMenuAction.AdvisorBuildShips,
                "Build Ships",
                faction != null
            ),
            new StrategyMenuCommand(
                StrategyMenuAction.AdvisorBuildTroops,
                "Build Troops",
                faction != null
            ),
            new StrategyMenuCommand(
                StrategyMenuAction.AdvisorBuildFacilities,
                "Build Facilities",
                faction != null
            ),
            new StrategyMenuCommand(
                StrategyMenuAction.AdvisorGalaxyOverview,
                "Galaxy Overview",
                faction != null
            ),
            new StrategyMenuCommand(
                StrategyMenuAction.AdvisorObjectives,
                "Objectives",
                faction != null
            ),
            CreateToggleCommand(
                StrategyMenuAction.AdvisorManageGarrisons,
                "Manage Garrisons",
                faction != null,
                faction?.ManageGarrisons == true
            ),
            CreateToggleCommand(
                StrategyMenuAction.AdvisorManageProduction,
                "Manage Production",
                faction != null,
                faction?.ManageProduction == true
            ),
            CreateToggleCommand(
                StrategyMenuAction.AdvisorTranslateCounterpart,
                "Translate Counterpart",
                faction != null,
                faction?.TranslateCounterpart == true
            ),
            CreateToggleCommand(
                StrategyMenuAction.AdvisorAgentAdvice,
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
            StrategyMenuAction.AdvisorLoyaltyMessages,
            "Loyalty",
            MessageType.PopularSupport
        );
        AddMessageCommand(
            alerts,
            faction,
            StrategyMenuAction.AdvisorFleetMessages,
            "Fleets",
            MessageType.Fleet
        );
        AddMessageCommand(
            alerts,
            faction,
            StrategyMenuAction.AdvisorMissionMessages,
            "Mission",
            MessageType.Mission
        );
        AddMessageCommand(
            alerts,
            faction,
            StrategyMenuAction.AdvisorResourceMessages,
            "Resources",
            MessageType.Resource
        );
        AddMessageCommand(
            alerts,
            faction,
            StrategyMenuAction.AdvisorManufacturingMessages,
            "Manufacturing",
            MessageType.Manufacturing
        );
        AddMessageCommand(
            alerts,
            faction,
            StrategyMenuAction.AdvisorDefenseMessages,
            "Defense",
            MessageType.Defense
        );
        AddMessageCommand(
            alerts,
            faction,
            StrategyMenuAction.AdvisorConflictMessages,
            "Conflict",
            MessageType.Conflict
        );
        AddMessageCommand(
            alerts,
            faction,
            StrategyMenuAction.AdvisorAdviceMessages,
            "Advice",
            MessageType.Advice
        );
        AddMessageCommand(
            alerts,
            faction,
            StrategyMenuAction.AdvisorChatMessages,
            "Chat",
            MessageType.Chat
        );
        return new List<StrategyMenuCommand>
        {
            new StrategyMenuCommand(
                StrategyMenuAction.AdvisorMessages,
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
            case StrategyMenuAction.AdvisorGalaxyOverview:
                actions.OpenAdvisorReport(AdvisorReportMode.GalaxyOverview);
                return;
            case StrategyMenuAction.AdvisorObjectives:
                actions.OpenAdvisorReport(AdvisorReportMode.Objectives);
                return;
            case StrategyMenuAction.AdvisorMessages:
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
            case StrategyMenuAction.AdvisorManageGarrisons:
                faction.ManageGarrisons = !faction.ManageGarrisons;
                if (faction.ManageGarrisons)
                    actions.ProcessAdvisorAutomation(faction);
                break;
            case StrategyMenuAction.AdvisorManageProduction:
                faction.ManageProduction = !faction.ManageProduction;
                if (faction.ManageProduction)
                    actions.ProcessAdvisorAutomation(faction);
                break;
            case StrategyMenuAction.AdvisorTranslateCounterpart:
                faction.TranslateCounterpart = !faction.TranslateCounterpart;
                break;
            case StrategyMenuAction.AdvisorAgentAdvice:
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
            ResolveTexture(advisorTheme.GetFramePath(advisorTheme.ProtocolIdleAnimation, 0, false)),
            ResolveTexture(advisorTheme.GetFramePath(advisorTheme.DroidIdleAnimation, 0, true)),
            ToRect(advisorTheme.ProtocolSourceLayout),
            ToRect(advisorTheme.DroidSourceLayout),
            advisorTheme.FrameIntervalSeconds
        );
    }

    /// <summary>
    /// Tries to project droid and optional protocol animations after every frame is available.
    /// </summary>
    /// <param name="notification">The selected advisor notification.</param>
    /// <param name="announcementsEnabled">Whether gated protocol announcements may play.</param>
    /// <param name="playbackBatch">Receives the ordered immutable playback batch.</param>
    /// <returns>True when every required animation frame is available.</returns>
    private bool TryCreatePlaybackBatch(
        StrategyAdvisorNotificationTheme notification,
        bool announcementsEnabled,
        out IReadOnlyList<StrategyAdvisorAnimationViewData> playbackBatch
    )
    {
        List<StrategyAdvisorAnimationViewData> playbacks =
            new List<StrategyAdvisorAnimationViewData>();
        bool droidReady = TryAddPlayback(playbacks, notification.Droid, true);
        bool protocolReady = true;
        if (notification.Protocol?.RequiresAnnouncementsEnabled != true || announcementsEnabled)
            protocolReady = TryAddPlayback(playbacks, notification.Protocol, false);

        playbackBatch = playbacks;
        return droidReady && protocolReady;
    }

    /// <summary>
    /// Tries to project one configured animation after every frame is available.
    /// </summary>
    /// <param name="playbacks">The destination playback batch.</param>
    /// <param name="animation">The configured animation.</param>
    /// <param name="usesDroid">Whether the droid image presents the animation.</param>
    /// <returns>True when the animation is absent, empty, or fully available.</returns>
    private bool TryAddPlayback(
        ICollection<StrategyAdvisorAnimationViewData> playbacks,
        StrategyAdvisorAnimationTheme animation,
        bool usesDroid
    )
    {
        if (animation == null || animation.FrameCount <= 0)
            return true;

        Texture2D[] frames = new Texture2D[animation.FrameCount];
        bool ready = true;
        for (int frameIndex = 0; frameIndex < animation.FrameCount; frameIndex++)
        {
            string framePath = string.IsNullOrWhiteSpace(animation.AnimationPath)
                ? theme.GetFramePath(animation.Animation, frameIndex, usesDroid)
                : $"{animation.AnimationPath}/frame-{frameIndex:D3}";
            frames[frameIndex] = ResolveTexture(framePath);
            ready &= frames[frameIndex] != null;
        }

        if (!ready)
            return false;

        playbacks.Add(
            new StrategyAdvisorAnimationViewData(
                frames,
                usesDroid,
                !string.IsNullOrWhiteSpace(animation.AudioPath) ? animation.AudioPath
                    : string.IsNullOrWhiteSpace(animation.Audio) ? null
                    : theme.GetAudioPath(animation.Audio),
                animation.DelayBeforeSeconds
            )
        );
        return true;
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
        Action started = playbackStarted;
        playbackStarted = null;
        started?.Invoke();

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
        StrategyMenuAction action,
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
        StrategyMenuAction action,
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
    private static bool TryGetMessageType(StrategyMenuAction action, out MessageType messageType)
    {
        switch (action)
        {
            case StrategyMenuAction.AdvisorLoyaltyMessages:
                messageType = MessageType.PopularSupport;
                return true;
            case StrategyMenuAction.AdvisorFleetMessages:
                messageType = MessageType.Fleet;
                return true;
            case StrategyMenuAction.AdvisorMissionMessages:
                messageType = MessageType.Mission;
                return true;
            case StrategyMenuAction.AdvisorResourceMessages:
                messageType = MessageType.Resource;
                return true;
            case StrategyMenuAction.AdvisorManufacturingMessages:
                messageType = MessageType.Manufacturing;
                return true;
            case StrategyMenuAction.AdvisorDefenseMessages:
                messageType = MessageType.Defense;
                return true;
            case StrategyMenuAction.AdvisorConflictMessages:
                messageType = MessageType.Conflict;
                return true;
            case StrategyMenuAction.AdvisorChatMessages:
                messageType = MessageType.Chat;
                return true;
            case StrategyMenuAction.AdvisorAdviceMessages:
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
    private static bool TryGetManufacturingType(
        StrategyMenuAction action,
        out ManufacturingType manufacturingType
    )
    {
        switch (action)
        {
            case StrategyMenuAction.AdvisorBuildShips:
                manufacturingType = ManufacturingType.Ship;
                return true;
            case StrategyMenuAction.AdvisorBuildTroops:
                manufacturingType = ManufacturingType.Troop;
                return true;
            case StrategyMenuAction.AdvisorBuildFacilities:
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
        view.PlaybackCompleted -= HandlePlaybackCompleted;
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
