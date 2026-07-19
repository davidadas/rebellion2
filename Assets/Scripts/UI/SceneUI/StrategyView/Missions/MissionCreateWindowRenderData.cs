using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Identifies one tab in the authored Mission Create workflow order.
/// </summary>
public enum MissionCreateWindowTab
{
    Mission = 0,
    Personnel = 1,
}

/// <summary>
/// Contains immutable presentation data for one mission-creation tab.
/// </summary>
public sealed class MissionCreateTabRenderData
{
    /// <summary>
    /// Creates one complete mission-creation tab snapshot.
    /// </summary>
    /// <param name="tab">The represented Mission Create tab.</param>
    /// <param name="texture">The tab texture shown while released.</param>
    /// <param name="pressedTexture">The tab texture shown while pressed.</param>
    public MissionCreateTabRenderData(
        MissionCreateWindowTab tab,
        Texture texture,
        Texture pressedTexture
    )
    {
        Tab = tab;
        Texture = texture;
        PressedTexture = pressedTexture;
    }

    public MissionCreateWindowTab Tab { get; }

    public Texture Texture { get; }

    public Texture PressedTexture { get; }
}

/// <summary>
/// Contains immutable presentation data for one Mission Create window.
/// </summary>
public sealed class MissionCreateWindowRenderData
{
    private static readonly MissionCreateWindowTab[] _orderedTabs =
    {
        MissionCreateWindowTab.Mission,
        MissionCreateWindowTab.Personnel,
    };
    private static readonly IReadOnlyList<MissionCreateWindowTab> _readOnlyOrderedTabs =
        Array.AsReadOnly(_orderedTabs);

    public static int TabCount => _orderedTabs.Length;

    public static IReadOnlyList<MissionCreateWindowTab> OrderedTabs => _readOnlyOrderedTabs;

    /// <summary>
    /// Creates one complete Mission Create presentation snapshot.
    /// </summary>
    /// <param name="x">The source-space horizontal position.</param>
    /// <param name="y">The source-space vertical position.</param>
    /// <param name="activeTab">The selected workflow tab.</param>
    /// <param name="dropdownOpen">Whether the mission dropdown is visible.</param>
    /// <param name="titleTexture">The faction-specific window title texture.</param>
    /// <param name="missionName">The selected mission name.</param>
    /// <param name="selectedMissionTexture">The selected mission icon.</param>
    /// <param name="targetName">The mission target name.</param>
    /// <param name="targetTexture">The mission target image.</param>
    /// <param name="usePlanetTargetPreview">Whether the view should use its authored planet preview.</param>
    /// <param name="agentsHeaderTexture">The faction-specific agents header.</param>
    /// <param name="decoysHeaderTexture">The faction-specific decoys header.</param>
    /// <param name="tabs">The ordered workflow tabs.</param>
    /// <param name="dropdownItems">The ordered mission dropdown rows.</param>
    /// <param name="agentRows">The ordered primary-agent rows.</param>
    /// <param name="decoyRows">The ordered decoy-agent rows.</param>
    public MissionCreateWindowRenderData(
        int x,
        int y,
        MissionCreateWindowTab activeTab,
        bool dropdownOpen,
        Texture titleTexture,
        string missionName,
        Texture selectedMissionTexture,
        string targetName,
        Texture targetTexture,
        bool usePlanetTargetPreview,
        Texture agentsHeaderTexture,
        Texture decoysHeaderTexture,
        IReadOnlyList<MissionCreateTabRenderData> tabs,
        IReadOnlyList<StrategyDropdownItemRenderData> dropdownItems,
        IReadOnlyList<MissionParticipantRowRenderData> agentRows,
        IReadOnlyList<MissionParticipantRowRenderData> decoyRows
    )
    {
        X = x;
        Y = y;
        ActiveTab = activeTab;
        DropdownOpen = dropdownOpen;
        TitleTexture = titleTexture;
        MissionName = missionName ?? string.Empty;
        SelectedMissionTexture = selectedMissionTexture;
        TargetName = targetName ?? string.Empty;
        TargetTexture = targetTexture;
        UsePlanetTargetPreview = usePlanetTargetPreview;
        AgentsHeaderTexture = agentsHeaderTexture;
        DecoysHeaderTexture = decoysHeaderTexture;
        Tabs = Copy(tabs, nameof(tabs));
        DropdownItems = Copy(dropdownItems, nameof(dropdownItems));
        AgentRows = Copy(agentRows, nameof(agentRows));
        DecoyRows = Copy(decoyRows, nameof(decoyRows));
    }

    public int X { get; }

    public int Y { get; }

    public MissionCreateWindowTab ActiveTab { get; }

    public bool DropdownOpen { get; }

    public Texture TitleTexture { get; }

    public string MissionName { get; }

    public Texture SelectedMissionTexture { get; }

    public string TargetName { get; }

    public Texture TargetTexture { get; }

    public bool UsePlanetTargetPreview { get; }

    public Texture AgentsHeaderTexture { get; }

    public Texture DecoysHeaderTexture { get; }

    public IReadOnlyList<MissionCreateTabRenderData> Tabs { get; }

    public IReadOnlyList<StrategyDropdownItemRenderData> DropdownItems { get; }

    public IReadOnlyList<MissionParticipantRowRenderData> AgentRows { get; }

    public IReadOnlyList<MissionParticipantRowRenderData> DecoyRows { get; }

    /// <summary>
    /// Copies a required presentation collection into an isolated read-only snapshot.
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
