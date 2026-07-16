using System;
using System.Collections.Generic;
using Rebellion.Game.Units;

/// <summary>
/// Owns the ordered build-template snapshot and mutable interaction state for one construction window.
/// </summary>
internal sealed class ConstructionWindowSession
{
    private const int _minimumBuildCount = 1;

    private readonly List<IManufacturable> items = new List<IManufacturable>();
    private string selectedItemTypeId;

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
    /// Gets the requested build count.
    /// </summary>
    public int BuildCount { get; private set; } = _minimumBuildCount;

    /// <summary>
    /// Gets the selected destination entity identifier.
    /// </summary>
    public string DestinationItemId { get; private set; }

    /// <summary>
    /// Gets the selected destination planet identifier.
    /// </summary>
    public string DestinationPlanetId { get; private set; }

    /// <summary>
    /// Gets whether the build-item dropdown is open.
    /// </summary>
    public bool DropdownOpen { get; private set; }

    /// <summary>
    /// Gets the ordered build templates represented by this session.
    /// </summary>
    public IReadOnlyList<IManufacturable> Items => items;

    /// <summary>
    /// Gets the active manufacturing facility tab.
    /// </summary>
    public FacilityWindowTab ManufacturingTab { get; private set; }

    /// <summary>
    /// Gets the producing strategy planet.
    /// </summary>
    public GalaxyMapPlanet Planet { get; private set; }

    /// <summary>
    /// Gets the selected build template.
    /// </summary>
    public IManufacturable SelectedItem =>
        SelectedItemIndex >= 0 && SelectedItemIndex < items.Count ? items[SelectedItemIndex] : null;

    /// <summary>
    /// Gets the selected build-item index.
    /// </summary>
    public int SelectedItemIndex { get; private set; }

    /// <summary>
    /// Gets the facility window that opened this construction window.
    /// </summary>
    public UIWindow SourceWindow { get; private set; }

    /// <summary>
    /// Gets the owning construction window.
    /// </summary>
    public UIWindow Window { get; }

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

        SelectedItemIndex = FindItemIndex(items, selectedItemTypeId);
        if (SelectedItemIndex < 0 && items.Count > 0)
            SelectedItemIndex = Math.Max(0, Math.Min(previousIndex, items.Count - 1));

        selectedItemTypeId = GetItemTypeId(SelectedItem);
        if (items.Count == 0)
        {
            SelectedItemIndex = 0;
            DropdownOpen = false;
        }
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
        selectedItemTypeId = GetItemTypeId(items[index]);
        DropdownOpen = false;
        return true;
    }

    /// <summary>
    /// Increments the build count within the supported byte range.
    /// </summary>
    public void IncrementBuildCount()
    {
        BuildCount = Math.Min(byte.MaxValue, BuildCount + 1);
    }

    /// <summary>
    /// Decrements the build count without allowing an empty order.
    /// </summary>
    public void DecrementBuildCount()
    {
        BuildCount = Math.Max(_minimumBuildCount, BuildCount - 1);
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
            || ConstructionOrderController.GetManufacturingType(ManufacturingTab)
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
    public string GetDestinationPlanetId()
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
        selectedItemTypeId = null;
        SelectedItemIndex = 0;
        BuildCount = _minimumBuildCount;
        DropdownOpen = false;
    }

    /// <summary>
    /// Finds a build template by stable type identifier.
    /// </summary>
    /// <param name="availableItems">The ordered build templates.</param>
    /// <param name="typeId">The selected template type identifier.</param>
    /// <returns>The current display index, or negative one.</returns>
    private static int FindItemIndex(IReadOnlyList<IManufacturable> availableItems, string typeId)
    {
        if (availableItems == null || string.IsNullOrEmpty(typeId))
            return -1;

        for (int index = 0; index < availableItems.Count; index++)
        {
            if (
                string.Equals(
                    GetItemTypeId(availableItems[index]),
                    typeId,
                    StringComparison.Ordinal
                )
            )
                return index;
        }

        return -1;
    }

    /// <summary>
    /// Gets one build template's stable type identifier.
    /// </summary>
    /// <param name="item">The build template.</param>
    /// <returns>The template type identifier, or null.</returns>
    private static string GetItemTypeId(IManufacturable item)
    {
        return item?.GetTypeID();
    }
}
