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

    /// <summary>
    /// Gets the represented mission-participant role.
    /// </summary>
    public MissionParticipantRole Role { get; }

    /// <summary>
    /// Gets the texture.
    /// </summary>
    public Texture Texture { get; }

    /// <summary>
    /// Gets the pressed texture.
    /// </summary>
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

    /// <summary>
    /// Gets the number of authored mission-participant tabs.
    /// </summary>
    public static int TabCount => _orderedRoles.Length;

    /// <summary>
    /// Gets the semantic participant roles in authored tab-slot order.
    /// </summary>
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

    /// <summary>
    /// Gets the horizontal coordinate.
    /// </summary>
    public int X { get; }

    /// <summary>
    /// Gets the vertical coordinate.
    /// </summary>
    public int Y { get; }

    /// <summary>
    /// Gets the title texture.
    /// </summary>
    public Texture TitleTexture { get; }

    /// <summary>
    /// Gets the caption.
    /// </summary>
    public string Caption { get; }

    /// <summary>
    /// Gets the active participant role.
    /// </summary>
    public MissionParticipantRole ActiveRole { get; }

    /// <summary>
    /// Gets the selected mission index.
    /// </summary>
    public int SelectedMissionIndex { get; }

    /// <summary>
    /// Gets a value indicating whether selected mission is present.
    /// </summary>
    public bool HasSelectedMission { get; }

    /// <summary>
    /// Gets the target name.
    /// </summary>
    public string TargetName { get; }

    /// <summary>
    /// Gets the target texture.
    /// </summary>
    public Texture TargetTexture { get; }

    /// <summary>
    /// Gets the missions.
    /// </summary>
    public IReadOnlyList<MissionListRowRenderData> Missions { get; }

    /// <summary>
    /// Gets the tabs.
    /// </summary>
    public IReadOnlyList<MissionsWindowTabRenderData> Tabs { get; }

    /// <summary>
    /// Gets the participants.
    /// </summary>
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
