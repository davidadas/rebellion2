using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Messages;
using Rebellion.Game.Results;
using Rebellion.Game.UIState;
using Rebellion.Game.Units;
using Rebellion.Gameplay;
using Rebellion.Gameplay.PlayerRequests;
using UnityEngine;

/// <summary>
/// Owns strategy HUD theme projection, resource resolution, audio policy, and action routing.
/// </summary>
public sealed class StrategyHudController : IContextMenuReceiver
{
    private readonly Func<FactionTheme> getPlayerTheme;
    private readonly Func<string, Texture2D> getTexture;
    private readonly Action<string> playSfx;
    private readonly StrategyAdvisorController advisorController;
    private readonly Dictionary<TickSpeed, ContextMenuSelectionState> speedSelectionStates = new()
    {
        [TickSpeed.Paused] = new(),
        [TickSpeed.VerySlow] = new(),
        [TickSpeed.Slow] = new(),
        [TickSpeed.Medium] = new(),
        [TickSpeed.Fast] = new(),
    };

    private IStrategyHudActions actions;
    private StrategyHudView view;
    private StrategyHudPlayerSpeedSelection pendingSpeedRequest;
    private TickSpeed localSpeedSelection;
    private IReadOnlyList<StrategyHudPlayerSpeedSelection> playerSpeedSelections =
        Array.Empty<StrategyHudPlayerSpeedSelection>();

    /// <summary>
    /// Creates a strategy HUD controller with current-faction, theme, texture, and audio dependencies.
    /// </summary>
    /// <param name="getPlayerFaction">Returns the current player faction.</param>
    /// <param name="getPlayerTheme">Returns the current player faction theme.</param>
    /// <param name="getTexture">Resolves a texture from a configured resource path.</param>
    /// <param name="playSfx">Plays a strategy sound-effect path.</param>
    /// <param name="playerRequests">Completes advisor requests through the session action boundary.</param>
    /// <param name="playSfxInstance">Plays an independently stoppable advisor response.</param>
    /// <param name="getAudioDuration">Gets the duration of a loaded advisor response.</param>
    public StrategyHudController(
        Func<Faction> getPlayerFaction,
        Func<FactionTheme> getPlayerTheme,
        Func<string, Texture2D> getTexture,
        Action<string> playSfx,
        IPlayerRequestDispatcher playerRequests,
        Func<string, AudioPlaybackHandle> playSfxInstance = null,
        Func<string, float> getAudioDuration = null
    )
    {
        if (getPlayerFaction == null)
            throw new ArgumentNullException(nameof(getPlayerFaction));

        this.getPlayerTheme =
            getPlayerTheme ?? throw new ArgumentNullException(nameof(getPlayerTheme));
        this.getTexture = getTexture ?? throw new ArgumentNullException(nameof(getTexture));
        this.playSfx = playSfx ?? throw new ArgumentNullException(nameof(playSfx));
        advisorController = new StrategyAdvisorController(
            getPlayerFaction,
            getTexture,
            playSfx,
            playerRequests,
            playSfxInstance: playSfxInstance,
            getAudioDuration: getAudioDuration
        );
    }

    /// <summary>
    /// Connects the HUD controller and its advisor controller to strategy-screen actions.
    /// </summary>
    /// <param name="actions">The strategy-screen action boundary.</param>
    public void Initialize(IStrategyHudActions actions)
    {
        this.actions = actions ?? throw new ArgumentNullException(nameof(actions));
        advisorController.Initialize(actions);
    }

    /// <summary>
    /// Binds advisor timing to the active local player's durable state and simulation counter.
    /// </summary>
    /// <param name="getPlayerUIState">Returns the current local player's interface state.</param>
    /// <param name="getSimulationTick">Returns the active simulation counter.</param>
    public void BindNotificationContext(
        Func<PlayerUIState> getPlayerUIState,
        Func<int> getSimulationTick
    )
    {
        advisorController.BindNotificationContext(getPlayerUIState, getSimulationTick);
    }

    /// <summary>
    /// Subscribes the controller to the authored HUD and advisor views exactly once.
    /// </summary>
    /// <param name="nextView">The authored HUD view.</param>
    public void BindView(StrategyHudView nextView)
    {
        if (nextView == null)
            throw new ArgumentNullException(nameof(nextView));

        EnsureInitialized();
        if (view == nextView)
            return;

        ReleaseView();
        view = nextView;
        view.ControlPressed += HandleControlPressed;
        view.Destroyed += HandleViewDestroyed;
        view.HudButtonRequested += HandleHudButtonRequested;
        view.MessageTabRequested += HandleMessageTabRequested;
        view.RenderRequested += HandleRenderRequested;
        view.SpeedContextRequested += HandleSpeedContextRequested;
        view.SpeedResponseRequested += HandleSpeedResponseRequested;
        advisorController.BindView(view.AdvisorView);
    }

    /// <summary>
    /// Projects a current strategy HUD state snapshot and renders the bound authored view.
    /// </summary>
    /// <param name="data">The current strategy HUD values.</param>
    public void Render(StrategyHudRenderData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        StrategyHudView targetView = GetRequiredView();
        FactionTheme theme = getPlayerTheme();
        pendingSpeedRequest = GetSpeedRequest(data);
        playerSpeedSelections = data.PlayerSpeeds;
        foreach (var selection in speedSelectionStates)
            selection.Value.SetColors(
                GetSpeedSelectionColors(selection.Key, playerSpeedSelections)
            );
        localSpeedSelection =
            data.PlayerSpeeds.FirstOrDefault(player => player.IsLocal)?.Speed ?? data.Speed;
        StrategyHudViewData viewData = CreateViewData(data, theme);
        targetView.Render(viewData);
        advisorController.Render(theme?.StrategyAdvisor);
    }

    /// <summary>
    /// Queues an advisor notification derived from a newly delivered player message.
    /// </summary>
    /// <param name="delivery">The delivered message and presentation request.</param>
    /// <param name="notificationEnabled">Whether its message category permits notification.</param>
    public void NotifyAdvisor(MessageDeliveredResult delivery, bool notificationEnabled)
    {
        advisorController.Notify(delivery, notificationEnabled);
    }

    /// <summary>
    /// Processes the next pending advisor notification.
    /// </summary>
    /// <param name="announcementsEnabled">Whether gated protocol announcements may play.</param>
    public void ProcessAdvisor(bool announcementsEnabled)
    {
        advisorController.ProcessPending(announcementsEnabled);
    }

    /// <summary>
    /// Begins protocol-advisor construction targeting for a keyboard shortcut.
    /// </summary>
    /// <param name="manufacturingType">The requested manufacturing category.</param>
    public void BeginAdvisorConstruction(ManufacturingType manufacturingType)
    {
        advisorController.BeginConstruction(manufacturingType);
    }

    /// <summary>
    /// Plays the protocol advisor's authored rejection for a unit already in transit.
    /// </summary>
    public void PlayInTransitOrderRejected()
    {
        advisorController.PlayInTransitOrderRejected();
    }

    /// <summary>
    /// Plays the protocol advisor's authored rejection for an invalid order.
    /// </summary>
    public void PlayInvalidOrderRejected()
    {
        advisorController.PlayInvalidOrderRejected();
    }

    /// <summary>
    /// Plays the protocol advisor's authored rejection for a destination under construction.
    /// </summary>
    public void PlayUnitUnderConstructionOrderRejected()
    {
        advisorController.PlayUnitUnderConstructionOrderRejected();
    }

    /// <summary>
    /// Cancels current protocol-advisor playback and replaces it with one resolved animation.
    /// </summary>
    /// <param name="animation">The resolved animation presentation.</param>
    /// <param name="started">Invoked when playback starts after its configured delay.</param>
    /// <param name="completed">Invoked after playback completes.</param>
    public void ReplaceAdvisorAnimation(
        StrategyAdvisorAnimationViewData animation,
        Action started,
        Action completed
    )
    {
        advisorController.ReplaceAnimation(animation, started, completed);
    }

    /// <summary>
    /// Cancels active advisor animation without invoking its completion callback.
    /// </summary>
    public void CancelAdvisorAnimation()
    {
        advisorController.CancelAnimation();
    }

    /// <summary>
    /// Pauses the active advisor animation.
    /// </summary>
    public void PauseAdvisorAnimation()
    {
        advisorController.PauseAnimation();
    }

    /// <summary>
    /// Resumes the active advisor animation.
    /// </summary>
    public void ResumeAdvisorAnimation()
    {
        advisorController.ResumeAnimation();
    }

    /// <summary>
    /// Clears advisor activity associated with a replaced game session.
    /// </summary>
    public void ResetSession()
    {
        advisorController.ResetSession();
    }

    /// <summary>
    /// Applies one selected command from a HUD-owned context-menu request.
    /// </summary>
    /// <param name="request">The completed HUD request.</param>
    /// <param name="command">The selected HUD command.</param>
    public void OnContextMenuCommandSelected(
        ContextMenuRequest request,
        IContextMenuCommand command
    )
    {
        if (
            !ReferenceEquals(request?.Source, this)
            || command is not StrategyMenuCommand menuCommand
            || !menuCommand.Enabled
        )
            return;

        if (menuCommand.Action.TryGetGameSpeed(out TickSpeed speed))
            actions.SetGameSpeed(speed);
    }

    /// <summary>
    /// Handles cancellation of a HUD-owned context-menu request.
    /// </summary>
    /// <param name="request">The canceled HUD request.</param>
    public void OnContextMenuCancelled(ContextMenuRequest request) { }

    /// <summary>
    /// Creates immutable HUD view data from strategy state and the active faction theme.
    /// </summary>
    /// <param name="data">The current strategy HUD values.</param>
    /// <param name="theme">The active player faction theme.</param>
    /// <returns>The complete presentation snapshot.</returns>
    internal StrategyHudViewData CreateViewData(StrategyHudRenderData data, FactionTheme theme)
    {
        TacticalHUDLayout hudTheme = theme?.TacticalHUDLayout;
        Color textColor = theme?.GetPrimaryColor() ?? Color.white;
        return new StrategyHudViewData(
            backgroundTexture: ResolveTexture(hudTheme?.ImagePath),
            tickCounter: CreateCounter(
                data.Speed == TickSpeed.Paused ? "PAUSED" : data.TickText,
                textColor,
                hudTheme?.TickCounterSourceLayout
            ),
            rawMaterialsCounter: CreateCounter(
                data.RawMaterialsText,
                textColor,
                hudTheme?.RawMaterialsSourceLayout
            ),
            refinedMaterialsCounter: CreateCounter(
                data.RefinedMaterialsText,
                textColor,
                hudTheme?.RefinedMaterialsSourceLayout
            ),
            maintenanceCounter: CreateCounter(
                data.MaintenanceText,
                textColor,
                hudTheme?.MaintenanceSourceLayout
            ),
            resourceBreakdown: CreateResourceBreakdownViewData(
                data.ResourceBreakdown,
                textColor,
                hudTheme?.RawMaterialsSourceLayout,
                hudTheme?.RefinedMaterialsSourceLayout,
                hudTheme?.MaintenanceSourceLayout
            ),
            speedIndicatorTexture: ResolveTexture(
                GetSpeedIndicatorPath(hudTheme?.SpeedIndicators, data.Speed)
            ),
            speedIndicatorBounds: ToRect(hudTheme?.SpeedIndicatorSourceLayout),
            speedContextBounds: ToRect(hudTheme?.SpeedContextSourceLayout),
            buttons: CreateButtons(hudTheme?.Buttons),
            messageNotifications: CreateMessageNotifications(hudTheme?.MessageNotifications, data),
            speedAgreementText: string.IsNullOrEmpty(data.ConnectionNotice)
                ? CreateSpeedAgreementText(data)
                : data.ConnectionNotice,
            canRespondToSpeedRequest: string.IsNullOrEmpty(data.ConnectionNotice)
                && GetSpeedRequest(data) != null,
            sessionNoticeBounds: CreateSessionNoticeBounds(
                theme?.StrategyWindowPlacements?.WindowBounds
            )
        );
    }

    /// <summary>
    /// Insets the notice from the upper-left corner of the faction's galaxy play area.
    /// </summary>
    /// <param name="playArea">The source-space rectangle available to strategy windows.</param>
    /// <returns>The left-aligned notice rectangle, or null when no play area is configured.</returns>
    private static RectInt? CreateSessionNoticeBounds(SourceRectLayout playArea)
    {
        if (playArea == null)
            return null;
        int width = Math.Min(
            StrategyHudView.SessionNoticeWidth,
            playArea.Width - 2 * StrategyHudView.SessionNoticePadding
        );
        return new RectInt(
            playArea.X + StrategyHudView.SessionNoticePadding,
            playArea.Y + StrategyHudView.SessionNoticePadding,
            width,
            StrategyHudView.SessionNoticeHeight
        );
    }

    /// <summary>
    /// Shows only an outstanding proposal from the other player.
    /// </summary>
    /// <param name="data">The immutable current HUD facts.</param>
    /// <returns>The compact request text, or empty when no response is needed.</returns>
    internal static string CreateSpeedAgreementText(StrategyHudRenderData data)
    {
        StrategyHudPlayerSpeedSelection request = GetSpeedRequest(data);
        return request == null
            ? string.Empty
            : request.Name
                + (
                    request.Speed == TickSpeed.Paused
                        ? " is requesting a pause"
                        : " is requesting " + GetSpeedName(request.Speed) + " speed"
                );
    }

    /// <summary>
    /// Identifies another human's unagreed proposal separately from the active clock speed.
    /// </summary>
    /// <param name="data">The current acknowledged participant selections.</param>
    /// <returns>The outstanding remote selection, or null when no response is needed.</returns>
    private static StrategyHudPlayerSpeedSelection GetSpeedRequest(StrategyHudRenderData data)
    {
        StrategyHudPlayerSpeedSelection local = data.PlayerSpeeds.FirstOrDefault(player =>
            player.IsLocal
        );
        return local == null
            ? null
            : data.PlayerSpeeds.FirstOrDefault(player =>
                !player.IsLocal && player.Speed != data.Speed && player.Speed != local.Speed
            );
    }

    /// <summary>
    /// Answers the proposal captured by the last rendered notice.
    /// </summary>
    /// <param name="accept">Whether to accept rather than decline the proposal.</param>
    private void HandleSpeedResponseRequested(bool accept)
    {
        if (pendingSpeedRequest != null)
            actions.RespondToSpeedRequest(
                pendingSpeedRequest.FactionID,
                pendingSpeedRequest.Speed,
                pendingSpeedRequest.Revision,
                accept
            );
    }

    /// <summary>
    /// Formats a speed selection for the native participant status panel.
    /// </summary>
    /// <param name="speed">The selected supported speed.</param>
    /// <returns>The readable speed label.</returns>
    private static string GetSpeedName(TickSpeed speed) =>
        speed == TickSpeed.VerySlow ? "Very slow" : speed.ToString();

    /// <summary>
    /// Enumerates all HUD artwork that can appear for one faction theme.
    /// </summary>
    /// <param name="theme">The faction theme to inspect.</param>
    /// <returns>The configured texture paths.</returns>
    internal static IEnumerable<string> GetTexturePaths(FactionTheme theme)
    {
        TacticalHUDLayout hud = theme?.TacticalHUDLayout;
        if (hud == null)
            return Array.Empty<string>();

        IEnumerable<string> paths = new[]
        {
            hud.ImagePath,
            hud.SpeedIndicators?.PausedImagePath,
            hud.SpeedIndicators?.VerySlowImagePath,
            hud.SpeedIndicators?.SlowImagePath,
            hud.SpeedIndicators?.MediumImagePath,
            hud.SpeedIndicators?.FastImagePath,
        };
        paths = paths.Concat(
            hud.Buttons.SelectMany(button =>
                new[] { button?.UpImagePath, button?.PressedImagePath }
            )
        );
        paths = paths.Concat(
            hud.MessageNotifications.SelectMany(notification =>
                new[] { notification?.DefaultImagePath, notification?.HighlightedImagePath }
            )
        );
        return paths.Where(path => !string.IsNullOrEmpty(path));
    }

    /// <summary>
    /// Maps a game speed to the corresponding configured speed-indicator path.
    /// </summary>
    /// <param name="theme">The configured speed-indicator artwork.</param>
    /// <param name="speed">The current game speed.</param>
    /// <returns>The selected indicator path, or null when no theme is available.</returns>
    internal static string GetSpeedIndicatorPath(SpeedIndicatorTheme theme, TickSpeed speed)
    {
        return theme?.GetImagePath(GetSourceSpeed(speed));
    }

    /// <summary>
    /// Maps a game speed to the numeric source speed used by the theme table.
    /// </summary>
    /// <param name="speed">The current game speed.</param>
    /// <returns>The corresponding source speed value.</returns>
    internal static int GetSourceSpeed(TickSpeed speed)
    {
        return speed switch
        {
            TickSpeed.VerySlow => 1,
            TickSpeed.Slow => 2,
            TickSpeed.Medium => 3,
            TickSpeed.Fast => 4,
            _ => 0,
        };
    }

    /// <summary>
    /// Collects message categories containing at least one unacknowledged notification.
    /// </summary>
    /// <param name="faction">The faction whose message state is projected into the HUD.</param>
    /// <returns>The message categories with unacknowledged notifications.</returns>
    internal static HashSet<MessageType> GetUnacknowledgedMessageTypes(Faction faction)
    {
        HashSet<MessageType> types = new HashSet<MessageType>();
        if (faction?.Messages == null)
            return types;

        foreach (KeyValuePair<MessageType, List<Message>> entry in faction.Messages)
        {
            if (entry.Value == null)
                continue;

            foreach (Message message in entry.Value)
            {
                if (message?.NotificationAcknowledged == false)
                {
                    types.Add(entry.Key);
                    break;
                }
            }
        }

        return types;
    }

    /// <summary>
    /// Maps the current resource economy into HUD presentation data.
    /// </summary>
    /// <param name="summary">The authoritative resource economy summary.</param>
    /// <returns>The resource-economy presentation.</returns>
    internal static StrategyHudResourceBreakdown CreateResourceBreakdown(
        ResourceEconomySummary summary
    )
    {
        if (summary == null)
            return StrategyHudResourceBreakdown.Empty;

        return new StrategyHudResourceBreakdown(
            summary.Mines.Active,
            summary.Mines.Offline,
            summary.Mines.Building,
            summary.Mines.EnRoute,
            summary.Refineries.Active,
            summary.Refineries.Offline,
            summary.Refineries.Building,
            summary.Refineries.EnRoute,
            summary.RawOutputPerTick,
            summary.DeliveredRawOutputPerTick,
            summary.ProjectedRawOutputPerTick,
            summary.RefinedOutputPerTick,
            summary.DeliveredRefinedOutputPerTick,
            summary.ProjectedRefinedOutputPerTick,
            summary.MaintenanceHeadroom,
            summary.DeliveredMaintenanceHeadroom,
            summary.ProjectedMaintenanceHeadroom,
            new StrategyHudMaintenanceBreakdown(
                summary.Maintenance.CapitalShips,
                summary.Maintenance.Starfighters,
                summary.Maintenance.Regiments,
                summary.Maintenance.SpecialForces,
                summary.Maintenance.Facilities,
                summary.Maintenance.Orders,
                summary.Maintenance.Deployed,
                summary.Maintenance.EnRoute,
                summary.Maintenance.Building
            )
        );
    }

    /// <summary>
    /// Projects the resource hover panel beneath the active faction's resource counters.
    /// </summary>
    /// <param name="totals">The current facility totals.</param>
    /// <param name="accentColor">The active faction accent color.</param>
    /// <param name="rawMaterialsLayout">The raw-material counter layout.</param>
    /// <param name="refinedMaterialsLayout">The refined-material counter layout.</param>
    /// <param name="maintenanceLayout">The maintenance counter layout.</param>
    /// <returns>The resource-breakdown presentation data.</returns>
    private static StrategyHudResourceBreakdownViewData CreateResourceBreakdownViewData(
        StrategyHudResourceBreakdown totals,
        Color accentColor,
        SourceRectLayout rawMaterialsLayout,
        SourceRectLayout refinedMaterialsLayout,
        SourceRectLayout maintenanceLayout
    )
    {
        return new StrategyHudResourceBreakdownViewData(
            totals,
            accentColor,
            CreateResourcePopover(
                rawMaterialsLayout,
                StrategyHudView.ResourceBreakdownPanelWidth,
                StrategyHudView.ResourceBreakdownPanelHeight
            ),
            CreateResourcePopover(
                refinedMaterialsLayout,
                StrategyHudView.ResourceBreakdownPanelWidth,
                StrategyHudView.ResourceBreakdownPanelHeight
            ),
            CreateResourcePopover(
                maintenanceLayout,
                StrategyHudView.ResourceBreakdownPanelWidth,
                StrategyHudView.ResourceBreakdownPanelHeight
            )
        );
    }

    /// <summary>
    /// Projects one resource counter into its own compact popover.
    /// </summary>
    /// <param name="layout">The resource-counter layout.</param>
    /// <param name="panelWidth">The popover width.</param>
    /// <param name="panelHeight">The popover height.</param>
    /// <returns>The resource popover placement.</returns>
    private static StrategyHudResourcePopoverViewData CreateResourcePopover(
        SourceRectLayout layout,
        int panelWidth,
        int panelHeight
    )
    {
        if (layout == null)
            return StrategyHudResourcePopoverViewData.Empty;

        RectInt hitArea = ToRequiredRect(layout);
        RectInt panelBounds = new RectInt(
            Math.Max(0, hitArea.xMax - panelWidth),
            hitArea.yMax + 3,
            panelWidth,
            panelHeight
        );
        return new StrategyHudResourcePopoverViewData(hitArea, panelBounds);
    }

    /// <summary>
    /// Projects one HUD counter from dynamic content and optional themed bounds.
    /// </summary>
    /// <param name="text">The displayed value.</param>
    /// <param name="color">The displayed faction color.</param>
    /// <param name="layout">The optional themed source-space bounds.</param>
    /// <returns>The immutable counter presentation data.</returns>
    private static StrategyHudCounterViewData CreateCounter(
        string text,
        Color color,
        SourceRectLayout layout
    )
    {
        return new StrategyHudCounterViewData(text, color, ToRect(layout));
    }

    /// <summary>
    /// Projects themed HUD buttons in their configured authored-slot order.
    /// </summary>
    /// <param name="themes">The configured HUD button themes.</param>
    /// <returns>The immutable button presentations.</returns>
    private IReadOnlyList<StrategyHudButtonViewData> CreateButtons(
        IReadOnlyList<StrategyHudButtonTheme> themes
    )
    {
        if (themes == null || themes.Count == 0)
            return Array.Empty<StrategyHudButtonViewData>();

        List<StrategyHudButtonViewData> buttons = new List<StrategyHudButtonViewData>(themes.Count);
        for (int i = 0; i < themes.Count; i++)
        {
            StrategyHudButtonTheme theme = themes[i];
            if (theme?.HitArea == null)
                throw new InvalidOperationException($"HUD button theme {i} is missing HitArea.");

            buttons.Add(
                new StrategyHudButtonViewData(
                    theme.Action,
                    ToRequiredRect(theme.HitArea),
                    ResolveTexture(theme.UpImagePath),
                    ResolveTexture(theme.PressedImagePath),
                    ToRequiredRect(theme.PressedImageLayout ?? theme.HitArea)
                )
            );
        }

        return buttons;
    }

    /// <summary>
    /// Projects default or highlighted notification artwork in configured slot order.
    /// </summary>
    /// <param name="themes">The configured notification slot themes.</param>
    /// <param name="data">The current unread-message state.</param>
    /// <returns>The immutable notification presentations.</returns>
    private IReadOnlyList<StrategyHudMessageNotificationViewData> CreateMessageNotifications(
        IReadOnlyList<StrategyHudMessageNotificationTheme> themes,
        StrategyHudRenderData data
    )
    {
        if (themes == null || themes.Count == 0)
            return Array.Empty<StrategyHudMessageNotificationViewData>();

        List<StrategyHudMessageNotificationViewData> notifications =
            new List<StrategyHudMessageNotificationViewData>(themes.Count);
        for (int i = 0; i < themes.Count; i++)
        {
            StrategyHudMessageNotificationTheme theme = themes[i];
            if (theme?.SourceLayout == null)
                throw new InvalidOperationException(
                    $"HUD message-notification theme {i} is missing SourceLayout."
                );

            string imagePath = data.HasUnacknowledgedMessageType(theme.MessageType)
                ? theme.HighlightedImagePath
                : theme.DefaultImagePath;
            notifications.Add(
                new StrategyHudMessageNotificationViewData(
                    theme.Tab,
                    ResolveTexture(imagePath),
                    ToRequiredRect(theme.SourceLayout)
                )
            );
        }

        return notifications;
    }

    /// <summary>
    /// Selects and requests the HUD control-press sound for one semantic action.
    /// </summary>
    /// <param name="action">The pressed semantic HUD action.</param>
    private void HandleControlPressed(StrategyHudAction action)
    {
        playSfx(
            action == StrategyHudAction.GalacticInformationDisplay
                ? StrategyUISoundPaths.GalacticInformationOpen
                : StrategyUISoundPaths.ControlPress
        );
    }

    /// <summary>
    /// Routes a released HUD button to the strategy screen.
    /// </summary>
    /// <param name="action">The released semantic HUD action.</param>
    /// <param name="sourceX">The source-space horizontal pointer coordinate.</param>
    /// <param name="sourceY">The source-space vertical pointer coordinate.</param>
    private void HandleHudButtonRequested(StrategyHudAction action, int sourceX, int sourceY)
    {
        actions.ReleaseHudButton(action, sourceX, sourceY);
    }

    /// <summary>
    /// Routes a message-notification click to the strategy screen.
    /// </summary>
    /// <param name="tab">The requested messages tab.</param>
    private void HandleMessageTabRequested(MessagesTab tab)
    {
        actions.OpenMessagesTab(tab);
    }

    /// <summary>
    /// Requests a strategy render when a HUD click cannot be mapped to source coordinates.
    /// </summary>
    private void HandleRenderRequested()
    {
        actions.RequestHudRender();
    }

    /// <summary>
    /// Routes a speed-menu request to the strategy screen.
    /// </summary>
    /// <param name="sourceX">The source-space horizontal pointer coordinate.</param>
    /// <param name="sourceY">The source-space vertical pointer coordinate.</param>
    private void HandleSpeedContextRequested(int sourceX, int sourceY)
    {
        IReadOnlyList<StrategyMenuCommand> commands = BuildSpeedMenuCommands(
            localSpeedSelection,
            playerSpeedSelections,
            speedSelectionStates
        );
        actions.OpenSpeedContextMenu(
            new ContextMenuRequest(this, commands, this),
            sourceX,
            sourceY
        );
    }

    /// <summary>
    /// Creates the ordered game-speed commands.
    /// </summary>
    /// <param name="selectedSpeed">The local participant's acknowledged selection, when present.</param>
    /// <param name="playerSpeeds">The acknowledged participant selections and their faction colors.</param>
    /// <param name="selectionStates">The optional owners that refresh markers while the menu is open.</param>
    /// <returns>The ordered game-speed commands.</returns>
    internal static IReadOnlyList<StrategyMenuCommand> BuildSpeedMenuCommands(
        TickSpeed? selectedSpeed = null,
        IReadOnlyList<StrategyHudPlayerSpeedSelection> playerSpeeds = null,
        IReadOnlyDictionary<TickSpeed, ContextMenuSelectionState> selectionStates = null
    )
    {
        return new List<StrategyMenuCommand>
        {
            new StrategyMenuCommand(
                StrategyMenuAction.GameSpeedPause,
                "Pause",
                true,
                StrategyContextMenuIconKeys.GetSpeedIconKey(GetSourceSpeed(TickSpeed.Paused)),
                selected: selectedSpeed == TickSpeed.Paused,
                selectionColors: GetSpeedSelectionColors(TickSpeed.Paused, playerSpeeds),
                selectionState: selectionStates?[TickSpeed.Paused]
            ),
            new StrategyMenuCommand(
                StrategyMenuAction.GameSpeedVerySlow,
                "Very Slow",
                true,
                StrategyContextMenuIconKeys.GetSpeedIconKey(GetSourceSpeed(TickSpeed.VerySlow)),
                selected: selectedSpeed == TickSpeed.VerySlow,
                selectionColors: GetSpeedSelectionColors(TickSpeed.VerySlow, playerSpeeds),
                selectionState: selectionStates?[TickSpeed.VerySlow]
            ),
            new StrategyMenuCommand(
                StrategyMenuAction.GameSpeedSlow,
                "Slow",
                true,
                StrategyContextMenuIconKeys.GetSpeedIconKey(GetSourceSpeed(TickSpeed.Slow)),
                selected: selectedSpeed == TickSpeed.Slow,
                selectionColors: GetSpeedSelectionColors(TickSpeed.Slow, playerSpeeds),
                selectionState: selectionStates?[TickSpeed.Slow]
            ),
            new StrategyMenuCommand(
                StrategyMenuAction.GameSpeedMedium,
                "Medium",
                true,
                StrategyContextMenuIconKeys.GetSpeedIconKey(GetSourceSpeed(TickSpeed.Medium)),
                selected: selectedSpeed == TickSpeed.Medium,
                selectionColors: GetSpeedSelectionColors(TickSpeed.Medium, playerSpeeds),
                selectionState: selectionStates?[TickSpeed.Medium]
            ),
            new StrategyMenuCommand(
                StrategyMenuAction.GameSpeedFast,
                "Fast",
                true,
                StrategyContextMenuIconKeys.GetSpeedIconKey(GetSourceSpeed(TickSpeed.Fast)),
                selected: selectedSpeed == TickSpeed.Fast,
                selectionColors: GetSpeedSelectionColors(TickSpeed.Fast, playerSpeeds),
                selectionState: selectionStates?[TickSpeed.Fast]
            ),
        };
    }

    /// <summary>
    /// Collects the configured faction markers for one speed from the current HUD snapshot.
    /// </summary>
    /// <param name="speed">The speed represented by the menu row.</param>
    /// <param name="playerSpeeds">The acknowledged participant selections.</param>
    /// <returns>The selecting participants' colors in their stable player order.</returns>
    private static IReadOnlyList<Color32> GetSpeedSelectionColors(
        TickSpeed speed,
        IReadOnlyList<StrategyHudPlayerSpeedSelection> playerSpeeds
    )
    {
        return playerSpeeds
                ?.Where(player => player.Speed == speed && player.FactionColor.HasValue)
                .Select(player => player.FactionColor.Value)
                .ToArray()
            ?? Array.Empty<Color32>();
    }

    /// <summary>
    /// Releases subscriptions when the bound authored HUD view is destroyed.
    /// </summary>
    /// <param name="destroyedView">The destroyed HUD view.</param>
    private void HandleViewDestroyed(StrategyHudView destroyedView)
    {
        if (!ReferenceEquals(view, destroyedView))
            return;

        ReleaseView();
    }

    /// <summary>
    /// Releases subscriptions from the currently bound HUD view.
    /// </summary>
    private void ReleaseView()
    {
        if (ReferenceEquals(view, null))
            return;

        advisorController.UnbindView(view.AdvisorView);
        view.ControlPressed -= HandleControlPressed;
        view.Destroyed -= HandleViewDestroyed;
        view.HudButtonRequested -= HandleHudButtonRequested;
        view.MessageTabRequested -= HandleMessageTabRequested;
        view.RenderRequested -= HandleRenderRequested;
        view.SpeedContextRequested -= HandleSpeedContextRequested;
        view.SpeedResponseRequested -= HandleSpeedResponseRequested;
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
    /// Verifies action routing is available before view binding or rendering.
    /// </summary>
    private void EnsureInitialized()
    {
        if (actions == null)
            throw new InvalidOperationException(
                $"{nameof(StrategyHudController)} must be initialized before use."
            );
    }

    /// <summary>
    /// Gets the bound authored view and rejects incomplete screen composition.
    /// </summary>
    /// <returns>The bound authored HUD view.</returns>
    private StrategyHudView GetRequiredView()
    {
        EnsureInitialized();
        return view
            ?? throw new InvalidOperationException(
                $"{nameof(StrategyHudController)} must bind a view before rendering."
            );
    }

    /// <summary>
    /// Converts optional serialized source-space layout to immutable bounds.
    /// </summary>
    /// <param name="layout">The optional serialized layout.</param>
    /// <returns>The equivalent immutable bounds, or null.</returns>
    private static RectInt? ToRect(SourceRectLayout layout)
    {
        return layout == null ? null : ToRequiredRect(layout);
    }

    /// <summary>
    /// Converts a required serialized source-space layout to immutable bounds.
    /// </summary>
    /// <param name="layout">The required serialized layout.</param>
    /// <returns>The equivalent immutable bounds.</returns>
    private static RectInt ToRequiredRect(SourceRectLayout layout)
    {
        return new RectInt(layout.X, layout.Y, layout.Width, layout.Height);
    }
}
