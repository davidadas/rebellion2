using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Identifies one tab in the authored facility-window order.
/// </summary>
public enum FacilityWindowTab
{
    Manufacturing = 0,
    Shipyards = 1,
    Training = 2,
    Construction = 3,
    Refineries = 4,
    Mines = 5,
}

/// <summary>
/// Identifies the visual availability state of a facility tab.
/// </summary>
public enum FacilityWindowTabState
{
    Active = 0,
    Inactive = 1,
    Disabled = 2,
}

/// <summary>
/// Contains immutable presentation data for one facility tab.
/// </summary>
public sealed class FacilityWindowTabRenderData
{
    /// <summary>
    /// Creates one facility-tab presentation snapshot.
    /// </summary>
    /// <param name="tab">The represented facility tab.</param>
    /// <param name="state">The tab's visual availability state.</param>
    public FacilityWindowTabRenderData(FacilityWindowTab tab, FacilityWindowTabState state)
    {
        Tab = tab;
        State = state;
    }

    public FacilityWindowTab Tab { get; }

    public FacilityWindowTabState State { get; }
}

/// <summary>
/// Contains immutable presentation data for one facility inventory item.
/// </summary>
public sealed class FacilityInventoryItemRenderData
{
    /// <summary>
    /// Creates an inventory item presentation.
    /// </summary>
    /// <param name="texture">The displayed facility or resource image.</param>
    /// <param name="selected">Whether the item is selected.</param>
    public FacilityInventoryItemRenderData(Texture texture, bool selected)
    {
        Texture = texture;
        Selected = selected;
    }

    public Texture Texture { get; }

    public bool Selected { get; }
}

/// <summary>
/// Contains immutable presentation data for one manufacturing lane.
/// </summary>
public sealed class ManufacturingLaneCardRenderData
{
    /// <summary>
    /// Creates a manufacturing lane presentation.
    /// </summary>
    /// <param name="stateTexture">The selected or inactive lane frame.</param>
    /// <param name="entityTexture">The currently manufactured entity image.</param>
    /// <param name="manufacturingProgress">The current construction progress.</param>
    /// <param name="manufacturingCost">The total construction cost.</param>
    /// <param name="title">The lane title.</param>
    /// <param name="emptyText">The text shown for an idle lane.</param>
    /// <param name="currentName">The current item name.</param>
    /// <param name="currentCount">The current queued count.</param>
    /// <param name="destinationText">The configured destination label.</param>
    /// <param name="facilityCount">The active and total facility count.</param>
    public ManufacturingLaneCardRenderData(
        Texture2D stateTexture,
        Texture entityTexture,
        int manufacturingProgress,
        int manufacturingCost,
        string title,
        string emptyText,
        string currentName,
        string currentCount,
        string destinationText,
        string facilityCount
    )
    {
        StateTexture = stateTexture;
        EntityTexture = entityTexture;
        ManufacturingProgress = manufacturingProgress;
        ManufacturingCost = manufacturingCost;
        Title = title ?? string.Empty;
        EmptyText = emptyText ?? string.Empty;
        CurrentName = currentName ?? string.Empty;
        CurrentCount = currentCount ?? string.Empty;
        DestinationText = destinationText ?? string.Empty;
        FacilityCount = facilityCount ?? string.Empty;
    }

    public Texture2D StateTexture { get; }

    public Texture EntityTexture { get; }

    public int ManufacturingProgress { get; }

    public int ManufacturingCost { get; }

    public string Title { get; }

    public string EmptyText { get; }

    public string CurrentName { get; }

    public string CurrentCount { get; }

    public string DestinationText { get; }

    public string FacilityCount { get; }
}

/// <summary>
/// Contains immutable presentation data for one facility window.
/// </summary>
public sealed class FacilityWindowRenderData
{
    private static readonly FacilityWindowTab[] _orderedTabs =
    {
        FacilityWindowTab.Manufacturing,
        FacilityWindowTab.Shipyards,
        FacilityWindowTab.Training,
        FacilityWindowTab.Construction,
        FacilityWindowTab.Refineries,
        FacilityWindowTab.Mines,
    };
    private static readonly IReadOnlyList<FacilityWindowTab> _readOnlyOrderedTabs =
        Array.AsReadOnly(_orderedTabs);

    public static int TabCount => _orderedTabs.Length;

    public static IReadOnlyList<FacilityWindowTab> OrderedTabs => _readOnlyOrderedTabs;

    /// <summary>
    /// Creates a complete facility-window presentation snapshot.
    /// </summary>
    /// <param name="x">The source-space horizontal position.</param>
    /// <param name="y">The source-space vertical position.</param>
    /// <param name="titleTexture">The active or inactive title texture.</param>
    /// <param name="caption">The planet caption.</param>
    /// <param name="activeTab">The selected facility tab.</param>
    /// <param name="tabs">The authored tab presentations.</param>
    /// <param name="controlTabTexture">The control-tab texture.</param>
    /// <param name="controlTabPressedTexture">The pressed control-tab texture.</param>
    /// <param name="manufacturingCards">The manufacturing lane presentations.</param>
    /// <param name="inventoryTitle">The inventory title.</param>
    /// <param name="inventoryItems">The inventory item presentations.</param>
    /// <param name="inventorySelectionTexture">The inventory selection frame.</param>
    public FacilityWindowRenderData(
        int x,
        int y,
        Texture2D titleTexture,
        string caption,
        FacilityWindowTab activeTab,
        IReadOnlyList<FacilityWindowTabRenderData> tabs,
        Texture2D controlTabTexture,
        Texture2D controlTabPressedTexture,
        IReadOnlyList<ManufacturingLaneCardRenderData> manufacturingCards,
        string inventoryTitle,
        IReadOnlyList<FacilityInventoryItemRenderData> inventoryItems,
        Texture2D inventorySelectionTexture
    )
    {
        X = x;
        Y = y;
        TitleTexture = titleTexture;
        Caption = caption ?? string.Empty;
        ActiveTab = activeTab;
        Tabs = Copy(tabs);
        ControlTabTexture = controlTabTexture;
        ControlTabPressedTexture = controlTabPressedTexture;
        ManufacturingCards = Copy(manufacturingCards);
        InventoryTitle = inventoryTitle ?? string.Empty;
        InventoryItems = Copy(inventoryItems);
        InventorySelectionTexture = inventorySelectionTexture;
    }

    public int X { get; }

    public int Y { get; }

    public Texture2D TitleTexture { get; }

    public string Caption { get; }

    public FacilityWindowTab ActiveTab { get; }

    public IReadOnlyList<FacilityWindowTabRenderData> Tabs { get; }

    public Texture2D ControlTabTexture { get; }

    public Texture2D ControlTabPressedTexture { get; }

    public IReadOnlyList<ManufacturingLaneCardRenderData> ManufacturingCards { get; }

    public string InventoryTitle { get; }

    public IReadOnlyList<FacilityInventoryItemRenderData> InventoryItems { get; }

    public Texture2D InventorySelectionTexture { get; }

    public bool ShowManufacturing => ManufacturingCards.Count > 0;

    /// <summary>
    /// Copies presentation items into an immutable list.
    /// </summary>
    /// <typeparam name="T">The presentation item type.</typeparam>
    /// <param name="items">The source items.</param>
    /// <returns>The immutable copy.</returns>
    private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> items)
    {
        return new List<T>(items ?? throw new ArgumentNullException(nameof(items))).AsReadOnly();
    }
}
