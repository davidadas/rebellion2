using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Galaxy;
using Rebellion.Game.Units;

/// <summary>
/// Owns the ordered facility inventory snapshot and mutable interaction state for one window.
/// </summary>
internal sealed class FacilityWindowSession
{
    private readonly Dictionary<ManufacturingType, (string PlanetId, string ItemId)> destinations =
        new Dictionary<ManufacturingType, (string PlanetId, string ItemId)>();
    private readonly Dictionary<FacilityWindowTab, int> displayCounts =
        new Dictionary<FacilityWindowTab, int>();
    private readonly Dictionary<FacilityWindowTab, List<Building>> itemsByTab =
        new Dictionary<FacilityWindowTab, List<Building>>();
    private readonly HashSet<string> selectedBuildingIds = new HashSet<string>(
        StringComparer.Ordinal
    );
    private readonly HashSet<int> selectedCards = new HashSet<int>();
    private string contextBuildingId;

    /// <summary>
    /// Creates a facility-window session for one represented strategy planet.
    /// </summary>
    /// <param name="window">The owning facility window.</param>
    /// <param name="planet">The represented strategy planet.</param>
    public FacilityWindowSession(UIWindow window, GalaxyMapPlanet planet)
    {
        Window = window ?? throw new ArgumentNullException(nameof(window));
        RebindPlanet(planet);
    }

    /// <summary>
    /// Gets the active tab.
    /// </summary>
    public FacilityWindowTab ActiveTab { get; private set; } = FacilityWindowTab.Manufacturing;

    /// <summary>
    /// Gets the context manufacturing facility tab.
    /// </summary>
    public FacilityWindowTab? ContextManufacturingTab { get; private set; }

    /// <summary>
    /// Gets the represented planet.
    /// </summary>
    public GalaxyMapPlanet Planet { get; private set; }

    /// <summary>
    /// Gets the selected inventory building identifiers.
    /// </summary>
    public IReadOnlyCollection<string> SelectedBuildingIds => selectedBuildingIds;

    /// <summary>
    /// Gets the selected manufacturing lane indexes.
    /// </summary>
    public IReadOnlyCollection<int> SelectedCards => selectedCards;

    /// <summary>
    /// Gets the owning facility window.
    /// </summary>
    public UIWindow Window { get; }

    /// <summary>
    /// Rebinds the session to the refreshed projection of its represented planet.
    /// </summary>
    /// <param name="planet">The refreshed strategy planet projection.</param>
    public void RebindPlanet(GalaxyMapPlanet planet)
    {
        if (planet?.Planet == null)
            throw new ArgumentException(
                "A facility session requires a projected planet.",
                nameof(planet)
            );

        Planet = planet;
        Reconcile();
    }

    /// <summary>
    /// Rebuilds the ordered tab snapshot and reconciles interaction state by building identity.
    /// </summary>
    public void Reconcile()
    {
        foreach (FacilityWindowTab tab in FacilityWindowRenderData.OrderedTabs)
        {
            List<Building> items = FacilityWindowInventory.GetItems(Planet?.Planet, tab);
            itemsByTab[tab] = items;
            displayCounts[tab] = GetDisplayCount(Planet?.Planet, tab, items.Count);
        }

        if (ActiveTab == FacilityWindowTab.Manufacturing)
        {
            selectedBuildingIds.Clear();
            contextBuildingId = null;
            selectedCards.RemoveWhere(index => !GetManufacturingTab(index).HasValue);
            return;
        }

        selectedCards.Clear();
        ContextManufacturingTab = null;
        IReadOnlyList<Building> inventoryItems = GetItems(ActiveTab);
        HashSet<string> availableIds = new HashSet<string>(
            inventoryItems.Select(GetBuildingId).Where(id => !string.IsNullOrEmpty(id)),
            StringComparer.Ordinal
        );
        selectedBuildingIds.RemoveWhere(id => !availableIds.Contains(id));
        if (!availableIds.Contains(contextBuildingId))
            contextBuildingId = null;
    }

    /// <summary>
    /// Gets the ordered facility snapshot for one tab.
    /// </summary>
    /// <param name="tab">The requested facility tab.</param>
    /// <returns>The session-owned ordered buildings.</returns>
    public IReadOnlyList<Building> GetItems(FacilityWindowTab tab)
    {
        return itemsByTab.TryGetValue(tab, out List<Building> items)
            ? items
            : Array.Empty<Building>();
    }

    /// <summary>
    /// Gets the number of authored item slots displayed by one tab.
    /// </summary>
    /// <param name="tab">The requested facility tab.</param>
    /// <returns>The displayed item-slot count.</returns>
    public int GetDisplayCount(FacilityWindowTab tab)
    {
        return displayCounts.TryGetValue(tab, out int count) ? count : 0;
    }

    /// <summary>
    /// Changes the active tab and clears incompatible interaction state.
    /// </summary>
    /// <param name="tab">The selected facility tab.</param>
    public void SetActiveTab(FacilityWindowTab tab)
    {
        if (ActiveTab == tab)
            return;

        ActiveTab = tab;
        ClearSelection();
    }

    /// <summary>
    /// Gets the singular selected manufacturing lane.
    /// </summary>
    /// <returns>The selected manufacturing tab, or null.</returns>
    public FacilityWindowTab? GetSelectedManufacturingTab()
    {
        if (ActiveTab != FacilityWindowTab.Manufacturing || selectedCards.Count != 1)
            return null;

        return GetManufacturingTab(selectedCards.First());
    }

    /// <summary>
    /// Gets the context-targeted or singular selected manufacturing lane.
    /// </summary>
    /// <returns>The context manufacturing tab, or null.</returns>
    public FacilityWindowTab? GetContextManufacturingTab()
    {
        return ActiveTab == FacilityWindowTab.Manufacturing
            ? ContextManufacturingTab ?? GetSelectedManufacturingTab()
            : null;
    }

    /// <summary>
    /// Captures one manufacturing lane as the active context target.
    /// </summary>
    /// <param name="tab">The manufacturing facility tab.</param>
    public void CaptureManufacturingContext(FacilityWindowTab tab)
    {
        ContextManufacturingTab = tab;
        contextBuildingId = null;
    }

    /// <summary>
    /// Applies normal selection rules to one manufacturing lane.
    /// </summary>
    /// <param name="cardIndex">The manufacturing lane index.</param>
    /// <param name="cardCount">The number of selectable manufacturing lanes.</param>
    public void SelectManufacturingCard(int cardIndex, int cardCount)
    {
        SelectableListSelection.SelectIndexedItem(selectedCards, cardIndex, cardCount);
    }

    /// <summary>
    /// Selects one manufacturing lane for a context command.
    /// </summary>
    /// <param name="tab">The manufacturing facility tab.</param>
    /// <param name="cardIndex">The manufacturing lane index.</param>
    public void SelectManufacturingCardForContext(FacilityWindowTab tab, int cardIndex)
    {
        CaptureManufacturingContext(tab);
        SelectContextItem(selectedCards, cardIndex);
    }

    /// <summary>
    /// Captures one inventory building as the active context target.
    /// </summary>
    /// <param name="itemIndex">The targeted inventory display index.</param>
    public void CaptureBuildingContext(int itemIndex)
    {
        ContextManufacturingTab = null;
        contextBuildingId = GetBuildingId(GetInventoryBuilding(itemIndex));
    }

    /// <summary>
    /// Selects one inventory building for a context command.
    /// </summary>
    /// <param name="itemIndex">The targeted inventory display index.</param>
    public void SelectBuildingForContext(int itemIndex)
    {
        CaptureBuildingContext(itemIndex);
        if (
            string.IsNullOrEmpty(contextBuildingId)
            || selectedBuildingIds.Contains(contextBuildingId)
        )
            return;

        selectedBuildingIds.Clear();
        selectedBuildingIds.Add(contextBuildingId);
    }

    /// <summary>
    /// Applies normal selection rules to one inventory building.
    /// </summary>
    /// <param name="itemIndex">The selected display index.</param>
    /// <param name="itemsPerRow">The number of inventory items in one visual row.</param>
    public void SelectBuilding(int itemIndex, int itemsPerRow)
    {
        IReadOnlyList<Building> items = GetItems(ActiveTab);
        if (
            itemIndex < 0
            || itemIndex >= items.Count
            || string.IsNullOrEmpty(GetBuildingId(items[itemIndex]))
        )
            return;

        HashSet<int> selectedIndexes = GetSelectedIndexes(items);
        SelectableListSelection.SelectIndexedItem(
            selectedIndexes,
            itemIndex,
            items.Count,
            itemsPerRow
        );
        StoreSelectedBuildings(items, selectedIndexes);
    }

    /// <summary>
    /// Selects a represented building while navigating to its inventory tab.
    /// </summary>
    /// <param name="tab">The building's inventory tab.</param>
    /// <param name="building">The represented building.</param>
    /// <returns>True when the building belongs to the represented inventory tab.</returns>
    public bool SelectBuilding(FacilityWindowTab tab, Building building)
    {
        string buildingId = GetBuildingId(building);
        if (
            string.IsNullOrEmpty(buildingId)
            || !GetItems(tab).Any(item => GetBuildingId(item) == buildingId)
        )
            return false;

        SetActiveTab(tab);
        ClearSelection();
        selectedBuildingIds.Add(buildingId);
        return true;
    }

    /// <summary>
    /// Gets selected inventory buildings in current display order.
    /// </summary>
    /// <returns>The selected buildings.</returns>
    public List<Building> GetSelectedBuildings()
    {
        return GetItems(ActiveTab)
            .Where(item => selectedBuildingIds.Contains(GetBuildingId(item)))
            .ToList();
    }

    /// <summary>
    /// Gets the context-targeted inventory building.
    /// </summary>
    /// <returns>The context building, or null.</returns>
    public Building GetContextBuilding()
    {
        return string.IsNullOrEmpty(contextBuildingId)
            ? null
            : GetItems(ActiveTab).FirstOrDefault(item => GetBuildingId(item) == contextBuildingId);
    }

    /// <summary>
    /// Gets the singular selected building or the current context building.
    /// </summary>
    /// <returns>The status building, or null.</returns>
    public Building GetStatusBuilding()
    {
        List<Building> selected = GetSelectedBuildings();
        return selected.Count == 1 ? selected[0] : GetContextBuilding();
    }

    /// <summary>
    /// Gets the current inventory building at one display index.
    /// </summary>
    /// <param name="itemIndex">The active inventory display index.</param>
    /// <returns>The represented building, or null.</returns>
    public Building GetInventoryBuilding(int itemIndex)
    {
        IReadOnlyList<Building> items = GetItems(ActiveTab);
        return itemIndex >= 0 && itemIndex < items.Count ? items[itemIndex] : null;
    }

    /// <summary>
    /// Stores one manufacturing destination as an inseparable planet and item pair.
    /// </summary>
    /// <param name="type">The manufacturing category.</param>
    /// <param name="planetId">The destination planet identifier.</param>
    /// <param name="itemId">The optional destination entity identifier.</param>
    public void SetDestination(ManufacturingType type, string planetId, string itemId)
    {
        if (string.IsNullOrEmpty(planetId))
            throw new ArgumentException("A destination planet is required.", nameof(planetId));

        destinations[type] = (planetId, itemId);
    }

    /// <summary>
    /// Gets one manufacturing destination pair with the represented planet fallback.
    /// </summary>
    /// <param name="type">The manufacturing category.</param>
    /// <param name="planetId">Receives the destination planet identifier.</param>
    /// <param name="itemId">Receives the optional destination entity identifier.</param>
    public void GetDestination(ManufacturingType type, out string planetId, out string itemId)
    {
        if (destinations.TryGetValue(type, out (string PlanetId, string ItemId) destination))
        {
            planetId = destination.PlanetId;
            itemId = destination.ItemId;
            return;
        }

        planetId = Planet?.Planet?.InstanceID;
        itemId = null;
    }

    /// <summary>
    /// Clears context state without changing the active selection.
    /// </summary>
    public void ClearContext()
    {
        ContextManufacturingTab = null;
        contextBuildingId = null;
    }

    /// <summary>
    /// Clears selection and context state.
    /// </summary>
    public void ClearSelection()
    {
        selectedCards.Clear();
        selectedBuildingIds.Clear();
        ClearContext();
    }

    /// <summary>
    /// Resolves durable building selection to current display indexes.
    /// </summary>
    /// <param name="items">The active inventory buildings in display order.</param>
    /// <returns>The selected display indexes.</returns>
    private HashSet<int> GetSelectedIndexes(IReadOnlyList<Building> items)
    {
        HashSet<int> indexes = new HashSet<int>();
        for (int index = 0; index < items.Count; index++)
        {
            if (selectedBuildingIds.Contains(GetBuildingId(items[index])))
                indexes.Add(index);
        }

        return indexes;
    }

    /// <summary>
    /// Replaces durable building selection from current display indexes.
    /// </summary>
    /// <param name="items">The active inventory buildings in display order.</param>
    /// <param name="indexes">The selected display indexes.</param>
    private void StoreSelectedBuildings(
        IReadOnlyList<Building> items,
        IReadOnlyCollection<int> indexes
    )
    {
        selectedBuildingIds.Clear();
        foreach (int index in indexes)
        {
            string buildingId =
                index >= 0 && index < items.Count ? GetBuildingId(items[index]) : null;
            if (!string.IsNullOrEmpty(buildingId))
                selectedBuildingIds.Add(buildingId);
        }
    }

    /// <summary>
    /// Gets the stable identifier for one inventory building.
    /// </summary>
    /// <param name="building">The inventory building.</param>
    /// <returns>The building identifier, or null.</returns>
    private static string GetBuildingId(Building building)
    {
        return building?.InstanceID;
    }

    /// <summary>
    /// Gets the authored display count for a refreshed facility-tab snapshot.
    /// </summary>
    /// <param name="planet">The represented planet.</param>
    /// <param name="tab">The facility tab.</param>
    /// <param name="buildingCount">The number of matching buildings.</param>
    /// <returns>The number of displayed item slots.</returns>
    private static int GetDisplayCount(Planet planet, FacilityWindowTab tab, int buildingCount)
    {
        return tab switch
        {
            FacilityWindowTab.Manufacturing => 1,
            FacilityWindowTab.Mines => planet?.NumRawResourceNodes ?? 0,
            _ => buildingCount,
        };
    }

    /// <summary>
    /// Converts a manufacturing card index to its facility tab.
    /// </summary>
    /// <param name="cardIndex">The manufacturing card index.</param>
    /// <returns>The matching manufacturing tab, or null.</returns>
    private static FacilityWindowTab? GetManufacturingTab(int cardIndex)
    {
        FacilityWindowTab tab = (FacilityWindowTab)cardIndex;
        return ConstructionOrderController.GetManufacturingType(tab).HasValue ? tab : null;
    }

    /// <summary>
    /// Selects one context index unless it already belongs to the selection.
    /// </summary>
    /// <param name="selection">The semantic manufacturing selection.</param>
    /// <param name="index">The context lane index.</param>
    private static void SelectContextItem(HashSet<int> selection, int index)
    {
        if (selection.Contains(index))
            return;

        selection.Clear();
        selection.Add(index);
    }
}
