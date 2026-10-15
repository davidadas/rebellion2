using System;
using System.Collections.Generic;
using Rebellion.Game.Units;

/// <summary>
/// Owns the ordered build-template snapshot and mutable interaction state for one construction window.
/// </summary>
internal sealed class ConstructionWindowSession
{
    private const int _minimumBuildCount = 1;
    private const int _defaultMaximumBuildCount = byte.MaxValue;

    private readonly List<IManufacturable> items = new List<IManufacturable>();
    private int maximumBuildCount = _defaultMaximumBuildCount;
    private bool orderInitialized;
    private string selectedItemId;

    public int BuildCount { get; private set; } = _minimumBuildCount;

    public string DestinationItemId { get; private set; }

    public string DestinationPlanetId { get; private set; }

    public bool DropdownOpen { get; private set; }

    public IReadOnlyList<IManufacturable> Items => items;

    public FacilityWindowTab ManufacturingTab { get; private set; }

    public GalaxyMapPlanet Planet { get; private set; }

    public IManufacturable SelectedItem =>
        SelectedItemIndex >= 0 && SelectedItemIndex < items.Count ? items[SelectedItemIndex] : null;

    public int OrderBuildCount => BuildCount;

    public int SelectedItemIndex { get; private set; }

    public UIWindow SourceWindow { get; private set; }

    public UIWindow Window { get; }

    /// <summary>
    /// Creates state for one construction window.
    /// </summary>
    /// <param name="window">The owning construction window.</param>
    /// <param name="planet">The producing strategy planet.</param>
    /// <param name="sourceWindow">The originating facility window.</param>
    /// <param name="manufacturingTab">The requested manufacturing facility tab.</param>
    /// <param name="destinationPlanetId">The destination planet identifier.</param>
    /// <param name="destinationItemId">The destination entity identifier.</param>
    public ConstructionWindowSession(
        UIWindow window,
        GalaxyMapPlanet planet,
        UIWindow sourceWindow,
        FacilityWindowTab manufacturingTab,
        string destinationPlanetId,
        string destinationItemId
    )
    {
        Window = window ?? throw new ArgumentNullException(nameof(window));
        Planet = planet;
        SourceWindow = sourceWindow;
        ManufacturingTab = manufacturingTab;
        DestinationPlanetId = destinationPlanetId;
        DestinationItemId = destinationItemId;
    }

    /// <summary>
    /// Applies external session inputs and resets local state only when the panel changes.
    /// </summary>
    /// <param name="planet">The producing strategy planet.</param>
    /// <param name="sourceWindow">The originating facility window.</param>
    /// <param name="manufacturingTab">The requested manufacturing facility tab.</param>
    /// <param name="destinationPlanetId">The destination planet identifier.</param>
    /// <param name="destinationItemId">The destination entity identifier.</param>
    public void Reinitialize(
        GalaxyMapPlanet planet,
        UIWindow sourceWindow,
        FacilityWindowTab manufacturingTab,
        string destinationPlanetId,
        string destinationItemId
    )
    {
        Planet = planet;
        SourceWindow = sourceWindow;
        DestinationPlanetId = destinationPlanetId;
        DestinationItemId = destinationItemId;
        if (ManufacturingTab == manufacturingTab)
            return;

        ManufacturingTab = manufacturingTab;
        ResetDialog();
    }

    /// <summary>
    /// Replaces the stale strategy-planet projection without changing local interaction state.
    /// </summary>
    /// <param name="planet">The refreshed strategy planet.</param>
    public void RebindPlanet(GalaxyMapPlanet planet)
    {
        Planet = planet;
    }

    /// <summary>
    /// Replaces the ordered build-template snapshot and reconciles selection by template identity.
    /// </summary>
    /// <param name="availableItems">The currently available templates in display order.</param>
    public void SetItems(IReadOnlyList<IManufacturable> availableItems)
    {
        int previousIndex = SelectedItemIndex;
        items.Clear();
        if (availableItems != null)
            items.AddRange(availableItems);

        SelectedItemIndex = FindItemIndex(items, selectedItemId);
        if (SelectedItemIndex < 0 && items.Count > 0)
            SelectedItemIndex = Math.Max(0, Math.Min(previousIndex, items.Count - 1));

        selectedItemId = GetItemSelectionID(SelectedItem);
        UpdateMaximumBuildCount();
        if (items.Count == 0)
        {
            SelectedItemIndex = 0;
            DropdownOpen = false;
        }
    }

    /// <summary>
    /// Initializes the editable selection from the active manufacturing order once.
    /// </summary>
    /// <param name="itemId">The active product or rebuild selection identifier.</param>
    /// <param name="buildCount">The active unfinished quantity.</param>
    public void InitializeOrder(string itemId, int buildCount)
    {
        if (orderInitialized || items.Count == 0)
            return;

        orderInitialized = true;
        int activeIndex = FindItemIndex(items, itemId);
        if (activeIndex < 0)
            return;

        SelectedItemIndex = activeIndex;
        selectedItemId = itemId;
        maximumBuildCount = SelectedItem is BuildingUpgradeSelection upgrade
            ? upgrade.Sources.Count
            : Math.Max(_defaultMaximumBuildCount, buildCount);
        SetBuildCount(buildCount);
    }

    /// <summary>
    /// Selects an available build item and closes the dropdown.
    /// </summary>
    /// <param name="index">The requested item index.</param>
    /// <returns>True when the requested index is available.</returns>
    public bool SelectItem(int index)
    {
        if (index < 0 || index >= items.Count)
            return false;

        SelectedItemIndex = index;
        selectedItemId = GetItemSelectionID(items[index]);
        UpdateMaximumBuildCount();
        DropdownOpen = false;
        return true;
    }

    /// <summary>
    /// Increments the build count without exceeding the session's supported maximum.
    /// </summary>
    public void IncrementBuildCount()
    {
        if (BuildCount < maximumBuildCount)
            BuildCount++;
    }

    /// <summary>
    /// Decrements the build count without allowing an empty order.
    /// </summary>
    public void DecrementBuildCount()
    {
        BuildCount = Math.Max(_minimumBuildCount, BuildCount - 1);
    }

    /// <summary>
    /// Sets the build count within the session's supported non-empty range.
    /// </summary>
    /// <param name="buildCount">The requested build count.</param>
    public void SetBuildCount(int buildCount)
    {
        BuildCount = Math.Max(_minimumBuildCount, Math.Min(maximumBuildCount, buildCount));
    }

    /// <summary>
    /// Toggles the build-item dropdown.
    /// </summary>
    public void ToggleDropdown()
    {
        DropdownOpen = !DropdownOpen;
    }

    /// <summary>
    /// Closes the build-item dropdown when it is open.
    /// </summary>
    /// <returns>True when the open dropdown was closed.</returns>
    public bool DismissDropdown()
    {
        if (!DropdownOpen)
            return false;

        DropdownOpen = false;
        return true;
    }

    /// <summary>
    /// Updates the destination when it belongs to this session's source and manufacturing type.
    /// </summary>
    /// <param name="sourceWindow">The facility window whose destination changed.</param>
    /// <param name="manufacturingType">The changed manufacturing category.</param>
    /// <param name="destinationPlanetId">The destination planet identifier.</param>
    /// <param name="destinationItemId">The destination entity identifier.</param>
    /// <returns>True when this session accepted the destination.</returns>
    public bool TryUpdateDestination(
        UIWindow sourceWindow,
        ManufacturingType manufacturingType,
        string destinationPlanetId,
        string destinationItemId
    )
    {
        if (
            SourceWindow != sourceWindow
            || FacilityManufacturingLaneCatalog.GetManufacturingType(ManufacturingTab)
                != manufacturingType
        )
            return false;

        DestinationPlanetId = destinationPlanetId;
        DestinationItemId = destinationItemId;
        return true;
    }

    /// <summary>
    /// Gets the selected destination planet or falls back to the producing planet.
    /// </summary>
    /// <returns>The destination planet identifier, or null when neither planet is available.</returns>
    public string GetDestinationPlanetID()
    {
        return !string.IsNullOrEmpty(DestinationPlanetId)
            ? DestinationPlanetId
            : Planet?.Planet?.InstanceID;
    }

    /// <summary>
    /// Resets state that belongs to the active construction dialog.
    /// </summary>
    private void ResetDialog()
    {
        items.Clear();
        selectedItemId = null;
        orderInitialized = false;
        SelectedItemIndex = 0;
        BuildCount = _minimumBuildCount;
        maximumBuildCount = _defaultMaximumBuildCount;
        DropdownOpen = false;
    }

    /// <summary>
    /// Finds a build template by stable type identifier.
    /// </summary>
    /// <param name="availableItems">The ordered build templates.</param>
    /// <param name="itemId">The selected template type or rubble instance identifier.</param>
    /// <returns>The current display index, or negative one.</returns>
    private static int FindItemIndex(IReadOnlyList<IManufacturable> availableItems, string itemId)
    {
        if (availableItems == null || string.IsNullOrEmpty(itemId))
            return -1;

        for (int index = 0; index < availableItems.Count; index++)
        {
            if (
                string.Equals(
                    GetItemSelectionID(availableItems[index]),
                    itemId,
                    StringComparison.Ordinal
                )
            )
                return index;
        }

        return -1;
    }

    /// <summary>
    /// Gets one build selection's stable template or rubble identifier.
    /// </summary>
    /// <param name="item">The build template.</param>
    /// <returns>The selection identifier, or null.</returns>
    private static string GetItemSelectionID(IManufacturable item)
    {
        return item switch
        {
            BuildingUpgradeSelection upgrade => upgrade.GetSelectionID(),
            Building { IsDestroyed: true } building => building.InstanceID,
            _ => item?.GetTypeID(),
        };
    }

    /// <summary>
    /// Applies the quantity limit for the selected construction operation.
    /// </summary>
    private void UpdateMaximumBuildCount()
    {
        maximumBuildCount = SelectedItem is BuildingUpgradeSelection upgrade
            ? upgrade.Sources.Count
            : Math.Max(_defaultMaximumBuildCount, BuildCount);
        SetBuildCount(BuildCount);
    }
}
