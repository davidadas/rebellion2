using System;
using System.Collections.Generic;
using System.Linq;
using Rebellion.Game.Units;
using Rebellion.SceneGraph;
using UnityEngine;

/// <summary>
/// Projects construction state into immutable window presentation data.
/// </summary>
internal sealed class ConstructionWindowProjector
{
    private const int _maximumDisplayedEstimate = 9999;

    private static readonly Color32 _selectedDropdownLabelColor = Color.white;
    private static readonly Color32 _unselectedDropdownLabelColor = new Color32(128, 128, 128, 255);

    private readonly Func<UIContext> getUIContext;

    /// <summary>
    /// Creates a construction-window presentation projector.
    /// </summary>
    /// <param name="getUIContext">Returns the current strategy presentation context.</param>
    public ConstructionWindowProjector(Func<UIContext> getUIContext)
    {
        this.getUIContext = getUIContext ?? throw new ArgumentNullException(nameof(getUIContext));
    }

    /// <summary>
    /// Creates the complete presentation snapshot for one construction window.
    /// </summary>
    /// <param name="x">The source-space horizontal window position.</param>
    /// <param name="y">The source-space vertical window position.</param>
    /// <param name="ownerFactionId">The producing planet owner identifier.</param>
    /// <param name="active">Whether the window currently has focus.</param>
    /// <param name="items">The available build templates.</param>
    /// <param name="selectedIndex">The selected build-template index.</param>
    /// <param name="buildCount">The requested build quantity.</param>
    /// <param name="canStartSelections">The indexes currently eligible to start.</param>
    /// <param name="estimates">The build estimates aligned with the available templates.</param>
    /// <param name="dropdownOpen">Whether the build-template dropdown is open.</param>
    /// <returns>The immutable construction-window presentation.</returns>
    public ConstructionWindowRenderData CreateRenderData(
        int x,
        int y,
        string ownerFactionId,
        bool active,
        IReadOnlyList<IManufacturable> items,
        int selectedIndex,
        int buildCount,
        IReadOnlyCollection<int> canStartSelections,
        IReadOnlyList<ConstructionBuildEstimate> estimates,
        bool dropdownOpen
    )
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));
        if (canStartSelections == null)
            throw new ArgumentNullException(nameof(canStartSelections));
        if (estimates == null)
            throw new ArgumentNullException(nameof(estimates));

        UIContext uiContext = GetRequiredUIContext();
        IManufacturable selected = GetSelectedItem(items, selectedIndex);
        ConstructionBuildEstimate selectedEstimate =
            selected == null ? null : GetSelectedEstimate(estimates, selectedIndex);
        WindowTitleTheme titleTheme = uiContext.GetTheme(ownerFactionId)?.WindowTitleTheme;

        return new ConstructionWindowRenderData(
            x,
            y,
            uiContext.GetTexture(
                active ? titleTheme?.ActiveImagePath : titleTheme?.InactiveImagePath
            ),
            GetItemTexture(uiContext, selected),
            selected?.GetDisplayName(),
            buildCount,
            selected == null
                ? string.Empty
                : FormatTotalCost(selected.GetConstructionCost(), buildCount),
            selected == null
                ? string.Empty
                : FormatTotalCost(selected.GetMaintenanceCost(), buildCount),
            FormatEstimate(selectedEstimate?.CompletionTicks),
            selectedEstimate != null,
            FormatEstimate(selectedEstimate?.DeploymentTicks),
            selectedEstimate != null,
            dropdownOpen,
            selected != null && canStartSelections.Contains(selectedIndex),
            CreateDropdownItems(uiContext, items, selectedIndex)
        );
    }

    /// <summary>
    /// Projects available build templates into dropdown-row presentations.
    /// </summary>
    /// <param name="uiContext">The current strategy presentation context.</param>
    /// <param name="items">The available build templates.</param>
    /// <param name="selectedIndex">The selected build-template index.</param>
    /// <returns>The projected dropdown rows.</returns>
    private static IReadOnlyList<StrategyDropdownItemRenderData> CreateDropdownItems(
        UIContext uiContext,
        IReadOnlyList<IManufacturable> items,
        int selectedIndex
    )
    {
        List<StrategyDropdownItemRenderData> rows = new List<StrategyDropdownItemRenderData>(
            items.Count
        );
        for (int index = 0; index < items.Count; index++)
        {
            IManufacturable item = items[index];
            rows.Add(
                new StrategyDropdownItemRenderData(
                    GetItemTexture(uiContext, item),
                    item.GetDisplayName(),
                    index == selectedIndex
                        ? _selectedDropdownLabelColor
                        : _unselectedDropdownLabelColor
                )
            );
        }

        return rows.AsReadOnly();
    }

    /// <summary>
    /// Formats an estimate for the authored fixed-width value field.
    /// </summary>
    /// <param name="ticks">The estimated tick count, when available.</param>
    /// <returns>The displayed estimate value.</returns>
    private static string FormatEstimate(int? ticks)
    {
        return ticks.HasValue
            ? Math.Max(0, Math.Min(ticks.Value, _maximumDisplayedEstimate)).ToString()
            : "N/A";
    }

    /// <summary>
    /// Resolves a build template's primary display texture.
    /// </summary>
    /// <param name="uiContext">The current strategy presentation context.</param>
    /// <param name="item">The build template.</param>
    /// <returns>The resolved display texture, or null.</returns>
    private static Texture2D GetItemTexture(UIContext uiContext, IManufacturable item)
    {
        return item is ISceneNode node ? uiContext.GetEntityTexture(node, false) : null;
    }

    /// <summary>
    /// Gets the selected build template when the supplied index is valid.
    /// </summary>
    /// <param name="items">The available build templates.</param>
    /// <param name="selectedIndex">The selected build-template index.</param>
    /// <returns>The selected template, or null.</returns>
    private static IManufacturable GetSelectedItem(
        IReadOnlyList<IManufacturable> items,
        int selectedIndex
    )
    {
        return selectedIndex >= 0 && selectedIndex < items.Count ? items[selectedIndex] : null;
    }

    /// <summary>
    /// Gets the estimate aligned with the selected build template.
    /// </summary>
    /// <param name="estimates">The build estimates.</param>
    /// <param name="selectedIndex">The selected build-template index.</param>
    /// <returns>The selected estimate, or null.</returns>
    private static ConstructionBuildEstimate GetSelectedEstimate(
        IReadOnlyList<ConstructionBuildEstimate> estimates,
        int selectedIndex
    )
    {
        return selectedIndex >= 0 && selectedIndex < estimates.Count
            ? estimates[selectedIndex]
            : null;
    }

    /// <summary>
    /// Formats one selected template's total cost.
    /// </summary>
    /// <param name="unitCost">The per-template cost.</param>
    /// <param name="buildCount">The requested build quantity.</param>
    /// <returns>The displayed total cost.</returns>
    private static string FormatTotalCost(int unitCost, int buildCount)
    {
        return (unitCost * buildCount).ToString();
    }

    /// <summary>
    /// Gets the current strategy presentation context and rejects incomplete composition.
    /// </summary>
    /// <returns>The current strategy presentation context.</returns>
    private UIContext GetRequiredUIContext()
    {
        return getUIContext()
            ?? throw new InvalidOperationException("The strategy UI context is unavailable.");
    }
}
