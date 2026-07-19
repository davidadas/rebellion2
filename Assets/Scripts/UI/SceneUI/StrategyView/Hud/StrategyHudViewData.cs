using System;
using System.Collections.Generic;
using Rebellion.Game;
using Rebellion.Game.Messages;
using UnityEngine;

/// <summary>
/// Carries the current strategy values required to project the HUD.
/// </summary>
public sealed class StrategyHudRenderData
{
    private readonly HashSet<MessageType> unreadMessageTypes;

    /// <summary>
    /// Creates an immutable strategy HUD state snapshot.
    /// </summary>
    /// <param name="tickText">The displayed game tick.</param>
    /// <param name="rawMaterialsText">The displayed raw-material total.</param>
    /// <param name="refinedMaterialsText">The displayed refined-material total.</param>
    /// <param name="maintenanceText">The displayed maintenance headroom.</param>
    /// <param name="speed">The current strategy speed.</param>
    /// <param name="unreadMessageTypes">The message categories containing unread messages.</param>
    public StrategyHudRenderData(
        string tickText,
        string rawMaterialsText,
        string refinedMaterialsText,
        string maintenanceText,
        TickSpeed speed,
        IEnumerable<MessageType> unreadMessageTypes
    )
    {
        TickText = tickText ?? string.Empty;
        RawMaterialsText = rawMaterialsText ?? string.Empty;
        RefinedMaterialsText = refinedMaterialsText ?? string.Empty;
        MaintenanceText = maintenanceText ?? string.Empty;
        Speed = speed;
        this.unreadMessageTypes =
            unreadMessageTypes == null
                ? new HashSet<MessageType>()
                : new HashSet<MessageType>(unreadMessageTypes);
    }

    public string TickText { get; }

    public string RawMaterialsText { get; }

    public string RefinedMaterialsText { get; }

    public string MaintenanceText { get; }

    public TickSpeed Speed { get; }

    /// <summary>
    /// Returns whether a message category contains at least one unread message.
    /// </summary>
    /// <param name="messageType">The message category to inspect.</param>
    /// <returns>True when the category contains an unread message.</returns>
    public bool HasUnreadMessageType(MessageType messageType)
    {
        return unreadMessageTypes.Contains(messageType);
    }
}

/// <summary>
/// Defines dynamic content for one authored HUD counter.
/// </summary>
public sealed class StrategyHudCounterViewData
{
    /// <summary>
    /// Creates immutable counter presentation data.
    /// </summary>
    /// <param name="text">The displayed counter value.</param>
    /// <param name="color">The displayed counter color.</param>
    /// <param name="bounds">The optional faction-specific source-space bounds.</param>
    public StrategyHudCounterViewData(string text, Color color, RectInt? bounds)
    {
        Text = text ?? string.Empty;
        Color = color;
        Bounds = bounds;
    }

    public string Text { get; }

    public Color Color { get; }

    public RectInt? Bounds { get; }
}

/// <summary>
/// Defines the action, hit area, and pressed presentation for one HUD button slot.
/// </summary>
public sealed class StrategyHudButtonViewData
{
    /// <summary>
    /// Creates immutable HUD button presentation data.
    /// </summary>
    /// <param name="action">The semantic action assigned to the button.</param>
    /// <param name="hitArea">The source-space button hit area.</param>
    /// <param name="pressedTexture">The texture displayed while pressed.</param>
    /// <param name="pressedBounds">The source-space pressed-art bounds.</param>
    public StrategyHudButtonViewData(
        StrategyHudAction action,
        RectInt hitArea,
        Texture2D pressedTexture,
        RectInt pressedBounds
    )
    {
        Action = action;
        HitArea = hitArea;
        PressedTexture = pressedTexture;
        PressedBounds = pressedBounds;
    }

    public StrategyHudAction Action { get; }

    public RectInt HitArea { get; }

    public Texture2D PressedTexture { get; }

    public RectInt PressedBounds { get; }
}

/// <summary>
/// Defines one message-notification slot in the strategy HUD.
/// </summary>
public sealed class StrategyHudMessageNotificationViewData
{
    /// <summary>
    /// Creates immutable message-notification presentation data.
    /// </summary>
    /// <param name="tab">The messages tab opened by the slot.</param>
    /// <param name="texture">The current default or highlighted texture.</param>
    /// <param name="bounds">The source-space slot bounds.</param>
    public StrategyHudMessageNotificationViewData(
        MessagesTab tab,
        Texture2D texture,
        RectInt bounds
    )
    {
        Tab = tab;
        Texture = texture;
        Bounds = bounds;
    }

    public MessagesTab Tab { get; }

    public Texture2D Texture { get; }

    public RectInt Bounds { get; }
}

/// <summary>
/// Contains the complete immutable presentation snapshot for the authored strategy HUD.
/// </summary>
public sealed class StrategyHudViewData
{
    private readonly IReadOnlyList<StrategyHudButtonViewData> buttons;
    private readonly IReadOnlyList<StrategyHudMessageNotificationViewData> messageNotifications;

    /// <summary>
    /// Creates a complete immutable HUD presentation snapshot.
    /// </summary>
    /// <param name="backgroundTexture">The active faction HUD background.</param>
    /// <param name="tickCounter">The game-tick counter.</param>
    /// <param name="rawMaterialsCounter">The raw-material counter.</param>
    /// <param name="refinedMaterialsCounter">The refined-material counter.</param>
    /// <param name="maintenanceCounter">The maintenance counter.</param>
    /// <param name="speedIndicatorTexture">The current speed-indicator texture.</param>
    /// <param name="speedIndicatorBounds">The speed-indicator source-space bounds.</param>
    /// <param name="galacticInformationDisplayTexture">The galactic display control texture.</param>
    /// <param name="galacticInformationDisplayBounds">The galactic display control bounds.</param>
    /// <param name="speedContextBounds">The speed context-menu hit area.</param>
    /// <param name="buttons">The HUD buttons in authored slot order.</param>
    /// <param name="messageNotifications">The notification slots in authored order.</param>
    public StrategyHudViewData(
        Texture2D backgroundTexture,
        StrategyHudCounterViewData tickCounter,
        StrategyHudCounterViewData rawMaterialsCounter,
        StrategyHudCounterViewData refinedMaterialsCounter,
        StrategyHudCounterViewData maintenanceCounter,
        Texture2D speedIndicatorTexture,
        RectInt? speedIndicatorBounds,
        Texture2D galacticInformationDisplayTexture,
        RectInt? galacticInformationDisplayBounds,
        RectInt? speedContextBounds,
        IReadOnlyList<StrategyHudButtonViewData> buttons,
        IReadOnlyList<StrategyHudMessageNotificationViewData> messageNotifications
    )
    {
        BackgroundTexture = backgroundTexture;
        TickCounter = tickCounter;
        RawMaterialsCounter = rawMaterialsCounter;
        RefinedMaterialsCounter = refinedMaterialsCounter;
        MaintenanceCounter = maintenanceCounter;
        SpeedIndicatorTexture = speedIndicatorTexture;
        SpeedIndicatorBounds = speedIndicatorBounds;
        GalacticInformationDisplayTexture = galacticInformationDisplayTexture;
        GalacticInformationDisplayBounds = galacticInformationDisplayBounds;
        SpeedContextBounds = speedContextBounds;
        this.buttons = Copy(buttons);
        this.messageNotifications = Copy(messageNotifications);
    }

    public Texture2D BackgroundTexture { get; }

    public StrategyHudCounterViewData TickCounter { get; }

    public StrategyHudCounterViewData RawMaterialsCounter { get; }

    public StrategyHudCounterViewData RefinedMaterialsCounter { get; }

    public StrategyHudCounterViewData MaintenanceCounter { get; }

    public Texture2D SpeedIndicatorTexture { get; }

    public RectInt? SpeedIndicatorBounds { get; }

    public Texture2D GalacticInformationDisplayTexture { get; }

    public RectInt? GalacticInformationDisplayBounds { get; }

    public RectInt? SpeedContextBounds { get; }

    public IReadOnlyList<StrategyHudButtonViewData> Buttons => buttons;

    public IReadOnlyList<StrategyHudMessageNotificationViewData> MessageNotifications =>
        messageNotifications;

    /// <summary>
    /// Copies a possibly null list into an immutable array-backed snapshot.
    /// </summary>
    /// <typeparam name="T">The copied element type.</typeparam>
    /// <param name="source">The source list.</param>
    /// <returns>The isolated read-only snapshot.</returns>
    private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> source)
    {
        if (source == null || source.Count == 0)
            return Array.Empty<T>();

        T[] copy = new T[source.Count];
        for (int i = 0; i < source.Count; i++)
            copy[i] = source[i];

        return Array.AsReadOnly(copy);
    }
}
