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

    public string TickText { get; }

    public string RawMaterialsText { get; }

    public string RefinedMaterialsText { get; }

    public string MaintenanceText { get; }

    public TickSpeed Speed { get; }

    public StrategyHudResourceBreakdown ResourceBreakdown { get; }

    /// <summary>
    /// Creates an immutable strategy HUD state snapshot.
    /// </summary>
    /// <param name="tickText">The displayed game tick.</param>
    /// <param name="rawMaterialsText">The displayed raw-material total.</param>
    /// <param name="refinedMaterialsText">The displayed refined-material total.</param>
    /// <param name="speed">The current strategy speed.</param>
    /// <param name="unreadMessageTypes">The message categories containing unread messages.</param>
    /// <param name="resourceBreakdown">The current resource-facility totals.</param>
    public StrategyHudRenderData(
        string tickText,
        string rawMaterialsText,
        string refinedMaterialsText,
        TickSpeed speed,
        IEnumerable<MessageType> unreadMessageTypes,
        StrategyHudResourceBreakdown resourceBreakdown = null
    )
    {
        TickText = tickText ?? string.Empty;
        RawMaterialsText = rawMaterialsText ?? string.Empty;
        RefinedMaterialsText = refinedMaterialsText ?? string.Empty;
        Speed = speed;
        ResourceBreakdown = resourceBreakdown ?? StrategyHudResourceBreakdown.Empty;
        MaintenanceText =
            resourceBreakdown == null
                ? string.Empty
                : ResourceBreakdown.MaintenanceHeadroom.ToString();
        this.unreadMessageTypes =
            unreadMessageTypes == null
                ? new HashSet<MessageType>()
                : new HashSet<MessageType>(unreadMessageTypes);
    }

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
/// Contains the player's current mine and refinery totals by lifecycle state.
/// </summary>
public sealed class StrategyHudResourceBreakdown
{
    public static StrategyHudResourceBreakdown Empty { get; } =
        new StrategyHudResourceBreakdown(
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            StrategyHudMaintenanceBreakdown.Empty
        );

    public int ActiveMines { get; }

    public int OfflineMines { get; }

    public int BuildingMines { get; }

    public int EnRouteMines { get; }

    public int DeployedMines => ActiveMines + OfflineMines;

    public int TotalMines => DeployedMines + EnRouteMines + BuildingMines;

    public int ActiveRefineries { get; }

    public int OfflineRefineries { get; }

    public int BuildingRefineries { get; }

    public int EnRouteRefineries { get; }

    public int DeployedRefineries => ActiveRefineries + OfflineRefineries;

    public int TotalRefineries => DeployedRefineries + EnRouteRefineries + BuildingRefineries;

    public double RawOutputPerTick { get; }

    public double DeliveredRawOutputPerTick { get; }

    public double ProjectedRawOutputPerTick { get; }

    public double RefinedOutputPerTick { get; }

    public double DeliveredRefinedOutputPerTick { get; }

    public double ProjectedRefinedOutputPerTick { get; }

    public StrategyHudMaintenanceBreakdown Maintenance { get; }

    public int MaintenanceCommitted => Maintenance.Committed;

    public int MaintenanceHeadroom { get; }

    public int DeliveredMaintenanceHeadroom { get; }

    public int ProjectedMaintenanceHeadroom { get; }

    /// <summary>
    /// Creates immutable resource-facility totals.
    /// </summary>
    /// <param name="activeMines">The mines currently producing.</param>
    /// <param name="offlineMines">The completed mines currently unable to produce.</param>
    /// <param name="buildingMines">The mines under construction.</param>
    /// <param name="enRouteMines">The mines traveling to a destination.</param>
    /// <param name="activeRefineries">The refineries currently producing.</param>
    /// <param name="offlineRefineries">The completed refineries currently unable to produce.</param>
    /// <param name="buildingRefineries">The refineries under construction.</param>
    /// <param name="enRouteRefineries">The refineries traveling to a destination.</param>
    /// <param name="rawOutputPerTick">The gross raw-material output per tick.</param>
    /// <param name="deliveredRawOutputPerTick">The gross raw-material output after deliveries.</param>
    /// <param name="projectedRawOutputPerTick">The projected gross raw-material output per tick.</param>
    /// <param name="refinedOutputPerTick">The gross refined-material output per tick.</param>
    /// <param name="deliveredRefinedOutputPerTick">The gross refined-material output after deliveries.</param>
    /// <param name="projectedRefinedOutputPerTick">The projected gross refined-material output per tick.</param>
    /// <param name="maintenanceHeadroom">The currently available maintenance headroom.</param>
    /// <param name="deliveredMaintenanceHeadroom">The maintenance headroom after deliveries.</param>
    /// <param name="projectedMaintenanceHeadroom">The maintenance headroom after all committed construction and deliveries.</param>
    /// <param name="maintenance">The maintenance committed by asset category.</param>
    public StrategyHudResourceBreakdown(
        int activeMines,
        int offlineMines,
        int buildingMines,
        int enRouteMines,
        int activeRefineries,
        int offlineRefineries,
        int buildingRefineries,
        int enRouteRefineries,
        double rawOutputPerTick,
        double deliveredRawOutputPerTick,
        double projectedRawOutputPerTick,
        double refinedOutputPerTick,
        double deliveredRefinedOutputPerTick,
        double projectedRefinedOutputPerTick,
        int maintenanceHeadroom,
        int deliveredMaintenanceHeadroom,
        int projectedMaintenanceHeadroom,
        StrategyHudMaintenanceBreakdown maintenance
    )
    {
        ActiveMines = activeMines;
        OfflineMines = offlineMines;
        BuildingMines = buildingMines;
        EnRouteMines = enRouteMines;
        ActiveRefineries = activeRefineries;
        OfflineRefineries = offlineRefineries;
        BuildingRefineries = buildingRefineries;
        EnRouteRefineries = enRouteRefineries;
        RawOutputPerTick = rawOutputPerTick;
        DeliveredRawOutputPerTick = deliveredRawOutputPerTick;
        ProjectedRawOutputPerTick = projectedRawOutputPerTick;
        RefinedOutputPerTick = refinedOutputPerTick;
        DeliveredRefinedOutputPerTick = deliveredRefinedOutputPerTick;
        ProjectedRefinedOutputPerTick = projectedRefinedOutputPerTick;
        MaintenanceHeadroom = maintenanceHeadroom;
        DeliveredMaintenanceHeadroom = deliveredMaintenanceHeadroom;
        ProjectedMaintenanceHeadroom = projectedMaintenanceHeadroom;
        Maintenance = maintenance ?? StrategyHudMaintenanceBreakdown.Empty;
    }
}

/// <summary>
/// Contains maintenance costs presented in the strategy HUD.
/// </summary>
public sealed class StrategyHudMaintenanceBreakdown
{
    public static StrategyHudMaintenanceBreakdown Empty { get; } =
        new StrategyHudMaintenanceBreakdown(0, 0, 0, 0, 0, 0, 0, 0, 0);

    public int CapitalShips { get; }

    public int Starfighters { get; }

    public int Regiments { get; }

    public int SpecialForces { get; }

    public int Facilities { get; }

    public int Orders { get; }

    public int Deployed { get; }

    public int EnRoute { get; }

    public int Building { get; }

    public int AfterDelivery => Deployed + EnRoute;

    public int Committed =>
        CapitalShips + Starfighters + Regiments + SpecialForces + Facilities + Orders;

    /// <summary>
    /// Creates immutable maintenance presentation totals.
    /// </summary>
    /// <param name="capitalShips">Maintenance committed to capital ships.</param>
    /// <param name="starfighters">Maintenance committed to starfighters.</param>
    /// <param name="regiments">Maintenance committed to regiments.</param>
    /// <param name="specialForces">Maintenance committed to special forces.</param>
    /// <param name="facilities">Maintenance committed to facilities.</param>
    /// <param name="orders">Maintenance reserved by unfinished orders.</param>
    /// <param name="deployed">Maintenance used by deployed assets.</param>
    /// <param name="enRoute">Maintenance used by assets being delivered.</param>
    /// <param name="building">Maintenance reserved by assets being manufactured.</param>
    public StrategyHudMaintenanceBreakdown(
        int capitalShips,
        int starfighters,
        int regiments,
        int specialForces,
        int facilities,
        int orders,
        int deployed,
        int enRoute,
        int building
    )
    {
        CapitalShips = capitalShips;
        Starfighters = starfighters;
        Regiments = regiments;
        SpecialForces = specialForces;
        Facilities = facilities;
        Orders = orders;
        Deployed = deployed;
        EnRoute = enRoute;
        Building = building;
    }
}

/// <summary>
/// Defines resource-facility totals and placement for the HUD hover panel.
/// </summary>
public sealed class StrategyHudResourceBreakdownViewData
{
    public StrategyHudResourceBreakdown Totals { get; }

    public Color AccentColor { get; }

    public StrategyHudResourcePopoverViewData RawMaterials { get; }

    public StrategyHudResourcePopoverViewData RefinedMaterials { get; }

    public StrategyHudResourcePopoverViewData Maintenance { get; }

    /// <summary>
    /// Creates immutable resource-breakdown presentation data.
    /// </summary>
    /// <param name="totals">The displayed facility totals.</param>
    /// <param name="accentColor">The active faction accent color.</param>
    /// <param name="rawMaterials">The raw-material popover placement.</param>
    /// <param name="refinedMaterials">The refined-material popover placement.</param>
    /// <param name="maintenance">The maintenance popover placement.</param>
    public StrategyHudResourceBreakdownViewData(
        StrategyHudResourceBreakdown totals,
        Color accentColor,
        StrategyHudResourcePopoverViewData rawMaterials,
        StrategyHudResourcePopoverViewData refinedMaterials,
        StrategyHudResourcePopoverViewData maintenance
    )
    {
        Totals = totals ?? StrategyHudResourceBreakdown.Empty;
        AccentColor = accentColor;
        RawMaterials = rawMaterials ?? StrategyHudResourcePopoverViewData.Empty;
        RefinedMaterials = refinedMaterials ?? StrategyHudResourcePopoverViewData.Empty;
        Maintenance = maintenance ?? StrategyHudResourcePopoverViewData.Empty;
    }
}

/// <summary>
/// Defines the placement of one resource-counter popover.
/// </summary>
public sealed class StrategyHudResourcePopoverViewData
{
    public static StrategyHudResourcePopoverViewData Empty { get; } = new(null, null);

    public RectInt? HitArea { get; }

    public RectInt? PanelBounds { get; }

    /// <summary>
    /// Creates immutable resource-popover placement data.
    /// </summary>
    /// <param name="hitArea">The counter hover area.</param>
    /// <param name="panelBounds">The popover bounds below the counter.</param>
    public StrategyHudResourcePopoverViewData(RectInt? hitArea, RectInt? panelBounds)
    {
        HitArea = hitArea;
        PanelBounds = panelBounds;
    }
}

/// <summary>
/// Defines dynamic content for one authored HUD counter.
/// </summary>
public sealed class StrategyHudCounterViewData
{
    public string Text { get; }

    public Color Color { get; }

    public RectInt? Bounds { get; }

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
}

/// <summary>
/// Defines the action, hit area, and artwork for one HUD button slot.
/// </summary>
public sealed class StrategyHudButtonViewData
{
    public StrategyHudAction Action { get; }

    public RectInt HitArea { get; }

    public Texture2D UpTexture { get; }

    public Texture2D PressedTexture { get; }

    public RectInt ImageBounds { get; }

    /// <summary>
    /// Creates immutable HUD button presentation data.
    /// </summary>
    /// <param name="action">The semantic action assigned to the button.</param>
    /// <param name="hitArea">The source-space button hit area.</param>
    /// <param name="upTexture">The texture displayed while released.</param>
    /// <param name="pressedTexture">The texture displayed while pressed.</param>
    /// <param name="imageBounds">The source-space artwork bounds.</param>
    public StrategyHudButtonViewData(
        StrategyHudAction action,
        RectInt hitArea,
        Texture2D upTexture,
        Texture2D pressedTexture,
        RectInt imageBounds
    )
    {
        Action = action;
        HitArea = hitArea;
        UpTexture = upTexture;
        PressedTexture = pressedTexture;
        ImageBounds = imageBounds;
    }
}

/// <summary>
/// Defines one message-notification slot in the strategy HUD.
/// </summary>
public sealed class StrategyHudMessageNotificationViewData
{
    public MessagesTab Tab { get; }

    public Texture2D Texture { get; }

    public RectInt Bounds { get; }

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
}

/// <summary>
/// Contains the complete immutable presentation snapshot for the authored strategy HUD.
/// </summary>
public sealed class StrategyHudViewData
{
    private readonly IReadOnlyList<StrategyHudButtonViewData> buttons;
    private readonly IReadOnlyList<StrategyHudMessageNotificationViewData> messageNotifications;

    public Texture2D BackgroundTexture { get; }

    public StrategyHudCounterViewData TickCounter { get; }

    public StrategyHudCounterViewData RawMaterialsCounter { get; }

    public StrategyHudCounterViewData RefinedMaterialsCounter { get; }

    public StrategyHudCounterViewData MaintenanceCounter { get; }

    public StrategyHudResourceBreakdownViewData ResourceBreakdown { get; }

    public Texture2D SpeedIndicatorTexture { get; }

    public RectInt? SpeedIndicatorBounds { get; }

    public RectInt? SpeedContextBounds { get; }

    public IReadOnlyList<StrategyHudButtonViewData> Buttons => buttons;

    public IReadOnlyList<StrategyHudMessageNotificationViewData> MessageNotifications =>
        messageNotifications;

    /// <summary>
    /// Creates a complete immutable HUD presentation snapshot.
    /// </summary>
    /// <param name="backgroundTexture">The active faction HUD background.</param>
    /// <param name="tickCounter">The game-tick counter.</param>
    /// <param name="rawMaterialsCounter">The raw-material counter.</param>
    /// <param name="refinedMaterialsCounter">The refined-material counter.</param>
    /// <param name="maintenanceCounter">The maintenance counter.</param>
    /// <param name="resourceBreakdown">The resource-facility hover panel.</param>
    /// <param name="speedIndicatorTexture">The current speed-indicator texture.</param>
    /// <param name="speedIndicatorBounds">The speed-indicator source-space bounds.</param>
    /// <param name="speedContextBounds">The speed context-menu hit area.</param>
    /// <param name="buttons">The HUD buttons in authored slot order.</param>
    /// <param name="messageNotifications">The notification slots in authored order.</param>
    public StrategyHudViewData(
        Texture2D backgroundTexture,
        StrategyHudCounterViewData tickCounter,
        StrategyHudCounterViewData rawMaterialsCounter,
        StrategyHudCounterViewData refinedMaterialsCounter,
        StrategyHudCounterViewData maintenanceCounter,
        StrategyHudResourceBreakdownViewData resourceBreakdown,
        Texture2D speedIndicatorTexture,
        RectInt? speedIndicatorBounds,
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
        ResourceBreakdown = resourceBreakdown;
        SpeedIndicatorTexture = speedIndicatorTexture;
        SpeedIndicatorBounds = speedIndicatorBounds;
        SpeedContextBounds = speedContextBounds;
        this.buttons = Copy(buttons);
        this.messageNotifications = Copy(messageNotifications);
    }

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
