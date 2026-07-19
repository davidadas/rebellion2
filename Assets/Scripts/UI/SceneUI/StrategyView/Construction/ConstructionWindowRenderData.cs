using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Contains immutable presentation data for one construction window.
/// </summary>
public sealed class ConstructionWindowRenderData
{
    /// <summary>
    /// Creates a complete construction-window presentation snapshot.
    /// </summary>
    /// <param name="x">The source-space horizontal position.</param>
    /// <param name="y">The source-space vertical position.</param>
    /// <param name="titleTexture">The active or inactive window-title texture.</param>
    /// <param name="selectedTexture">The selected build-item image.</param>
    /// <param name="selectedName">The selected build-item name.</param>
    /// <param name="buildCount">The selected build count.</param>
    /// <param name="constructionCost">The total construction cost.</param>
    /// <param name="maintenanceCost">The total maintenance cost.</param>
    /// <param name="completionEstimate">The displayed completion estimate.</param>
    /// <param name="completionHasDays">Whether the completion estimate includes a days label.</param>
    /// <param name="deploymentEstimate">The displayed deployment estimate.</param>
    /// <param name="deploymentHasDays">Whether the deployment estimate includes a days label.</param>
    /// <param name="dropdownOpen">Whether the build-item dropdown is open.</param>
    /// <param name="canStart">Whether construction can start for the current selection.</param>
    /// <param name="dropdownItems">The available build-item presentations.</param>
    public ConstructionWindowRenderData(
        int x,
        int y,
        Texture2D titleTexture,
        Texture2D selectedTexture,
        string selectedName,
        int buildCount,
        string constructionCost,
        string maintenanceCost,
        string completionEstimate,
        bool completionHasDays,
        string deploymentEstimate,
        bool deploymentHasDays,
        bool dropdownOpen,
        bool canStart,
        IReadOnlyList<StrategyDropdownItemRenderData> dropdownItems
    )
    {
        X = x;
        Y = y;
        TitleTexture = titleTexture;
        SelectedTexture = selectedTexture;
        SelectedName = selectedName ?? string.Empty;
        BuildCount = buildCount;
        ConstructionCost = constructionCost ?? string.Empty;
        MaintenanceCost = maintenanceCost ?? string.Empty;
        CompletionEstimate = completionEstimate ?? string.Empty;
        CompletionHasDays = completionHasDays;
        DeploymentEstimate = deploymentEstimate ?? string.Empty;
        DeploymentHasDays = deploymentHasDays;
        DropdownOpen = dropdownOpen;
        CanStart = canStart;
        DropdownItems = new List<StrategyDropdownItemRenderData>(
            dropdownItems ?? throw new ArgumentNullException(nameof(dropdownItems))
        ).AsReadOnly();
    }

    public int X { get; }

    public int Y { get; }

    public Texture2D TitleTexture { get; }

    public Texture2D SelectedTexture { get; }

    public string SelectedName { get; }

    public int BuildCount { get; }

    public string ConstructionCost { get; }

    public string MaintenanceCost { get; }

    public string CompletionEstimate { get; }

    public bool CompletionHasDays { get; }

    public string DeploymentEstimate { get; }

    public bool DeploymentHasDays { get; }

    public bool DropdownOpen { get; }

    public bool CanStart { get; }

    public IReadOnlyList<StrategyDropdownItemRenderData> DropdownItems { get; }

    public bool HasSelection => DropdownItems.Count > 0;
}
