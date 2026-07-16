using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;
using UnityEngine;

/// <summary>
/// Projects facility game state into immutable window presentation data.
/// </summary>
internal sealed class FacilityWindowProjector
{
    private readonly Func<UIContext> getUIContext;

    /// <summary>
    /// Creates a facility window projector.
    /// </summary>
    /// <param name="getUIContext">Returns the active strategy presentation context.</param>
    public FacilityWindowProjector(Func<UIContext> getUIContext)
    {
        this.getUIContext = getUIContext ?? throw new ArgumentNullException(nameof(getUIContext));
    }

    /// <summary>
    /// Creates the presentation snapshot for one facility window.
    /// </summary>
    /// <param name="window">The owning window shell.</param>
    /// <param name="session">The controller-owned facility session.</param>
    /// <param name="destinationNames">The configured manufacturing destination labels.</param>
    /// <returns>The completed facility presentation snapshot.</returns>
    public FacilityWindowRenderData CreateRenderData(
        UIWindow window,
        FacilityWindowSession session,
        IReadOnlyDictionary<ManufacturingType, string> destinationNames
    )
    {
        if (window == null)
            throw new ArgumentNullException(nameof(window));
        if (session == null)
            throw new ArgumentNullException(nameof(session));

        UIContext uiContext = GetRequiredUIContext();
        GalaxyMapPlanet planet = session.Planet;
        Planet source = planet?.Planet;
        string ownerFactionId = planet?.OwnerFactionId;
        FacilityWindowTab activeTab = session.ActiveTab;
        IReadOnlyList<FacilityWindowTabRenderData> tabs = CreateTabs(session);
        WindowTabImageTheme controlTabTheme = uiContext
            .GetTheme(ownerFactionId)
            ?.StrategyWindows?.Facility?.ControlTab;
        FacilityWindowTabState controlState = tabs.First(tab =>
            tab.Tab == FacilityWindowTab.Manufacturing
        ).State;

        List<ManufacturingLaneCardRenderData> manufacturingCards =
            activeTab == FacilityWindowTab.Manufacturing
                ? CreateManufacturingCards(uiContext, session, ownerFactionId, destinationNames)
                : new List<ManufacturingLaneCardRenderData>();
        List<FacilityInventoryItemRenderData> inventoryItems =
            activeTab == FacilityWindowTab.Manufacturing
                ? new List<FacilityInventoryItemRenderData>()
                : CreateInventoryItems(uiContext, session, ownerFactionId);
        WindowTitleTheme titleTheme = uiContext.GetTheme(ownerFactionId)?.WindowTitleTheme;
        string selectionPath = uiContext
            .GetTheme(ownerFactionId)
            ?.StrategyWindows?.Facility?.SelectionImagePath;

        return new FacilityWindowRenderData(
            window.X,
            window.Y,
            uiContext.GetTexture(
                window.ActiveWindow ? titleTheme?.ActiveImagePath : titleTheme?.InactiveImagePath
            ),
            source?.GetDisplayName(),
            activeTab,
            tabs,
            uiContext.GetTexture(controlTabTheme?.GetImagePath((int)controlState)),
            uiContext.GetTexture(controlTabTheme?.GetImagePath((int)FacilityWindowTabState.Active)),
            manufacturingCards,
            GetInventoryTitle(activeTab),
            inventoryItems,
            uiContext.GetTexture(selectionPath)
        );
    }

    /// <summary>
    /// Creates authored presentations for every facility tab.
    /// </summary>
    /// <param name="session">The controller-owned facility session.</param>
    /// <returns>The facility-tab presentations in authored order.</returns>
    private static IReadOnlyList<FacilityWindowTabRenderData> CreateTabs(
        FacilityWindowSession session
    )
    {
        List<FacilityWindowTabRenderData> tabs = new List<FacilityWindowTabRenderData>();
        foreach (FacilityWindowTab tab in FacilityWindowRenderData.OrderedTabs)
        {
            int count = session.GetDisplayCount(tab);
            tabs.Add(
                new FacilityWindowTabRenderData(
                    tab,
                    tab == session.ActiveTab ? FacilityWindowTabState.Active
                        : count > 0 ? FacilityWindowTabState.Inactive
                        : FacilityWindowTabState.Disabled
                )
            );
        }

        return tabs.AsReadOnly();
    }

    /// <summary>
    /// Creates the three manufacturing lane presentations.
    /// </summary>
    /// <param name="uiContext">The active presentation context.</param>
    /// <param name="session">The controller-owned facility session.</param>
    /// <param name="ownerFactionId">The planet owner faction identifier.</param>
    /// <param name="destinationNames">The configured manufacturing destination labels.</param>
    /// <returns>The manufacturing lane presentations.</returns>
    private static List<ManufacturingLaneCardRenderData> CreateManufacturingCards(
        UIContext uiContext,
        FacilityWindowSession session,
        string ownerFactionId,
        IReadOnlyDictionary<ManufacturingType, string> destinationNames
    )
    {
        Planet planet = session.Planet?.Planet;
        return new List<ManufacturingLaneCardRenderData>
        {
            CreateManufacturingCard(
                uiContext,
                planet,
                ownerFactionId,
                ManufacturingType.Ship,
                FacilityWindowTab.Shipyards,
                session.GetItems(FacilityWindowTab.Shipyards),
                session.SelectedCards,
                destinationNames
            ),
            CreateManufacturingCard(
                uiContext,
                planet,
                ownerFactionId,
                ManufacturingType.Troop,
                FacilityWindowTab.Training,
                session.GetItems(FacilityWindowTab.Training),
                session.SelectedCards,
                destinationNames
            ),
            CreateManufacturingCard(
                uiContext,
                planet,
                ownerFactionId,
                ManufacturingType.Building,
                FacilityWindowTab.Construction,
                session.GetItems(FacilityWindowTab.Construction),
                session.SelectedCards,
                destinationNames
            ),
        };
    }

    /// <summary>
    /// Creates one manufacturing lane presentation.
    /// </summary>
    /// <param name="uiContext">The active presentation context.</param>
    /// <param name="planet">The represented planet.</param>
    /// <param name="ownerFactionId">The planet owner faction identifier.</param>
    /// <param name="type">The lane manufacturing category.</param>
    /// <param name="manufacturingTab">The manufacturing facility tab.</param>
    /// <param name="facilities">The session-owned facilities for this lane.</param>
    /// <param name="selectedCards">The selected manufacturing lane indexes.</param>
    /// <param name="destinationNames">The configured manufacturing destination labels.</param>
    /// <returns>The manufacturing lane presentation.</returns>
    private static ManufacturingLaneCardRenderData CreateManufacturingCard(
        UIContext uiContext,
        Planet planet,
        string ownerFactionId,
        ManufacturingType type,
        FacilityWindowTab manufacturingTab,
        IReadOnlyList<Building> facilities,
        IReadOnlyCollection<int> selectedCards,
        IReadOnlyDictionary<ManufacturingType, string> destinationNames
    )
    {
        List<IManufacturable> queue = GetQueue(planet, type);
        IManufacturable current = queue.FirstOrDefault(item =>
            item.GetManufacturingStatus() == ManufacturingStatus.Building
        );
        int activeFacilityCount = facilities.Count(building =>
            building.GetManufacturingStatus() == ManufacturingStatus.Complete
        );
        bool selected = selectedCards?.Contains((int)manufacturingTab) == true;
        ManufacturingLaneStateTheme stateTheme = uiContext
            .GetTheme(ownerFactionId)
            ?.PlanetWindowTheme?.BuildingsPane?.ManufacturingLaneState;

        return new ManufacturingLaneCardRenderData(
            uiContext.GetTexture(
                selected ? stateTheme?.ActiveImagePath : stateTheme?.InactiveImagePath
            ),
            current == null ? null : uiContext.GetEntityTexture(current, true),
            current?.GetManufacturingProgress() ?? 0,
            current?.GetConstructionCost() ?? 0,
            GetManufacturingTitle(type),
            GetManufacturingEmptyText(type),
            current?.GetDisplayName(),
            current == null ? string.Empty : "Building " + Math.Max(1, queue.Count),
            "Destination: " + GetDestinationName(planet, type, destinationNames),
            $"{activeFacilityCount}:{facilities.Count}"
        );
    }

    /// <summary>
    /// Gets the configured destination label for one manufacturing category.
    /// </summary>
    /// <param name="planet">The represented planet.</param>
    /// <param name="type">The manufacturing category.</param>
    /// <param name="destinationNames">The configured destination labels.</param>
    /// <returns>The destination label.</returns>
    private static string GetDestinationName(
        Planet planet,
        ManufacturingType type,
        IReadOnlyDictionary<ManufacturingType, string> destinationNames
    )
    {
        return
            destinationNames?.TryGetValue(type, out string destinationName) == true
            && !string.IsNullOrEmpty(destinationName)
            ? destinationName
            : planet?.GetDisplayName() ?? string.Empty;
    }

    /// <summary>
    /// Creates the inventory item presentations for one facility tab.
    /// </summary>
    /// <param name="uiContext">The active presentation context.</param>
    /// <param name="session">The controller-owned facility session.</param>
    /// <param name="ownerFactionId">The represented planet owner.</param>
    /// <returns>The inventory item presentations.</returns>
    private static List<FacilityInventoryItemRenderData> CreateInventoryItems(
        UIContext uiContext,
        FacilityWindowSession session,
        string ownerFactionId
    )
    {
        FacilityWindowTab activeTab = session.ActiveTab;
        IReadOnlyList<Building> items = session.GetItems(activeTab);
        int total = session.GetDisplayCount(activeTab);
        List<FacilityInventoryItemRenderData> presentations =
            new List<FacilityInventoryItemRenderData>(total);
        for (int index = 0; index < total; index++)
        {
            Building item = index < items.Count ? items[index] : null;
            presentations.Add(
                new FacilityInventoryItemRenderData(
                    GetInventoryTexture(uiContext, ownerFactionId, activeTab, item),
                    item != null && session.SelectedBuildingIds.Contains(item.InstanceID)
                )
            );
        }

        return presentations;
    }

    /// <summary>
    /// Resolves the image displayed for one inventory item.
    /// </summary>
    /// <param name="uiContext">The active presentation context.</param>
    /// <param name="ownerFactionId">The represented planet owner.</param>
    /// <param name="tab">The inventory tab.</param>
    /// <param name="building">The represented building, when present.</param>
    /// <returns>The resolved inventory image.</returns>
    private static Texture GetInventoryTexture(
        UIContext uiContext,
        string ownerFactionId,
        FacilityWindowTab tab,
        Building building
    )
    {
        if (building == null)
        {
            return tab == FacilityWindowTab.Mines
                ? uiContext.GetTexture(
                    uiContext
                        .GetTheme(ownerFactionId)
                        ?.StrategyWindows?.Facility?.RawResourceNodeImagePath
                )
                : null;
        }

        if (building.Movement != null)
        {
            Texture movementTexture = uiContext.GetTexture(building.InTransitSmallImagePath);
            if (movementTexture != null)
                return movementTexture;
        }

        if (building.GetManufacturingStatus() == ManufacturingStatus.Building)
        {
            string constructionPath = uiContext
                .GetTheme(ownerFactionId)
                ?.StrategyWindows?.Facility?.GetConstructionImagePath(building.GetTypeID());
            Texture constructionTexture = uiContext.GetTexture(constructionPath);
            if (constructionTexture != null)
                return constructionTexture;
        }

        return uiContext.GetEntityTexture(building, true);
    }

    /// <summary>
    /// Gets the manufacturing queue for one category.
    /// </summary>
    /// <param name="planet">The represented planet.</param>
    /// <param name="type">The manufacturing category.</param>
    /// <returns>The queue or an empty list.</returns>
    private static List<IManufacturable> GetQueue(Planet planet, ManufacturingType type)
    {
        return planet?.ManufacturingQueue.TryGetValue(type, out List<IManufacturable> queue) == true
            ? queue
            : new List<IManufacturable>();
    }

    /// <summary>
    /// Gets the display title for one manufacturing lane.
    /// </summary>
    /// <param name="type">The manufacturing category.</param>
    /// <returns>The lane title.</returns>
    private static string GetManufacturingTitle(ManufacturingType type)
    {
        return type switch
        {
            ManufacturingType.Ship => "Ship Construction",
            ManufacturingType.Troop => "Troops in Training",
            ManufacturingType.Building => "Facilities Under Construction",
            _ => string.Empty,
        };
    }

    /// <summary>
    /// Gets the idle text for one manufacturing lane.
    /// </summary>
    /// <param name="type">The manufacturing category.</param>
    /// <returns>The idle lane text.</returns>
    private static string GetManufacturingEmptyText(ManufacturingType type)
    {
        return type switch
        {
            ManufacturingType.Ship => "No Ships are being built",
            ManufacturingType.Troop => "No Troops in training",
            ManufacturingType.Building => "No Facilities are being built",
            _ => string.Empty,
        };
    }

    /// <summary>
    /// Gets the inventory title for one facility tab.
    /// </summary>
    /// <param name="activeTab">The selected inventory tab.</param>
    /// <returns>The inventory title.</returns>
    private static string GetInventoryTitle(FacilityWindowTab activeTab)
    {
        return activeTab switch
        {
            FacilityWindowTab.Shipyards => "Shipyards",
            FacilityWindowTab.Training => "Training Facilities",
            FacilityWindowTab.Construction => "Construction Yards",
            FacilityWindowTab.Refineries => "Refineries",
            FacilityWindowTab.Mines => "Mines",
            _ => string.Empty,
        };
    }

    /// <summary>
    /// Gets the current presentation context and rejects incomplete composition.
    /// </summary>
    /// <returns>The active presentation context.</returns>
    private UIContext GetRequiredUIContext()
    {
        return getUIContext()
            ?? throw new InvalidOperationException("The strategy UI context is unavailable.");
    }
}
