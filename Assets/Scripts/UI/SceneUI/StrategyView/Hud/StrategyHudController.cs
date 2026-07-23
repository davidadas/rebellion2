using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Factions;
using Rebellion.Game.Messages;
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

    private IStrategyHudActions actions;
    private StrategyHudView view;

    /// <summary>
    /// Creates a strategy HUD controller with current-faction, theme, texture, and audio dependencies.
    /// </summary>
    /// <param name="getPlayerFaction">Returns the current player faction.</param>
    /// <param name="getPlayerTheme">Returns the current player faction theme.</param>
    /// <param name="getTexture">Resolves a texture from a configured resource path.</param>
    /// <param name="playSfx">Plays a strategy sound-effect path.</param>
    public StrategyHudController(
        Func<Faction> getPlayerFaction,
        Func<FactionTheme> getPlayerTheme,
        Func<string, Texture2D> getTexture,
        Action<string> playSfx
    )
    {
        if (getPlayerFaction == null)
            throw new ArgumentNullException(nameof(getPlayerFaction));

        this.getPlayerTheme =
            getPlayerTheme ?? throw new ArgumentNullException(nameof(getPlayerTheme));
        this.getTexture = getTexture ?? throw new ArgumentNullException(nameof(getTexture));
        this.playSfx = playSfx ?? throw new ArgumentNullException(nameof(playSfx));
        advisorController = new StrategyAdvisorController(getPlayerFaction, getTexture, playSfx);
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
        targetView.Render(CreateViewData(data, theme));
        advisorController.Render(theme?.StrategyAdvisor);
    }

    /// <summary>
    /// Queues an advisor notification derived from a newly delivered player message.
    /// </summary>
    /// <param name="message">The delivered message.</param>
    /// <param name="currentTick">The current game tick.</param>
    /// <param name="notificationEnabled">Whether its message category permits notification.</param>
    public void NotifyAdvisor(Message message, int currentTick, bool notificationEnabled)
    {
        advisorController.Notify(message, currentTick, notificationEnabled);
    }

    /// <summary>
    /// Processes the highest-priority pending advisor notification for the current tick.
    /// </summary>
    /// <param name="currentTick">The current game tick.</param>
    /// <param name="announcementsEnabled">Whether gated protocol announcements may play.</param>
    public void ProcessAdvisor(int currentTick, bool announcementsEnabled)
    {
        advisorController.ProcessPending(currentTick, announcementsEnabled);
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
            tickCounter: CreateCounter(data.TickText, textColor, hudTheme?.TickCounterSourceLayout),
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
            speedIndicatorTexture: ResolveTexture(
                GetSpeedIndicatorPath(hudTheme?.SpeedIndicators, data.Speed)
            ),
            speedIndicatorBounds: ToRect(hudTheme?.SpeedIndicatorSourceLayout),
            galacticInformationDisplayTexture: ResolveTexture(
                hudTheme?.GalacticInformationDisplayImagePath
            ),
            galacticInformationDisplayBounds: ToRect(
                hudTheme?.GalacticInformationDisplayImageLayout
            ),
            speedContextBounds: ToRect(hudTheme?.SpeedContextSourceLayout),
            buttons: CreateButtons(hudTheme?.Buttons),
            messageNotifications: CreateMessageNotifications(hudTheme?.MessageNotifications, data)
        );
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
    /// Collects message categories containing at least one unread message.
    /// </summary>
    /// <param name="faction">The faction whose message state is projected into the HUD.</param>
    /// <returns>The unread message categories.</returns>
    internal static HashSet<MessageType> GetUnreadMessageTypes(Faction faction)
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
                if (message?.Read == false)
                {
                    types.Add(entry.Key);
                    break;
                }
            }
        }

        return types;
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

            string imagePath = data.HasUnreadMessageType(theme.MessageType)
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
        IReadOnlyList<StrategyMenuCommand> commands = BuildSpeedMenuCommands();
        actions.OpenSpeedContextMenu(
            new ContextMenuRequest(this, commands, this),
            sourceX,
            sourceY
        );
    }

    /// <summary>
    /// Creates the ordered game-speed commands.
    /// </summary>
    /// <returns>The ordered game-speed commands.</returns>
    internal static IReadOnlyList<StrategyMenuCommand> BuildSpeedMenuCommands()
    {
        return new List<StrategyMenuCommand>
        {
            new StrategyMenuCommand(
                StrategyMenuAction.GameSpeedPause,
                "Pause",
                true,
                StrategyContextMenuIconKeys.GetSpeedIconKey(GetSourceSpeed(TickSpeed.Paused))
            ),
            new StrategyMenuCommand(
                StrategyMenuAction.GameSpeedVerySlow,
                "Very Slow",
                true,
                StrategyContextMenuIconKeys.GetSpeedIconKey(GetSourceSpeed(TickSpeed.VerySlow))
            ),
            new StrategyMenuCommand(
                StrategyMenuAction.GameSpeedSlow,
                "Slow",
                true,
                StrategyContextMenuIconKeys.GetSpeedIconKey(GetSourceSpeed(TickSpeed.Slow))
            ),
            new StrategyMenuCommand(
                StrategyMenuAction.GameSpeedMedium,
                "Medium",
                true,
                StrategyContextMenuIconKeys.GetSpeedIconKey(GetSourceSpeed(TickSpeed.Medium))
            ),
            new StrategyMenuCommand(
                StrategyMenuAction.GameSpeedFast,
                "Fast",
                true,
                StrategyContextMenuIconKeys.GetSpeedIconKey(GetSourceSpeed(TickSpeed.Fast))
            ),
        };
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
