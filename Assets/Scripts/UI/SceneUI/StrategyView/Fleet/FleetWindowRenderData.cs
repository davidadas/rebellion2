using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Identifies one tab in the authored fleet-window order.
/// </summary>
public enum FleetWindowTab
{
    CapitalShips = 0,
    Starfighters = 1,
    Regiments = 2,
    Personnel = 3,
}

/// <summary>
/// Contains immutable presentation data for one fleet-list row.
/// </summary>
public sealed class FleetListRowRenderData
{
    /// <summary>
    /// Creates one complete fleet-list row snapshot.
    /// </summary>
    /// <param name="name">The displayed fleet name.</param>
    /// <param name="iconTexture">The fleet icon.</param>
    /// <param name="enrouteOverlayTexture">The optional in-transit overlay.</param>
    /// <param name="damagedOverlayTexture">The optional damaged overlay.</param>
    /// <param name="starfighterBadgeTexture">The optional starfighter badge.</param>
    /// <param name="troopBadgeTexture">The optional troop badge.</param>
    /// <param name="personnelBadgeTexture">The optional personnel badge.</param>
    /// <param name="selectionTexture">The optional selection frame.</param>
    public FleetListRowRenderData(
        string name,
        Texture iconTexture,
        Texture enrouteOverlayTexture,
        Texture damagedOverlayTexture,
        Texture starfighterBadgeTexture,
        Texture troopBadgeTexture,
        Texture personnelBadgeTexture,
        Texture selectionTexture
    )
    {
        Name = name ?? string.Empty;
        IconTexture = iconTexture;
        EnrouteOverlayTexture = enrouteOverlayTexture;
        DamagedOverlayTexture = damagedOverlayTexture;
        StarfighterBadgeTexture = starfighterBadgeTexture;
        TroopBadgeTexture = troopBadgeTexture;
        PersonnelBadgeTexture = personnelBadgeTexture;
        SelectionTexture = selectionTexture;
    }

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the icon texture.
    /// </summary>
    public Texture IconTexture { get; }

    /// <summary>
    /// Gets the enroute overlay texture.
    /// </summary>
    public Texture EnrouteOverlayTexture { get; }

    /// <summary>
    /// Gets the damaged overlay texture.
    /// </summary>
    public Texture DamagedOverlayTexture { get; }

    /// <summary>
    /// Gets the starfighter badge texture.
    /// </summary>
    public Texture StarfighterBadgeTexture { get; }

    /// <summary>
    /// Gets the troop badge texture.
    /// </summary>
    public Texture TroopBadgeTexture { get; }

    /// <summary>
    /// Gets the personnel badge texture.
    /// </summary>
    public Texture PersonnelBadgeTexture { get; }

    /// <summary>
    /// Gets the selection texture.
    /// </summary>
    public Texture SelectionTexture { get; }
}

/// <summary>
/// Contains immutable presentation data for one fleet-window tab.
/// </summary>
public sealed class FleetWindowTabRenderData
{
    /// <summary>
    /// Creates one tab presentation snapshot.
    /// </summary>
    /// <param name="tab">The represented fleet tab.</param>
    /// <param name="texture">The tab's current texture.</param>
    /// <param name="pressedTexture">The tab's pressed texture.</param>
    public FleetWindowTabRenderData(FleetWindowTab tab, Texture texture, Texture pressedTexture)
    {
        Tab = tab;
        Texture = texture;
        PressedTexture = pressedTexture;
    }

    /// <summary>
    /// Gets the represented fleet tab.
    /// </summary>
    public FleetWindowTab Tab { get; }

    /// <summary>
    /// Gets the texture.
    /// </summary>
    public Texture Texture { get; }

    /// <summary>
    /// Gets the pressed texture.
    /// </summary>
    public Texture PressedTexture { get; }
}

/// <summary>
/// Contains immutable presentation data for one fleet window.
/// </summary>
public sealed class FleetWindowRenderData
{
    private static readonly FleetWindowTab[] _orderedTabs =
    {
        FleetWindowTab.CapitalShips,
        FleetWindowTab.Starfighters,
        FleetWindowTab.Regiments,
        FleetWindowTab.Personnel,
    };
    private static readonly IReadOnlyList<FleetWindowTab> _readOnlyOrderedTabs = Array.AsReadOnly(
        _orderedTabs
    );

    /// <summary>
    /// Gets the number of authored fleet tabs.
    /// </summary>
    public static int TabCount => _orderedTabs.Length;

    /// <summary>
    /// Gets the semantic fleet tabs in authored slot order.
    /// </summary>
    public static IReadOnlyList<FleetWindowTab> OrderedTabs => _readOnlyOrderedTabs;

    /// <summary>
    /// Creates one complete fleet-window presentation snapshot.
    /// </summary>
    /// <param name="x">The source-space horizontal position.</param>
    /// <param name="y">The source-space vertical position.</param>
    /// <param name="titleTexture">The active or inactive title texture.</param>
    /// <param name="caption">The displayed planet name.</param>
    /// <param name="detailBackgroundTexture">The selected faction's detail background.</param>
    /// <param name="fleetRows">The ordered fleet-list rows.</param>
    /// <param name="activeTab">The selected detail tab.</param>
    /// <param name="selectedFleetIndex">The selected fleet-row index.</param>
    /// <param name="hasSelectedFleet">Whether the detail panel has a selected fleet.</param>
    /// <param name="bannerTexture">The selected fleet banner.</param>
    /// <param name="bannerEnrouteOverlayTexture">The selected fleet in-transit overlay.</param>
    /// <param name="bannerDamagedOverlayTexture">The selected fleet damaged overlay.</param>
    /// <param name="fleetName">The selected fleet name.</param>
    /// <param name="fleetNameColor">The selected fleet name color.</param>
    /// <param name="showCapacity">Whether capacity values are visible.</param>
    /// <param name="capacityLeft">The current capacity value.</param>
    /// <param name="capacityRight">The maximum capacity value.</param>
    /// <param name="tabs">The ordered tab presentations.</param>
    /// <param name="detailItems">The ordered selected-fleet detail cards.</param>
    /// <param name="renameFleetRowIndex">The fleet-row rename target, or -1.</param>
    /// <param name="renameDetailItemIndex">The detail-card rename target, or -1.</param>
    /// <param name="renameText">The current rename value.</param>
    public FleetWindowRenderData(
        int x,
        int y,
        Texture titleTexture,
        string caption,
        Texture detailBackgroundTexture,
        IReadOnlyList<FleetListRowRenderData> fleetRows,
        FleetWindowTab activeTab,
        int selectedFleetIndex,
        bool hasSelectedFleet,
        Texture bannerTexture,
        Texture bannerEnrouteOverlayTexture,
        Texture bannerDamagedOverlayTexture,
        string fleetName,
        Color32 fleetNameColor,
        bool showCapacity,
        string capacityLeft,
        string capacityRight,
        IReadOnlyList<FleetWindowTabRenderData> tabs,
        IReadOnlyList<StrategyUnitCardRenderData> detailItems,
        int renameFleetRowIndex,
        int renameDetailItemIndex,
        string renameText
    )
    {
        X = x;
        Y = y;
        TitleTexture = titleTexture;
        Caption = caption ?? string.Empty;
        DetailBackgroundTexture = detailBackgroundTexture;
        FleetRows = Copy(fleetRows, nameof(fleetRows));
        ActiveTab = activeTab;
        SelectedFleetIndex = selectedFleetIndex;
        HasSelectedFleet = hasSelectedFleet;
        BannerTexture = bannerTexture;
        BannerEnrouteOverlayTexture = bannerEnrouteOverlayTexture;
        BannerDamagedOverlayTexture = bannerDamagedOverlayTexture;
        FleetName = fleetName ?? string.Empty;
        FleetNameColor = fleetNameColor;
        ShowCapacity = showCapacity;
        CapacityLeft = capacityLeft ?? string.Empty;
        CapacityRight = capacityRight ?? string.Empty;
        Tabs = Copy(tabs, nameof(tabs));
        DetailItems = Copy(detailItems, nameof(detailItems));
        RenameFleetRowIndex = renameFleetRowIndex;
        RenameDetailItemIndex = renameDetailItemIndex;
        RenameText = renameText ?? string.Empty;
    }

    /// <summary>
    /// Gets the horizontal coordinate.
    /// </summary>
    public int X { get; }

    /// <summary>
    /// Gets the vertical coordinate.
    /// </summary>
    public int Y { get; }

    /// <summary>
    /// Gets the title texture.
    /// </summary>
    public Texture TitleTexture { get; }

    /// <summary>
    /// Gets the caption.
    /// </summary>
    public string Caption { get; }

    /// <summary>
    /// Gets the detail background texture.
    /// </summary>
    public Texture DetailBackgroundTexture { get; }

    /// <summary>
    /// Gets the fleet rows.
    /// </summary>
    public IReadOnlyList<FleetListRowRenderData> FleetRows { get; }

    /// <summary>
    /// Gets the active tab.
    /// </summary>
    public FleetWindowTab ActiveTab { get; }

    /// <summary>
    /// Gets the selected fleet index.
    /// </summary>
    public int SelectedFleetIndex { get; }

    /// <summary>
    /// Gets a value indicating whether selected fleet is present.
    /// </summary>
    public bool HasSelectedFleet { get; }

    /// <summary>
    /// Gets the banner texture.
    /// </summary>
    public Texture BannerTexture { get; }

    /// <summary>
    /// Gets the banner enroute overlay texture.
    /// </summary>
    public Texture BannerEnrouteOverlayTexture { get; }

    /// <summary>
    /// Gets the banner damaged overlay texture.
    /// </summary>
    public Texture BannerDamagedOverlayTexture { get; }

    /// <summary>
    /// Gets the fleet name.
    /// </summary>
    public string FleetName { get; }

    /// <summary>
    /// Gets the fleet name color.
    /// </summary>
    public Color32 FleetNameColor { get; }

    /// <summary>
    /// Gets a value indicating whether capacity is shown.
    /// </summary>
    public bool ShowCapacity { get; }

    /// <summary>
    /// Gets the capacity left.
    /// </summary>
    public string CapacityLeft { get; }

    /// <summary>
    /// Gets the capacity right.
    /// </summary>
    public string CapacityRight { get; }

    /// <summary>
    /// Gets the tabs.
    /// </summary>
    public IReadOnlyList<FleetWindowTabRenderData> Tabs { get; }

    /// <summary>
    /// Gets the detail items.
    /// </summary>
    public IReadOnlyList<StrategyUnitCardRenderData> DetailItems { get; }

    /// <summary>
    /// Gets the rename fleet row index.
    /// </summary>
    public int RenameFleetRowIndex { get; }

    /// <summary>
    /// Gets the rename detail item index.
    /// </summary>
    public int RenameDetailItemIndex { get; }

    /// <summary>
    /// Gets the rename text.
    /// </summary>
    public string RenameText { get; }

    /// <summary>
    /// Copies a required presentation collection into a read-only snapshot.
    /// </summary>
    /// <typeparam name="T">The collection element type.</typeparam>
    /// <param name="items">The source collection.</param>
    /// <param name="parameterName">The source parameter name.</param>
    /// <returns>The isolated read-only collection.</returns>
    private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> items, string parameterName)
    {
        return new List<T>(items ?? throw new ArgumentNullException(parameterName)).AsReadOnly();
    }
}
