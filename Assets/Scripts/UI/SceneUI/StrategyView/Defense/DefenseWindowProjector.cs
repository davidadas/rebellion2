using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using UnityEngine;

/// <summary>
/// Projects Defense domain state into immutable view presentation data.
/// </summary>
internal sealed class DefenseWindowProjector
{
    private static readonly Color32 _white = Color.white;

    private readonly Func<UIContext> getUIContext;

    /// <summary>
    /// Creates the Defense presentation projector.
    /// </summary>
    /// <param name="getUIContext">Returns the current strategy presentation context.</param>
    public DefenseWindowProjector(Func<UIContext> getUIContext)
    {
        this.getUIContext = getUIContext ?? throw new ArgumentNullException(nameof(getUIContext));
    }

    /// <summary>
    /// Projects one complete Defense-window presentation snapshot.
    /// </summary>
    /// <param name="session">The controller-owned Defense session.</param>
    /// <param name="window">The owning window shell.</param>
    /// <param name="active">Whether the window currently has focus.</param>
    /// <returns>The immutable Defense-window presentation.</returns>
    public DefenseWindowRenderData Build(DefenseWindowSession session, UIWindow window, bool active)
    {
        if (session == null)
            throw new ArgumentNullException(nameof(session));
        if (window == null)
            throw new ArgumentNullException(nameof(window));

        UIContext uiContext = GetRequiredUIContext();
        DefenseWindowTab normalizedTab = session.ActiveTab;
        GalaxyMapPlanet planet = session.Planet;
        string ownerFactionId = planet?.OwnerFactionId;
        FactionTheme factionTheme = uiContext.GetTheme(ownerFactionId);
        WindowTitleTheme titleTheme = factionTheme?.WindowTitleTheme;
        DefenseWindowTheme defenseTheme = factionTheme?.StrategyWindows?.Defense;

        List<DefenseWindowTabRenderData> tabs = new List<DefenseWindowTabRenderData>(
            DefenseWindowRenderData.TabCount
        );
        foreach (DefenseWindowTab tab in DefenseWindowRenderData.OrderedTabs)
        {
            IReadOnlyList<ISceneNode> tabItems = session.GetItems(tab);
            tabs.Add(
                CreateTabRenderData(
                    uiContext,
                    defenseTheme,
                    tab,
                    tabItems.Count > 0,
                    tab == normalizedTab
                )
            );
        }

        List<StrategyUnitCardRenderData> items = new List<StrategyUnitCardRenderData>();
        foreach (ISceneNode item in session.GetItems(normalizedTab))
        {
            items.Add(
                CreateItemRenderData(
                    uiContext,
                    defenseTheme,
                    ownerFactionId,
                    item,
                    items.Count,
                    session.SelectedItemIndexes
                )
            );
        }

        return new DefenseWindowRenderData(
            window.X,
            window.Y,
            uiContext.GetTexture(
                active ? titleTheme?.ActiveImagePath : titleTheme?.InactiveImagePath
            ),
            planet?.Planet?.GetDisplayName(),
            normalizedTab,
            GetTabTitle(normalizedTab),
            tabs,
            items
        );
    }

    /// <summary>
    /// Returns the displayed title for one Defense tab.
    /// </summary>
    /// <param name="tab">The requested Defense tab.</param>
    /// <returns>The displayed tab title.</returns>
    public static string GetTabTitle(DefenseWindowTab tab)
    {
        return tab switch
        {
            DefenseWindowTab.Personnel => "Personnel",
            DefenseWindowTab.Regiments => "Troops/Regiments",
            DefenseWindowTab.Starfighters => "Fighter Squadrons",
            DefenseWindowTab.Shields => "Planetary Shields",
            DefenseWindowTab.Batteries => "Planetary Batteries",
            _ => string.Empty,
        };
    }

    /// <summary>
    /// Projects one themed Defense tab state.
    /// </summary>
    /// <param name="uiContext">The current presentation context.</param>
    /// <param name="theme">The applicable Defense theme.</param>
    /// <param name="tab">The Defense tab.</param>
    /// <param name="enabled">Whether the tab contains items.</param>
    /// <param name="active">Whether the tab is selected.</param>
    /// <returns>The immutable tab presentation.</returns>
    private static DefenseWindowTabRenderData CreateTabRenderData(
        UIContext uiContext,
        DefenseWindowTheme theme,
        DefenseWindowTab tab,
        bool enabled,
        bool active
    )
    {
        Texture normalTexture = GetTabTexture(uiContext, theme, tab, enabled, active);
        Texture pressedTexture = enabled ? GetTabTexture(uiContext, theme, tab, true, true) : null;
        return new DefenseWindowTabRenderData(tab, normalTexture, pressedTexture);
    }

    /// <summary>
    /// Resolves one complete Defense tab-state texture.
    /// </summary>
    /// <param name="uiContext">The current presentation context.</param>
    /// <param name="theme">The applicable Defense theme.</param>
    /// <param name="tab">The Defense tab.</param>
    /// <param name="enabled">Whether the tab contains items.</param>
    /// <param name="active">Whether the tab is selected.</param>
    /// <returns>The resolved tab texture.</returns>
    private static Texture GetTabTexture(
        UIContext uiContext,
        DefenseWindowTheme theme,
        DefenseWindowTab tab,
        bool enabled,
        bool active
    )
    {
        return tab switch
        {
            DefenseWindowTab.Personnel => GetThemedTabTexture(
                uiContext,
                theme?.PersonnelTab,
                enabled,
                active
            ),
            DefenseWindowTab.Regiments => GetThemedTabTexture(
                uiContext,
                theme?.TroopTab,
                enabled,
                active
            ),
            DefenseWindowTab.Starfighters => GetThemedTabTexture(
                uiContext,
                theme?.FighterTab,
                enabled,
                active
            ),
            DefenseWindowTab.Shields => GetThemedTabTexture(
                uiContext,
                theme?.ShieldTab,
                enabled,
                active
            ),
            DefenseWindowTab.Batteries => GetThemedTabTexture(
                uiContext,
                theme?.BatteryTab,
                enabled,
                active
            ),
            _ => null,
        };
    }

    /// <summary>
    /// Resolves one faction-themed tab texture.
    /// </summary>
    /// <param name="uiContext">The current presentation context.</param>
    /// <param name="theme">The applicable tab theme.</param>
    /// <param name="enabled">Whether the tab contains items.</param>
    /// <param name="active">Whether the tab is selected.</param>
    /// <returns>The resolved tab texture.</returns>
    private static Texture GetThemedTabTexture(
        UIContext uiContext,
        WindowTabImageTheme theme,
        bool enabled,
        bool active
    )
    {
        return uiContext.GetTexture(theme?.GetImagePath(enabled, active));
    }

    /// <summary>
    /// Projects one Defense unit card from current domain and theme state.
    /// </summary>
    /// <param name="uiContext">The current presentation context.</param>
    /// <param name="theme">The represented planet owner's Defense theme.</param>
    /// <param name="ownerFactionId">The represented planet owner.</param>
    /// <param name="item">The represented scene node.</param>
    /// <param name="index">The card index.</param>
    /// <param name="selectedItems">The current selected indices.</param>
    /// <returns>The immutable unit-card presentation.</returns>
    private static StrategyUnitCardRenderData CreateItemRenderData(
        UIContext uiContext,
        DefenseWindowTheme theme,
        string ownerFactionId,
        ISceneNode item,
        int index,
        IReadOnlyCollection<int> selectedItems
    )
    {
        bool selected = selectedItems?.Contains(index) == true;
        return new StrategyUnitCardRenderData(
            name: item?.GetDisplayName(),
            nameColor: selected ? GetFactionColor(uiContext, ownerFactionId) : _white,
            showName: true,
            useAlternateNameLayout: false,
            backgroundTexture: GetItemBackgroundTexture(uiContext, theme, item),
            constructionOverlayTexture: GetItemConstructionOverlayTexture(uiContext, item),
            enrouteOverlayTexture: GetItemEnrouteOverlayTexture(uiContext, item),
            damagedOverlayTexture: GetItemDamagedOverlayTexture(uiContext, item),
            entityTexture: uiContext.GetEntityTexture(item, true),
            capturedOverlayTexture: uiContext.GetEntityCapturedOverlayTexture(item),
            selectionTexture: selected ? GetSelectionTexture(uiContext, ownerFactionId) : null,
            entityFrameYOffset: 0,
            starfighterBadgeTexture: null,
            troopBadgeTexture: null,
            personnelBadgeTexture: null,
            canDrag: DefenseWindowSession.CanDragItem(item)
        );
    }

    /// <summary>
    /// Resolves the card background for personnel and in-transit units.
    /// </summary>
    /// <param name="uiContext">The current presentation context.</param>
    /// <param name="theme">The represented planet owner's Defense theme.</param>
    /// <param name="item">The represented scene node.</param>
    /// <returns>The optional card background texture.</returns>
    private static Texture GetItemBackgroundTexture(
        UIContext uiContext,
        DefenseWindowTheme theme,
        ISceneNode item
    )
    {
        if (IsItemInTransit(item))
        {
            return GetPersonnelEnrouteBackgroundTexture(uiContext, item)
                ?? uiContext.GetTexture(theme?.EnrouteBackgroundImagePath);
        }

        return item is Officer or SpecialForces
            ? uiContext.GetTexture(theme?.PersonnelBackgroundImagePath)
            : null;
    }

    /// <summary>
    /// Resolves the construction overlay for a building unit.
    /// </summary>
    /// <param name="uiContext">The current presentation context.</param>
    /// <param name="item">The represented scene node.</param>
    /// <returns>The optional construction overlay.</returns>
    private static Texture GetItemConstructionOverlayTexture(UIContext uiContext, ISceneNode item)
    {
        if (
            item is not IManufacturable manufacturable
            || manufacturable.GetManufacturingStatus() != ManufacturingStatus.Building
        )
            return null;

        UnitTileIcons icons = uiContext
            .GetTheme(item.GetOwnerInstanceID())
            ?.PlanetOverlayTheme?.UnitTileIcons;
        return uiContext.GetTexture(icons?.FleetConstructionSmallImagePath);
    }

    /// <summary>
    /// Resolves the in-transit overlay for a non-personnel unit.
    /// </summary>
    /// <param name="uiContext">The current presentation context.</param>
    /// <param name="item">The represented scene node.</param>
    /// <returns>The optional in-transit overlay.</returns>
    private static Texture GetItemEnrouteOverlayTexture(UIContext uiContext, ISceneNode item)
    {
        if (!IsItemInTransit(item) || item is Regiment or Officer or SpecialForces)
            return null;
        if (item is IManufacturable { ManufacturingStatus: ManufacturingStatus.Building })
            return null;

        return uiContext.GetTexture(
            SelectStatusPath(item.InTransitSmallImagePath, item.InTransitImagePath)
        );
    }

    /// <summary>
    /// Resolves the in-transit personnel background for a personnel card.
    /// </summary>
    /// <param name="uiContext">The current presentation context.</param>
    /// <param name="item">The represented scene node.</param>
    /// <returns>The optional in-transit personnel background.</returns>
    private static Texture GetPersonnelEnrouteBackgroundTexture(
        UIContext uiContext,
        ISceneNode item
    )
    {
        if (item is not Officer and not SpecialForces)
            return null;

        return uiContext.GetTexture(
            SelectStatusPath(item.InTransitSmallImagePath, item.InTransitImagePath)
        );
    }

    /// <summary>
    /// Resolves the injured, damaged, or depleted overlay for one unit.
    /// </summary>
    /// <param name="uiContext">The current presentation context.</param>
    /// <param name="item">The represented scene node.</param>
    /// <returns>The optional damage-state overlay.</returns>
    private static Texture GetItemDamagedOverlayTexture(UIContext uiContext, ISceneNode item)
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
    /// Resolves the configured selection frame for the represented faction.
    /// </summary>
    /// <param name="uiContext">The current presentation context.</param>
    /// <param name="ownerFactionId">The represented planet owner.</param>
    /// <returns>The selection-frame texture.</returns>
    private static Texture GetSelectionTexture(UIContext uiContext, string ownerFactionId)
    {
        string path = uiContext
            .GetTheme(ownerFactionId)
            ?.StrategyWindows?.Defense?.SelectionImagePath;
        return uiContext.GetTexture(path);
    }

    /// <summary>
    /// Resolves the configured display color for the represented faction.
    /// </summary>
    /// <param name="uiContext">The current presentation context.</param>
    /// <param name="ownerFactionId">The represented planet owner.</param>
    /// <returns>The configured faction color.</returns>
    private static Color32 GetFactionColor(UIContext uiContext, string ownerFactionId)
    {
        return uiContext.GetTheme(ownerFactionId)?.GetPrimaryColor() ?? Color.white;
    }

    /// <summary>
    /// Reports whether one scene node is currently in transit.
    /// </summary>
    /// <param name="item">The scene node to inspect.</param>
    /// <returns>True when the node has active movement.</returns>
    private static bool IsItemInTransit(ISceneNode item)
    {
        return item is IMovable { Movement: not null };
    }

    /// <summary>
    /// Selects the preferred non-empty status path with a fallback.
    /// </summary>
    /// <param name="preferredPath">The preferred compact status path.</param>
    /// <param name="fallbackPath">The fallback status path.</param>
    /// <returns>The selected status path.</returns>
    private static string SelectStatusPath(string preferredPath, string fallbackPath)
    {
        return !string.IsNullOrEmpty(preferredPath) ? preferredPath : fallbackPath;
    }

    /// <summary>
    /// Gets the current required presentation context.
    /// </summary>
    /// <returns>The current presentation context.</returns>
    private UIContext GetRequiredUIContext()
    {
        return getUIContext()
            ?? throw new InvalidOperationException("Defense presentation context is unavailable.");
    }
}
