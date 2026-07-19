using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Contains immutable presentation data for one Missions-window tab.
/// </summary>
public sealed class MissionsWindowTabRenderData
{
    /// <summary>
    /// Creates one complete Missions-tab presentation snapshot.
    /// </summary>
    /// <param name="role">The represented mission-participant role.</param>
    /// <param name="texture">The tab texture shown while released.</param>
    /// <param name="pressedTexture">The tab texture shown while pressed.</param>
    public MissionsWindowTabRenderData(
        MissionParticipantRole role,
        Texture texture,
        Texture pressedTexture
    )
    {
        Role = role;
        Texture = texture;
        PressedTexture = pressedTexture;
    }

    public MissionParticipantRole Role { get; }

    public Texture Texture { get; }

    public Texture PressedTexture { get; }
}

/// <summary>
/// Contains immutable presentation data for one Missions window.
/// </summary>
public sealed class MissionsWindowRenderData
{
    private static readonly MissionParticipantRole[] _orderedRoles =
    {
        MissionParticipantRole.Agent,
        MissionParticipantRole.Decoy,
    };
    private static readonly IReadOnlyList<MissionParticipantRole> _readOnlyOrderedRoles =
        Array.AsReadOnly(_orderedRoles);

    public static int TabCount => _orderedRoles.Length;

    public static IReadOnlyList<MissionParticipantRole> OrderedRoles => _readOnlyOrderedRoles;

    /// <summary>
    /// Creates one complete Missions-window presentation snapshot.
    /// </summary>
    /// <param name="x">The source-space horizontal position.</param>
    /// <param name="y">The source-space vertical position.</param>
    /// <param name="titleTexture">The active or inactive title texture.</param>
    /// <param name="caption">The displayed planet name.</param>
    /// <param name="activeRole">The selected participant role.</param>
    /// <param name="selectedMissionIndex">The selected mission's visual index.</param>
    /// <param name="hasSelectedMission">Whether the detail pane has a selected mission.</param>
    /// <param name="targetName">The selected mission target name.</param>
    /// <param name="targetTexture">The selected mission target image.</param>
    /// <param name="missions">The ordered mission rows.</param>
    /// <param name="tabs">The ordered participant tabs.</param>
    /// <param name="participants">The ordered participant rows.</param>
    public MissionsWindowRenderData(
        int x,
        int y,
        Texture titleTexture,
        string caption,
        MissionParticipantRole activeRole,
        int selectedMissionIndex,
        bool hasSelectedMission,
        string targetName,
        Texture targetTexture,
        IReadOnlyList<MissionListRowRenderData> missions,
        IReadOnlyList<MissionsWindowTabRenderData> tabs,
        IReadOnlyList<MissionParticipantRowRenderData> participants
    )
    {
        X = x;
        Y = y;
        TitleTexture = titleTexture;
        Caption = caption ?? string.Empty;
        ActiveRole = activeRole;
        SelectedMissionIndex = selectedMissionIndex;
        HasSelectedMission = hasSelectedMission;
        TargetName = targetName ?? string.Empty;
        TargetTexture = targetTexture;
        Missions = Copy(missions, nameof(missions));
        Tabs = Copy(tabs, nameof(tabs));
        Participants = Copy(participants, nameof(participants));
    }

    public int X { get; }

    public int Y { get; }

    public Texture TitleTexture { get; }

    public string Caption { get; }

    public MissionParticipantRole ActiveRole { get; }

    public int SelectedMissionIndex { get; }

    public bool HasSelectedMission { get; }

    public string TargetName { get; }

    public Texture TargetTexture { get; }

    public IReadOnlyList<MissionListRowRenderData> Missions { get; }

    public IReadOnlyList<MissionsWindowTabRenderData> Tabs { get; }

    public IReadOnlyList<MissionParticipantRowRenderData> Participants { get; }

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
