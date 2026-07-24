using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using Rebellion.Util.Extensions;
using UnityEngine;

/// <summary>
/// Projects controller-owned fleet sessions into immutable themed presentation snapshots.
/// </summary>
internal sealed class FleetWindowProjector
{
    private readonly Func<UIContext> getUIContext;

    /// <summary>
    /// Creates a fleet-window projector with access to the current presentation context.
    /// </summary>
    /// <param name="getUIContext">Returns the current strategy presentation context.</param>
    public FleetWindowProjector(Func<UIContext> getUIContext)
    {
        this.getUIContext = getUIContext ?? throw new ArgumentNullException(nameof(getUIContext));
    }

    /// <summary>
    /// Builds one complete fleet-window presentation snapshot.
    /// </summary>
    /// <param name="session">The controller-owned fleet session.</param>
    /// <param name="window">The owning window shell.</param>
    /// <param name="active">Whether the window currently has focus.</param>
    /// <returns>The complete fleet-window presentation snapshot.</returns>
    public FleetWindowRenderData Build(FleetWindowSession session, UIWindow window, bool active)
    {
        if (session == null)
            throw new ArgumentNullException(nameof(session));
        if (window == null)
            throw new ArgumentNullException(nameof(window));

        UIContext uiContext = GetUIContext();
        IReadOnlyList<Fleet> fleets = session.Fleets;
        Fleet selectedFleet = session.SelectedFleet;
        string ownerFactionId = session.Planet.OwnerFactionId;
        string selectedOwnerFactionId = selectedFleet?.OwnerInstanceID ?? ownerFactionId;

        List<FleetListRowRenderData> rows = BuildFleetRows(uiContext, session, fleets);
        List<FleetWindowTabRenderData> tabs = new List<FleetWindowTabRenderData>();
        List<StrategyUnitCardRenderData> detailItems = new List<StrategyUnitCardRenderData>();
        bool showCapacity = false;
        string capacityLeft = string.Empty;
        string capacityRight = string.Empty;
        if (selectedFleet != null)
        {
            tabs = BuildTabs(uiContext, selectedOwnerFactionId, session);
            detailItems = BuildDetailItems(uiContext, session, selectedFleet);
            if (session.ActiveTab == FleetWindowTab.Starfighters)
            {
                showCapacity = true;
                capacityLeft = selectedFleet.GetStarfighters().Count().ToString();
                capacityRight = selectedFleet.GetStarfighterCapacity().ToString();
            }
            else if (session.ActiveTab == FleetWindowTab.Regiments)
            {
                showCapacity = true;
                capacityLeft = selectedFleet.GetRegiments().Count().ToString();
                capacityRight = selectedFleet.GetRegimentCapacity().ToString();
            }
        }

        return new FleetWindowRenderData(
            window.X,
            window.Y,
            GetTitleTexture(uiContext, ownerFactionId, active),
            session.Planet.Planet.GetDisplayName(),
            GetDetailBackgroundTexture(uiContext, selectedOwnerFactionId),
            rows,
            session.ActiveTab,
            session.SelectedFleetIndex,
            selectedFleet != null,
            GetFleetBannerTexture(uiContext, selectedFleet),
            GetFleetBannerEnrouteOverlayTexture(uiContext, selectedFleet),
            GetFleetBannerDamagedOverlayTexture(uiContext, selectedFleet),
            selectedFleet?.GetDisplayName(),
            GetFactionColor(uiContext, selectedOwnerFactionId),
            showCapacity,
            capacityLeft,
            capacityRight,
            tabs,
            detailItems,
            session.RenameFleetRowIndex,
            session.RenameDetailItemIndex,
            session.RenameTarget?.GetDisplayName()
        );
    }

    /// <summary>
    /// Projects the fleet-list rows and all resolved themed textures.
    /// </summary>
    /// <param name="uiContext">The current presentation context.</param>
    /// <param name="session">The controller-owned fleet session.</param>
    /// <param name="fleets">The ordered fleets.</param>
    /// <returns>The ordered immutable row snapshots.</returns>
    private static List<FleetListRowRenderData> BuildFleetRows(
        UIContext uiContext,
        FleetWindowSession session,
        IReadOnlyList<Fleet> fleets
    )
    {
        List<FleetListRowRenderData> rows = new List<FleetListRowRenderData>();
        for (int i = 0; i < fleets.Count; i++)
        {
            Fleet fleet = fleets[i];
            bool selected =
                session.SelectedFleetItems.Contains(i)
                || session.SelectedFleetItems.Count == 0 && i == session.SelectedFleetIndex;
            FactionTheme theme = uiContext.GetTheme(fleet.OwnerInstanceID);
            UnitTileIcons icons = theme?.PlanetOverlayTheme?.UnitTileIcons;
            rows.Add(
                new FleetListRowRenderData(
                    fleet.GetDisplayName(),
                    uiContext.GetTexture(icons?.FleetListIconImagePath),
                    IsFleetInTransit(fleet)
                        ? uiContext.GetTexture(icons?.FleetListEnrouteIconImagePath)
                        : null,
                    fleet.CapitalShips.Any(ship => ship.IsDamaged())
                        ? uiContext.GetTexture(icons?.FleetListDamagedIconImagePath)
                        : null,
                    GetBadgeTexture(
                        uiContext,
                        icons,
                        fleet.GetStarfighters().Any(),
                        badgeIcons => badgeIcons.FleetStarfightersBadgeImagePath
                    ),
                    GetBadgeTexture(
                        uiContext,
                        icons,
                        fleet.GetRegiments().Any(),
                        badgeIcons => badgeIcons.FleetTroopsBadgeImagePath
                    ),
                    GetBadgeTexture(
                        uiContext,
                        icons,
                        fleet.GetOfficers().Any() || fleet.GetSpecialForces().Any(),
                        badgeIcons => badgeIcons.FleetPersonnelBadgeImagePath
                    ),
                    selected ? uiContext.GetTexture(icons?.FleetListSelectionImagePath) : null
                )
            );
        }

        return rows;
    }

    /// <summary>
    /// Projects the four ordered tab textures for one selected fleet.
    /// </summary>
    /// <param name="uiContext">The current presentation context.</param>
    /// <param name="ownerFactionId">The selected fleet owner.</param>
    /// <param name="session">The current fleet-window session.</param>
    /// <returns>The ordered immutable tab snapshots.</returns>
    private static List<FleetWindowTabRenderData> BuildTabs(
        UIContext uiContext,
        string ownerFactionId,
        FleetWindowSession session
    )
    {
        List<FleetWindowTabRenderData> tabs = new List<FleetWindowTabRenderData>();
        FleetWindowTabsTheme theme = uiContext
            .GetTheme(ownerFactionId)
            ?.StrategyWindows?.Fleet?.Tabs;
        foreach (FleetWindowTab tab in FleetWindowRenderData.OrderedTabs)
        {
            WindowTabImageTheme tabTheme = GetTabTheme(theme, tab);
            bool empty = !session.HasDetailItems(tab);
            bool active = tab == session.ActiveTab;
            tabs.Add(
                new FleetWindowTabRenderData(
                    tab,
                    uiContext.GetTexture(tabTheme?.GetImagePathForContent(!empty, active)),
                    empty
                        ? null
                        : uiContext.GetTexture(
                            tabTheme?.GetImagePathForContent(hasItems: true, active: true)
                        )
                )
            );
        }

        return tabs;
    }

    /// <summary>
    /// Projects the selected fleet's current detail-tab cards.
    /// </summary>
    /// <param name="uiContext">The current presentation context.</param>
    /// <param name="session">The controller-owned fleet session.</param>
    /// <param name="fleet">The selected fleet.</param>
    /// <returns>The ordered detail-card snapshots.</returns>
    private static List<StrategyUnitCardRenderData> BuildDetailItems(
        UIContext uiContext,
        FleetWindowSession session,
        Fleet fleet
    )
    {
        IReadOnlyList<ISceneNode> items = session.DetailItems;
        List<StrategyUnitCardRenderData> data = new List<StrategyUnitCardRenderData>();
        FleetWindowTheme theme = uiContext.GetTheme(fleet.OwnerInstanceID)?.StrategyWindows?.Fleet;
        for (int i = 0; i < items.Count; i++)
        {
            ISceneNode item = items[i];
            CapitalShip capitalShip = item as CapitalShip;
            UnitTileIcons icons = uiContext
                .GetTheme(item.GetOwnerInstanceID())
                ?.PlanetOverlayTheme?.UnitTileIcons;
            data.Add(
                new StrategyUnitCardRenderData(
                    name: item.GetDisplayName(),
                    nameColor: Color.white,
                    showName: true,
                    useAlternateNameLayout: session.ActiveTab == FleetWindowTab.Personnel,
                    backgroundTexture: GetDetailItemBackgroundTexture(
                        uiContext,
                        theme,
                        fleet,
                        item,
                        session.ActiveTab
                    ),
                    enrouteOverlayTexture: GetDetailEnrouteOverlayTexture(uiContext, fleet, item),
                    damagedOverlayTexture: GetDetailDamagedOverlayTexture(uiContext, item),
                    entityTexture: uiContext.GetEntityTexture(item, true),
                    capturedOverlayTexture: uiContext.GetEntityCapturedOverlayTexture(item),
                    selectionTexture: session.SelectedDetailItems.Contains(i)
                        ? uiContext.GetTexture(
                            uiContext
                                .GetTheme(fleet.OwnerInstanceID)
                                ?.PlanetOverlayTheme?.UnitTileIcons?.FleetDetailSelectionImagePath
                        )
                        : null,
                    entityFrameYOffset: session.ActiveTab == FleetWindowTab.CapitalShips ? -1 : 0,
                    starfighterBadgeTexture: GetBadgeTexture(
                        uiContext,
                        icons,
                        capitalShip?.Starfighters.Any() == true,
                        badgeIcons => badgeIcons.FleetStarfightersBadgeImagePath
                    ),
                    troopBadgeTexture: GetBadgeTexture(
                        uiContext,
                        icons,
                        capitalShip?.Regiments.Any() == true,
                        badgeIcons => badgeIcons.FleetTroopsBadgeImagePath
                    ),
                    personnelBadgeTexture: GetBadgeTexture(
                        uiContext,
                        icons,
                        capitalShip?.Officers.Any() == true,
                        badgeIcons => badgeIcons.FleetPersonnelBadgeImagePath
                    ),
                    canDrag: true
                )
            );
        }

        return data;
    }

    /// <summary>
    /// Gets the theme for one fleet tab.
    /// </summary>
    /// <param name="tabs">The fleet tab theme.</param>
    /// <param name="tab">The fleet tab.</param>
    /// <returns>The matching tab theme, or null.</returns>
    private static WindowTabImageTheme GetTabTheme(FleetWindowTabsTheme tabs, FleetWindowTab tab)
    {
        return tab switch
        {
            FleetWindowTab.CapitalShips => tabs?.CapitalShips,
            FleetWindowTab.Starfighters => tabs?.Starfighters,
            FleetWindowTab.Regiments => tabs?.Regiments,
            FleetWindowTab.Personnel => tabs?.Officers,
            _ => null,
        };
    }

    /// <summary>
    /// Resolves an optional fleet badge texture.
    /// </summary>
    /// <param name="uiContext">The current presentation context.</param>
    /// <param name="icons">The applicable unit-tile icon theme.</param>
    /// <param name="visible">Whether the badge should be visible.</param>
    /// <param name="getPath">Selects the badge path.</param>
    /// <returns>The resolved badge texture, or null.</returns>
    private static Texture2D GetBadgeTexture(
        UIContext uiContext,
        UnitTileIcons icons,
        bool visible,
        Func<UnitTileIcons, string> getPath
    )
    {
        return visible && icons != null ? uiContext.GetTexture(getPath(icons)) : null;
    }

    /// <summary>
    /// Resolves one detail card's themed personnel background state.
    /// </summary>
    /// <param name="uiContext">The current presentation context.</param>
    /// <param name="theme">The selected fleet owner's window theme.</param>
    /// <param name="fleet">The selected fleet.</param>
    /// <param name="item">The represented detail item.</param>
    /// <param name="activeTab">The active detail tab.</param>
    /// <returns>The selected detail-card background, or null.</returns>
    private static Texture GetDetailItemBackgroundTexture(
        UIContext uiContext,
        FleetWindowTheme theme,
        Fleet fleet,
        ISceneNode item,
        FleetWindowTab activeTab
    )
    {
        if (item is IManufacturable { ManufacturingStatus: ManufacturingStatus.Building })
        {
            UnitTileIcons icons = uiContext
                .GetTheme(item.GetOwnerInstanceID())
                ?.PlanetOverlayTheme?.UnitTileIcons;
            return uiContext.GetTexture(icons?.FleetConstructionSmallImagePath);
        }

        bool enroute = IsItemInTransit(fleet, item);
        Texture2D personnelBackground = uiContext.GetTexture(theme?.PersonnelBackgroundImagePath);
        Texture2D personnelEnrouteBackground = uiContext.GetTexture(
            theme?.PersonnelEnrouteBackgroundImagePath
        );
        if (activeTab == FleetWindowTab.Regiments && item is Regiment)
            return enroute ? personnelEnrouteBackground : personnelBackground;
        if (activeTab != FleetWindowTab.Personnel || item is not Officer and not SpecialForces)
            return null;
        if (!enroute)
            return personnelBackground;

        string path = SelectStatusPath(item.InTransitSmallImagePath, item.InTransitImagePath);
        return uiContext.GetTexture(path) ?? personnelEnrouteBackground;
    }

    /// <summary>
    /// Resolves a non-personnel in-transit detail overlay.
    /// </summary>
    /// <param name="uiContext">The current presentation context.</param>
    /// <param name="fleet">The selected fleet.</param>
    /// <param name="item">The represented detail item.</param>
    /// <returns>The in-transit overlay, or null.</returns>
    private static Texture2D GetDetailEnrouteOverlayTexture(
        UIContext uiContext,
        Fleet fleet,
        ISceneNode item
    )
    {
        if (
            !IsItemInTransit(fleet, item)
            || item is Regiment or Officer or SpecialForces
            || item is IManufacturable { ManufacturingStatus: ManufacturingStatus.Building }
        )
            return null;

        return uiContext.GetTexture(
            SelectStatusPath(item.InTransitSmallImagePath, item.InTransitImagePath)
        );
    }

    /// <summary>
    /// Resolves injured, damaged, or losses artwork for one detail item.
    /// </summary>
    /// <param name="uiContext">The current presentation context.</param>
    /// <param name="item">The represented detail item.</param>
    /// <returns>The applicable status overlay, or null.</returns>
    private static Texture2D GetDetailDamagedOverlayTexture(UIContext uiContext, ISceneNode item)
    {
        if (
            item == null
            || item is IManufacturable { ManufacturingStatus: ManufacturingStatus.Building }
        )
            return null;
        if (item is Officer { InjuryPoints: > 0 } && !string.IsNullOrEmpty(item.InjuredImagePath))
            return uiContext.GetTexture(item.InjuredImagePath);

        bool damaged =
            item is CapitalShip capitalShip && capitalShip.IsDamaged()
            || item is Starfighter starfighter && starfighter.HasLosses();
        return damaged
            ? uiContext.GetTexture(
                SelectStatusPath(item.DamagedSmallImagePath, item.DamagedImagePath)
            )
            : null;
    }

    /// <summary>
    /// Resolves the fleet-window title texture for current focus state.
    /// </summary>
    /// <param name="uiContext">The current presentation context.</param>
    /// <param name="ownerFactionId">The represented planet owner.</param>
    /// <param name="active">Whether the window currently has focus.</param>
    /// <returns>The resolved title texture.</returns>
    private static Texture2D GetTitleTexture(
        UIContext uiContext,
        string ownerFactionId,
        bool active
    )
    {
        WindowTitleTheme theme = uiContext.GetTheme(ownerFactionId)?.WindowTitleTheme;
        return uiContext.GetTexture(active ? theme?.ActiveImagePath : theme?.InactiveImagePath);
    }

    /// <summary>
    /// Resolves the selected fleet owner's detail background.
    /// </summary>
    /// <param name="uiContext">The current presentation context.</param>
    /// <param name="ownerFactionId">The selected fleet owner.</param>
    /// <returns>The resolved detail background.</returns>
    private static Texture2D GetDetailBackgroundTexture(UIContext uiContext, string ownerFactionId)
    {
        return uiContext.GetTexture(
            uiContext.GetTheme(ownerFactionId)?.StrategyWindows?.Fleet?.DetailBackgroundImagePath
        );
    }

    /// <summary>
    /// Resolves the selected fleet banner.
    /// </summary>
    /// <param name="uiContext">The current presentation context.</param>
    /// <param name="fleet">The selected fleet.</param>
    /// <returns>The resolved banner, or null.</returns>
    private static Texture2D GetFleetBannerTexture(UIContext uiContext, Fleet fleet)
    {
        return fleet == null
            ? null
            : uiContext.GetTexture(
                uiContext.GetTheme(fleet.OwnerInstanceID)?.StrategyWindows?.Fleet?.BannerImagePath
            );
    }

    /// <summary>
    /// Resolves the selected fleet's in-transit banner overlay.
    /// </summary>
    /// <param name="uiContext">The current presentation context.</param>
    /// <param name="fleet">The selected fleet.</param>
    /// <returns>The resolved in-transit overlay, or null.</returns>
    private static Texture2D GetFleetBannerEnrouteOverlayTexture(UIContext uiContext, Fleet fleet)
    {
        return !IsFleetInTransit(fleet)
            ? null
            : uiContext.GetTexture(
                uiContext
                    .GetTheme(fleet.OwnerInstanceID)
                    ?.StrategyWindows?.Status?.FleetBannerEnrouteImagePath
            );
    }

    /// <summary>
    /// Resolves the selected fleet's damaged banner overlay.
    /// </summary>
    /// <param name="uiContext">The current presentation context.</param>
    /// <param name="fleet">The selected fleet.</param>
    /// <returns>The resolved damaged overlay, or null.</returns>
    private static Texture2D GetFleetBannerDamagedOverlayTexture(UIContext uiContext, Fleet fleet)
    {
        if (fleet?.CapitalShips.Any(ship => ship.IsDamaged()) != true)
            return null;

        return uiContext.GetTexture(
            uiContext
                .GetTheme(fleet.OwnerInstanceID)
                ?.StrategyWindows?.Status?.FleetBannerDamagedImagePath
        );
    }

    /// <summary>
    /// Resolves the configured faction color for a selected fleet owner.
    /// </summary>
    /// <param name="uiContext">The current presentation context.</param>
    /// <param name="ownerFactionId">The selected fleet owner.</param>
    /// <returns>The configured faction color.</returns>
    private static Color32 GetFactionColor(UIContext uiContext, string ownerFactionId)
    {
        return uiContext.GetTheme(ownerFactionId)?.GetPrimaryColor() ?? Color.white;
    }

    /// <summary>
    /// Reports whether one fleet is currently in transit.
    /// </summary>
    /// <param name="fleet">The fleet to inspect.</param>
    /// <returns>True when the fleet has movement state.</returns>
    private static bool IsFleetInTransit(Fleet fleet)
    {
        return fleet?.Movement != null;
    }

    /// <summary>
    /// Reports whether one detail item inherits or owns in-transit state.
    /// </summary>
    /// <param name="fleet">The selected fleet.</param>
    /// <param name="item">The detail item.</param>
    /// <returns>True when the fleet, item, or owning capital ship is moving.</returns>
    private static bool IsItemInTransit(Fleet fleet, ISceneNode item)
    {
        return fleet?.Movement != null
            || item is IMovable movable && movable.GetTransitMovement() != null;
    }

    /// <summary>
    /// Selects a preferred non-empty status-art path with a fallback.
    /// </summary>
    /// <param name="preferredPath">The preferred compact path.</param>
    /// <param name="fallbackPath">The fallback regular path.</param>
    /// <returns>The selected path.</returns>
    private static string SelectStatusPath(string preferredPath, string fallbackPath)
    {
        return !string.IsNullOrEmpty(preferredPath) ? preferredPath : fallbackPath;
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
}
